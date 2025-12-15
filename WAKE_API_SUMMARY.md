# Wake-on-LAN API 端点总览

## 所有可用的唤醒端点

IT Device Manager 现在提供 **三种** Wake-on-LAN 唤醒方式，满足不同使用场景：

### 1️⃣ 通过设备 ID 唤醒
```
POST /api/devices/{id}/wake
```
**示例**:
```bash
curl -X POST http://localhost:5095/api/devices/5/wake
```

**特点**:
- ✅ 性能最佳（主键查询）
- ✅ ID 永不改变
- ❌ 不易记忆
- 🎯 适用场景: API 集成、程序调用

---

### 2️⃣ 通过设备名称唤醒
```
POST /api/devices/name/{name}/wake
```
**示例**:
```bash
curl -X POST http://localhost:5095/api/devices/name/MyPC/wake
curl -X POST http://localhost:5095/api/devices/name/office-computer/wake
```

**特点**:
- ✅ 易记易用
- ✅ 不区分大小写
- ❌ 名称可能改变
- 🎯 适用场景: 手动操作、快速测试

---

### 3️⃣ 通过 MAC 地址唤醒 ⭐ 最新
```
POST /api/devices/mac/{macAddress}/wake
```

**示例**:
```bash
# 支持多种格式，不区分大小写
curl -X POST http://localhost:5095/api/devices/mac/AA:BB:CC:DD:EE:FF/wake
curl -X POST http://localhost:5095/api/devices/mac/aa-bb-cc-dd-ee-ff/wake
curl -X POST http://localhost:5095/api/devices/mac/AABBCCDDEEFF/wake
```

**支持的格式**:
| 格式 | 示例 |
|------|------|
| 冒号分隔 | `AA:BB:CC:DD:EE:FF` |
| 连字符分隔 | `AA-BB-CC-DD-EE-FF` |
| 无分隔符 | `AABBCCDDEEFF` |
| 小写 | `aa:bb:cc:dd:ee:ff` |
| 混合 | `aa-BB:cc-DD:ee-FF` |

**特点**:
- ✅ MAC 地址唯一且固定
- ✅ 支持多种格式
- ✅ 自动格式验证
- ✅ 不区分大小写
- ✅ 从硬件标签直接获取
- 🎯 适用场景: 硬件管理、网络扫描、批量导入

---

## 快速对比

| 方式 | 端点 | 优点 | 缺点 | 推荐场景 |
|------|------|------|------|----------|
| **ID** | `/api/devices/{id}/wake` | 性能最佳、稳定 | 不易记 | API 集成 |
| **名称** | `/api/devices/name/{name}/wake` | 易记易用 | 名称可变 | 手动操作 |
| **MAC** ⭐ | `/api/devices/mac/{mac}/wake` | 唯一、多格式 | 较长 | 硬件管理 |

---

## 统一的响应格式

所有三种方式都返回相同格式的响应：

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

### 错误响应
- **404 Not Found**: 设备不存在
- **400 Bad Request**: MAC 地址格式无效（仅 MAC 端点）
- **500 Internal Server Error**: 发送失败

---

## 使用示例

### PowerShell 统一脚本
```powershell
# wake-device.ps1 - 支持三种方式唤醒
param(
    [string]$Id,
    [string]$Name,
    [string]$Mac,
    [string]$ApiBase = "http://localhost:5095"
)

if ($Id) {
    $uri = "$ApiBase/api/devices/$Id/wake"
} elseif ($Name) {
    $uri = "$ApiBase/api/devices/name/$Name/wake"
} elseif ($Mac) {
    $uri = "$ApiBase/api/devices/mac/$Mac/wake"
} else {
    Write-Host "❌ 请提供 -Id, -Name 或 -Mac 参数" -ForegroundColor Red
    exit 1
}

Write-Host "正在唤醒设备..." -ForegroundColor Cyan
$response = Invoke-RestMethod -Uri $uri -Method Post

if ($response.success) {
    Write-Host "✅ 成功: $($response.message)" -ForegroundColor Green
} else {
    Write-Host "❌ 失败: $($response.error)" -ForegroundColor Red
}

# 使用方法:
# .\wake-device.ps1 -Id 5
# .\wake-device.ps1 -Name "MyPC"
# .\wake-device.ps1 -Mac "AA:BB:CC:DD:EE:FF"
```

### JavaScript 统一函数
```javascript
async function wakeDevice({ id, name, mac }) {
    let endpoint;

    if (id) {
        endpoint = `/api/devices/${id}/wake`;
    } else if (name) {
        endpoint = `/api/devices/name/${encodeURIComponent(name)}/wake`;
    } else if (mac) {
        endpoint = `/api/devices/mac/${encodeURIComponent(mac)}/wake`;
    } else {
        throw new Error('Please provide id, name, or mac');
    }

    const response = await fetch(endpoint, { method: 'POST' });
    return await response.json();
}

// 使用示例:
wakeDevice({ id: 5 });
wakeDevice({ name: 'MyPC' });
wakeDevice({ mac: 'AA:BB:CC:DD:EE:FF' });
```

### Python 统一函数
```python
import requests

def wake_device(*, device_id=None, name=None, mac=None,
                base_url="http://localhost:5095"):
    """
    通过 ID、名称或 MAC 地址唤醒设备
    """
    if device_id:
        endpoint = f"{base_url}/api/devices/{device_id}/wake"
    elif name:
        endpoint = f"{base_url}/api/devices/name/{name}/wake"
    elif mac:
        endpoint = f"{base_url}/api/devices/mac/{mac}/wake"
    else:
        raise ValueError("Please provide device_id, name, or mac")

    response = requests.post(endpoint)
    return response.json()

# 使用示例:
wake_device(device_id=5)
wake_device(name="MyPC")
wake_device(mac="AA:BB:CC:DD:EE:FF")
```

---

## 选择指南

### 什么时候使用 ID 方式？
- ✅ 程序内部 API 调用
- ✅ 自动化脚本（已知 ID）
- ✅ 高性能要求的场景
- ✅ 需要最稳定的标识符

### 什么时候使用名称方式？
- ✅ 命令行手动操作
- ✅ 快速测试和调试
- ✅ 用户友好的界面
- ✅ 设备名称易记且不常改

### 什么时候使用 MAC 地址方式？⭐
- ✅ 从硬件标签/文档获取 MAC
- ✅ 网络扫描后批量唤醒
- ✅ 从其他系统导入设备
- ✅ 不确定设备 ID 或名称
- ✅ MAC 地址作为唯一标识
- ✅ 处理多种 MAC 格式来源

---

## 实际使用案例

### 案例 1: IT 管理员早晨批量唤醒
```powershell
# 使用名称方式（易记）
$offices = @("Office-PC-01", "Office-PC-02", "Office-PC-03")
foreach ($pc in $offices) {
    Invoke-RestMethod -Uri "http://server:5095/api/devices/name/$pc/wake" -Method Post
}
```

### 案例 2: 从资产清单导入并唤醒
```python
# CSV 包含 MAC 地址，使用 MAC 方式
import csv
import requests

with open('asset_list.csv') as f:
    reader = csv.DictReader(f)
    for row in reader:
        mac = row['MAC Address']
        requests.post(f"http://server:5095/api/devices/mac/{mac}/wake")
```

### 案例 3: 程序自动化（已知 ID）
```csharp
// 使用 ID 方式（性能最佳）
var deviceIds = new[] { 1, 2, 3, 4, 5 };
foreach (var id in deviceIds)
{
    await httpClient.PostAsync($"/api/devices/{id}/wake", null);
}
```

---

## 文档链接

- 📄 [通过名称唤醒 API 详细文档](WAKE_BY_NAME_API.md)
- 📄 [通过 MAC 地址唤醒 API 详细文档](WAKE_BY_MAC_API.md)
- 📄 [Wake-on-LAN 功能修复说明](WAKE_ON_LAN_FIX.md)
- 📄 [按钮测试说明](TESTING_WAKE_BUTTON.md)

---

## 测试所有端点

```bash
# 1. 通过 ID
curl -X POST http://localhost:5095/api/devices/5/wake

# 2. 通过名称
curl -X POST http://localhost:5095/api/devices/name/MyPC/wake

# 3. 通过 MAC（冒号格式）
curl -X POST http://localhost:5095/api/devices/mac/AA:BB:CC:DD:EE:FF/wake

# 4. 通过 MAC（连字符格式）
curl -X POST http://localhost:5095/api/devices/mac/AA-BB-CC-DD-EE-FF/wake

# 5. 通过 MAC（无分隔符）
curl -X POST http://localhost:5095/api/devices/mac/AABBCCDDEEFF/wake

# 6. 通过 MAC（小写）
curl -X POST http://localhost:5095/api/devices/mac/aa:bb:cc:dd:ee:ff/wake
```

---

## 认证说明

所有 Wake-on-LAN 端点当前设置为 `[AllowAnonymous]`，无需认证即可使用。

**生产环境建议**:
```csharp
[HttpPost("mac/{macAddress}/wake")]
[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Operator}")]
public async Task<IActionResult> WakeDeviceByMac(string macAddress)
```

---

## 总结

✅ **三种唤醒方式**: ID、名称、MAC 地址
✅ **灵活选择**: 根据使用场景选择最合适的方式
✅ **统一响应**: 所有方式返回相同格式
✅ **完整审计**: 所有操作记录到 PowerOperations 表
✅ **多格式支持**: MAC 地址支持多种格式（最新）
✅ **简单易用**: 无需认证，开箱即用

选择适合您场景的唤醒方式，享受灵活的设备管理！🚀
