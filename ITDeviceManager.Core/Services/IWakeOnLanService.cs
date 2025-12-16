using ITDeviceManager.Core.Models;

namespace ITDeviceManager.Core.Services;

/// <summary>
/// 提供WOL服务
/// </summary>
public interface IWakeOnLanService
{
    public Task<bool> WakeDeviceAsync(string macAddress, string? ipAddress = null);
    public Task<bool> WakeDeviceAsync(Device device);
    public Task<PowerOperation> CreateWakeOperationAsync(int deviceId, string? requestedBy = null);
    public Task<bool> ShutdownDeviceAsync(string ipAddress);
}
