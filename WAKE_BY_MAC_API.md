# Wake-on-LAN 通过 MAC 地址唤醒 API

## 功能概述

新增了通过 **MAC 地址**唤醒机器的 Web API 端点，支持多种常见格式，不区分大小写。

### 三种唤醒方式对比

| 方式 | 端点 | 参数 | 使用场景 |
|------|------|------|----------|
| **通过 ID** | `POST /api/devices/{id}/wake` | 设备ID (数字) | 适合程序内部调用，ID 固定不变 |
| **通过名称** | `POST /api/devices/name/{name}/wake` | 设备名称 (字符串) | 适合手动调用，名称易记 |
| **通过 MAC** ⭐ | `POST /api/devices/mac/{macAddress}/wake` | MAC地址 (多格式) | 最灵活，支持多种格式 |

## 新端点详情

### 路由
```
POST /api/devices/mac/{macAddress}/wake
```

### 认证
- 当前设置为 `[AllowAnonymous]` - 无需认证
- 生产环境建议启用认证

### 参数
| 参数 | 位置 | 类型 | 必填 | 说明 |
|------|------|------|------|------|
| macAddress | URL路径 | string | ✅ | MAC地址，支持多种格式，不区分大小写 |

### 支持的 MAC 地址格式

✅ **所有以下格式均支持**：

| 格式 | 示例 | 说明 |
|------|------|------|
| 冒号分隔 (推荐) | `AA:BB:CC:DD:EE:FF` | 最常见的格式 |
| 连字符分隔 | `AA-BB-CC-DD-EE-FF` | Windows 常用格式 |
| 无分隔符 | `AABBCCDDEEFF` | 紧凑格式 |
| 小写 | `aa:bb:cc:dd:ee:ff` | 自动转换为大写 |
| 混合格式 | `aa:BB-cc:DD-ee:FF` | 自动规范化 |

**注意**: 所有格式会自动规范化为统一的内部格式进行匹配。

### 特性
- ✅ **多格式支持**: 冒号、连字符、无分隔符
- ✅ **不区分大小写**: "AA:BB:CC" = "aa:bb:cc" = "Aa:Bb:Cc"
- ✅ **自动验证**: 无效格式会返回 400 Bad Request
- ✅ **智能匹配**: 自动规范化后与数据库中的 MAC 地址匹配
- ✅ **统一响应格式**: 与其他唤醒方式的响应格式完全一致
- ✅ **完整审计日志**: 操作记录保存到 PowerOperations 表

## 使用示例

### 示例 1: 使用 curl (冒号格式)

```bash
# 标准冒号分隔格式
curl -X POST http://localhost:5095/api/devices/mac/AA:BB:CC:DD:EE:FF/wake

# 小写也可以
curl -X POST http://localhost:5095/api/devices/mac/aa:bb:cc:dd:ee:ff/wake
```

### 示例 2: 使用 curl (连字符格式)

```bash
# Windows 风格连字符格式
curl -X POST http://localhost:5095/api/devices/mac/AA-BB-CC-DD-EE-FF/wake

# 小写连字符
curl -X POST http://localhost:5095/api/devices/mac/aa-bb-cc-dd-ee-ff/wake
```

### 示例 3: 使用 curl (无分隔符格式)

```bash
# 紧凑格式（无分隔符）
curl -X POST http://localhost:5095/api/devices/mac/AABBCCDDEEFF/wake

# 小写紧凑格式
curl -X POST http://localhost:5095/api/devices/mac/aabbccddeeff/wake
```

### 示例 4: 使用 PowerShell

```powershell
# 方法1: 标准格式
$mac = "AA:BB:CC:DD:EE:FF"
$response = Invoke-RestMethod -Uri "http://localhost:5095/api/devices/mac/$mac/wake" -Method Post
Write-Host "结果: $($response.message)"

# 方法2: 连字符格式（从 Get-NetAdapter 获取）
$adapter = Get-NetAdapter | Where-Object {$_.Status -eq "Up"} | Select-Object -First 1
$mac = $adapter.MacAddress  # 格式: AA-BB-CC-DD-EE-FF
$response = Invoke-RestMethod -Uri "http://localhost:5095/api/devices/mac/$mac/wake" -Method Post

# 方法3: 批量唤醒
$macAddresses = @(
    "AA:BB:CC:DD:EE:01",
    "AA-BB-CC-DD-EE-02",
    "AABBCCDDEEFF03"
)

foreach ($mac in $macAddresses) {
    Write-Host "正在唤醒 $mac..."
    Invoke-RestMethod -Uri "http://localhost:5095/api/devices/mac/$mac/wake" -Method Post
    Start-Sleep -Seconds 1
}
```

### 示例 5: 使用 JavaScript (前端)

```javascript
async function wakeDeviceByMac(macAddress) {
    try {
        // MAC 地址可以是任何支持的格式
        const response = await fetch(`/api/devices/mac/${encodeURIComponent(macAddress)}/wake`, {
            method: 'POST'
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.message || error.error);
        }

        const result = await response.json();

        if (result.success) {
            console.log(`✅ ${result.message}`);
            console.log('设备信息:', result.device);
        } else {
            console.error(`❌ ${result.error}`);
        }

        return result;
    } catch (error) {
        console.error('唤醒失败:', error);
        throw error;
    }
}

// 使用示例 - 支持多种格式
wakeDeviceByMac('AA:BB:CC:DD:EE:FF');
wakeDeviceByMac('aa-bb-cc-dd-ee-ff');
wakeDeviceByMac('AABBCCDDEEFF');
```

### 示例 6: 使用 Python

```python
import requests

def wake_device_by_mac(mac_address):
    """
    通过 MAC 地址唤醒设备
    支持格式: AA:BB:CC:DD:EE:FF, AA-BB-CC-DD-EE-FF, AABBCCDDEEFF
    """
    url = f"http://localhost:5095/api/devices/mac/{mac_address}/wake"

    try:
        response = requests.post(url)
        response.raise_for_status()

        result = response.json()

        if result.get('success'):
            print(f"✅ {result['message']}")
            print(f"设备: {result['device']['name']} ({result['device']['macAddress']})")
            return True
        else:
            print(f"❌ {result.get('error', 'Unknown error')}")
            return False

    except requests.exceptions.RequestException as e:
        print(f"请求失败: {e}")
        return False

# 使用示例 - 支持多种格式
wake_device_by_mac('AA:BB:CC:DD:EE:FF')
wake_device_by_mac('aa-bb-cc-dd-ee-ff')
wake_device_by_mac('AABBCCDDEEFF')
```

### 示例 7: 使用 C# (.NET)

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;

public class WakeOnLanClient
{
    private readonly HttpClient _httpClient;

    public WakeOnLanClient(string baseUrl = "http://localhost:5095")
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task<bool> WakeDeviceByMacAsync(string macAddress)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/devices/mac/{macAddress}/wake",
                null
            );

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"✅ 唤醒成功: {result}");
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ 唤醒失败: {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 异常: {ex.Message}");
            return false;
        }
    }
}

// 使用示例
var client = new WakeOnLanClient();
await client.WakeDeviceByMacAsync("AA:BB:CC:DD:EE:FF");
await client.WakeDeviceByMacAsync("aa-bb-cc-dd-ee-ff");
await client.WakeDeviceByMacAsync("AABBCCDDEEFF");
```

## API 响应

### 成功响应 (200 OK)

```json
{
  "success": true,
  "message": "Wake-on-LAN packet sent to MyPC",
  "device": {
    "id": 5,
    "name": "MyPC",
    "ipAddress": "192.168.1.100",
    "macAddress": "AA:BB:CC:DD:EE:FF"
  },
  "operation": {
    "id": 15,
    "operation": 1,
    "result": 1,
    "resultMessage": "Wake-on-LAN packet sent successfully to AA:BB:CC:DD:EE:FF",
    "requestedAt": "2025-12-15T10:30:00Z",
    "completedAt": "2025-12-15T10:30:01Z"
  }
}
```

### 设备未找到 (404 Not Found)

```json
{
  "error": "Device not found",
  "message": "No device found with MAC address 'aa:bb:cc:dd:ee:ff' (normalized: AA:BB:CC:DD:EE:FF)"
}
```

### 无效的 MAC 地址格式 (400 Bad Request)

```json
{
  "error": "Invalid MAC address format",
  "message": "MAC address 'INVALID' is invalid. Expected format: AA:BB:CC:DD:EE:FF, AA-BB-CC-DD-EE-FF, or AABBCCDDEEFF"
}
```

### 发送失败 (500 Internal Server Error)

```json
{
  "success": false,
  "error": "Failed to send Wake-on-LAN packet",
  "message": "Network error or invalid MAC address"
}
```

## 实用脚本

### 快速唤醒脚本 (wake-mac.ps1)

```powershell
# wake-mac.ps1 - 通过 MAC 地址快速唤醒设备
param(
    [Parameter(Mandatory=$true)]
    [string]$MacAddress,

    [string]$ApiBase = "http://localhost:5095"
)

Write-Host "正在唤醒设备: $MacAddress..." -ForegroundColor Cyan

try {
    $uri = "$ApiBase/api/devices/mac/$MacAddress/wake"
    $response = Invoke-RestMethod -Uri $uri -Method Post -ErrorAction Stop

    if ($response.success) {
        Write-Host "✅ 成功: $($response.message)" -ForegroundColor Green
        Write-Host "   设备: $($response.device.name)" -ForegroundColor Gray
        Write-Host "   IP: $($response.device.ipAddress)" -ForegroundColor Gray
        Write-Host "   MAC: $($response.device.macAddress)" -ForegroundColor Gray
    } else {
        Write-Host "❌ 失败: $($response.error)" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ 错误: $($_.Exception.Message)" -ForegroundColor Red
}

# 使用方法:
# .\wake-mac.ps1 -MacAddress "AA:BB:CC:DD:EE:FF"
# .\wake-mac.ps1 "aa-bb-cc-dd-ee-ff"
# .\wake-mac.ps1 "AABBCCDDEEFF"
```

### 批量唤醒脚本 (wake-macs.ps1)

```powershell
# wake-macs.ps1 - 批量通过 MAC 地址唤醒设备
param(
    [string[]]$MacAddresses = @(),
    [string]$MacListFile,
    [string]$ApiBase = "http://localhost:5095",
    [int]$DelaySeconds = 2
)

# 从文件读取 MAC 地址列表（如果指定）
if ($MacListFile -and (Test-Path $MacListFile)) {
    $MacAddresses = Get-Content $MacListFile
}

if ($MacAddresses.Count -eq 0) {
    Write-Host "❌ 请提供 MAC 地址或 MAC 地址列表文件" -ForegroundColor Red
    Write-Host "使用方法: .\wake-macs.ps1 -MacAddresses 'AA:BB:CC:DD:EE:01','AA-BB-CC-DD-EE-02'"
    Write-Host "或:      .\wake-macs.ps1 -MacListFile macs.txt"
    exit 1
}

Write-Host "准备唤醒 $($MacAddresses.Count) 个设备..." -ForegroundColor Cyan
Write-Host ""

$successCount = 0
$failCount = 0

foreach ($mac in $MacAddresses) {
    $mac = $mac.Trim()
    if ([string]::IsNullOrWhiteSpace($mac)) { continue }

    Write-Host "[$($successCount + $failCount + 1)/$($MacAddresses.Count)] 唤醒: $mac" -ForegroundColor Yellow

    try {
        $uri = "$ApiBase/api/devices/mac/$mac/wake"
        $response = Invoke-RestMethod -Uri $uri -Method Post -ErrorAction Stop

        if ($response.success) {
            Write-Host "  ✅ 成功 - $($response.device.name)" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "  ❌ 失败: $($response.error)" -ForegroundColor Red
            $failCount++
        }
    } catch {
        Write-Host "  ❌ 错误: $($_.Exception.Message)" -ForegroundColor Red
        $failCount++
    }

    if ($DelaySeconds -gt 0) {
        Start-Sleep -Seconds $DelaySeconds
    }
}

Write-Host ""
Write-Host "完成! 成功: $successCount, 失败: $failCount" -ForegroundColor Cyan

# 使用方法:
# .\wake-macs.ps1 -MacAddresses "AA:BB:CC:DD:EE:01","AA-BB-CC-DD-EE-02"
# .\wake-macs.ps1 -MacListFile "macs.txt" -DelaySeconds 3
```

### macs.txt 示例

```
AA:BB:CC:DD:EE:01
aa-bb-cc-dd-ee-02
AABBCCDDEEFF03
AA:bb:CC:dd:EE:04
```

### 从网络适配器获取 MAC 并唤醒 (get-and-wake.ps1)

```powershell
# get-and-wake.ps1 - 获取本机网卡 MAC 地址并唤醒目标设备
param(
    [string]$ApiBase = "http://localhost:5095"
)

# 获取所有活动网络适配器的 MAC 地址
$adapters = Get-NetAdapter | Where-Object {$_.Status -eq "Up"}

Write-Host "发现 $($adapters.Count) 个活动网络适配器:" -ForegroundColor Cyan
Write-Host ""

foreach ($adapter in $adapters) {
    $mac = $adapter.MacAddress
    Write-Host "[$($adapter.Name)] MAC: $mac" -ForegroundColor Yellow

    # 尝试唤醒这个 MAC 地址对应的设备
    try {
        $uri = "$ApiBase/api/devices/mac/$mac/wake"
        $response = Invoke-RestMethod -Uri $uri -Method Post -ErrorAction Stop

        if ($response.success) {
            Write-Host "  ✅ 找到并唤醒设备: $($response.device.name)" -ForegroundColor Green
        }
    } catch {
        Write-Host "  ℹ️  该 MAC 地址不在设备管理系统中" -ForegroundColor Gray
    }

    Write-Host ""
}
```

## Swagger UI 测试

1. **启动应用**:
   ```bash
   cd ITDeviceManager.API
   dotnet run
   ```

2. **打开 Swagger**:
   ```
   http://localhost:5095/swagger
   ```

3. **找到新端点**:
   - 展开 `Devices` 控制器
   - 找到 `POST /api/devices/mac/{macAddress}/wake`

4. **测试**:
   - 点击 "Try it out"
   - 输入 MAC 地址（任意支持的格式）:
     - `AA:BB:CC:DD:EE:FF`
     - `aa-bb-cc-dd-ee-ff`
     - `AABBCCDDEEFF`
   - 点击 "Execute"
   - 查看响应

## 常见使用场景

### 场景 1: 从硬件信息唤醒

```powershell
# 场景：你有设备的 MAC 地址，但不记得设备名或 ID
$targetMac = "AA:BB:CC:DD:EE:FF"  # 从设备标签或文档获取
Invoke-RestMethod -Uri "http://server:5095/api/devices/mac/$targetMac/wake" -Method Post
```

### 场景 2: 网络扫描后唤醒

```powershell
# 扫描网络获取所有 MAC 地址
$arpTable = arp -a | Select-String "([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})"

foreach ($entry in $arpTable) {
    # 提取 MAC 地址
    if ($entry -match "([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})") {
        $mac = $Matches[0]
        Write-Host "尝试唤醒: $mac"
        Invoke-RestMethod -Uri "http://server:5095/api/devices/mac/$mac/wake" -Method Post
    }
}
```

### 场景 3: 从 CSV 文件批量导入并唤醒

```powershell
# devices.csv 格式:
# DeviceName,MacAddress
# PC1,AA:BB:CC:DD:EE:01
# PC2,AA-BB-CC-DD-EE-02
# Server1,AABBCCDDEEFF03

$devices = Import-Csv "devices.csv"

foreach ($device in $devices) {
    Write-Host "唤醒 $($device.DeviceName) - MAC: $($device.MacAddress)"
    Invoke-RestMethod -Uri "http://server:5095/api/devices/mac/$($device.MacAddress)/wake" -Method Post
    Start-Sleep -Seconds 2
}
```

### 场景 4: Home Assistant 自动化

```yaml
# configuration.yaml
rest_command:
  wake_office_pc:
    url: "http://your-server:5095/api/devices/mac/AA:BB:CC:DD:EE:FF/wake"
    method: POST

automation:
  - alias: "早晨唤醒办公电脑"
    trigger:
      platform: time
      at: "08:00:00"
    action:
      service: rest_command.wake_office_pc
```

## MAC 地址格式说明

### 格式验证规则

API 会自动验证 MAC 地址格式：

1. **移除分隔符**: 自动移除 `:`、`-` 和空格
2. **转换大小写**: 统一转换为大写
3. **长度检查**: 必须是 12 个十六进制字符（6 字节）
4. **字符验证**: 只允许 `0-9` 和 `A-F`

### 格式转换示例

| 输入格式 | 规范化后 | 是否有效 |
|---------|----------|---------|
| `AA:BB:CC:DD:EE:FF` | `AABBCCDDEEFF` | ✅ |
| `aa:bb:cc:dd:ee:ff` | `AABBCCDDEEFF` | ✅ |
| `AA-BB-CC-DD-EE-FF` | `AABBCCDDEEFF` | ✅ |
| `aa-bb-cc-dd-ee-ff` | `AABBCCDDEEFF` | ✅ |
| `AABBCCDDEEFF` | `AABBCCDDEEFF` | ✅ |
| `aabbccddeeff` | `AABBCCDDEEFF` | ✅ |
| `AA BB CC DD EE FF` | `AABBCCDDEEFF` | ✅ |
| `AA:BB:CC:DD:EE` | - | ❌ (长度不对) |
| `GG:HH:II:JJ:KK:LL` | - | ❌ (无效字符) |
| `AABBCCDD` | - | ❌ (长度不对) |

### 数据库匹配逻辑

数据库中的 MAC 地址可能存储为不同格式，API 会自动处理：

```csharp
// 数据库中可能的格式:
// - AA:BB:CC:DD:EE:FF
// - AA-BB-CC-DD-EE-FF
// - AABBCCDDEEFF

// API 会将所有格式规范化后进行匹配
var normalizedInput = NormalizeMacAddress(macAddress);  // AABBCCDDEEFF
var device = devices.FirstOrDefault(d =>
    NormalizeMacAddress(d.MACAddress) == normalizedInput
);
```

## 故障排查

### Q1: 提示 "Invalid MAC address format"

**原因**: MAC 地址格式不符合要求

**检查**:
- 长度是否正确（12个十六进制字符）
- 是否包含非法字符（只允许 0-9、A-F、a-f、:、-）
- 分隔符是否正确

**示例**:
```bash
# ❌ 错误的格式
curl -X POST http://localhost:5095/api/devices/mac/AA:BB:CC/wake  # 长度不够
curl -X POST http://localhost:5095/api/devices/mac/GG:HH:II:JJ:KK:LL/wake  # 非法字符

# ✅ 正确的格式
curl -X POST http://localhost:5095/api/devices/mac/AA:BB:CC:DD:EE:FF/wake
curl -X POST http://localhost:5095/api/devices/mac/aabbccddeeff/wake
```

### Q2: 提示 "Device not found"

**原因**: 数据库中没有匹配的 MAC 地址

**检查**:
```bash
# 获取所有设备列表，查看实际的 MAC 地址
curl http://localhost:5095/api/devices | jq '.[] | {id, name, macAddress}'

# 查看特定设备
curl http://localhost:5095/api/devices/3 | jq '{name, macAddress}'
```

**提示**: MAC 地址匹配是规范化后进行的，所以格式不同不影响匹配。

### Q3: URL 编码问题

如果 MAC 地址包含特殊字符（如 `:`），在某些情况下可能需要 URL 编码：

```bash
# 方法1: 使用 --data-urlencode（推荐）
curl -G http://localhost:5095/api/devices/mac/wake \
  --data-urlencode "macAddress=AA:BB:CC:DD:EE:FF"

# 方法2: 手动编码（: = %3A）
curl -X POST http://localhost:5095/api/devices/mac/AA%3ABB%3ACC%3ADD%3AEE%3AFF/wake

# 方法3: 使用无分隔符格式（最简单）
curl -X POST http://localhost:5095/api/devices/mac/AABBCCDDEEFF/wake
```

**JavaScript 自动编码**:
```javascript
const mac = "AA:BB:CC:DD:EE:FF";
fetch(`/api/devices/mac/${encodeURIComponent(mac)}/wake`, {
    method: 'POST'
});
```

### Q4: 性能考虑

**查询性能**: 通过 MAC 地址查询需要规范化所有数据库中的 MAC 地址，比 ID 查询稍慢。

**优化建议**:
1. 在 Device 表的 MACAddress 列添加函数索引（如果数据库支持）
2. 或在数据库中存储规范化后的 MAC 地址副本
3. 高性能场景仍推荐使用 ID 方式

## 三种唤醒方式的选择建议

### 按 ID 唤醒
```
POST /api/devices/{id}/wake
```
**优点**:
- ⭐⭐⭐⭐⭐ 性能最佳（主键查询）
- ⭐⭐⭐⭐⭐ ID 永不改变

**缺点**:
- ⭐⭐ 不易记忆
- ⭐⭐ 需要先查询 ID

**适用场景**: 程序内部调用、API 集成、自动化脚本

### 按名称唤醒
```
POST /api/devices/name/{name}/wake
```
**优点**:
- ⭐⭐⭐⭐⭐ 易记易用
- ⭐⭐⭐⭐ 人类友好

**缺点**:
- ⭐⭐⭐ 名称可能改变
- ⭐⭐⭐⭐ 性能较好（字符串匹配）

**适用场景**: 手动操作、命令行工具、快速测试

### 按 MAC 地址唤醒 ⭐
```
POST /api/devices/mac/{macAddress}/wake
```
**优点**:
- ⭐⭐⭐⭐⭐ MAC 地址唯一且固定
- ⭐⭐⭐⭐⭐ 支持多种格式
- ⭐⭐⭐⭐ 从硬件标签直接获取
- ⭐⭐⭐⭐ 无需预先查询

**缺点**:
- ⭐⭐⭐ 性能中等（需要规范化）
- ⭐⭐ MAC 地址较长不易手动输入

**适用场景**: 硬件管理、网络扫描、批量导入、外部系统集成

## API 对比总结

| 特性 | 通过 ID | 通过名称 | 通过 MAC 地址 |
|------|---------|----------|--------------|
| 端点 | `/api/devices/{id}/wake` | `/api/devices/name/{name}/wake` | `/api/devices/mac/{macAddress}/wake` |
| 易记性 | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| 性能 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ |
| 稳定性 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| 灵活性 | ⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| 格式支持 | 单一 | 单一 | 多种格式 |
| 适用场景 | API集成 | 手动操作 | 硬件管理、批量操作 |
| 推荐用途 | 程序调用 | 命令行 | 硬件识别、网络扫描 |

## 总结

✅ **新增端点**: `POST /api/devices/mac/{macAddress}/wake`
✅ **保留端点**: `POST /api/devices/{id}/wake` 和 `POST /api/devices/name/{name}/wake`
✅ **多格式支持**: 冒号、连字符、无分隔符
✅ **不区分大小写**: 自动规范化
✅ **自动验证**: 无效格式返回 400 错误
✅ **统一响应**: 三种方式返回相同格式
✅ **完整审计**: 所有操作都有日志记录

现在您可以通过设备的 MAC 地址灵活地唤醒机器了！🎉

## 快速参考

```bash
# 三种唤醒方式一览

# 1. 通过 ID
curl -X POST http://localhost:5095/api/devices/5/wake

# 2. 通过名称
curl -X POST http://localhost:5095/api/devices/name/MyPC/wake

# 3. 通过 MAC 地址（推荐用于硬件管理）
curl -X POST http://localhost:5095/api/devices/mac/AA:BB:CC:DD:EE:FF/wake
curl -X POST http://localhost:5095/api/devices/mac/aa-bb-cc-dd-ee-ff/wake
curl -X POST http://localhost:5095/api/devices/mac/AABBCCDDEEFF/wake
```
