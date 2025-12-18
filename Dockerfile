# ============================================
# 多阶段构建 Dockerfile for IT Device Manager
# 支持 Linux/Windows 容器
# ============================================

# 阶段 1: 基础运行时镜像
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 10590
EXPOSE 7/udp
EXPOSE 9/udp

# 设置时区为中国标准时间
ENV TZ=Asia/Shanghai
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# 安装网络工具（用于设备发现功能）
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    net-tools \
    iputils-ping \
    && rm -rf /var/lib/apt/lists/*

# 阶段 2: SDK 构建镜像
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 复制项目文件并还原依赖（利用 Docker 缓存）
COPY ["ITDeviceManager.API/ITDeviceManager.API.csproj", "ITDeviceManager.API/"]
COPY ["ITDeviceManager.Core/ITDeviceManager.Core.csproj", "ITDeviceManager.Core/"]
RUN dotnet restore "ITDeviceManager.API/ITDeviceManager.API.csproj"

# 复制所有源代码
COPY . .

# 构建项目
WORKDIR "/src/ITDeviceManager.API"
RUN dotnet build "ITDeviceManager.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 阶段 3: 发布镜像
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ITDeviceManager.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# 阶段 4: 最终运行镜像
FROM base AS final
WORKDIR /app

# 创建数据目录
RUN mkdir -p /app/data && \
    mkdir -p /app/logs && \
    chmod -R 755 /app/data /app/logs

# 复制发布输出
COPY --from=publish /app/publish .

# 设置环境变量
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:10590
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/ITDeviceManager.db;Cache=Shared"

# 健康检查
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:10590/api/devices || exit 1

# 使用非 root 用户运行（安全最佳实践）
RUN useradd -m -u 1000 appuser && \
    chown -R appuser:appuser /app
USER appuser

ENTRYPOINT ["dotnet", "ITDeviceManager.API.dll"]
