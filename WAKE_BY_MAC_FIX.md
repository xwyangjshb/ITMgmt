# Wake-by-MAC API 错误修复

## 问题描述

使用通过 MAC 地址唤醒的 API 时遇到错误：

```
POST http://localhost:5095/api/Devices/mac/58:3B:D9:72:51:1B/wake

错误响应:
{
  "success": false,
  "error": "Exception occurred while waking device",
  "message": "The LINQ expression 'DbSet<Device>()
    .Where(d => DevicesController.NormalizeMacAddress(d.MACAddress) == __normalizedMac_0)'
    could not be translated..."
}
```

## 根本原因

Entity Framework Core 无法将 C# 方法 `NormalizeMacAddress()` 转换为 SQL 查询。这是因为 EF Core 需要将 LINQ 表达式转换为数据库可以执行的 SQL 语句，但自定义的 C# 方法无法被转换。

### 原始代码（有问题）
```csharp
// ❌ 这行代码会导致错误
var device = await _context.Set<Device>()
    .FirstOrDefaultAsync(d => NormalizeMacAddress(d.MACAddress) == normalizedMac);
```

Entity Framework 试图执行类似这样的操作：
```sql
-- EF 无法生成这样的 SQL，因为 NormalizeMacAddress() 不是数据库函数
SELECT * FROM Device
WHERE NormalizeMacAddress(MACAddress) = 'AABBCCDDEEFF'
```

## 解决方案

修改查询逻辑：先将所有设备加载到内存，然后在内存中进行 MAC 地址匹配。

### 修复后的代码
```csharp
// ✅ 修复：先加载数据到内存，再进行匹配
var devices = await _context.Set<Device>().ToListAsync();
var device = devices.FirstOrDefault(d =>
    NormalizeMacAddress(d.MACAddress ?? string.Empty) == normalizedMac
);
```

## 修改文件

**文件**: `ITDeviceManager.API/Controllers/DevicesController.cs`
**位置**: 第 447-450 行
**方法**: `WakeDeviceByMac(string macAddress)`

### 修改内容
```csharp
// 查找设备（通过规范化的 MAC 地址匹配）
// 注意：由于 NormalizeMacAddress 无法转换为 SQL，需要先加载数据到内存
var devices = await _context.Set<Device>().ToListAsync();
var device = devices.FirstOrDefault(d =>
    NormalizeMacAddress(d.MACAddress ?? string.Empty) == normalizedMac
);
```

## 如何应用修复

### 步骤 1: 停止当前运行的应用程序

应用程序正在运行，导致 DLL 文件被锁定。

**方法 1: 手动停止**
- 在运行 `dotnet run` 的终端窗口按 `Ctrl+C`

**方法 2: 强制终止所有 dotnet 进程**
```powershell
# PowerShell (管理员权限)
taskkill /F /IM dotnet.exe /T

# 或者只终止特定 PID (从错误信息中看到是 33692)
taskkill /F /PID 33692
```

### 步骤 2: 重新编译

```bash
cd E:\Docs\ITMgmt\ITDeviceManager.API
dotnet build
```

### 步骤 3: 启动应用程序

```bash
dotnet run
```

### 步骤 4: 测试 API

```bash
# 使用您的 MAC 地址测试
curl -X POST http://localhost:5095/api/devices/mac/58:3B:D9:72:51:1B/wake

# 或者使用 URL 编码格式（浏览器自动编码）
# 58:3B:D9:72:51:1B -> 58%3A3B%3AD9%3A72%3A51%3A1B
```

**预期成功响应**:
```json
{
  "success": true,
  "message": "Wake-on-LAN packet sent to [设备名称]",
  "device": {
    "id": 123,
    "name": "设备名称",
    "ipAddress": "192.168.x.x",
    "macAddress": "58:3B:D9:72:51:1B"
  },
  "operation": {
    "id": 456,
    "operation": 1,
    "result": 1,
    "resultMessage": "Wake-on-LAN packet sent successfully to 58:3B:D9:72:51:1B",
    "requestedAt": "2025-12-15T...",
    "completedAt": "2025-12-15T..."
  }
}
```

## 性能考虑

### 当前实现
```csharp
// 加载所有设备到内存，然后匹配
var devices = await _context.Set<Device>().ToListAsync();
var device = devices.FirstOrDefault(d =>
    NormalizeMacAddress(d.MACAddress ?? string.Empty) == normalizedMac
);
```

**性能特点**:
- ✅ 简单直接
- ✅ 适用于设备数量少的情况（< 1000 台）
- ⚠️ 设备数量多时（> 10000 台）可能有性能影响
- ⚠️ 每次查询都会加载所有设备

### 优化方案（如果需要）

如果设备数量很多，可以考虑以下优化：

#### 方案 1: 数据库存储规范化的 MAC 地址

在 `Device` 表中添加 `NormalizedMACAddress` 列：

```csharp
public class Device
{
    // 现有字段...
    public string MACAddress { get; set; }

    // 新增字段：规范化的 MAC 地址（用于快速查询）
    public string NormalizedMACAddress { get; set; }  // AABBCCDDEEFF
}

// 在保存设备时自动设置
device.NormalizedMACAddress = NormalizeMacAddress(device.MACAddress);
```

然后查询时：
```csharp
var device = await _context.Set<Device>()
    .FirstOrDefaultAsync(d => d.NormalizedMACAddress == normalizedMac);
```

#### 方案 2: 使用数据库字符串函数

使用 EF Core 支持的字符串函数进行模糊匹配：

```csharp
// 移除分隔符后进行比较（在数据库端执行）
var searchPattern = normalizedMac
    .Select((c, i) => i % 2 == 0 ? c.ToString() : c + "[-:]?")
    .Aggregate((a, b) => a + b);

var device = await _context.Set<Device>()
    .Where(d => EF.Functions.Like(
        d.MACAddress.Replace(":", "").Replace("-", "").Replace(" ", "").ToUpper(),
        normalizedMac
    ))
    .FirstOrDefaultAsync();
```

#### 方案 3: 使用缓存

如果设备列表变化不频繁，可以使用内存缓存：

```csharp
private static List<Device> _deviceCache;
private static DateTime _cacheExpiry;

public async Task<Device> FindDeviceByMacAsync(string normalizedMac)
{
    if (_deviceCache == null || DateTime.UtcNow > _cacheExpiry)
    {
        _deviceCache = await _context.Set<Device>().ToListAsync();
        _cacheExpiry = DateTime.UtcNow.AddMinutes(5); // 5 分钟缓存
    }

    return _deviceCache.FirstOrDefault(d =>
        NormalizeMacAddress(d.MACAddress ?? string.Empty) == normalizedMac
    );
}
```

## 为什么不使用其他方案？

### 为什么不使用 AsEnumerable()？
```csharp
// 这种方式也可以，但和 ToListAsync() 本质相同
var device = _context.Set<Device>()
    .AsEnumerable()  // 转为内存查询
    .FirstOrDefault(d => NormalizeMacAddress(d.MACAddress) == normalizedMac);
```

`AsEnumerable()` 和 `ToListAsync()` 都会加载所有数据到内存，但 `ToListAsync()` 更明确表达意图。

### 为什么不注册为 EF Core 函数？

虽然可以通过 `DbFunctionAttribute` 注册自定义函数映射到数据库函数，但：
1. MAC 地址规范化逻辑较复杂（移除分隔符、大小写转换）
2. 不同数据库（SQL Server、PostgreSQL、MySQL）实现不同
3. 增加复杂度，不值得

## 测试用例

### 测试 1: 标准冒号格式
```bash
curl -X POST http://localhost:5095/api/devices/mac/58:3B:D9:72:51:1B/wake
```

### 测试 2: 连字符格式
```bash
curl -X POST http://localhost:5095/api/devices/mac/58-3B-D9-72-51-1B/wake
```

### 测试 3: 无分隔符格式
```bash
curl -X POST http://localhost:5095/api/devices/mac/583BD972511B/wake
```

### 测试 4: 小写格式
```bash
curl -X POST http://localhost:5095/api/devices/mac/58:3b:d9:72:51:1b/wake
```

### 测试 5: 混合格式
```bash
curl -X POST http://localhost:5095/api/devices/mac/58-3b:D9-72:51-1B/wake
```

### 测试 6: URL 编码格式（浏览器）
```bash
# 浏览器会自动将 : 编码为 %3A
http://localhost:5095/api/devices/mac/58%3A3B%3AD9%3A72%3A51%3A1B/wake
```

所有这些格式都应该能找到 MAC 地址为 `58:3B:D9:72:51:1B` 的设备。

## 验证修复

### 1. 检查日志
启动应用后，查看控制台输出是否有错误。

### 2. 测试 API
```bash
# 方法 1: 使用 curl
curl -X POST http://localhost:5095/api/devices/mac/58:3B:D9:72:51:1B/wake

# 方法 2: 使用 PowerShell
Invoke-RestMethod -Uri "http://localhost:5095/api/devices/mac/58:3B:D9:72:51:1B/wake" -Method Post

# 方法 3: 使用浏览器（需要安装 REST 客户端插件）
POST http://localhost:5095/api/devices/mac/58:3B:D9:72:51:1B/wake
```

### 3. 检查数据库
确认设备确实存在：
```bash
# 获取所有设备
curl http://localhost:5095/api/devices

# 查找特定 MAC
curl http://localhost:5095/api/devices | jq '.[] | select(.macAddress | contains("58:3B"))'
```

## 常见问题

### Q1: 如果设备数量很多（> 10000 台），怎么优化？

使用上面提到的优化方案 1（添加 NormalizedMACAddress 列）或方案 3（缓存）。

### Q2: 其他两个唤醒端点是否有同样的问题？

不会。
- **通过 ID 唤醒**: `FindAsync(id)` 直接用主键查询，非常高效
- **通过名称唤醒**: `ToLower()` 是 EF Core 支持的字符串函数，可以转换为 SQL

### Q3: 为什么不在创建 MAC 端点时就使用 ToListAsync()？

当时考虑的是直接数据库查询更高效，但忽略了 EF Core 无法转换自定义方法的限制。现在已修复。

### Q4: 这个修复会影响性能吗？

对于小型部署（几百台设备），性能影响可以忽略不计。如果设备数量很多，请考虑使用优化方案。

## 总结

✅ **问题**: EF Core 无法转换 `NormalizeMacAddress()` 为 SQL
✅ **原因**: 自定义 C# 方法无法映射到数据库函数
✅ **修复**: 先用 `ToListAsync()` 加载数据，再在内存中匹配
✅ **性能**: 适用于中小型部署（< 1000 台设备）
✅ **优化**: 如需更好性能，可添加 NormalizedMACAddress 列

现在您可以使用任意格式的 MAC 地址成功唤醒设备了！🎉
