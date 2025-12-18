# 部署脚本说明

本目录包含 IT Device Manager 的各种部署和维护脚本。

## 脚本列表

### 1. deploy-linux.sh - Linux 一键部署脚本

在 Linux 系统上自动部署应用程序，包括安装依赖、配置服务等。

**使用方法**:
```bash
sudo bash scripts/deploy-linux.sh
```

**功能**:
- 自动检测操作系统（Ubuntu/Debian/CentOS/RHEL）
- 安装 .NET 9.0 Runtime
- 安装网络工具
- 发布应用程序
- 创建 systemd 服务
- 配置防火墙

**支持的系统**:
- Ubuntu 20.04+
- Debian 11+
- CentOS 8+
- RHEL 8+

---

### 2. docker-deploy.sh - Docker 快速部署脚本

使用 Docker Compose 快速部署应用。

**使用方法**:
```bash
bash scripts/docker-deploy.sh
```

**功能**:
- 自动检测操作系统并选择合适的网络模式
- 构建 Docker 镜像
- 启动容器
- 健康检查
- 显示访问地址和常用命令

**网络模式**:
- Linux: host 模式（最佳性能，完整网络发现功能）
- Windows/Mac: bridge 模式（网络发现功能受限）

---

### 3. update.sh - 应用更新脚本

更新已部署的应用程序到最新版本。

**使用方法**:
```bash
sudo bash scripts/update.sh
```

**功能**:
- 自动备份当前版本
- 备份数据库
- 拉取最新代码
- 发布新版本
- 应用数据库迁移
- 自动回滚（更新失败时）

**安全特性**:
- 更新前自动备份
- 失败自动回滚
- 保留最近 3 个备份

---

### 4. backup.sh - 数据库备份脚本

备份 SQLite 数据库文件。

**使用方法**:
```bash
bash scripts/backup.sh
```

**功能**:
- 执行 WAL 检查点
- 复制并压缩数据库文件
- 自动清理过期备份（默认保留 7 天）
- 显示备份文件列表

**定时备份**:

添加到 crontab（每天凌晨 2 点备份）:
```bash
crontab -e
# 添加以下行
0 2 * * * /path/to/scripts/backup.sh
```

---

## 快速开始

### 首次部署（Linux 原生）

```bash
# 1. 克隆仓库
git clone https://github.com/xwyangjshb/ITMgmt.git
cd ITMgmt

# 2. 运行部署脚本
sudo bash scripts/deploy-linux.sh

# 3. 访问应用
# http://your-server-ip:10590
```

### 首次部署（Docker）

```bash
# 1. 克隆仓库
git clone https://github.com/xwyangjshb/ITMgmt.git
cd ITMgmt

# 2. 运行 Docker 部署脚本
bash scripts/docker-deploy.sh

# 3. 访问应用
# http://localhost:10590
```

---

## 常见场景

### 场景 1: 更新应用到最新版本

```bash
cd ITMgmt
sudo bash scripts/update.sh
```

### 场景 2: 手动备份数据库

```bash
cd ITMgmt
bash scripts/backup.sh
```

### 场景 3: 查看应用状态

**原生部署**:
```bash
sudo systemctl status itdevicemanager
sudo journalctl -u itdevicemanager -f
```

**Docker 部署**:
```bash
docker ps --filter name=itdevicemanager
docker logs itdevicemanager -f
```

### 场景 4: 重启服务

**原生部署**:
```bash
sudo systemctl restart itdevicemanager
```

**Docker 部署**:
```bash
docker-compose restart
```

---

## 配置文件位置

### 原生部署

- **应用程序**: `/opt/itdevicemanager/`
- **数据库**: `/var/lib/itdevicemanager/ITDeviceManager.db`
- **日志**: `/var/lib/itdevicemanager/logs/`
- **备份**: `/var/backups/itdevicemanager/`
- **配置文件**: `/opt/itdevicemanager/appsettings.Production.json`

### Docker 部署

- **数据库**: Docker Volume `itdevicemanager-data`
- **日志**: Docker Volume `itdevicemanager-logs`
- **配置文件**: 通过环境变量配置（见 docker-compose.yml）

---

## 故障排查

### 问题 1: 脚本执行权限错误

```bash
# 授予执行权限
chmod +x scripts/*.sh
```

### 问题 2: 端口被占用

```bash
# 查看端口占用
sudo netstat -tulpn | grep 10590

# 停止占用进程或修改配置文件中的端口
```

### 问题 3: 服务启动失败

```bash
# 查看详细日志
sudo journalctl -u itdevicemanager -n 100 --no-pager
```

### 问题 4: Docker 容器无法启动

```bash
# 查看容器日志
docker logs itdevicemanager

# 查看 Docker Compose 日志
docker-compose logs
```

---

## 生产环境建议

1. **修改 JWT 密钥**: 在部署前生成随机密钥
   ```bash
   openssl rand -base64 32
   ```

2. **配置 HTTPS**: 使用 Nginx 或 Traefik 作为反向代理

3. **设置定时备份**: 使用 crontab 定期备份数据库

4. **监控服务状态**: 使用 systemd 或 Docker 健康检查

5. **配置防火墙**: 仅开放必要的端口

---

## 技术支持

- **GitHub Issues**: https://github.com/xwyangjshb/ITMgmt/issues
- **部署文档**: [DEPLOY.md](../DEPLOY.md)
- **项目文档**: [README.md](../README.md)

---

**最后更新**: 2025-12-19
