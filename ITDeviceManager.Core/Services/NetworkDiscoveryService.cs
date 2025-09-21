using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using ITDeviceManager.Core.Models;
using System.Net.Sockets;

namespace ITDeviceManager.Core.Services
{
    public class NetworkDiscoveryService : INetworkDiscoveryService
    {
        public async Task<List<Device>> DiscoverDevicesAsync(string networkRange)
        {
            var devices = new List<Device>();
            var ipAddresses = GenerateIPAddresses(networkRange);
            
            var tasks = ipAddresses.Select(async ip =>
            {
                if (await PingDeviceAsync(ip))
                {
                    var device = await GetDeviceInfoAsync(ip);
                    if (device != null)
                    {
                        return device;
                    }
                }
                return null;
            });
            
            var results = await Task.WhenAll(tasks);
            devices.AddRange(results.Where(d => d != null)!);
            
            return devices;
        }
        
        public async Task<Device?> GetDeviceInfoAsync(string ipAddress)
        {
            try
            {
                var macAddress = await GetMacAddressAsync(ipAddress);
                if (string.IsNullOrEmpty(macAddress))
                    return null;
                    
                var hostname = await GetHostnameAsync(ipAddress);
                
                // 由于未定义设备类型识别方法，暂时返回默认值
                var deviceType = await IdentifyDeviceTypeAsync(ipAddress, macAddress, hostname);
                
                return new Device
                {
                    Name = hostname ?? ipAddress,
                    IPAddress = ipAddress,
                    MACAddress = macAddress,
                    Status = DeviceStatus.Online,
                    LastSeen = DateTime.UtcNow,
                    DeviceType = deviceType
                };
            }
            catch
            {
                return null;
            }
        }
        
        public async Task<bool> PingDeviceAsync(string ipAddress)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ipAddress, 3000);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }
        
        public async Task<string?> GetMacAddressAsync(string ipAddress)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return await GetMacAddressWindowsAsync(ipAddress);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return await GetMacAddressLinuxAsync(ipAddress);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        public async Task<string?> GetHostnameAsync(string ipAddress)
        {
            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
                return hostEntry.HostName;
            }
            catch
            {
                return null;
            }
        }
        
        private List<string> GenerateIPAddresses(string networkRange)
        {
            var addresses = new List<string>();
            
            // 简单的IP范围解析，支持 192.168.1.1-254 格式
            if (networkRange.Contains('-'))
            {
                var parts = networkRange.Split('-');
                if (parts.Length == 2)
                {
                    var baseIP = parts[0].Trim();
                    var endRange = int.Parse(parts[1].Trim());
                    
                    var ipParts = baseIP.Split('.');
                    if (ipParts.Length == 4)
                    {
                        var baseNetwork = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}";
                        var startRange = int.Parse(ipParts[3]);
                        
                        for (int i = startRange; i <= endRange; i++)
                        {
                            addresses.Add($"{baseNetwork}.{i}");
                        }
                    }
                }
            }
            else
            {
                // 默认扫描 .1 到 .254
                var ipParts = networkRange.Split('.');
                if (ipParts.Length >= 3)
                {
                    var baseNetwork = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}";
                    for (int i = 1; i <= 254; i++)
                    {
                        addresses.Add($"{baseNetwork}.{i}");
                    }
                }
            }
            
            return addresses;
        }
        
        private async Task<string?> GetMacAddressWindowsAsync(string ipAddress)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "arp",
                        Arguments = $"-a {ipAddress}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains(ipAddress))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            var mac = parts[1].Replace('-', ':').ToUpper();
                            if (IsValidMacAddress(mac))
                            {
                                return mac;
                            }
                        }
                    }
                }
            }
            catch { }
            
            return null;
        }
        
        private async Task<string?> GetMacAddressLinuxAsync(string ipAddress)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "arp",
                        Arguments = $"-n {ipAddress}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains(ipAddress))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3)
                        {
                            var mac = parts[2].ToUpper();
                            if (IsValidMacAddress(mac))
                            {
                                return mac;
                            }
                        }
                    }
                }
            }
            catch { }
            
            return null;
        }
        
        private bool IsValidMacAddress(string mac)
        {
            return mac.Length == 17 && 
                   mac.Count(c => c == ':') == 5 &&
                   mac.All(c => char.IsDigit(c) || "ABCDEF:".Contains(c));
        }
        
        private async Task<DeviceType> IdentifyDeviceTypeAsync(string ipAddress, string macAddress, string? hostname)
        {
            try
            {
                //在这里，可以参考 在线MAC查找功能，发现更精准的厂家品牌信息 
                //比如： https://macvendors.com/
                // 基于MAC地址前缀识别设备类型
                var macPrefix = macAddress.Replace(":", "").Replace("-", "").ToUpper();
                var macPrefix6 = macPrefix.Substring(0, 6); // 前6位
                var macPrefix8 = macPrefix.Length >= 8 ? macPrefix.Substring(0, 8) : macPrefix; // 前8位
                
                // 添加调试日志
                Console.WriteLine($"[DEBUG] 识别设备类型 - IP: {ipAddress}, MAC: {macAddress}, 前缀6: {macPrefix6}, 前缀8: {macPrefix8}");
                
                // 常见厂商MAC地址前缀
                var vendorPrefixes = new Dictionary<string, DeviceType>
                {
                    // 路由器/网络设备 (6位前缀)
                    {"00E04C", DeviceType.Router}, // Realtek
                    {"001E58", DeviceType.Router}, // WNC
                    {"00259C", DeviceType.Router}, // Belkin
                    {"001DD8", DeviceType.Router}, // Tenda
                    {"C83A35", DeviceType.Router}, // Tenda
                    {"E8DE27", DeviceType.Router}, // TP-Link
                    {"F4F26D", DeviceType.Router}, // TP-Link
                    {"A0F3C1", DeviceType.Router}, // TP-Link
                    {"00E0FC", DeviceType.Switch}, // Realtek Switch
                    
                    // 虚拟机和虚拟网络适配器 (6位前缀)
                    {"000C29", DeviceType.Computer}, // VMware
                    {"005056", DeviceType.Computer}, // VMware
                    {"001C14", DeviceType.Computer}, // VMware
                    {"080027", DeviceType.Computer}, // VirtualBox
                    {"0003FF", DeviceType.Computer}, // Microsoft Virtual PC
                    {"00155D", DeviceType.Computer}, // Microsoft Hyper-V
                    {"525400", DeviceType.Computer}, // QEMU/KVM
                    {"020000", DeviceType.Computer}, // Generic Virtual
                    {"8261AC", DeviceType.Computer}, // Specific for 82:61:AC prefix
                    
                    // 常见计算机网卡厂商 (6位前缀)
                    {"001B21", DeviceType.Computer}, // Intel
                    {"0019D1", DeviceType.Computer}, // Intel
                    {"001E65", DeviceType.Computer}, // Intel
                    {"0024D7", DeviceType.Computer}, // Intel
                    {"7085C2", DeviceType.Computer}, // Intel
                    {"B42E99", DeviceType.Computer}, // Intel
                    {"D05099", DeviceType.Computer}, // Intel
                    {"E4B318", DeviceType.Computer}, // Intel
                    {"F0DEF1", DeviceType.Computer}, // Intel
                    {"001AA0", DeviceType.Computer}, // Realtek
                    {"52540B", DeviceType.Computer}, // Realtek
                    {"E0CB4E", DeviceType.Computer}, // Realtek
                    {"E85C5F", DeviceType.Computer}, // 对应 E8:5C:5F
                    {"7CB566", DeviceType.Computer}, // 对应 7C:B5:66
                    {"9C2DCD", DeviceType.Computer}, // 对应 9C:2D:CD
                    {"34CE00", DeviceType.Computer}, // 对应 34:CE:00
                    {"8C18D9", DeviceType.Computer}, // 对应 8C:18:D9
                    {"8CBD37", DeviceType.Computer}, // 对应 8C:BD:37
                    {"EC4D3E", DeviceType.Computer}, // 对应 EC:4D:3E
                    {"286C07", DeviceType.Computer}, // 对应 28:6C:07
                    {"1AB5A3", DeviceType.Computer}, // 对应 1A:B5:A3
                    {"B2344A", DeviceType.Computer}, // 对应 B2:34:4A
                    {"583BD9", DeviceType.Router}, // 对应 58:3B:D9 (可能是路由器)
                    
                    // 打印机 (6位前缀)
                    {"00BB01", DeviceType.Printer}, // Brother
                    {"008037", DeviceType.Printer}, // Canon
                    {"00A0B0", DeviceType.Printer}, // Canon
                    {"001E4F", DeviceType.Printer}, // HP
                    {"009027", DeviceType.Printer}, // HP
                    {"B499BA", DeviceType.Printer}, // HP
                    {"001CF0", DeviceType.Printer}, // Epson
                    {"04F021", DeviceType.Printer}, // Epson
                    
                    // 手机/平板
                    {"001E52", DeviceType.Phone}, // Apple iPhone/iPad
                    {"001F5B", DeviceType.Phone}, // Apple iPhone
                    {"002312", DeviceType.Phone}, // Apple iPhone
                    {"002332", DeviceType.Phone}, // Apple iPhone
                    {"002436", DeviceType.Phone}, // Apple iPhone
                    {"002500", DeviceType.Phone}, // Apple iPhone
                    {"0025BC", DeviceType.Phone}, // Apple iPhone
                    {"28E02C", DeviceType.Phone}, // Apple iPhone
                    {"40A6D9", DeviceType.Phone}, // Apple iPhone
                    {"64B9E8", DeviceType.Phone}, // Apple iPhone
                    {"78A3E4", DeviceType.Phone}, // Apple iPhone
                    {"8C2937", DeviceType.Phone}, // Apple iPhone
                    {"A45E60", DeviceType.Phone}, // Apple iPhone/iPad
                    {"B8E856", DeviceType.Phone}, // Apple iPhone/iPad
                    {"D0E140", DeviceType.Phone}, // Apple iPhone
                    {"F0DBE2", DeviceType.Phone}, // Apple iPhone
                    {"F81EDF", DeviceType.Phone}, // Apple iPhone
                    {"FC253F", DeviceType.Phone}, // Apple iPhone
                    {"001E10", DeviceType.Phone}, // Samsung
                    {"002454", DeviceType.Phone}, // Samsung
                    {"0025E5", DeviceType.Phone}, // Samsung
                    {"78D6F0", DeviceType.Phone}, // Samsung
                    {"E8039A", DeviceType.Phone}, // Samsung
                    
                    // 摄像头
                    {"001788", DeviceType.Camera}, // Hikvision
                    {"4C11AE", DeviceType.Camera}, // Hikvision
                    {"001E06", DeviceType.Camera}, // Dahua
                };
                
                if (vendorPrefixes.ContainsKey(macPrefix6))
                {
                    Console.WriteLine($"[DEBUG] 匹配到6位前缀: {macPrefix6} -> {vendorPrefixes[macPrefix6]}");
                    return vendorPrefixes[macPrefix6];
                }
                
                if (vendorPrefixes.ContainsKey(macPrefix8))
                {
                    Console.WriteLine($"[DEBUG] 匹配到8位前缀: {macPrefix8} -> {vendorPrefixes[macPrefix8]}");
                    return vendorPrefixes[macPrefix8];
                }
                
                Console.WriteLine($"[DEBUG] 未匹配到MAC前缀，继续主机名识别");
                
                // 基于主机名识别
                if (!string.IsNullOrEmpty(hostname))
                {
                    var hostnameLower = hostname.ToLower();
                    
                    if (hostnameLower.Contains("router") || hostnameLower.Contains("gateway"))
                        return DeviceType.Router;
                    if (hostnameLower.Contains("switch"))
                        return DeviceType.Switch;
                    if (hostnameLower.Contains("printer") || hostnameLower.Contains("print"))
                        return DeviceType.Printer;
                    if (hostnameLower.Contains("server"))
                        return DeviceType.Server;
                    if (hostnameLower.Contains("camera") || hostnameLower.Contains("cam"))
                        return DeviceType.Camera;
                    if (hostnameLower.Contains("phone") || hostnameLower.Contains("mobile"))
                        return DeviceType.Phone;
                    if (hostnameLower.Contains("tablet") || hostnameLower.Contains("ipad"))
                        return DeviceType.Tablet;
                    if (hostnameLower.Contains("desktop") || hostnameLower.Contains("pc") || hostnameLower.Contains("laptop"))
                        return DeviceType.Computer;
                }
                
                // 基于端口扫描进行更精确的识别
                var openPorts = await ScanCommonPortsAsync(ipAddress);
                
                if (openPorts.Contains(80) || openPorts.Contains(443))
                {
                    if (openPorts.Contains(22) || openPorts.Contains(23))
                        return DeviceType.Router;
                    if (openPorts.Contains(631) || openPorts.Contains(9100))
                        return DeviceType.Printer;
                    if (openPorts.Contains(554) || openPorts.Contains(8080))
                        return DeviceType.Camera;
                }
                
                if (openPorts.Contains(3389) || openPorts.Contains(5900))
                    return DeviceType.Computer;
                
                if (openPorts.Contains(22) && (openPorts.Contains(3306) || openPorts.Contains(5432) || openPorts.Contains(1433)))
                    return DeviceType.Server;
                
                Console.WriteLine($"[DEBUG] 最终返回未知设备类型");
                return DeviceType.Unknown;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] 设备类型识别异常: {ex.Message}");
                return DeviceType.Unknown;
            }
        }
        
        private async Task<List<int>> ScanCommonPortsAsync(string ipAddress)
        {
            var openPorts = new List<int>();
            var commonPorts = new[] { 22, 23, 53, 80, 135, 139, 443, 445, 554, 631, 993, 995, 1433, 3306, 3389, 5432, 5900, 8080, 9100 };
            
            var tasks = commonPorts.Select(async port =>
            {
                try
                {
                    using var client = new System.Net.Sockets.TcpClient();
                    var connectTask = client.ConnectAsync(ipAddress, port);
                    var timeoutTask = Task.Delay(1000);
                    
                    if (await Task.WhenAny(connectTask, timeoutTask) == connectTask && client.Connected)
                    {
                        return port;
                    }
                }
                catch { }
                return -1;
            });
            
            var results = await Task.WhenAll(tasks);
            openPorts.AddRange(results.Where(p => p != -1));
            
            return openPorts;
        }
    }
}