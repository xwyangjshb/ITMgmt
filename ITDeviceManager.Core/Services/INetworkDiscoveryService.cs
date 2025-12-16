using ITDeviceManager.Core.Models;

namespace ITDeviceManager.Core.Services;

public interface INetworkDiscoveryService
{
    public Task<List<Device>> DiscoverDevicesAsync(string networkRange);
    public Task<Device?> GetDeviceInfoAsync(string ipAddress);
    public Task<bool> PingDeviceAsync(string ipAddress);
    public Task<string?> GetMacAddressAsync(string ipAddress);
    public Task<string?> GetHostnameAsync(string ipAddress);
}
