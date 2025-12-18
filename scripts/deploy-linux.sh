#!/bin/bash
#
# IT Device Manager - Linux 一键部署脚本
# 使用方法: sudo bash deploy-linux.sh
#

set -e

# 配置变量
APP_NAME="itdevicemanager"
INSTALL_DIR="/opt/${APP_NAME}"
DATA_DIR="/var/lib/${APP_NAME}"
SERVICE_USER="www-data"
REPO_URL="https://github.com/xwyangjshb/ITMgmt.git"
DOTNET_VERSION="9.0"

# 颜色输出
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

echo_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

echo_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# 检查是否为 root
if [ "$EUID" -ne 0 ]; then
    echo_error "请使用 sudo 运行此脚本"
    exit 1
fi

echo_info "开始部署 IT Device Manager..."

# 检测操作系统
if [ -f /etc/os-release ]; then
    . /etc/os-release
    OS=$ID
else
    echo_error "无法检测操作系统"
    exit 1
fi

echo_info "检测到操作系统: $OS"

# 安装 .NET Runtime
echo_info "检查 .NET Runtime..."
if ! command -v dotnet &> /dev/null; then
    echo_info "安装 .NET ${DOTNET_VERSION} Runtime..."

    if [ "$OS" = "ubuntu" ] || [ "$OS" = "debian" ]; then
        wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
        dpkg -i packages-microsoft-prod.deb
        rm packages-microsoft-prod.deb
        apt-get update
        apt-get install -y aspnetcore-runtime-${DOTNET_VERSION}
    elif [ "$OS" = "centos" ] || [ "$OS" = "rhel" ]; then
        rpm -Uvh https://packages.microsoft.com/config/centos/8/packages-microsoft-prod.rpm
        dnf install -y aspnetcore-runtime-${DOTNET_VERSION}
    else
        echo_error "不支持的操作系统: $OS"
        exit 1
    fi
else
    echo_info ".NET Runtime 已安装: $(dotnet --version)"
fi

# 安装网络工具
echo_info "安装网络工具..."
if [ "$OS" = "ubuntu" ] || [ "$OS" = "debian" ]; then
    apt-get install -y git net-tools iputils-ping curl
elif [ "$OS" = "centos" ] || [ "$OS" = "rhel" ]; then
    dnf install -y git net-tools iputils curl
fi

# 创建目录
echo_info "创建应用目录..."
mkdir -p "$INSTALL_DIR"
mkdir -p "$DATA_DIR"
mkdir -p "$DATA_DIR/logs"

# 克隆或更新代码
if [ -d "/tmp/ITMgmt" ]; then
    echo_info "更新代码仓库..."
    cd /tmp/ITMgmt
    git pull
else
    echo_info "克隆代码仓库..."
    git clone "$REPO_URL" /tmp/ITMgmt
    cd /tmp/ITMgmt
fi

# 发布应用
echo_info "发布应用程序..."
dotnet publish ITDeviceManager.API/ITDeviceManager.API.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o "$INSTALL_DIR"

# 配置数据库路径
echo_info "配置数据库路径..."
cat > "$INSTALL_DIR/appsettings.Production.json" <<EOF
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=${DATA_DIR}/ITDeviceManager.db;Cache=Shared"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Serilog": {
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "${DATA_DIR}/logs/log-.txt",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
EOF

# 设置权限
echo_info "设置文件权限..."
chown -R $SERVICE_USER:$SERVICE_USER "$INSTALL_DIR"
chown -R $SERVICE_USER:$SERVICE_USER "$DATA_DIR"
chmod +x "$INSTALL_DIR/ITDeviceManager.API"

# 创建 systemd 服务
echo_info "创建 systemd 服务..."
cat > /etc/systemd/system/${APP_NAME}.service <<EOF
[Unit]
Description=IT Device Manager API Service
After=network.target

[Service]
Type=notify
User=$SERVICE_USER
Group=$SERVICE_USER
WorkingDirectory=$INSTALL_DIR
ExecStart=/usr/bin/dotnet $INSTALL_DIR/ITDeviceManager.API.dll
Restart=on-failure
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=$APP_NAME

Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ASPNETCORE_URLS=http://+:10590

NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ReadWritePaths=$DATA_DIR

LimitNOFILE=65536

[Install]
WantedBy=multi-user.target
EOF

# 应用数据库迁移
echo_info "应用数据库迁移..."
cd "$INSTALL_DIR"
sudo -u $SERVICE_USER dotnet ITDeviceManager.API.dll || true

# 重新加载 systemd
systemctl daemon-reload

# 启用并启动服务
echo_info "启动服务..."
systemctl enable $APP_NAME
systemctl restart $APP_NAME

# 配置防火墙
echo_info "配置防火墙..."
if command -v ufw &> /dev/null; then
    ufw allow 10590/tcp
    ufw allow 7/udp
    ufw allow 9/udp
elif command -v firewall-cmd &> /dev/null; then
    firewall-cmd --permanent --add-port=10590/tcp
    firewall-cmd --permanent --add-port=7/udp
    firewall-cmd --permanent --add-port=9/udp
    firewall-cmd --reload
fi

# 等待服务启动
sleep 5

# 检查服务状态
if systemctl is-active --quiet $APP_NAME; then
    echo_info "✅ 部署成功！"
    echo ""
    echo "服务状态: $(systemctl is-active $APP_NAME)"
    echo "访问地址: http://$(hostname -I | awk '{print $1}'):10590"
    echo "Swagger 文档: http://$(hostname -I | awk '{print $1}'):10590/swagger"
    echo ""
    echo "常用命令:"
    echo "  查看状态: sudo systemctl status $APP_NAME"
    echo "  查看日志: sudo journalctl -u $APP_NAME -f"
    echo "  重启服务: sudo systemctl restart $APP_NAME"
    echo "  停止服务: sudo systemctl stop $APP_NAME"
else
    echo_error "❌ 部署失败，请查看日志"
    systemctl status $APP_NAME
    journalctl -u $APP_NAME -n 50
    exit 1
fi
