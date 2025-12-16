using System.ComponentModel.DataAnnotations;

namespace ITDeviceManager.Core.Models;

public class MagicPacketCapture
{
    public int Id { get; set; }

    [Required]
    [StringLength(17)]
    public string TargetMACAddress { get; set; } = string.Empty;

    [Required]
    [StringLength(45)]  // IPv6-ready
    public string SourceIPAddress { get; set; } = string.Empty;

    public DateTime CapturedAt { get; set; }

    public int? MatchedDeviceId { get; set; }
    public virtual Device? MatchedDevice { get; set; }

    [StringLength(100)]
    public string? MatchedDeviceName { get; set; }

    public int PacketSize { get; set; }

    public bool IsValid { get; set; }

    [StringLength(200)]
    public string? Notes { get; set; }
}
