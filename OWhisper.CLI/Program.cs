using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using OWhisper.Core.Services;
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
                services.AddSingleton<IWebApiService, WebApiService>();
                
                // 注册后台服务
                services.AddHostedService<WebApiHostedService>();
            })
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true)
                      .AddEnvironmentVariables()
                      .AddCommandLine(args);
            });
}

public class WebApiHostedService : BackgroundService
{
    private readonly ILogger<WebApiHostedService> _logger;
    private readonly IWebApiService _webApiService;
    private readonly IConfiguration _configuration;

    public WebApiHostedService(
        ILogger<WebApiHostedService> logger,
        IWebApiService webApiService,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _webApiService = webApiService ?? throw new ArgumentNullException(nameof(webApiService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebAPI托管服务启动中...");
        
        try
        {
            // 启动WebAPI服务
            await _webApiService.StartAsync(stoppingToken);
            
            _logger.LogInformation("OWhisper CLI 已启动，Web服务器运行在 {Url}", _webApiService.ListenUrl);
            
            // 保持服务运行
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消，不记录错误
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebAPI托管服务运行时出错");
            throw;
        }
        finally
        {
            _logger.LogInformation("正在停止WebAPI服务...");
            await _webApiService.StopAsync(CancellationToken.None);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止WebAPI托管服务...");
        
        try
        {
            // 停止WebAPI服务
            await _webApiService.StopAsync(cancellationToken);
            _logger.LogInformation("WebAPI服务已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止WebAPI服务时出错");
            throw;
        }
        finally
        {
            await base.StopAsync(cancellationToken);
        }
    }

    public override void Dispose()
    {
        _webApiService?.Dispose();
        base.Dispose();
    }
} 