using System.Net.NetworkInformation;
using ITDeviceManager.API.Data;
using ITDeviceManager.Core.Models;
using ITDeviceManager.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITDeviceManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication for all endpoints by default
public class DevicesController : ControllerBase
{
    private readonly DeviceContext _context;
    private readonly INetworkDiscoveryService _discoveryService;
    private readonly IWakeOnLanService _wakeOnLanService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(DeviceContext context, INetworkDiscoveryService discoveryService, IWakeOnLanService wakeOnLanService, ILogger<DevicesController> logger)
    {
        _context = context;
        _discoveryService = discoveryService;
        _wakeOnLanService = wakeOnLanService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous] // Allow public access to view devices
    public async Task<IActionResult> GetDevices()
    {
        // prevent client caching
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        // load entities so we can update LastSeen/status if reachable
        var devices = await _context.Set<Device>().ToListAsync();

        var semaphore = new SemaphoreSlim(20); // limit concurrent pings
        var tasks = new List<Task>();
        var changed = false;

        foreach (var dev in devices)
        {
            var device = dev; // capture
            await semaphore.WaitAsync();
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(device.IPAddress))
                    {
                        try
                        {
                            using var p = new Ping();
                            var reply = await p.SendPingAsync(device.IPAddress, 400);
                            if (reply.Status == IPStatus.Success)
                            {
                                device.LastSeen = DateTime.UtcNow;
                                device.Status = DeviceStatus.Online;
                                device.UpdatedAt = DateTime.UtcNow;
                                changed = true;
                            }
                        }
                        catch
                        {
                            // ignore ping errors
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        if (changed)
        {
            await _context.SaveChangesAsync();
        }

        var projection = devices.Select(d => new
        {
            d.Id,
            d.Name,
            d.IPAddress,
            d.MACAddress,
            d.DeviceType,
            d.Status,
            d.LastSeen,
            d.CreatedAt,
            d.UpdatedAt,
            d.WakeOnLanEnabled,
            d.Description
        }).ToList();

        return Ok(projection);
    }

    [HttpPost("refresh")]
    [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Operator}")]
    public async Task<IActionResult> RefreshDevices([FromBody] RefreshRequest? request)
    {
        // If networkRange specified, run discovery for that range and update DB
        if (!string.IsNullOrWhiteSpace(request?.NetworkRange))
        {
            var discovered = await _discoveryService.DiscoverDevicesAsync(request!.NetworkRange);
            int added = 0, updated = 0;

            foreach (var d in discovered)
            {
                // normalize MAC for comparison
                var normMac = (d.MACAddress ?? string.Empty).Replace(":", "").Replace("-", "").Replace(" ", "").ToUpperInvariant();
                var existing = await _context.Set<Device>()
                    .FirstOrDefaultAsync(x => (x.MACAddress ?? string.Empty).Replace(":", "").Replace("-", "").Replace(" ", "").ToUpper() == normMac);

                if (existing == null)
                {
                    d.CreatedAt = DateTime.UtcNow;
                    d.UpdatedAt = DateTime.UtcNow;
                    d.LastSeen = DateTime.UtcNow;
                    _context.Set<Device>().Add(d);
                    added++;
                }
                else
                {
                    bool changed = false;
                    if (existing.IPAddress != d.IPAddress)
                    {
                        existing.IPAddress = d.IPAddress;
                        changed = true;
                    }
                    if (existing.DeviceType != d.DeviceType && d.DeviceType != DeviceType.Unknown)
                    {
                        existing.DeviceType = d.DeviceType;
                        changed = true;
                    }
                    // always update LastSeen/Status
                    existing.LastSeen = DateTime.UtcNow;
                    existing.Status = d.Status;
                    existing.UpdatedAt = DateTime.UtcNow;
                    if (changed)
                    {
                        updated++;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { action = "discovery", networkRange = request.NetworkRange, added, updated, total = discovered.Count });
        }

        // Otherwise ping existing devices and update LastSeen/status
        var devices = await _context.Set<Device>().ToListAsync();
        var sem = new SemaphoreSlim(20);
        var tasks = new List<Task>();
        int onlineCount = 0;

        foreach (var dev in devices)
        {
            var device = dev;
            await sem.WaitAsync();
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    bool reachable = false;
                    try
                    {
                        // prefer discovery service ping if available
                        if (_discoveryService != null)
                        {
                            try
                            {
                                reachable = await _discoveryService.PingDeviceAsync(device.IPAddress);
                            }
                            catch
                            {
                                // fallback to System Ping
                                using var p = new Ping();
                                var reply = await p.SendPingAsync(device.IPAddress, 400);
                                reachable = reply.Status == IPStatus.Success;
                            }
                        }
                        else
                        {
                            using var p = new Ping();
                            var reply = await p.SendPingAsync(device.IPAddress, 400);
                            reachable = reply.Status == IPStatus.Success;
                        }
                    }
                    catch
                    {
                        reachable = false;
                    }

                    if (reachable)
                    {
                        device.LastSeen = DateTime.UtcNow;
                        device.Status = DeviceStatus.Online;
                        device.UpdatedAt = DateTime.UtcNow;
                        Interlocked.Increment(ref onlineCount);
                    }
                    else
                    {
                        // don't mark offline here; background job handles offline detection
                    }
                }
                finally
                {
                    sem.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
        await _context.SaveChangesAsync();

        return Ok(new { action = "ping", checkedCount = devices.Count, online = onlineCount });
    }

    [HttpGet("{id}")]
    [AllowAnonymous] // Allow public access to view device details
    public async Task<IActionResult> GetDevice(int id)
    {
        var device = await _context.Set<Device>().FindAsync(id);
        if (device == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            device.Id,
            device.Name,
            device.IPAddress,
            device.MACAddress,
            device.DeviceType,
            device.Status,
            device.LastSeen,
            device.CreatedAt,
            device.UpdatedAt,
            device.WakeOnLanEnabled,
            device.Description
        });
    }

    [HttpPost("discover")]
    [AllowAnonymous] // Allow discovery without authentication for convenience
    public async Task<IActionResult> DiscoverDevices([FromBody] DiscoveryRequest request)
    {
        var discovered = await _discoveryService.DiscoverDevicesAsync(request.NetworkRange);
        return Ok(discovered);
    }

    /// <summary>
    /// Wake up device using Wake-on-LAN
    /// </summary>
    [HttpPost("{id}/wake")]
    [AllowAnonymous] // Allow wake without authentication for convenience (consider security implications)
    public async Task<IActionResult> WakeDevice(int id)
    {
        try
        {
            var device = await _context.Set<Device>().FindAsync(id);
            if (device == null)
            {
                return NotFound(new { error = "Device not found" });
            }

            // Check if WOL is enabled for this device
            if (false & !device.WakeOnLanEnabled)
            {
                return BadRequest(new { error = "Wake-on-LAN is not enabled for this device" });
            }

            // Create power operation record
            var operation = await _wakeOnLanService.CreateWakeOperationAsync(id, "WebUI");
            _context.PowerOperations.Add(operation);
            await _context.SaveChangesAsync();

            // Send WOL packet
            var success = await _wakeOnLanService.WakeDeviceAsync(device);

            // Update operation result
            operation.Result = success ? PowerOperationResult.Success : PowerOperationResult.Failed;
            operation.CompletedAt = DateTime.UtcNow;
            operation.ResultMessage = success
                ? $"Wake-on-LAN packet sent successfully to {device.MACAddress}"
                : "Failed to send Wake-on-LAN packet";

            await _context.SaveChangesAsync();

            if (success)
            {
                return Ok(new
                {
                    success = true,
                    message = $"Wake-on-LAN packet sent to {device.Name}",
                    device = new
                    {
                        device.Id,
                        device.Name,
                        device.IPAddress,
                        device.MACAddress
                    },
                    operation = new
                    {
                        operation.Id,
                        operation.Operation,
                        operation.Result,
                        operation.ResultMessage,
                        operation.RequestedAt,
                        operation.CompletedAt
                    }
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Failed to send Wake-on-LAN packet",
                    message = operation.ResultMessage
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                error = "Exception occurred while waking device",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Wake up device by name using Wake-on-LAN
    /// </summary>
    [HttpPost("name/{name}/wake")]
    [AllowAnonymous]
    public async Task<IActionResult> WakeDeviceByName(string name)
    {
        try
        {
            // 查找设备（不区分大小写）
            var device = await _context.Set<Device>()
                .FirstOrDefaultAsync(d => d.Name.ToLower() == name.ToLower());

            if (device == null)
            {
                return NotFound(new
                {
                    error = "Device not found",
                    message = $"No device found with name '{name}'"
                });
            }

            // Check if WOL is enabled for this device (currently disabled in code)
            if (false & !device.WakeOnLanEnabled)
            {
                return BadRequest(new { error = "Wake-on-LAN is not enabled for this device" });
            }

            // Create power operation record
            var operation = await _wakeOnLanService.CreateWakeOperationAsync(device.Id, "WebUI");
            _context.PowerOperations.Add(operation);
            await _context.SaveChangesAsync();

            // Send WOL packet
            var success = await _wakeOnLanService.WakeDeviceAsync(device);

            // Update operation result
            operation.Result = success ? PowerOperationResult.Success : PowerOperationResult.Failed;
            operation.CompletedAt = DateTime.UtcNow;
            operation.ResultMessage = success
                ? $"Wake-on-LAN packet sent successfully to {device.MACAddress}"
                : "Failed to send Wake-on-LAN packet";

            await _context.SaveChangesAsync();

            if (success)
            {
                return Ok(new
                {
                    success = true,
                    message = $"Wake-on-LAN packet sent to {device.Name}",
                    device = new
                    {
                        device.Id,
                        device.Name,
                        device.IPAddress,
                        device.MACAddress
                    },
                    operation = new
                    {
                        operation.Id,
                        operation.Operation,
                        operation.Result,
                        operation.ResultMessage,
                        operation.RequestedAt,
                        operation.CompletedAt
                    }
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Failed to send Wake-on-LAN packet",
                    message = operation.ResultMessage
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                error = "Exception occurred while waking device",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Wake up device by MAC address using Wake-on-LAN
    /// Supports multiple MAC address formats: AA:BB:CC:DD:EE:FF, AA-BB-CC-DD-EE-FF, AABBCCDDEEFF (case-insensitive)
    /// </summary>
    [HttpPost("mac/{macAddress}/wake")]
    [AllowAnonymous]
    public async Task<IActionResult> WakeDeviceByMac(string macAddress)
    {
        try
        {
            // 规范化 MAC 地址（移除分隔符，转大写）
            var normalizedMac = NormalizeMacAddress(macAddress);

            if (string.IsNullOrEmpty(normalizedMac))
            {
                return BadRequest(new
                {
                    error = "Invalid MAC address format",
                    message = $"MAC address '{macAddress}' is invalid. Expected format: AA:BB:CC:DD:EE:FF, AA-BB-CC-DD-EE-FF, or AABBCCDDEEFF"
                });
            }

            // 查找设备（通过规范化的 MAC 地址匹配）
            // 注意：由于 NormalizeMacAddress 无法转换为 SQL，需要先加载数据到内存
            var devices = await _context.Set<Device>().ToListAsync();
            var device = devices.FirstOrDefault(d => NormalizeMacAddress(d.MACAddress ?? string.Empty) == normalizedMac);

            bool isNewDevice = false;

            if (device == null)
            {
                // Auto-register new device with MAC-derived placeholder IP
                var formattedMac = FormatMacAddress(normalizedMac);
                var placeholderIp = GeneratePlaceholderIpFromMac(normalizedMac);

                _logger.LogInformation("[WOL] Auto-registering new device - MAC: {FormattedMac}, Placeholder IP: {PlaceholderIp}", formattedMac, placeholderIp);

                device = new Device
                {
                    Name = formattedMac,  // Use formatted MAC as name (e.g., "AA:BB:CC:DD:EE:FF")
                    MACAddress = formattedMac,
                    IPAddress = placeholderIp,  // Placeholder until discovered (e.g., "0.0.238.255")
                    DeviceType = DeviceType.Computer,  // Assume computer for WOL-capable devices
                    Status = DeviceStatus.Offline,  // Not yet awakened
                    WakeOnLanEnabled = true,  // Explicitly enabling since we're sending WOL
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow
                };

                try
                {
                    _context.Set<Device>().Add(device);
                    await _context.SaveChangesAsync();
                    isNewDevice = true;

                    _logger.LogInformation("[WOL] Device auto-registered successfully - ID: {DeviceId}, Name: {DeviceName}", device.Id, device.Name);
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                    when (ex.InnerException?.Message?.Contains("duplicate key") == true)
                {
                    // Handle race condition: another request registered the same MAC concurrently
                    _logger.LogWarning("[WOL] Concurrent registration detected for MAC {FormattedMac}, reloading from database", formattedMac);

                    // Reload from database
                    devices = await _context.Set<Device>().ToListAsync();
                    device = devices.FirstOrDefault(d => NormalizeMacAddress(d.MACAddress ?? string.Empty) == normalizedMac);

                    if (device == null)
                    {
                        // Still not found - this shouldn't happen, return error
                        return StatusCode(500, new
                        {
                            success = false,
                            error = "Failed to register or find device",
                            message = "Concurrent registration error"
                        });
                    }
                }
            }

            // Check if WOL is enabled for this device (currently disabled in code)
            if (false & !device.WakeOnLanEnabled)
            {
                return BadRequest(new { error = "Wake-on-LAN is not enabled for this device" });
            }

            // Create power operation record
            var operation = await _wakeOnLanService.CreateWakeOperationAsync(device.Id, "WebUI");
            _context.PowerOperations.Add(operation);
            await _context.SaveChangesAsync();

            // Send WOL packet
            var success = await _wakeOnLanService.WakeDeviceAsync(device);

            // Update operation result
            operation.Result = success ? PowerOperationResult.Success : PowerOperationResult.Failed;
            operation.CompletedAt = DateTime.UtcNow;
            operation.ResultMessage = success
                ? $"Wake-on-LAN packet sent successfully to {device.MACAddress}"
                : "Failed to send Wake-on-LAN packet";

            await _context.SaveChangesAsync();

            if (success)
            {
                return Ok(new
                {
                    success = true,
                    message = isNewDevice
                        ? $"Device auto-registered and Wake-on-LAN packet sent to {device.Name} (placeholder IP: {device.IPAddress})"
                        : $"Wake-on-LAN packet sent to {device.Name}",
                    isNewDevice = isNewDevice,
                    device = new
                    {
                        device.Id,
                        device.Name,
                        device.IPAddress,
                        device.MACAddress
                    },
                    operation = new
                    {
                        operation.Id,
                        operation.Operation,
                        operation.Result,
                        operation.ResultMessage,
                        operation.RequestedAt,
                        operation.CompletedAt
                    }
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Failed to send Wake-on-LAN packet",
                    message = operation.ResultMessage
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                error = "Exception occurred while waking device",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Normalize MAC address to a consistent format (remove separators, uppercase, validate)
    /// Supports formats: AA:BB:CC:DD:EE:FF, AA-BB-CC-DD-EE-FF, AABBCCDDEEFF
    /// </summary>
    private string NormalizeMacAddress(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
        {
            return string.Empty;
        }

        // Remove all separators (: - and spaces)
        var normalized = macAddress.Replace(":", "").Replace("-", "").Replace(" ", "").ToUpperInvariant();

        // Validate: must be exactly 12 hexadecimal characters
        if (normalized.Length != 12)
        {
            return string.Empty;
        }

        // Validate: all characters must be valid hex (0-9, A-F)
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, "^[0-9A-F]{12}$"))
        {
            return string.Empty;
        }

        return normalized;
    }

    /// <summary>
    /// Format normalized MAC address to standard colon-separated format
    /// </summary>
    private string FormatMacAddress(string normalizedMac)
    {
        if (string.IsNullOrEmpty(normalizedMac) || normalizedMac.Length != 12)
        {
            return normalizedMac;
        }

        return string.Join(":", Enumerable.Range(0, 6).Select(i => normalizedMac.Substring(i * 2, 2)));
    }

    /// <summary>
    /// Generates a unique placeholder IP address from MAC address
    /// Format: 0.0.X.Y where X,Y are the last 2 bytes of MAC in decimal
    /// Example: MAC AA:BB:CC:DD:EE:FF (normalized: AABBCCDDEEFF) -> 0.0.238.255
    /// </summary>
    private string GeneratePlaceholderIpFromMac(string normalizedMac)
    {
        if (string.IsNullOrEmpty(normalizedMac) || normalizedMac.Length != 12)
        {
            return "0.0.0.1"; // Fallback
        }

        try
        {
            // Extract last 2 bytes (characters 8-11 of 12-char string)
            string byte5Hex = normalizedMac.Substring(8, 2);   // e.g., "EE"
            string byte6Hex = normalizedMac.Substring(10, 2);  // e.g., "FF"

            // Convert to decimal
            int byte5 = Convert.ToInt32(byte5Hex, 16);  // e.g., 238
            int byte6 = Convert.ToInt32(byte6Hex, 16);  // e.g., 255

            // Generate placeholder IP in 0.0.X.Y format
            return $"0.0.{byte5}.{byte6}";
        }
        catch
        {
            return "0.0.0.1"; // Fallback on any error
        }
    }

    /// <summary>
    /// Shutdown device remotely (placeholder - requires additional implementation)
    /// </summary>
    [HttpPost("{id}/shutdown")]
    [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Operator}")]
    public async Task<IActionResult> ShutdownDevice(int id)
    {
        try
        {
            var device = await _context.Set<Device>().FindAsync(id);
            if (device == null)
            {
                return NotFound(new { error = "Device not found" });
            }

            var operation = new PowerOperation
            {
                DeviceId = id,
                Operation = PowerOperationType.Shutdown,
                Result = PowerOperationResult.Pending,
                RequestedBy = "WebUI",
                RequestedAt = DateTime.UtcNow
            };

            _context.PowerOperations.Add(operation);
            await _context.SaveChangesAsync();

            var success = await _wakeOnLanService.ShutdownDeviceAsync(device.IPAddress);

            operation.Result = success ? PowerOperationResult.Success : PowerOperationResult.Failed;
            operation.CompletedAt = DateTime.UtcNow;
            operation.ResultMessage = success
                ? "Shutdown command sent successfully"
                : "Failed to send shutdown command (not implemented)";

            await _context.SaveChangesAsync();

            if (success)
            {
                return Ok(new { success = true, message = "Shutdown command sent", operation });
            }
            else
            {
                return StatusCode(501, new
                {
                    success = false,
                    error = "Shutdown not implemented",
                    message = "Remote shutdown requires SSH/WMI/IPMI configuration"
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                error = "Exception occurred while shutting down device",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Update device details
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Operator}")]
    public async Task<IActionResult> UpdateDevice(int id, [FromBody] DeviceUpdateRequest request)
    {
        try
        {
            var device = await _context.Set<Device>().FindAsync(id);
            if (device == null)
            {
                return NotFound(new { error = "Device not found" });
            }

            // Update fields if provided
            if (!string.IsNullOrEmpty(request.Name))
            {
                device.Name = request.Name;
            }

            if (request.WakeOnLanEnabled.HasValue)
            {
                device.WakeOnLanEnabled = request.WakeOnLanEnabled.Value;
            }

            if (!string.IsNullOrEmpty(request.Description))
            {
                device.Description = request.Description;
            }

            device.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Device updated successfully", device });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to update device",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Delete device
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> DeleteDevice(int id)
    {
        try
        {
            var device = await _context.Set<Device>().FindAsync(id);
            if (device == null)
            {
                return NotFound(new { error = "Device not found" });
            }

            _context.Set<Device>().Remove(device);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Device {device.Name} deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to delete device",
                message = ex.Message
            });
        }
    }

    public class DiscoveryRequest { public string NetworkRange { get; set; } = string.Empty; }
    public class RefreshRequest { public string? NetworkRange { get; set; } }
    public class DeviceUpdateRequest
    {
        public string? Name { get; set; }
        public bool? WakeOnLanEnabled { get; set; }
        public string? Description { get; set; }
    }
}
