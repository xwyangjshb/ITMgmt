using ITDeviceManager.API.Data;
using ITDeviceManager.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITDeviceManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Operator}")] // Only Admin and Operator can manage relations
public class DeviceRelationsController : ControllerBase
{
    private readonly DeviceContext _context;

    public DeviceRelationsController(DeviceContext context)
    {
        _context = context;
    }

    [HttpGet]
    [AllowAnonymous] // Allow public access to view relations
    public async Task<ActionResult<IEnumerable<DeviceRelation>>> GetDeviceRelations()
    {
        return await _context.DeviceRelations
            .Include(r => r.ParentDevice)
            .Include(r => r.ChildDevice)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    [AllowAnonymous] // Allow public access to view relation details
    public async Task<ActionResult<DeviceRelation>> GetDeviceRelation(int id)
    {
        var relation = await _context.DeviceRelations
            .Include(r => r.ParentDevice)
            .Include(r => r.ChildDevice)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (relation == null)
        {
            return NotFound();
        }

        return relation;
    }

    [HttpPost]
    public async Task<ActionResult<DeviceRelation>> CreateDeviceRelation(DeviceRelation relation)
    {
        // 验证父子设备存在
        var parentExists = await _context.Set<Device>().AnyAsync(d => d.Id == relation.ParentDeviceId);
        var childExists = await _context.Set<Device>().AnyAsync(d => d.Id == relation.ChildDeviceId);

        if (!parentExists || !childExists)
        {
            return BadRequest("Parent or child device does not exist");
        }

        // 防止自引用
        if (relation.ParentDeviceId == relation.ChildDeviceId)
        {
            return BadRequest("Device cannot be related to itself");
        }

        // 检查是否已存在相同关系
        var existingRelation = await _context.DeviceRelations
            .FirstOrDefaultAsync(r => r.ParentDeviceId == relation.ParentDeviceId &&
                                     r.ChildDeviceId == relation.ChildDeviceId);
        if (existingRelation != null)
        {
            return Conflict("Relation already exists");
        }

        relation.CreatedAt = DateTime.UtcNow;
        _context.DeviceRelations.Add(relation);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetDeviceRelation), new { id = relation.Id }, relation);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDeviceRelation(int id, DeviceRelation relation)
    {
        if (id != relation.Id)
        {
            return BadRequest();
        }

        _context.Entry(relation).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!DeviceRelationExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDeviceRelation(int id)
    {
        var relation = await _context.DeviceRelations.FindAsync(id);
        if (relation == null)
        {
            return NotFound();
        }

        _context.DeviceRelations.Remove(relation);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("device/{deviceId}/children")]
    [AllowAnonymous] // Allow public access to view device children
    public async Task<ActionResult<IEnumerable<Device>>> GetChildDevices(int deviceId)
    {
        var childDevices = await _context.DeviceRelations
            .Where(r => r.ParentDeviceId == deviceId)
            .Include(r => r.ChildDevice)
            .Select(r => r.ChildDevice)
            .ToListAsync();

        return Ok(childDevices);
    }

    [HttpGet("device/{deviceId}/parents")]
    [AllowAnonymous] // Allow public access to view device parents
    public async Task<ActionResult<IEnumerable<Device>>> GetParentDevices(int deviceId)
    {
        var parentDevices = await _context.DeviceRelations
            .Where(r => r.ChildDeviceId == deviceId)
            .Include(r => r.ParentDevice)
            .Select(r => r.ParentDevice)
            .ToListAsync();

        return Ok(parentDevices);
    }

    private bool DeviceRelationExists(int id)
    {
        return _context.DeviceRelations.Any(e => e.Id == id);
    }
}
