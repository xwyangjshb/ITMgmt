using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ITDeviceManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class LogsController : ControllerBase
{
    private readonly ILogger<LogsController> _logger;
    private readonly string _logDirectory;

    public LogsController(ILogger<LogsController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
    }

    /// <summary>
    /// 获取可用的日志文件列表
    /// </summary>
    [HttpGet("files")]
    public IActionResult GetLogFiles()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                return Ok(new { files = Array.Empty<object>() });
            }

            var files = Directory.GetFiles(_logDirectory, "log-*.txt")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .Select(f => new
                {
                    name = f.Name,
                    path = f.FullName,
                    size = f.Length,
                    lastModified = f.LastWriteTime,
                    sizeFormatted = FormatFileSize(f.Length)
                })
                .ToList();

            return Ok(new { files });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取日志文件列表时发生错误");
            return StatusCode(500, new { message = "获取日志文件列表失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 搜索和过滤日志条目
    /// </summary>
    /// <param name="search">关键词搜索</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="level">日志级别 (Information, Warning, Error)</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页条目数</param>
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? search,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        [FromQuery] string? level,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                return Ok(new
                {
                    total = 0,
                    page,
                    pageSize,
                    data = Array.Empty<object>()
                });
            }

            var logEntries = new List<LogEntry>();

            // 读取所有日志文件
            var logFiles = Directory.GetFiles(_logDirectory, "log-*.txt")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime);

            foreach (var file in logFiles)
            {
                try
                {
                    var entries = await ParseLogFileAsync(file.FullName);
                    logEntries.AddRange(entries);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "解析日志文件失败: {FileName}", file.Name);
                }
            }

            // 应用过滤条件
            var filteredEntries = logEntries.AsEnumerable();

            if (start.HasValue)
            {
                filteredEntries = filteredEntries.Where(e => e.Timestamp >= start.Value);
            }

            if (end.HasValue)
            {
                filteredEntries = filteredEntries.Where(e => e.Timestamp <= end.Value);
            }

            if (!string.IsNullOrWhiteSpace(level))
            {
                filteredEntries = filteredEntries.Where(e =>
                    e.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                filteredEntries = filteredEntries.Where(e =>
                    (e.Message?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Exception?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.SourceContext?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            // 排序（最新的在前）
            var sortedEntries = filteredEntries.OrderByDescending(e => e.Timestamp).ToList();

            var total = sortedEntries.Count;

            // 分页
            var pagedEntries = sortedEntries
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                total,
                page,
                pageSize,
                data = pagedEntries
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取日志时发生错误");
            return StatusCode(500, new { message = "获取日志失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 获取日志统计信息
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                return Ok(new
                {
                    totalEntries = 0,
                    errorCount = 0,
                    warningCount = 0,
                    infoCount = 0,
                    fileCount = 0,
                    totalSize = 0
                });
            }

            var logFiles = Directory.GetFiles(_logDirectory, "log-*.txt")
                .Select(f => new FileInfo(f))
                .ToList();

            var totalSize = logFiles.Sum(f => f.Length);
            var fileCount = logFiles.Count;

            var logEntries = new List<LogEntry>();

            foreach (var file in logFiles)
            {
                try
                {
                    var entries = await ParseLogFileAsync(file.FullName);
                    logEntries.AddRange(entries);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "解析日志文件失败: {FileName}", file.Name);
                }
            }

            var errorCount = logEntries.Count(e => e.Level.Equals("Error", StringComparison.OrdinalIgnoreCase));
            var warningCount = logEntries.Count(e => e.Level.Equals("Warning", StringComparison.OrdinalIgnoreCase));
            var infoCount = logEntries.Count(e => e.Level.Equals("Information", StringComparison.OrdinalIgnoreCase));

            return Ok(new
            {
                totalEntries = logEntries.Count,
                errorCount,
                warningCount,
                infoCount,
                fileCount,
                totalSize,
                totalSizeFormatted = FormatFileSize(totalSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取日志统计信息时发生错误");
            return StatusCode(500, new { message = "获取统计信息失败", error = ex.Message });
        }
    }

    /// <summary>
    /// 解析日志文件
    /// </summary>
    private async Task<List<LogEntry>> ParseLogFileAsync(string filePath)
    {
        var entries = new List<LogEntry>();

        // 以只读和共享模式打开文件，允许其他进程写入
        using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using var reader = new StreamReader(fileStream);
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                // Serilog Compact JSON格式解析
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var entry = new LogEntry
                {
                    Timestamp = root.TryGetProperty("@t", out var timestamp)
                        ? DateTime.Parse(timestamp.GetString()!)
                        : DateTime.MinValue,
                    Level = root.TryGetProperty("@l", out var level)
                        ? level.GetString() ?? "Information"
                        : "Information",
                    Message = root.TryGetProperty("@mt", out var messageTemplate)
                        ? messageTemplate.GetString()
                        : root.TryGetProperty("@m", out var message)
                            ? message.GetString()
                            : null,
                    Exception = root.TryGetProperty("@x", out var exception)
                        ? exception.GetString()
                        : null,
                    SourceContext = root.TryGetProperty("SourceContext", out var sourceContext)
                        ? sourceContext.GetString()
                        : null
                };

                // 提取其他属性
                var properties = new Dictionary<string, object?>();
                foreach (var property in root.EnumerateObject())
                {
                    if (!property.Name.StartsWith("@") && property.Name != "SourceContext")
                    {
                        properties[property.Name] = property.Value.ValueKind switch
                        {
                            JsonValueKind.String => property.Value.GetString(),
                            JsonValueKind.Number => property.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => null,
                            _ => property.Value.ToString()
                        };
                    }
                }

                if (properties.Count > 0)
                {
                    entry.Properties = properties;
                }

                entries.Add(entry);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "解析日志行失败: {Line}", line);
            }
        }

        return entries;
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// 日志条目模型
    /// </summary>
    private class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = "Information";
        public string? Message { get; set; }
        public string? Exception { get; set; }
        public string? SourceContext { get; set; }
        public Dictionary<string, object?>? Properties { get; set; }
    }
}
