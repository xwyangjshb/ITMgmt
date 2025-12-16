using ITDeviceManager.Core.Models;
using ITDeviceManager.Core.Services;
using ITDeviceManager.API.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ITDeviceManager.API.Services;

public class MagicPacketListenerService : IMagicPacketListenerService
{
    private readonly ILogger<MagicPacketListenerService> _logger;
    private readonly DeviceContext _context;

    public MagicPacketListenerService(ILogger<MagicPacketListenerService> logger, DeviceContext context)
    {
        _logger = logger;
        _context = context;
    }

    public bool ValidateMagicPacket(byte[] packet)
    {
        try
        {
            // 魔术包必须是102字节
            if (packet.Length != 102)
            {
                _logger.LogDebug("包大小不正确: {PacketSize}字节（期望102字节）", packet.Length);
                return false;
            }

            // 前6个字节必须是0xFF
            for (int i = 0; i < 6; i++)
            {
                if (packet[i] != 0xFF)
                {
                    _logger.LogDebug("前6字节验证失败，位置{Index}的值为{Value}（期望0xFF）", i, packet[i]);
                    return false;
                }
            }

            // 提取MAC地址（第7-12字节）
            var macBytes = new byte[6];
            Array.Copy(packet, 6, macBytes, 0, 6);

            // 验证后面的96字节是否是MAC地址重复16次
            for (int i = 1; i < 16; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    if (packet[6 + i * 6 + j] != macBytes[j])
                    {
                        _logger.LogDebug("MAC重复验证失败，位置{Position}", 6 + i * 6 + j);
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "魔术包验证时发生异常");
            return false;
        }
    }

    public string? ExtractTargetMAC(byte[] packet)
    {
        try
        {
            if (packet.Length < 12)
            {
                return null;
            }

            // MAC地址在第7-12字节（索引6-11）
            var macBytes = new byte[6];
            Array.Copy(packet, 6, macBytes, 0, 6);

            // 格式化为 XX:XX:XX:XX:XX:XX
            return string.Join(":", macBytes.Select(b => b.ToString("X2")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取MAC地址时发生异常");
            return null;
        }
    }

    public async Task<MagicPacketCapture?> ParseMagicPacketAsync(byte[] packet, string sourceIP)
    {
        try
        {
            // 验证包格式
            bool isValid = ValidateMagicPacket(packet);

            // 提取目标MAC
            string? targetMac = ExtractTargetMAC(packet);

            if (string.IsNullOrEmpty(targetMac))
            {
                _logger.LogWarning("无法从包中提取MAC地址");
                return null;
            }

            // 查找匹配的设备
            var normalizedMac = targetMac.Replace(":", "").ToUpperInvariant();
            var devices = await _context.Set<Device>().ToListAsync();
            var matchedDevice = devices.FirstOrDefault(d =>
                d.MACAddress?.Replace(":", "").Replace("-", "").ToUpperInvariant() == normalizedMac);

            // 创建捕获记录
            var capture = new MagicPacketCapture
            {
                TargetMACAddress = targetMac,
                SourceIPAddress = sourceIP,
                CapturedAt = DateTime.UtcNow,
                PacketSize = packet.Length,
                IsValid = isValid,
                MatchedDeviceId = matchedDevice?.Id,
                MatchedDeviceName = matchedDevice?.Name,
                Notes = isValid ? null : "Invalid magic packet format"
            };

            _logger.LogInformation("解析魔术包成功 - 目标MAC: {TargetMac}, 来源: {SourceIP}, 匹配设备: {DeviceName}",
                targetMac, sourceIP, matchedDevice?.Name ?? "未知");

            return capture;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析魔术包时发生异常");
            return null;
        }
    }
}
