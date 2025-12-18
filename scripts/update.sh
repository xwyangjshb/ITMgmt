#!/bin/bash
#
# IT Device Manager - 应用更新脚本
# 使用方法: sudo bash update.sh
#

set -e

# 配置变量
APP_NAME="itdevicemanager"
INSTALL_DIR="/opt/${APP_NAME}"
DATA_DIR="/var/lib/${APP_NAME}"
SERVICE_USER="www-data"
REPO_URL="https://github.com/xwyangjshb/ITMgmt.git"

# 颜色输出
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

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

echo_info "开始更新 IT Device Manager..."

# 检查服务是否存在
if ! systemctl list-unit-files | grep -q "^${APP_NAME}.service"; then
    echo_error "服务 ${APP_NAME} 不存在，请先运行 deploy-linux.sh"
    exit 1
fi

# 备份当前版本
echo_info "备份当前版本..."
BACKUP_DIR="${INSTALL_DIR}_backup_$(date +%Y%m%d_%H%M%S)"
cp -r "$INSTALL_DIR" "$BACKUP_DIR"
echo_info "备份保存至: $BACKUP_DIR"

# 备份数据库
echo_info "备份数据库..."
bash "$(dirname "$0")/backup.sh" || echo_warn "数据库备份失败，继续更新..."

# 停止服务
echo_info "停止服务..."
systemctl stop $APP_NAME

# 更新代码
if [ -d "/tmp/ITMgmt" ]; then
    echo_info "更新代码仓库..."
    cd /tmp/ITMgmt
    git fetch origin
    git reset --hard origin/main
else
    echo_info "克隆代码仓库..."
    git clone "$REPO_URL" /tmp/ITMgmt
    cd /tmp/ITMgmt
fi

# 显示版本信息
CURRENT_COMMIT=$(git rev-parse --short HEAD)
CURRENT_DATE=$(git log -1 --format=%cd --date=short)
echo_info "更新到版本: $CURRENT_COMMIT ($CURRENT_DATE)"

# 发布新版本
echo_info "发布应用程序..."
dotnet publish ITDeviceManager.API/ITDeviceManager.API.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o "$INSTALL_DIR"

# 恢复配置文件（如果存在）
if [ -f "${BACKUP_DIR}/appsettings.Production.json" ]; then
    echo_info "恢复生产配置文件..."
    cp "${BACKUP_DIR}/appsettings.Production.json" "$INSTALL_DIR/"
fi

# 设置权限
echo_info "设置文件权限..."
chown -R $SERVICE_USER:$SERVICE_USER "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/ITDeviceManager.API"

# 应用数据库迁移
echo_info "应用数据库迁移..."
cd "$INSTALL_DIR"
sudo -u $SERVICE_USER dotnet ITDeviceManager.API.dll || true

# 启动服务
echo_info "启动服务..."
systemctl start $APP_NAME

# 等待服务启动
sleep 5

# 检查服务状态
if systemctl is-active --quiet $APP_NAME; then
    echo_info "✅ 更新成功！"
    echo ""
    echo "版本信息: $CURRENT_COMMIT ($CURRENT_DATE)"
    echo "服务状态: $(systemctl is-active $APP_NAME)"
    echo ""
    echo "如需回滚，请运行:"
    echo "  sudo systemctl stop $APP_NAME"
    echo "  sudo rm -rf $INSTALL_DIR"
    echo "  sudo mv $BACKUP_DIR $INSTALL_DIR"
    echo "  sudo systemctl start $APP_NAME"
else
    echo_error "❌ 更新失败，正在回滚..."

    # 自动回滚
    systemctl stop $APP_NAME
    rm -rf "$INSTALL_DIR"
    mv "$BACKUP_DIR" "$INSTALL_DIR"
    systemctl start $APP_NAME

    echo_error "已回滚到之前的版本"
    echo "请查看日志: sudo journalctl -u $APP_NAME -n 50"
    exit 1
fi

# 清理旧备份（保留最近 3 个）
echo_info "清理旧备份..."
ls -dt ${INSTALL_DIR}_backup_* 2>/dev/null | tail -n +4 | xargs rm -rf 2>/dev/null || true

echo_info "✅ 更新完成"
