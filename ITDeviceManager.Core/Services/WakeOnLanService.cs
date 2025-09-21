using System.Net;
using System.Net.Sockets;
using ITDeviceManager.Core.Models;

namespace ITDeviceManager.Core.Services
{
    public class WakeOnLanService : IWakeOnLanService
    {
        
        public async Task<bool> WakeDeviceAsync(string macAddress, string? ipAddress = null)
        {
            try
            {
                Console.WriteLine($"[WOL] 开始唤醒设备 - MAC: {macAddress}, IP: {ipAddress}");
                
                var macBytes = ParseMacAddress(macAddress);
                if (macBytes == null)
                {
                    Console.WriteLine($"[WOL] MAC地址解析失败: {macAddress}");
                    return false;
                }
                
                Console.WriteLine($"[WOL] MAC地址解析成功: {BitConverter.ToString(macBytes)}");
                
                var magicPacket = CreateMagicPacket(macBytes);
                Console.WriteLine($"[WOL] 魔术包创建完成，大小: {magicPacket.Length} 字节");
                
                using var client = new UdpClient();
                client.EnableBroadcast = true;
                
                // 发送到广播地址
                Console.WriteLine($"[WOL] 发送魔术包到广播地址 255.255.255.255:9");
                await client.SendAsync(magicPacket, magicPacket.Length, new IPEndPoint(IPAddress.Broadcast, 9));
                
                // 如果提供了IP地址，也发送到该地址
                if (!string.IsNullOrEmpty(ipAddress) && IPAddress.TryParse(ipAddress, out var targetIP))
                {
                    Console.WriteLine($"[WOL] 发送魔术包到目标地址 {targetIP}:9");
                    await client.SendAsync(magicPacket, magicPacket.Length, new IPEndPoint(targetIP, 9));
                }
                
                Console.WriteLine($"[WOL] Wake-on-LAN 包发送成功");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WOL] Wake-on-LAN 发送失败: {ex.Message}");
                Console.WriteLine($"[WOL] 错误详情: {ex.StackTrace}");
                return false;
            }
        }
        
        public async Task<bool> WakeDeviceAsync(Device device)
        {
            return await WakeDeviceAsync(device.MACAddress, device.IPAddress);
        }
        
        public Task<PowerOperation> CreateWakeOperationAsync(int deviceId, string? requestedBy = null)
        {
            var operation = new PowerOperation
            {
                DeviceId = deviceId,
                Operation = PowerOperationType.WakeOnLan,
                Result = PowerOperationResult.Pending,
                RequestedBy = requestedBy,
                RequestedAt = DateTime.UtcNow
            };
            
            return Task.FromResult(operation);
        }
        
        public Task<bool> ShutdownDeviceAsync(string ipAddress)
        {
            // 注意：远程关机需要特殊权限和配置，这里只是示例实现
            // 实际生产环境中需要使用WMI或其他方法
            Console.WriteLine($"远程关机功能需要额外配置，设备: {ipAddress}");
            return Task.FromResult(false);
        }
        
        private static byte[]? ParseMacAddress(string macAddress)
        {
            try
            {
                var cleanMac = macAddress.Replace(":", "").Replace("-", "").Replace(" ", "");
                if (cleanMac.Length != 12)
                    return null;
                
                var bytes = new byte[6];
                for (int i = 0; i < 6; i++)
                {
                    bytes[i] = Convert.ToByte(cleanMac.Substring(i * 2, 2), 16);
                }
                
                return bytes;
            }
            catch
            {
                return null;
            }
        }
        
        private byte[] CreateMagicPacket(byte[] macAddress)
        {
            var packet = new byte[102];
            
            // 前6个字节为0xFF
            for (int i = 0; i < 6; i++)
            {
                packet[i] = 0xFF;
            }
            
            // 接下来重复16次MAC地址
            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    packet[6 + i * 6 + j] = macAddress[j];
                }
            }
            
            return packet;
        }
    }
}