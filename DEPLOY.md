# IT Device Manager 部署指南

本文档提供 IT Device Manager 在 Linux、Windows 和 Docker 环境下的完整部署指南。

---

## 目录

- [系统要求](#系统要求)
- [快速开始（Docker）](#快速开始docker)
- [Linux 原生部署](#linux-原生部署)
- [Windows 部署](#windows-部署)
- [生产环境配置](#生产环境配置)
- [故障排查](#故障排查)

---

## 系统要求

### 最低配置
- **CPU**: 1 核心
- **内存**: 512 MB
- **磁盘**: 1 GB 可用空间
- **网络**: 支持局域网访问

### 推荐配置
- **CPU**: 2 核心或更多
- **内存**: 1 GB 或更多
- **磁盘**: 5 GB 可用空间（用于日志和数据库）

### 软件依赖
- **.NET 9.0 Runtime**（原生部署）
- **Docker & Docker Compose**（容器部署）

---

## 快速开始（Docker）

### 方式 1: 使用预构建镜像（推荐）

```bash
# 1. 克隆仓库
git clone https://github.com/xwyangjshb/ITMgmt.git
cd ITMgmt

# 2. 启动服务（host 网络模式 - Linux）
docker-compose up -d

# 3. 查看日志
docker-compose logs -f

# 4. 停止服务
docker-compose down
```

### 方式 2: 桥接网络模式（Windows/Mac）

```bash
# 使用桥接模式配置
docker-compose -f docker-compose.bridge.yml up -d
```

### 方式 3: 手动构建和运行

```bash
# 1. 构建镜像
docker build -t itdevicemanager:latest .

# 2. 运行容器
docker run -d \
  --name itdevicemanager \
  --network host \
  -v /opt/itdevicemanager/data:/app/data \
  -v /opt/itdevicemanager/logs:/app/logs \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Jwt__SecretKey="YOUR_SECRET_KEY_AT_LEAST_32_CHARACTERS" \
  --restart unless-stopped \
  itdevicemanager:latest

# 3. 查看容器状态
docker ps
docker logs itdevicemanager
```

### 访问应用

- **API 地址**: http://localhost:10590
- **Swagger 文档**: http://localhost:10590/swagger
- **健康检查**: http://localhost:10590/api/devices

---

## Linux 原生部署

### Ubuntu/Debian 系统

#### 1. 安装 .NET 9.0 Runtime

```bash
# 添加 Microsoft 包仓库
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# 安装 ASP.NET Core Runtime
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-9.0

# 验证安装
dotnet --version
```

#### 2. 部署应用程序

```bash
# 创建应用目录
sudo mkdir -p /opt/itdevicemanager
sudo mkdir -p /var/lib/itdevicemanager

# 克隆或复制代码
git clone https://github.com/xwyangjshb/ITMgmt.git /tmp/ITMgmt
cd /tmp/ITMgmt

# 发布应用
dotnet publish ITDeviceManager.API/ITDeviceManager.API.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o /opt/itdevicemanager

# 设置权限
sudo chown -R www-data:www-data /opt/itdevicemanager
sudo chown -R www-data:www-data /var/lib/itdevicemanager
sudo chmod +x /opt/itdevicemanager/ITDeviceManager.API
```

#### 3. 配置生产环境

编辑 `/opt/itdevicemanager/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/var/lib/itdevicemanager/ITDeviceManager.db;Cache=Shared"
  },
  "Jwt": {
    "SecretKey": "生产环境请更换为安全的随机密钥至少32字符",
    "Issuer": "ITDeviceManager",
    "Audience": "ITDeviceManagerAPI",
    "ExpirationMinutes": 60
  },
  "Cors": {
    "AllowedOrigins": [
      "http://your-frontend-domain.com",
      "https://your-frontend-domain.com"
    ]
  }
}
```

#### 4. 创建 Systemd 服务

创建 `/etc/systemd/system/itdevicemanager.service`:

```ini
[Unit]
Description=IT Device Manager API Service
After=network.target

[Service]
Type=notify
User=www-data
Group=www-data
WorkingDirectory=/opt/itdevicemanager
ExecStart=/usr/bin/dotnet /opt/itdevicemanager/ITDeviceManager.API.dll
Restart=on-failure
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=itdevicemanager

# 环境变量
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ASPNETCORE_URLS=http://+:10590

# 安全增强
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ReadWritePaths=/var/lib/itdevicemanager

# 资源限制
LimitNOFILE=65536

[Install]
WantedBy=multi-user.target
```

#### 5. 启动服务

```bash
# 重新加载 systemd
sudo systemctl daemon-reload

# 启用开机自启
sudo systemctl enable itdevicemanager

# 启动服务
sudo systemctl start itdevicemanager

# 查看状态
sudo systemctl status itdevicemanager

# 查看日志
sudo journalctl -u itdevicemanager -f
```

### CentOS/RHEL 系统

#### 1. 安装 .NET 9.0 Runtime

```bash
# 添加 Microsoft 包仓库
sudo rpm -Uvh https://packages.microsoft.com/config/centos/8/packages-microsoft-prod.rpm

# 安装 ASP.NET Core Runtime
sudo dnf install -y aspnetcore-runtime-9.0

# 验证安装
dotnet --version
```

其他步骤与 Ubuntu 相同，参考上述 Ubuntu 部署流程。

---

## Windows 部署

### 方式 1: IIS 托管

#### 1. 安装必要组件

- 安装 [.NET 9.0 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
- 安装 [ASP.NET Core Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/9.0)
- 启用 IIS 功能（控制面板 → 程序 → 启用或关闭 Windows 功能）

#### 2. 发布应用

```powershell
# 在项目目录执行
dotnet publish ITDeviceManager.API\ITDeviceManager.API.csproj `
  -c Release `
  -o C:\inetpub\ITDeviceManager
```

#### 3. 配置 IIS

1. 打开 IIS 管理器
2. 创建新应用程序池
   - 名称: `ITDeviceManager`
   - .NET CLR 版本: `无托管代码`
3. 创建网站
   - 站点名称: `ITDeviceManager`
   - 物理路径: `C:\inetpub\ITDeviceManager`
   - 绑定: `http://*:10590`
   - 应用程序池: `ITDeviceManager`

#### 4. 配置权限

```powershell
# 授予 IIS_IUSRS 权限
icacls "C:\inetpub\ITDeviceManager" /grant "IIS_IUSRS:(OI)(CI)F" /T
```

### 方式 2: Windows 服务

使用 [NSSM](https://nssm.cc/) 将应用注册为 Windows 服务：

```powershell
# 1. 下载 NSSM
# 2. 安装服务
nssm install ITDeviceManager "C:\path\to\ITDeviceManager.API.exe"

# 3. 配置服务
nssm set ITDeviceManager AppDirectory "C:\path\to\ITDeviceManager"
nssm set ITDeviceManager AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"

# 4. 启动服务
nssm start ITDeviceManager
```

### 方式 3: 直接运行

```powershell
cd E:\Docs\ITMgmt\ITDeviceManager.API
dotnet run --launch-profile Production
```

---

## 生产环境配置

### 1. 安全配置

#### JWT 密钥生成

```bash
# 生成随机密钥（Linux/Mac）
openssl rand -base64 32

# 生成随机密钥（PowerShell）
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

更新 `appsettings.json`:

```json
{
  "Jwt": {
    "SecretKey": "生成的随机密钥",
    "Issuer": "ITDeviceManager",
    "Audience": "ITDeviceManagerAPI",
    "ExpirationMinutes": 60
  }
}
```

#### CORS 配置

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://yourdomain.com"
    ]
  }
}
```

### 2. 反向代理配置

#### Nginx

创建 `/etc/nginx/sites-available/itdevicemanager`:

```nginx
upstream itdevicemanager {
    server 127.0.0.1:10590;
}

server {
    listen 80;
    server_name itdevicemanager.yourdomain.com;

    # 重定向到 HTTPS
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name itdevicemanager.yourdomain.com;

    # SSL 证书配置
    ssl_certificate /etc/ssl/certs/itdevicemanager.crt;
    ssl_certificate_key /etc/ssl/private/itdevicemanager.key;

    # 安全头
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;

    location / {
        proxy_pass http://itdevicemanager;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

启用站点:

```bash
sudo ln -s /etc/nginx/sites-available/itdevicemanager /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

### 3. 防火墙配置

#### Ubuntu/Debian (UFW)

```bash
# 允许 API 端口
sudo ufw allow 10590/tcp

# 允许魔术包监听端口
sudo ufw allow 7/udp
sudo ufw allow 9/udp

# 如果使用 Nginx
sudo ufw allow 'Nginx Full'

# 启用防火墙
sudo ufw enable
```

#### CentOS/RHEL (firewalld)

```bash
# 允许端口
sudo firewall-cmd --permanent --add-port=10590/tcp
sudo firewall-cmd --permanent --add-port=7/udp
sudo firewall-cmd --permanent --add-port=9/udp

# 重新加载
sudo firewall-cmd --reload
```

### 4. 日志管理

#### Logrotate 配置

创建 `/etc/logrotate.d/itdevicemanager`:

```
/opt/itdevicemanager/logs/*.txt {
    daily
    rotate 7
    compress
    delaycompress
    missingok
    notifempty
    create 0640 www-data www-data
    sharedscripts
    postrotate
        systemctl reload itdevicemanager > /dev/null 2>&1 || true
    endscript
}
```

### 5. 数据库备份

创建备份脚本 `/usr/local/bin/backup-itdevicemanager.sh`:

```bash
#!/bin/bash

BACKUP_DIR="/var/backups/itdevicemanager"
DB_PATH="/var/lib/itdevicemanager/ITDeviceManager.db"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

# 创建备份目录
mkdir -p "$BACKUP_DIR"

# 备份数据库（包括 WAL 文件）
sqlite3 "$DB_PATH" "PRAGMA wal_checkpoint(TRUNCATE);"
cp "$DB_PATH" "$BACKUP_DIR/ITDeviceManager_${TIMESTAMP}.db"

# 压缩备份
gzip "$BACKUP_DIR/ITDeviceManager_${TIMESTAMP}.db"

# 删除 7 天前的备份
find "$BACKUP_DIR" -name "*.gz" -mtime +7 -delete

echo "Backup completed: ITDeviceManager_${TIMESTAMP}.db.gz"
```

添加到 crontab:

```bash
# 每天凌晨 2 点备份
0 2 * * * /usr/local/bin/backup-itdevicemanager.sh
```

---

## 故障排查

### 1. 应用无法启动

**检查端口占用**:

```bash
# Linux
sudo netstat -tulpn | grep 10590

# Windows
netstat -ano | findstr :10590
```

**查看日志**:

```bash
# Docker
docker logs itdevicemanager

# Systemd
sudo journalctl -u itdevicemanager -n 50

# Windows 事件查看器
查看应用程序日志
```

### 2. 数据库锁定错误

```bash
# 检查 WAL 模式
sqlite3 /var/lib/itdevicemanager/ITDeviceManager.db "PRAGMA journal_mode;"

# 应返回 "wal"

# 手动启用 WAL
sqlite3 /var/lib/itdevicemanager/ITDeviceManager.db "PRAGMA journal_mode=WAL;"
```

### 3. 网络发现功能不工作

**检查权限** (Linux):

```bash
# 授予网络能力
sudo setcap 'cap_net_raw,cap_net_admin=+ep' /opt/itdevicemanager/ITDeviceManager.API
```

**检查 ARP 命令**:

```bash
# 测试 ARP 是否可用
arp -a
```

### 4. 魔术包监听失败

**检查端口权限**:

```bash
# Linux - 需要 root 权限或 CAP_NET_BIND_SERVICE
sudo setcap 'cap_net_bind_service=+ep' /opt/itdevicemanager/ITDeviceManager.API
```

**防火墙放行**:

```bash
# 确保 UDP 端口 7 和 9 已开放
sudo ufw allow 7/udp
sudo ufw allow 9/udp
```

### 5. Docker 网络问题

**使用 host 网络模式** (Linux):

```yaml
# docker-compose.yml
network_mode: host
```

**检查容器网络**:

```bash
docker network inspect bridge
docker exec itdevicemanager ping 192.168.1.1
```

### 6. 性能问题

**监控 SQLite 数据库**:

```bash
# 检查数据库大小
du -h /var/lib/itdevicemanager/ITDeviceManager.db*

# WAL 检查点
sqlite3 /var/lib/itdevicemanager/ITDeviceManager.db "PRAGMA wal_checkpoint(FULL);"
```

**调整后台扫描间隔**:

编辑 `appsettings.json`:

```json
{
  "DeviceDiscovery": {
    "ScanIntervalMinutes": 60,  // 从 30 增加到 60 分钟
    "RetryDelayMinutes": 5,
    "OfflineThresholdMinutes": 120
  }
}
```

---

## 监控和维护

### 健康检查

```bash
# API 健康检查
curl http://localhost:10590/api/devices

# 返回 200 表示正常
```

### 性能监控

```bash
# 查看资源使用
docker stats itdevicemanager

# 或使用 systemd
systemctl status itdevicemanager
```

### 数据库维护

```bash
# 定期 VACUUM（压缩数据库）
sqlite3 /var/lib/itdevicemanager/ITDeviceManager.db "VACUUM;"

# 分析和优化
sqlite3 /var/lib/itdevicemanager/ITDeviceManager.db "ANALYZE;"
```

---

## 升级指南

### Docker 升级

```bash
# 1. 停止服务
docker-compose down

# 2. 拉取最新代码
git pull

# 3. 重新构建
docker-compose build

# 4. 启动服务
docker-compose up -d

# 5. 验证
docker logs itdevicemanager
```

### 原生部署升级

```bash
# 1. 停止服务
sudo systemctl stop itdevicemanager

# 2. 备份数据库
sudo cp /var/lib/itdevicemanager/ITDeviceManager.db /var/backups/

# 3. 更新应用
cd /tmp/ITMgmt
git pull
dotnet publish ITDeviceManager.API/ITDeviceManager.API.csproj \
  -c Release -r linux-x64 --self-contained false \
  -o /opt/itdevicemanager

# 4. 应用迁移
cd /opt/itdevicemanager
dotnet ITDeviceManager.API.dll -- ef database update

# 5. 启动服务
sudo systemctl start itdevicemanager
```

---

## 技术支持

- **GitHub Issues**: https://github.com/xwyangjshb/ITMgmt/issues
- **文档**: https://github.com/xwyangjshb/ITMgmt/blob/main/README.md

---

**生成时间**: 2025-12-19
**文档版本**: 1.0
**应用版本**: 基于 SQLite + .NET 9.0
