using Microsoft.EntityFrameworkCore;
using ITDeviceManager.API.Data;
using ITDeviceManager.Core.Services;

namespace ITDeviceManager.API.Services
{
    public class DeviceDiscoveryBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DeviceDiscoveryBackgroundService> _logger;
        private readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(30); // 每30分钟扫描一次
        
        public DeviceDiscoveryBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<DeviceDiscoveryBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("设备发现后台服务已启动");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformDeviceDiscovery();
                    await Task.Delay(_scanInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "设备发现过程中发生错误");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // 错误后等待5分钟再重试
                }
            }
            
            _logger.LogInformation("设备发现后台服务已停止");
        }
        
        private async Task PerformDeviceDiscovery()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DeviceContext>();
            var discoveryService = scope.ServiceProvider.GetRequiredService<INetworkDiscoveryService>();
            
            _logger.LogInformation("开始自动设备发现");
            
            // 获取已知设备的网络段
            var knownDevices = await context.Set<Core.Models.Device>().ToListAsync();
            var networkRanges = GetNetworkRanges(knownDevices);
            
            // 如果没有已知设备，使用默认网络段
            if (networkRanges.Count == 0)
            {
                networkRanges.Add("192.168.1.1-254");
                networkRanges.Add("192.168.0.1-254");
            }

            // 用于跟踪本次扫描中已处理的MAC地址，避免重复插入（使用规范化）
            var processedMacAddresses = new HashSet<string>();
            
            foreach (string? networkRange in networkRanges)
            {
                try
                {
                    _logger.LogInformation($"扫描网络段: {networkRange}");
                    var discoveredDevices = await discoveryService.DiscoverDevicesAsync(networkRange);
                    
                    foreach (var device in discoveredDevices)
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(device.MACAddress))
                            {
                                _logger.LogWarning($"[后台服务] 跳过无MAC地址的设备: {device.Name} (IP: {device.IPAddress})");
                                continue;
                            }

                            // 规范化 MAC 地址用于比较（去除分隔符并大写）
                            var normalizedMac = device.MACAddress.Replace(":", "").Replace("-", "").Replace(" ", "").ToUpperInvariant();

                            // 检查本次扫描中是否已处理过此MAC地址
                            if (processedMacAddresses.Contains(normalizedMac))
                            {
                                _logger.LogWarning($"[后台服务] 跳过重复MAC地址: {device.MACAddress} (设备: {device.Name}, IP: {device.IPAddress})");
                                continue;
                            }
                            
                            // 使用规范化比较从数据库查找设备
                            var existingDevice = await context.Set<Core.Models.Device>()
                                .FirstOrDefaultAsync(d => d.MACAddress != null &&
                                    d.MACAddress.Replace(":", "").Replace("-", "").Replace(" ", "").ToUpper() == normalizedMac);
                                
                            if (existingDevice == null)
                            {
                                // 检查是否有其他设备使用相同的IP地址
                                var deviceWithSameIp = await context.Set<Core.Models.Device>()
                                    .FirstOrDefaultAsync(d => d.IPAddress == device.IPAddress);
                                    
                                if (deviceWithSameIp != null)
                                {
                                    _logger.LogWarning($"[后台服务] 发现IP冲突: 新设备 {device.Name} (MAC: {device.MACAddress}) 与现有设备 {deviceWithSameIp.Name} (MAC: {deviceWithSameIp.MACAddress}) 使用相同IP {device.IPAddress}");
                                    // 更新现有设备的MAC地址（可能是设备更换了网卡）
                                    deviceWithSameIp.MACAddress = device.MACAddress;
                                    deviceWithSameIp.Name = device.Name;
                                    deviceWithSameIp.DeviceType = device.DeviceType;
                                    deviceWithSameIp.Status = device.Status;
                                    deviceWithSameIp.LastSeen = DateTime.UtcNow;
                                    deviceWithSameIp.UpdatedAt = DateTime.UtcNow;
                                    processedMacAddresses.Add(normalizedMac);
                                    continue;
                                }
                                
                                // 确保新设备的 LastSeen/CreatedAt/UpdatedAt 设置
                                device.LastSeen = DateTime.UtcNow;
                                device.CreatedAt = DateTime.UtcNow;
                                device.UpdatedAt = DateTime.UtcNow;
                                context.Set<Core.Models.Device>().Add(device);
                                processedMacAddresses.Add(normalizedMac);
                                _logger.LogInformation($"[后台服务] 发现新设备: {device.Name} (MAC: {device.MACAddress}, IP: {device.IPAddress})");
                            }
                            else
                            {
                                // 更新现有设备信息
                                bool hasChanges = false;
                                
                                if (existingDevice.IPAddress != device.IPAddress)
                                {
                                    _logger.LogInformation($"[后台服务] 设备 {existingDevice.Name} IP地址变更: {existingDevice.IPAddress} -> {device.IPAddress}");
                                    existingDevice.IPAddress = device.IPAddress;
                                    hasChanges = true;
                                }
                                
                                if (existingDevice.Status != device.Status)
                                {
                                    existingDevice.Status = device.Status;
                                    hasChanges = true;
                                }
                                
                                if (existingDevice.DeviceType != device.DeviceType && device.DeviceType != Core.Models.DeviceType.Unknown)
                                {
                                    existingDevice.DeviceType = device.DeviceType;
                                    hasChanges = true;
                                }
                                
                                if (string.IsNullOrEmpty(existingDevice.Name) || existingDevice.Name == existingDevice.IPAddress)
                                {
                                    existingDevice.Name = device.Name;
                                    hasChanges = true;
                                }

                                // 总是刷新 LastSeen / UpdatedAt，表示刚刚被发现
                                existingDevice.LastSeen = DateTime.UtcNow;
                                existingDevice.UpdatedAt = DateTime.UtcNow;

                                // 如果有其他变化也会触发保存（已设置 above）
                                processedMacAddresses.Add(normalizedMac);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"[后台服务] 处理设备 {device.Name} (MAC: {device.MACAddress}, IP: {device.IPAddress}) 时发生错误");
                        }
                    }
                    
                    // 每个网络段扫描完成后保存一次，避免跨网络段的重复插入
                    try
                    {
                        await context.SaveChangesAsync();
                        _logger.LogInformation($"网络段 {networkRange} 扫描完成并保存到数据库");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"保存网络段 {networkRange} 的扫描结果时发生错误");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"扫描网络段 {networkRange} 时发生错误");
                }
            }
            
            // 更新离线设备状态
            await UpdateOfflineDevices(context);
            
            _logger.LogInformation("自动设备发现完成");
        }
        
        private static List<string> GetNetworkRanges(List<Core.Models.Device> devices)
        {
            var ranges = new HashSet<string>();
            
            foreach (var device in devices)
            {
                try
                {
                    var ipParts = device.IPAddress.Split('.');
                    if (ipParts.Length == 4)
                    {
                        var networkBase = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}";
                        ranges.Add($"{networkBase}.1-254");
                    }
                }
                catch
                {
                    // 忽略无效IP地址
                }
            }
            
            return ranges.ToList();
        }
        
        private async Task UpdateOfflineDevices(DeviceContext context)
        {
            var offlineThreshold = DateTime.UtcNow.AddMinutes(-60); // 60分钟未见视为离线
            
            var devicesToUpdate = await context.Set<Core.Models.Device>()
                .Where(d => d.Status == Core.Models.DeviceStatus.Online && d.LastSeen < offlineThreshold)
                .ToListAsync();
                
            foreach (var device in devicesToUpdate)
            {
                device.Status = Core.Models.DeviceStatus.Offline;
                device.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation("设备 {Name} ({IPAddress}) 已标记为离线", device.Name, device.IPAddress);
            }
            
            if (devicesToUpdate.Count > 0)
            {
                await context.SaveChangesAsync();
            }
        }
    }
}