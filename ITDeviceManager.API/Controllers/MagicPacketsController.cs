using ITDeviceManager.API.Data;
using ITDeviceManager.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITDeviceManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MagicPacketsController : ControllerBase
{
    private readonly DeviceContext _context;
    private readonly ILogger<MagicPacketsController> _logger;

    public MagicPacketsController(DeviceContext context, ILogger<MagicPacketsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 获取捕获的魔术包记录
    /// </summary>
    /// <param name="since">仅返回此时间之后的记录（用于轮询）</param>
    /// <param name="page">页码，从1开始</param>
    /// <param name="pageSize">每页记录数，默认50</param>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCaptures(
        [FromQuery] DateTime? since,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var query = _context.MagicPacketCaptures
                .Include(m => m.MatchedDevice)
                .OrderByDescending(m => m.CapturedAt)
                .AsQueryable();

            // 如果提供了since参数，仅返回此时间之后的记录
            if (since.HasValue)
            {
                query = query.Where(m => m.CapturedAt > since.Value);
            }

            // 计算总数
            var total = await query.CountAsync();

            // 分页
            var captures = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.Id,
                    m.TargetMACAddress,
                    m.SourceIPAddress,
                    m.CapturedAt,
                    m.PacketSize,
                    m.IsValid,
                    m.Notes,
                    MatchedDeviceId = m.MatchedDeviceId,
                    MatchedDeviceName = m.MatchedDevice != null ? m.MatchedDevice.Name : null,
                    MatchedDeviceIP = m.MatchedDevice != null ? m.MatchedDevice.IPAddress : null
                })
                .ToListAsync();

            _logger.LogDebug("返回 {Count} 条魔术包捕获记录，页码: {Page}, 自: {Since}",
                captures.Count, page, since?.ToString() ?? "全部");

            return Ok(new
            {
                total,
                page,
                pageSize,
                data = captures
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取魔术包捕获记录时发生错误");
            return StatusCode(500, new { message = "获取记录时发生错误", error = ex.Message });
        }
    }

    /// <summary>
    /// 获取魔术包捕获统计信息
    /// </summary>
    [HttpGet("stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var total = await _context.MagicPacketCaptures.CountAsync();
            var today = await _context.MagicPacketCaptures
                .Where(m => m.CapturedAt >= DateTime.UtcNow.Date)
                .CountAsync();

            var lastCapture = await _context.MagicPacketCaptures
                .OrderByDescending(m => m.CapturedAt)
                .Select(m => new
                {
                    m.CapturedAt,
                    m.TargetMACAddress,
                    m.SourceIPAddress,
                    MatchedDeviceName = m.MatchedDevice != null ? m.MatchedDevice.Name : null
                })
                .FirstOrDefaultAsync();

            var validCount = await _context.MagicPacketCaptures
                .CountAsync(m => m.IsValid);

            var matchedCount = await _context.MagicPacketCaptures
                .CountAsync(m => m.MatchedDeviceId != null);

            _logger.LogDebug("返回魔术包统计 - 总计: {Total}, 今天: {Today}, 有效: {Valid}, 已匹配: {Matched}",
                total, today, validCount, matchedCount);

            return Ok(new
            {
                totalCaptures = total,
                todayCaptures = today,
                validCaptures = validCount,
                matchedCaptures = matchedCount,
                lastCapture
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取魔术包统计信息时发生错误");
            return StatusCode(500, new { message = "获取统计信息时发生错误", error = ex.Message });
        }
    }

    /// <summary>
    /// 获取最近的捕获记录（用于实时监控）
    /// </summary>
    /// <param name="count">返回的记录数，默认10条</param>
    [HttpGet("recent")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRecent([FromQuery] int count = 10)
    {
        try
        {
            var captures = await _context.MagicPacketCaptures
                .Include(m => m.MatchedDevice)
                .OrderByDescending(m => m.CapturedAt)
                .Take(count)
                .Select(m => new
                {
                    m.Id,
                    m.TargetMACAddress,
                    m.SourceIPAddress,
                    m.CapturedAt,
                    m.IsValid,
                    MatchedDeviceName = m.MatchedDevice != null ? m.MatchedDevice.Name : null
                })
                .ToListAsync();

            return Ok(captures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最近的魔术包记录时发生错误");
            return StatusCode(500, new { message = "获取记录时发生错误", error = ex.Message });
        }
    }

    /// <summary>
    /// 删除指定时间之前的旧记录（用于清理）
    /// </summary>
    /// <param name="before">删除此时间之前的记录</param>
    [HttpDelete("cleanup")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CleanupOldRecords([FromQuery] DateTime before)
    {
        try
        {
            var oldRecords = await _context.MagicPacketCaptures
                .Where(m => m.CapturedAt < before)
                .ToListAsync();

            _context.MagicPacketCaptures.RemoveRange(oldRecords);
            await _context.SaveChangesAsync();

            _logger.LogInformation("清理了 {Count} 条旧的魔术包记录，截止日期: {Before}",
                oldRecords.Count, before);

            return Ok(new { message = $"已删除 {oldRecords.Count} 条旧记录", deletedCount = oldRecords.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理旧记录时发生错误");
            return StatusCode(500, new { message = "清理记录时发生错误", error = ex.Message });
        }
    }
}
