using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using OWhisper.Core.Services;
using EmbedIO;
using EmbedIO.WebApi;
using OWhisper.Core.Controllers;
using System.Threading;
using System.Threading.Tasks;

namespace OWhisper.CLI;

public class Program
{
    static async Task Main(string[] args)
    {
        // 配置Serilog
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/owhisper-cli-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("OWhisper CLI 启动中...");

            var host = CreateHostBuilder(args).Build();
            
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用程序启动失败");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // 注册服务
                services.AddSingleton<IPlatformPathService, PlatformPathService>();
                
                // 注册后台服务
                services.AddHostedService<WebServerHostedService>();
            })
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true)
                      .AddEnvironmentVariables()
                      .AddCommandLine(args);
            });
}

public class WebServerHostedService : BackgroundService
{
    private readonly ILogger<WebServerHostedService> _logger;
    private readonly IPlatformPathService _pathService;
    private readonly IConfiguration _configuration;
    private WebServer? _webServer;

    public WebServerHostedService(
        ILogger<WebServerHostedService> logger,
        IPlatformPathService pathService,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Web服务器启动中...");
        
        // 确保目录存在
        _pathService.EnsureDirectoriesExist();
        
        // 启动Whisper服务和队列服务
        var whisperService = WhisperService.Instance;
        whisperService.Start();
        
        var queueService = TranscriptionQueueService.Instance;
        queueService.Start();
        
        // 配置Web服务器
        var port = _configuration.GetValue<int>("WebServer:Port", 5000);
        var host = _configuration.GetValue<string>("WebServer:Host", "localhost");
        var url = $"http://{host}:{port}/";
        
        // 创建EmbedIO Web服务器，使用Core中的Controller
        _webServer = new WebServer(o => o
            .WithUrlPrefix(url)
            .WithMode(HttpListenerMode.EmbedIO))
            .WithWebApi("/", m => m.WithController<WhisperController>())
            .WithWebApi("/api", m => m.WithController<WhisperController>());
        
        _logger.LogInformation("OWhisper CLI 已启动，Web服务器运行在 {Url}", url);
        _logger.LogInformation("模型路径: {ModelPath}", _pathService.GetModelsPath());
        _logger.LogInformation("日志路径: {LogPath}", _pathService.GetLogsPath());
        _logger.LogInformation("可用的API端点:");
        _logger.LogInformation("  GET  /                    - API信息");
        _logger.LogInformation("  GET  /api/model/status    - 模型状态");
        _logger.LogInformation("  POST /api/transcribe      - 提交转录任务");
        _logger.LogInformation("  GET  /api/tasks           - 获取任务列表");
        _logger.LogInformation("  GET  /api/tasks/{{taskId}}  - 获取任务详情");
        _logger.LogInformation("  POST /api/tasks/{{taskId}}/cancel - 取消任务");
        
        // 启动Web服务器
        _ = _webServer.RunAsync(stoppingToken);
        
        // 保持服务运行
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
        
        _logger.LogInformation("正在停止Web服务器...");
        whisperService.Stop();
        queueService.Stop();
        _webServer?.Dispose();
    }
} 