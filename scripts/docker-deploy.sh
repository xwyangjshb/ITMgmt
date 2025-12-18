#!/bin/bash
#
# IT Device Manager - Docker 快速部署脚本
# 使用方法: bash docker-deploy.sh
#

set -e

# 颜色输出
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
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

echo_step() {
    echo -e "${BLUE}==>${NC} $1"
}

# 检查 Docker 是否安装
if ! command -v docker &> /dev/null; then
    echo_error "Docker 未安装，请先安装 Docker"
    echo_info "安装方法: curl -fsSL https://get.docker.com | bash"
    exit 1
fi

# 检查 Docker Compose 是否安装
if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
    echo_error "Docker Compose 未安装"
    exit 1
fi

COMPOSE_CMD="docker-compose"
if ! command -v docker-compose &> /dev/null; then
    COMPOSE_CMD="docker compose"
fi

echo_step "IT Device Manager Docker 快速部署"
echo ""

# 检测操作系统
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    NETWORK_MODE="host"
    COMPOSE_FILE="docker-compose.yml"
    echo_info "检测到 Linux 系统，使用 host 网络模式"
else
    NETWORK_MODE="bridge"
    COMPOSE_FILE="docker-compose.bridge.yml"
    echo_warn "检测到非 Linux 系统，使用 bridge 网络模式"
    echo_warn "网络发现功能可能受限"
fi

# 提示用户修改 JWT 密钥
echo ""
echo_warn "⚠️  安全提示："
echo "生产环境部署前，请修改 ${COMPOSE_FILE} 中的以下配置："
echo "  - Jwt__SecretKey: 替换为随机生成的安全密钥"
echo "  - CORS 策略: 限制允许的来源域名"
echo ""
read -p "是否继续部署? (y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo_info "取消部署"
    exit 0
fi

# 构建并启动
echo_step "构建 Docker 镜像..."
$COMPOSE_CMD -f "$COMPOSE_FILE" build

echo_step "启动容器..."
$COMPOSE_CMD -f "$COMPOSE_FILE" up -d

# 等待服务启动
echo_info "等待服务启动..."
sleep 10

# 检查容器状态
if docker ps | grep -q itdevicemanager; then
    echo ""
    echo_info "✅ 部署成功！"
    echo ""
    echo "容器状态:"
    docker ps --filter name=itdevicemanager --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
    echo ""

    # 获取 IP 地址
    if [[ "$NETWORK_MODE" == "host" ]]; then
        IP=$(hostname -I | awk '{print $1}')
    else
        IP="localhost"
    fi

    echo "访问地址:"
    echo "  - API 地址: http://${IP}:10590"
    echo "  - Swagger 文档: http://${IP}:10590/swagger"
    echo "  - 健康检查: http://${IP}:10590/api/devices"
    echo ""
    echo "常用命令:"
    echo "  查看日志: $COMPOSE_CMD -f ${COMPOSE_FILE} logs -f"
    echo "  重启服务: $COMPOSE_CMD -f ${COMPOSE_FILE} restart"
    echo "  停止服务: $COMPOSE_CMD -f ${COMPOSE_FILE} down"
    echo "  进入容器: docker exec -it itdevicemanager bash"
    echo ""

    # 测试 API
    echo_info "测试 API 连接..."
    sleep 2
    if curl -s -f "http://${IP}:10590/api/devices" > /dev/null 2>&1; then
        echo_info "✅ API 响应正常"
    else
        echo_warn "⚠️  API 暂时无响应，可能仍在初始化中"
        echo_info "请稍后使用以下命令查看日志："
        echo "  $COMPOSE_CMD -f ${COMPOSE_FILE} logs -f"
    fi
else
    echo_error "❌ 部署失败"
    echo_info "查看日志:"
    $COMPOSE_CMD -f "$COMPOSE_FILE" logs
    exit 1
fi
