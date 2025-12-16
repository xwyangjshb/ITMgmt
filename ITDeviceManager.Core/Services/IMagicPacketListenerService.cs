using ITDeviceManager.Core.Models;

namespace ITDeviceManager.Core.Services;

public interface IMagicPacketListenerService
{
    /// <summary>
    /// 验证魔术包格式是否正确
    /// </summary>
    bool ValidateMagicPacket(byte[] packet);

    /// <summary>
    /// 从魔术包中提取目标MAC地址
    /// </summary>
    string? ExtractTargetMAC(byte[] packet);

    /// <summary>
    /// 解析魔术包并创建捕获记录
    /// </summary>
    Task<MagicPacketCapture?> ParseMagicPacketAsync(byte[] packet, string sourceIP);
}
