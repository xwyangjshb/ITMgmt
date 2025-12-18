#!/bin/bash
#
# IT Device Manager - 数据库备份脚本
# 使用方法: bash backup.sh
#

set -e

# 配置变量
APP_NAME="itdevicemanager"
DATA_DIR="/var/lib/${APP_NAME}"
BACKUP_DIR="/var/backups/${APP_NAME}"
DB_PATH="${DATA_DIR}/ITDeviceManager.db"
RETENTION_DAYS=7

# 颜色输出
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
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

# 检查数据库文件是否存在
if [ ! -f "$DB_PATH" ]; then
    echo_error "数据库文件不存在: $DB_PATH"
    exit 1
fi

# 创建备份目录
mkdir -p "$BACKUP_DIR"

# 生成时间戳
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/ITDeviceManager_${TIMESTAMP}.db"

echo_info "开始备份数据库..."
echo_info "源文件: $DB_PATH"
echo_info "备份文件: $BACKUP_FILE.gz"

# 执行 WAL 检查点（确保数据完整性）
if command -v sqlite3 &> /dev/null; then
    echo_info "执行 WAL 检查点..."
    sqlite3 "$DB_PATH" "PRAGMA wal_checkpoint(TRUNCATE);" 2>/dev/null || true
else
    echo_warn "sqlite3 命令未安装，跳过 WAL 检查点"
fi

# 复制数据库文件
cp "$DB_PATH" "$BACKUP_FILE"

# 压缩备份文件
echo_info "压缩备份文件..."
gzip "$BACKUP_FILE"

# 计算文件大小
BACKUP_SIZE=$(du -h "${BACKUP_FILE}.gz" | cut -f1)
echo_info "备份完成，文件大小: $BACKUP_SIZE"

# 删除过期备份
echo_info "清理 ${RETENTION_DAYS} 天前的备份..."
DELETED_COUNT=$(find "$BACKUP_DIR" -name "ITDeviceManager_*.db.gz" -mtime +${RETENTION_DAYS} -delete -print | wc -l)

if [ "$DELETED_COUNT" -gt 0 ]; then
    echo_info "删除了 $DELETED_COUNT 个过期备份"
else
    echo_info "没有需要清理的过期备份"
fi

# 列出所有备份
echo ""
echo_info "当前备份列表:"
ls -lh "$BACKUP_DIR"/ITDeviceManager_*.db.gz 2>/dev/null || echo "无备份文件"

echo ""
echo_info "✅ 备份完成: ${BACKUP_FILE}.gz"
