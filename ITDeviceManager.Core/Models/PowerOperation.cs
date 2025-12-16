using System.ComponentModel.DataAnnotations;

namespace ITDeviceManager.Core.Models;

public class PowerOperation
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public virtual Device Device { get; set; } = null!;

    public PowerOperationType Operation { get; set; }

    public PowerOperationResult Result { get; set; }

    [StringLength(500)]
    public string? ResultMessage { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [StringLength(100)]
    public string? RequestedBy { get; set; }

    [StringLength(200)]
    public string? Notes { get; set; }
}

public enum PowerOperationType
{
    WakeOnLan = 1,
    Shutdown = 2,
    Restart = 3,
    Sleep = 4,
    Hibernate = 5
}

public enum PowerOperationResult
{
    Pending = 0,
    Success = 1,
    Failed = 2,
    Timeout = 3,
    NotSupported = 4
}
