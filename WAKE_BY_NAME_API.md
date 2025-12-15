# Wake-on-LAN 通过设备名称唤醒 API

## 功能概述

新增了通过**设备名称**唤醒机器的 Web API 端点，使用机器名比 ID 更直观、更方便。

### 两种唤醒方式对比

| 方式 | 端点 | 参数 | 使用场景 |
|------|------|------|----------|
| **通过 ID** | `POST /api/devices/{id}/wake` | 设备ID (数字) | 适合程序内部调用，ID 固定不变 |
| **通过名称** ⭐ | `POST /api/devices/name/{name}/wake` | 设备名称 (字符串) | 适合手动调用，名称更易记 |

## 新端点详情

### 路由
```
POST /api/devices/name/{name}/wake
```

### 认证
- 当前设置为 `[AllowAnonymous]` - 无需认证
- 生产环境建议启用认证

### 参数
| 参数 | 位置 | 类型 | 必填 | 说明 |
|------|------|------|------|------|
| name | URL路径 | string | ✅ | 设备名称，不区分大小写 |

### 特性
- ✅ **不区分大小写**: "MyComputer" = "mycomputer" = "MYCOMPUTER"
- ✅ **自动查找设备**: 通过名称自动匹配数据库中的设备
- ✅ **统一响应格式**: 与通过 ID 唤醒的响应格式完全一致
- ✅ **完整审计日志**: 操作记录保存到 PowerOperations 表

## 使用示例

### 示例 1: 使用 curl

```bash
# 唤醒名为 "MyPC" 的设备
curl -X POST http://localhost:5095/api/devices/name/MyPC/wake

# 唤醒名为 "Server-01" 的设备
curl -X POST http://localhost:5095/api/devices/name/Server-01/wake

# 名称不区分大小写
curl -X POST http://localhost:5095/api/devices/name/mypc/wake
```

### 示例 2: 使用 PowerShell

```powershell
# 唤醒设备
$deviceName = "MyPC"
$response = Invoke-RestMethod -Uri "http://localhost:5095/api/devices/name/$deviceName/wake" -Method Post
Write-Host "结果: $($response.message)"

# 批量唤醒多个设备
$devices = @("PC-001", "PC-002", "Server-Main")
foreach ($device in $devices) {
    Write-Host "正在唤醒 $device..."
    Invoke-RestMethod -Uri "http://localhost:5095/api/devices/name/$device/wake" -Method Post
    Start-Sleep -Seconds 1
}
```

### 示例 3: 使用 JavaScript (前端)

```javascript
async function wakeDeviceByName(deviceName) {
    try {
        const response = await fetch(`/api/devices/name/${encodeURIComponent(deviceName)}/wake`, {
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

// 使用示例
wakeDeviceByName('MyPC');
wakeDeviceByName('办公室电脑');
```

### 示例 4: 使用 Python

```python
import requests

def wake_device_by_name(device_name):
    url = f"http://localhost:5095/api/devices/name/{device_name}/wake"

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

# 使用
wake_device_by_name('MyPC')
wake_device_by_name('192.168.1.100')  # 如果设备名称就是IP也可以
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
    "requestedAt": "2025-12-14T10:30:00Z",
    "completedAt": "2025-12-14T10:30:01Z"
  }
}
```

### 设备未找到 (404 Not Found)

```json
{
  "error": "Device not found",
  "message": "No device found with name 'NonExistentPC'"
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

### 异常错误 (500 Internal Server Error)

```json
{
  "success": false,
  "error": "Exception occurred while waking device",
  "message": "Detailed error message here"
}
```

## 实用脚本

### 快速唤醒脚本 (wake.ps1)

```powershell
# wake.ps1 - 快速唤醒指定设备
param(
    [Parameter(Mandatory=$true)]
    [string]$DeviceName,

    [string]$ApiBase = "http://localhost:5095"
)

Write-Host "正在唤醒设备: $DeviceName..." -ForegroundColor Cyan

try {
    $uri = "$ApiBase/api/devices/name/$DeviceName/wake"
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
# .\wake.ps1 -DeviceName "MyPC"
# .\wake.ps1 "Server-01"
```

### 批量唤醒脚本 (wake-all.ps1)

```powershell
# wake-all.ps1 - 批量唤醒设备列表
param(
    [string[]]$DeviceNames = @(),
    [string]$DeviceListFile,
    [string]$ApiBase = "http://localhost:5095",
    [int]$DelaySeconds = 2
)

# 从文件读取设备列表（如果指定）
if ($DeviceListFile -and (Test-Path $DeviceListFile)) {
    $DeviceNames = Get-Content $DeviceListFile
}

if ($DeviceNames.Count -eq 0) {
    Write-Host "❌ 请提供设备名称或设备列表文件" -ForegroundColor Red
    Write-Host "使用方法: .\wake-all.ps1 -DeviceNames 'PC1','PC2','PC3'"
    Write-Host "或:      .\wake-all.ps1 -DeviceListFile devices.txt"
    exit 1
}

Write-Host "准备唤醒 $($DeviceNames.Count) 个设备..." -ForegroundColor Cyan
Write-Host ""

$successCount = 0
$failCount = 0

foreach ($deviceName in $DeviceNames) {
    $deviceName = $deviceName.Trim()
    if ([string]::IsNullOrWhiteSpace($deviceName)) { continue }

    Write-Host "[$($successCount + $failCount + 1)/$($DeviceNames.Count)] 唤醒: $deviceName" -ForegroundColor Yellow

    try {
        $uri = "$ApiBase/api/devices/name/$deviceName/wake"
        $response = Invoke-RestMethod -Uri $uri -Method Post -ErrorAction Stop

        if ($response.success) {
            Write-Host "  ✅ 成功" -ForegroundColor Green
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
# .\wake-all.ps1 -DeviceNames "PC1","PC2","Server01"
# .\wake-all.ps1 -DeviceListFile "devices.txt" -DelaySeconds 3
```

### devices.txt 示例

```
MyPC
Office-Computer
Server-Main
Backup-Server
Dev-Machine
```

## Swagger UI 测试

1. **启动应用**:
   ```bash
   cd ITDeviceManager.API
   dotnet run
   ```

2. **打开 Swagger**:
   ```
   https://localhost:5001/swagger
   ```

3. **找到新端点**:
   - 展开 `Devices` 控制器
   - 找到 `POST /api/devices/name/{name}/wake`

4. **测试**:
   - 点击 "Try it out"
   - 输入设备名称（如 "MyPC"）
   - 点击 "Execute"
   - 查看响应

## 常见使用场景

### 场景 1: 定时唤醒任务

使用 Windows 任务计划程序 + PowerShell 脚本:

```powershell
# scheduled-wake.ps1
# 每天早上 8:00 唤醒办公室所有电脑

$devices = @(
    "Office-PC-001",
    "Office-PC-002",
    "Office-PC-003",
    "Meeting-Room-PC"
)

foreach ($device in $devices) {
    Invoke-RestMethod -Uri "http://server:5095/api/devices/name/$device/wake" -Method Post
    Start-Sleep -Seconds 2
}
```

### 场景 2: 远程管理面板

创建简单的 HTML 控制面板:

```html
<!DOCTYPE html>
<html>
<head>
    <title>设备管理面板</title>
</head>
<body>
    <h1>快速唤醒</h1>
    <div id="devices">
        <button onclick="wakeByName('Office-PC')">办公电脑</button>
        <button onclick="wakeByName('Lab-Server')">实验室服务器</button>
        <button onclick="wakeByName('Backup-01')">备份服务器</button>
    </div>

    <script>
    async function wakeByName(name) {
        const response = await fetch(`/api/devices/name/${name}/wake`, {
            method: 'POST'
        });
        const result = await response.json();
        alert(result.success ? `✅ ${result.message}` : `❌ ${result.error}`);
    }
    </script>
</body>
</html>
```

### 场景 3: Home Assistant 集成

```yaml
# configuration.yaml
rest_command:
  wake_office_pc:
    url: "http://your-server:5095/api/devices/name/Office-PC/wake"
    method: POST

automation:
  - alias: "早晨唤醒办公电脑"
    trigger:
      platform: time
      at: "08:00:00"
    action:
      service: rest_command.wake_office_pc
```

## 名称规范建议

为了更好地使用此功能，建议采用统一的设备命名规范：

### 推荐格式

```
<位置>-<类型>-<编号>
```

示例:
- `Office-PC-001`
- `Lab-Server-Main`
- `MeetingRoom-Display-01`
- `Warehouse-Camera-05`

### 避免的命名

- ❌ 使用空格: `My PC` → 推荐: `MyPC` 或 `My-PC`
- ❌ 特殊字符: `PC#1`, `Server@Office` → 推荐: `PC-01`, `Server-Office`
- ❌ 纯数字: `12345` → 推荐: `PC-12345`
- ❌ 中文（虽然支持，但URL编码麻烦）: `办公室电脑` → 推荐: `Office-PC`

## 故障排查

### Q1: 提示 "Device not found"

**检查**:
```bash
# 获取所有设备列表
curl http://localhost:5095/api/devices | jq '.[] | {id, name}'

# 查看设备的实际名称
curl http://localhost:5095/api/devices/3
```

**原因**: 设备名称不匹配（注意空格、特殊字符）

### Q2: 名称包含特殊字符

如果设备名包含空格或特殊字符，需要 URL 编码:

```bash
# 设备名: "My PC"
curl -X POST "http://localhost:5095/api/devices/name/My%20PC/wake"
```

```javascript
// JavaScript 自动编码
const deviceName = "My PC";
fetch(`/api/devices/name/${encodeURIComponent(deviceName)}/wake`, {
    method: 'POST'
});
```

### Q3: 重名设备

如果有多个设备同名，API 会返回第一个匹配的设备。

**解决方案**:
1. 使用唯一的设备名称
2. 或使用通过 ID 唤醒的方式: `/api/devices/{id}/wake`

## 性能考虑

- **查询性能**: 通过名称查询比 ID 慢一点（需要字符串匹配）
- **建议**: 如果有性能要求，仍然使用 ID 方式；名称方式更适合人工操作
- **索引**: 可以在 Device 表的 Name 列添加索引提升查询速度

## 安全建议

1. **生产环境启用认证**:
   ```csharp
   [HttpPost("name/{name}/wake")]
   [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Operator}")]
   ```

2. **限制访问来源**: 使用防火墙或 IP 白名单

3. **审计日志**: 操作自动记录到 PowerOperations 表

4. **速率限制**: 防止滥用（考虑使用 AspNetCoreRateLimit 包）

## API 对比总结

| 特性 | 通过 ID | 通过名称 |
|------|---------|----------|
| 端点 | `/api/devices/{id}/wake` | `/api/devices/name/{name}/wake` |
| 易记性 | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| 性能 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| 稳定性 | ⭐⭐⭐⭐⭐ (ID不变) | ⭐⭐⭐⭐ (名称可能改变) |
| 适用场景 | 程序调用 | 手动操作 |
| URL长度 | 短 | 中等 |
| 推荐用途 | API集成、自动化 | 命令行、快速测试 |

## 总结

✅ **新增端点**: `POST /api/devices/name/{name}/wake`
✅ **保留端点**: `POST /api/devices/{id}/wake`
✅ **不区分大小写**: 名称匹配更灵活
✅ **统一响应**: 两种方式返回相同格式
✅ **完整审计**: 所有操作都有日志记录

现在您可以更方便地通过设备名称唤醒机器了！🎉
