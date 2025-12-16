using System.Net;
using System.Net.Sockets;
using ITDeviceManager.API.Data;
using ITDeviceManager.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITDeviceManager.API.Services;

public class MagicPacketBackgroundService : BackgroundService
{
    private readonly ILogger<MagicPacketBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private Socket? _socket7;
    private Socket? _socket9;

    public MagicPacketBackgroundService(ILogger<MagicPacketBackgroundService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("魔术包监听服务已启动");

        try
        {
            var tasks = new List<Task>();

            // 尝试监听端口7
            try
            {
                _socket7 = CreateListeningSocket(7);
                tasks.Add(ListenOnSocketAsync(_socket7, 7, stoppingToken));
                _logger.LogInformation("成功开始监听UDP端口7");
            }
            catch (SocketException ex) when (ex.ErrorCode == 10013)
            {
                _logger.LogWarning("无法监听端口7（需要管理员权限）。请以管理员身份运行应用程序以启用完整的魔术包监听功能。");
            }

            // 尝试监听端口9
            try
            {
                _socket9 = CreateListeningSocket(9);
                tasks.Add(ListenOnSocketAsync(_socket9, 9, stoppingToken));
                _logger.LogInformation("成功开始监听UDP端口9");
            }
            catch (SocketException ex) when (ex.ErrorCode == 10013)
            {
                _logger.LogWarning("无法监听端口9（需要管理员权限）。请以管理员身份运行应用程序以启用完整的魔术包监听功能。");
            }

            if (tasks.Count == 0)
            {
                _logger.LogError("无法监听任何端口。魔术包监听功能已禁用。请以管理员身份运行应用程序。");
                return;
            }

            _logger.LogInformation("魔术包监听服务运行中，已绑定 {Count} 个端口", tasks.Count);

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "魔术包监听服务发生严重错误");
        }
    }

    private Socket CreateListeningSocket(int port)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(new IPEndPoint(IPAddress.Any, port));
        return socket;
    }

    private async Task ListenOnSocketAsync(Socket socket, int port, CancellationToken stoppingToken)
    {
        var buffer = new byte[1024];
        var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        _logger.LogInformation("开始在端口{Port}上监听UDP数据包", port);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 接收数据
                var result = await socket.ReceiveFromAsync(
                    new ArraySegment<byte>(buffer),
                    SocketFlags.None,
                    remoteEndPoint,
                    stoppingToken);

                // 复制接收到的数据到新数组
                var receivedData = new byte[result.ReceivedBytes];
                Array.Copy(buffer, 0, receivedData, 0, result.ReceivedBytes);

                var sourceIP = ((IPEndPoint)result.RemoteEndPoint).Address.ToString();

                // 异步处理包（不阻塞接收循环）
                _ = Task.Run(async () => await ProcessPacketAsync(receivedData, sourceIP, port), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("端口{Port}的监听已被取消", port);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "端口{Port}接收数据时发生错误", port);
                // 短暂延迟后继续
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("端口{Port}的监听已停止", port);
    }

    private async Task ProcessPacketAsync(byte[] packet, string sourceIP, int port)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var listenerService = scope.ServiceProvider.GetRequiredService<IMagicPacketListenerService>();
            var context = scope.ServiceProvider.GetRequiredService<DeviceContext>();

            // 解析魔术包
            var capture = await listenerService.ParseMagicPacketAsync(packet, sourceIP);

            if (capture != null)
            {
                // 保存到数据库
                context.MagicPacketCaptures.Add(capture);
                await context.SaveChangesAsync();

                if (capture.IsValid)
                {
                    _logger.LogInformation(
                        "捕获有效魔术包 - 端口: {Port}, 目标MAC: {TargetMac}, 来源: {SourceIP}, 匹配设备: {DeviceName}",
                        port, capture.TargetMACAddress, sourceIP, capture.MatchedDeviceName ?? "未知");
                }
                else
                {
                    _logger.LogWarning(
                        "捕获无效魔术包 - 端口: {Port}, 来源: {SourceIP}, 大小: {PacketSize}字节",
                        port, sourceIP, capture.PacketSize);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理魔术包时发生异常 - 端口: {Port}, 来源: {SourceIP}", port, sourceIP);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止魔术包监听服务");

        _socket7?.Close();
        _socket9?.Close();
        _socket7?.Dispose();
        _socket9?.Dispose();

        await base.StopAsync(cancellationToken);

        _logger.LogInformation("魔术包监听服务已停止");
    }
}
