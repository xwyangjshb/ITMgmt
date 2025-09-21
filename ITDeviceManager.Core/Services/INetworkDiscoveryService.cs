using ITDeviceManager.Core.Models;

namespace ITDeviceManager.Core.Services
{
    public interface INetworkDiscoveryService
    {
        Task<List<Device>> DiscoverDevicesAsync(string networkRange);
        Task<Device?> GetDeviceInfoAsync(string ipAddress);
        Task<bool> PingDeviceAsync(string ipAddress);
        Task<string?> GetMacAddressAsync(string ipAddress);
        Task<string?> GetHostnameAsync(string ipAddress);
    }
}