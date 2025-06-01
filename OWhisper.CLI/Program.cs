using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using OWhisper.Core.Services;

namespace OWhisper.CLI;

class Program
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

    static IHostBuilder CreateHostBuilder(string[] args) =>
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

    public WebServerHostedService(
        ILogger<WebServerHostedService> logger,
        IPlatformPathService pathService)
    {
        _logger = logger;
        _pathService = pathService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Web服务器启动中...");
        
        // 确保目录存在
        _pathService.EnsureDirectoriesExist();
        
        // 使用WhisperService单例
        var whisperService = WhisperService.Instance;
        whisperService.Start();
        
        _logger.LogInformation("OWhisper CLI 已启动，Web服务器运行在 http://localhost:5000");
        _logger.LogInformation("模型路径: {ModelPath}", _pathService.GetModelsPath());
        _logger.LogInformation("日志路径: {LogPath}", _pathService.GetLogsPath());
        
        // 保持服务运行
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
        
        _logger.LogInformation("正在停止Web服务器...");
        whisperService.Stop();
    }
} 