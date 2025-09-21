using ITDeviceManager.Core.Models;

namespace ITDeviceManager.Core.Services
{
    /// <summary>
    /// 提供WOL服务
    /// </summary>
    public interface IWakeOnLanService
    {
        Task<bool> WakeDeviceAsync(string macAddress, string? ipAddress = null);
        Task<bool> WakeDeviceAsync(Device device);
        Task<PowerOperation> CreateWakeOperationAsync(int deviceId, string? requestedBy = null);
        Task<bool> ShutdownDeviceAsync(string ipAddress);
    }
}