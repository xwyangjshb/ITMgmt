# Wake-on-LAN 功能修复说明

## 问题诊断

**原始错误**:
```
POST http://localhost:5095/api/devices/3/wake 404 (Not Found)
唤醒设备失败: SyntaxError: Failed to execute 'json' on 'Response': Unexpected end of JSON input
```

**根本原因**: DevicesController 被修改后，Wake-on-LAN 端点（`/api/devices/{id}/wake`）被删除了。

## 已修复内容

### 1. 恢复了 Wake-on-LAN 端点
**路由**: `POST /api/devices/{id}/wake`

**功能**:
- 查找设备并验证存在
- 检查设备是否启用了 WOL
- 创建 PowerOperation 记录用于审计
- 发送 Wake-on-LAN 魔术包
- 返回详细的操作结果

**认证**: 设置为 `[AllowAnonymous]` 方便使用（如需加强安全可改为需要认证）

### 2. 添加了其他缺失的端点
- `POST /api/devices/{id}/shutdown` - 远程关机（需要 Admin/Operator 权限）
- `PUT /api/devices/{id}` - 更新设备信息（需要 Admin/Operator 权限）
- `DELETE /api/devices/{id}` - 删除设备（需要 Admin 权限）

## 测试步骤

### 方法 1: 使用现有前端（推荐）

1. **启动应用**:
   ```bash
   cd ITDeviceManager.API
   dotnet run
   ```

2. **打开浏览器**:
   ```
   http://localhost:5095
   ```

3. **测试 Wake-on-LAN**:
   - 在设备列表中找到支持 WOL 的设备
   - 点击 "唤醒" 按钮
   - 查看响应消息

4. **检查结果**:
   - 成功: 显示 "Wake-on-LAN packet sent to {设备名}"
   - 失败: 显示错误原因（如 "Wake-on-LAN is not enabled for this device"）

### 方法 2: 使用 Swagger UI

1. **打开 Swagger**:
   ```
   https://localhost:5001/swagger
   ```

2. **测试端点**:
   - 找到 `POST /api/devices/{id}/wake`
   - 点击 "Try it out"
   - 输入设备 ID
   - 点击 "Execute"

3. **查看响应**:
   ```json
   {
     "success": true,
     "message": "Wake-on-LAN packet sent to Device Name",
     "device": {
       "id": 3,
       "name": "My Computer",
       "ipAddress": "192.168.1.100",
       "macAddress": "AA:BB:CC:DD:EE:FF"
     },
     "operation": {
       "id": 1,
       "operation": 1,
       "result": 1,
       "resultMessage": "Wake-on-LAN packet sent successfully to AA:BB:CC:DD:EE:FF",
       "requestedAt": "2025-12-14T10:00:00Z",
       "completedAt": "2025-12-14T10:00:01Z"
     }
   }
   ```

### 方法 3: 使用 curl

```bash
# 唤醒设备 ID 为 3 的设备
curl -X POST http://localhost:5095/api/devices/3/wake

# 预期成功响应
{
  "success": true,
  "message": "Wake-on-LAN packet sent to Device Name",
  ...
}
```

## API 响应说明

### 成功响应 (200 OK)
```json
{
  "success": true,
  "message": "Wake-on-LAN packet sent to {设备名}",
  "device": { ... },
  "operation": { ... }
}
```

### 设备未找到 (404 Not Found)
```json
{
  "error": "Device not found"
}
```

### WOL 未启用 (400 Bad Request)
```json
{
  "error": "Wake-on-LAN is not enabled for this device"
}
```

### 发送失败 (500 Internal Server Error)
```json
{
  "success": false,
  "error": "Failed to send Wake-on-LAN packet",
  "message": "详细错误信息"
}
```

## 前端集成说明

如果您在前端遇到 CORS 或认证问题，可能需要更新 `app.js`:

### 基本调用示例
```javascript
async function wakeDevice(deviceId) {
    try {
        const response = await fetch(`http://localhost:5095/api/devices/${deviceId}/wake`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || error.message || 'Wake failed');
        }

        const result = await response.json();

        if (result.success) {
            alert(`成功: ${result.message}`);
        } else {
            alert(`失败: ${result.message || result.error}`);
        }
    } catch (error) {
        console.error('Wake device error:', error);
        alert(`唤醒设备失败: ${error.message}`);
    }
}
```

### 带认证的调用（如果启用认证）
```javascript
async function wakeDevice(deviceId) {
    const token = localStorage.getItem('authToken'); // 如果有认证

    const headers = {
        'Content-Type': 'application/json'
    };

    if (token) {
        headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`http://localhost:5095/api/devices/${deviceId}/wake`, {
        method: 'POST',
        headers: headers
    });

    // ... 处理响应
}
```

## Wake-on-LAN 工作原理

1. **解析 MAC 地址**: 将格式化的 MAC 地址（如 `AA:BB:CC:DD:EE:FF`）转换为字节数组

2. **创建魔术包**:
   - 前 6 字节: `0xFF 0xFF 0xFF 0xFF 0xFF 0xFF`
   - 后续: MAC 地址重复 16 次
   - 总大小: 102 字节

3. **发送 UDP 包**:
   - 目标端口: 9 (也可以是 7)
   - 广播地址: `255.255.255.255`
   - 如果提供了 IP，也发送到目标 IP

4. **记录操作**: 在 PowerOperations 表中记录操作历史用于审计

## 常见问题

### Q1: 设备没有唤醒
**可能原因**:
- 设备未开启 WOL 功能（BIOS 设置）
- 网络交换机不支持 WOL
- 设备已关闭太久（某些设备需要软关机才能响应 WOL）
- MAC 地址不正确
- 设备和服务器不在同一子网

**解决方案**:
1. 在设备 BIOS 中启用 "Wake on LAN" 或 "Power on by PCI-E"
2. 在操作系统网络适配器设置中启用 WOL
3. 确保设备是软关机（Shutdown）而不是硬关机（断电）
4. 验证 MAC 地址正确
5. 如果跨子网，需要配置路由器支持 WOL

### Q2: 返回 404 错误
**原因**: 端点不存在或路由不正确

**解决方案**:
1. 确认应用已重新构建和启动
2. 检查端口号是否正确（5095 或 5001）
3. 查看 Swagger UI 确认端点存在: `https://localhost:5001/swagger`

### Q3: WakeOnLanEnabled 为 false
**原因**: 设备在数据库中未标记为支持 WOL

**解决方案**:
1. 使用 PUT 端点更新设备:
   ```bash
   curl -X PUT http://localhost:5095/api/devices/3 \
     -H "Content-Type: application/json" \
     -d '{"wakeOnLanEnabled": true}'
   ```

2. 或直接在数据库中更新:
   ```sql
   UPDATE Device SET WakeOnLanEnabled = 1 WHERE Id = 3;
   ```

## 日志检查

启动应用时，查看控制台日志以了解 WOL 详细信息:

```
[WOL] 开始唤醒设备 - MAC: AA:BB:CC:DD:EE:FF, IP: 192.168.1.100
[WOL] MAC地址解析成功: AA-BB-CC-DD-EE-FF
[WOL] 魔术包创建完成，大小: 102 字节
[WOL] 发送魔术包到广播地址 255.255.255.255:9
[WOL] 发送魔术包到目标地址 192.168.1.100:9
[WOL] Wake-on-LAN 包发送成功
```

如果看到错误:
```
[WOL] MAC地址解析失败: XX:YY:ZZ
[WOL] Wake-on-LAN 发送失败: [错误信息]
```

这说明 MAC 地址格式有问题或网络配置有问题。

## 安全建议

目前 Wake 端点设置为 `[AllowAnonymous]` 便于测试。生产环境中建议：

1. **启用认证**:
   ```csharp
   [HttpPost("{id}/wake")]
   [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Operator}")]
   public async Task<IActionResult> WakeDevice(int id)
   ```

2. **添加速率限制**: 防止滥用

3. **审计日志**: 已自动记录到 PowerOperations 表

4. **IP 白名单**: 只允许特定 IP 访问 WOL 功能

## 下一步

1. ✅ Wake-on-LAN 功能已修复
2. ⚠️ Shutdown 功能需要额外配置（SSH/WMI/IPMI）
3. 🔄 测试设备唤醒功能
4. 📝 根据需要更新前端代码
5. 🔒 考虑在生产环境启用认证

## 支持

如果仍有问题:
1. 查看应用日志（特别是 `[WOL]` 前缀的消息）
2. 使用 Swagger UI 测试 API
3. 确认设备 MAC 地址和 WOL 设置正确
4. 检查网络配置和防火墙规则
