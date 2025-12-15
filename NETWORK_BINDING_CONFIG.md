# 网络监听配置说明

## 修改内容

已将应用程序从监听 `localhost` (127.0.0.1) 改为监听所有网络接口 (`0.0.0.0`)。

### 修改的文件

#### 1. Properties/launchSettings.json
```json
{
  "profiles": {
    "http": {
      "applicationUrl": "http://0.0.0.0:5095"  // 原: http://localhost:5095
    },
    "https": {
      "applicationUrl": "https://0.0.0.0:7096;http://0.0.0.0:5095"
    }
  }
}
```

#### 2. appsettings.json (CORS 配置更新)
添加了本地访问的 CORS 配置，开发环境下已启用 "AllowAll" 策略。

## 访问方式

### 从本机访问
```bash
# HTTP
http://localhost:5095

# HTTPS
https://localhost:7096
```

### 从同一局域网的其他机器访问
```bash
# 假设服务器的物理IP是 192.168.1.100

# HTTP
http://192.168.1.100:5095

# HTTPS (需要信任证书)
https://192.168.1.100:7096
```

## 启动应用

```bash
cd ITDeviceManager.API
dotnet run --launch-profile http
```

启动后会看到类似输出：
```
Now listening on: http://0.0.0.0:5095
Application started. Press Ctrl+C to shut down.
```

## 查看本机物理IP地址

### Windows
```powershell
# 方法1: 使用 ipconfig
ipconfig

# 方法2: 仅显示IPv4地址
ipconfig | findstr "IPv4"

# 方法3: 使用 PowerShell
Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.InterfaceAlias -notlike "*Loopback*"}
```

### 示例输出
```
以太网适配器 以太网:
   IPv4 地址 . . . . . . . . . . . . : 192.168.1.100
   子网掩码  . . . . . . . . . . . . : 255.255.255.0
   默认网关. . . . . . . . . . . . . : 192.168.1.1
```

## 测试连接

### 从本机测试
```bash
# 测试设备列表API
curl http://localhost:5095/api/devices

# 使用物理IP测试
curl http://192.168.1.100:5095/api/devices
```

### 从其他机器测试
```bash
# 替换为实际的服务器IP
curl http://192.168.1.100:5095/api/devices
```

### 浏览器测试
打开浏览器访问：
```
http://192.168.1.100:5095
```

## 防火墙配置

### Windows 防火墙规则

如果无法从其他机器访问，需要添加防火墙规则：

#### 方法1: PowerShell (管理员权限)
```powershell
# 允许入站HTTP连接 (端口5095)
New-NetFirewallRule -DisplayName "IT Device Manager HTTP" -Direction Inbound -LocalPort 5095 -Protocol TCP -Action Allow

# 允许入站HTTPS连接 (端口7096)
New-NetFirewallRule -DisplayName "IT Device Manager HTTPS" -Direction Inbound -LocalPort 7096 -Protocol TCP -Action Allow
```

#### 方法2: 图形界面
1. 打开 "Windows Defender 防火墙"
2. 点击 "高级设置"
3. 选择 "入站规则" → "新建规则"
4. 选择 "端口" → "TCP"
5. 输入端口号: `5095` (HTTP) 或 `7096` (HTTPS)
6. 选择 "允许连接"
7. 应用到所有配置文件
8. 命名规则: "IT Device Manager HTTP/HTTPS"

#### 方法3: 临时关闭防火墙测试（不推荐生产环境）
```powershell
# 仅用于测试，完成后记得重新启用
Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled False

# 重新启用防火墙
Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True
```

## 安全建议

### 1. 生产环境建议
如果在生产环境部署，建议：
- 使用 HTTPS (端口7096)
- 配置有效的SSL证书
- 限制 CORS 的 AllowedOrigins 为特定域名
- 启用 JWT 认证验证

### 2. 仅监听特定网卡
如果只想监听特定物理网卡（例如 `192.168.1.100`），修改 `launchSettings.json`：
```json
{
  "applicationUrl": "http://192.168.1.100:5095"
}
```

### 3. 限制访问IP段
在 Program.cs 中添加 IP 限制中间件（可选）：
```csharp
app.Use(async (context, next) =>
{
    var remoteIp = context.Connection.RemoteIpAddress?.ToString();
    if (remoteIp != null && !remoteIp.StartsWith("192.168.1."))
    {
        context.Response.StatusCode = 403;
        await context.Response.WriteAsync("Access denied");
        return;
    }
    await next();
});
```

## 常见问题

### Q1: 无法从其他机器访问
**检查清单**:
1. ✅ 确认应用已启动并监听 0.0.0.0
2. ✅ 确认防火墙规则已添加
3. ✅ 确认服务器和客户端在同一网络
4. ✅ 使用 `ipconfig` 确认服务器IP地址正确
5. ✅ 测试端口是否开放：`Test-NetConnection -ComputerName 192.168.1.100 -Port 5095`

### Q2: HTTPS 证书警告
在局域网使用 HTTPS 时，浏览器会警告证书不受信任。

**开发环境解决方案**:
```bash
# 信任开发证书
dotnet dev-certs https --trust
```

**生产环境解决方案**:
- 使用真实的SSL证书（如 Let's Encrypt）
- 或在内网使用内部CA签发的证书

### Q3: 跨子网访问
如果需要跨子网访问（如从 192.168.2.x 访问 192.168.1.100），需要：
1. 确保路由器支持跨子网路由
2. 或使用反向代理（如 Nginx）
3. 或使用 VPN

### Q4: Wake-on-LAN 跨子网
Wake-on-LAN 广播包默认不能跨子网传输，需要：
1. 在目标子网部署代理服务
2. 或配置路由器支持 Directed Broadcast
3. 或使用 `subnet directed broadcast` 功能

## 验证配置

### 步骤1: 启动应用
```bash
cd E:\Docs\ITMgmt\ITDeviceManager.API
dotnet run
```

### 步骤2: 确认监听地址
查看控制台输出：
```
Now listening on: http://0.0.0.0:5095
Application started. Press Ctrl+C to shut down.
```

### 步骤3: 查看本机IP
```powershell
ipconfig | findstr "IPv4"
```

### 步骤4: 本机测试
```bash
curl http://localhost:5095/api/devices
```

### 步骤5: 局域网测试
从另一台机器或手机浏览器访问：
```
http://192.168.1.100:5095
```

## 使用场景

### 场景1: 本机开发
使用 `http://localhost:5095` 访问，性能最优。

### 场景2: 团队内部测试
团队成员通过 `http://<服务器IP>:5095` 访问，方便协作测试。

### 场景3: 移动设备访问
手机/平板通过 `http://<服务器IP>:5095` 访问管理设备。

### 场景4: 远程管理
通过 Wake-on-LAN 远程唤醒网络设备，无需物理接触。

## 网络监听模式对比

| 监听地址 | 访问方式 | 使用场景 | 安全性 |
|---------|---------|---------|--------|
| `127.0.0.1` 或 `localhost` | 仅本机 | 本地开发、测试 | ⭐⭐⭐⭐⭐ |
| `0.0.0.0` | 所有网络接口 | 局域网访问、生产部署 | ⭐⭐⭐ (需配置防火墙) |
| `192.168.1.100` (特定IP) | 指定网卡 | 多网卡服务器 | ⭐⭐⭐⭐ |
| `*` (通配符) | 所有接口 | Kestrel配置方式 | ⭐⭐⭐ |

## 端口占用检查

### 检查端口是否被占用
```powershell
# 检查端口5095
netstat -ano | findstr :5095

# 检查端口7096
netstat -ano | findstr :7096
```

### 杀死占用端口的进程
```powershell
# 查找进程ID (PID)
netstat -ano | findstr :5095

# 杀死进程 (替换<PID>为实际进程ID)
taskkill /PID <PID> /F
```

## 性能优化

### Kestrel 配置 (可选)
在 `appsettings.json` 中添加 Kestrel 高级配置：
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5095"
      },
      "Https": {
        "Url": "https://0.0.0.0:7096"
      }
    },
    "Limits": {
      "MaxConcurrentConnections": 100,
      "MaxConcurrentUpgradedConnections": 100,
      "MaxRequestBodySize": 10485760,
      "KeepAliveTimeout": "00:02:00",
      "RequestHeadersTimeout": "00:00:30"
    }
  }
}
```

## 总结

✅ **已完成**: 将监听地址从 `localhost` 改为 `0.0.0.0`
✅ **效果**: 现在可以从局域网内的任何设备访问API
✅ **下一步**: 配置防火墙规则，允许入站连接
✅ **安全**: 开发环境已启用 AllowAll CORS，生产环境需要限制

现在您可以从网络内的其他设备访问和管理IT设备了！🎉
