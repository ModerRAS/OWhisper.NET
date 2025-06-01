using System;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.WebApi;
using Microsoft.Extensions.Logging;
using OWhisper.Core.Controllers;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// WebAPI服务实现
    /// </summary>
    public class WebApiService : IWebApiService
    {
        private readonly ILogger<WebApiService> _logger;
        private readonly IPlatformPathService _pathService;
        private WebServer? _webServer;
        private bool _disposed = false;
        
        public bool IsRunning { get; private set; }
        public string ListenUrl { get; }
        
        /// <summary>
        /// 默认配置常量
        /// </summary>
        public const string DEFAULT_HOST = "+";
        public const int DEFAULT_PORT = 11899;
        
        public WebApiService(ILogger<WebApiService> logger, IPlatformPathService pathService, string? host = null, int? port = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
            
            var listenHost = host ?? GetConfiguredHost();
            var listenPort = port ?? GetConfiguredPort();
            ListenUrl = $"http://{listenHost}:{listenPort}";
        }
        
        /// <summary>
        /// 启动WebAPI服务
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (IsRunning)
            {
                _logger.LogWarning("WebAPI服务已经在运行中");
                return;
            }
            
            try
            {
                _logger.LogInformation("正在启动Web API服务器: {Url}", ListenUrl);
                
                // 确保目录存在
                _pathService.EnsureDirectoriesExist();
                
                // 启动核心服务
                await StartCoreServicesAsync();
                
                // 创建并配置WebServer
                _webServer = new WebServer(o => o
                    .WithUrlPrefix(ListenUrl)
                    .WithMode(HttpListenerMode.EmbedIO))
                    .WithWebApi("/", m => m
                        .WithController<WhisperController>()
                        .WithController<SseController>());
                
                // 启动WebServer
                _ = _webServer.RunAsync(cancellationToken);
                IsRunning = true;
                
                _logger.LogInformation("Web API服务器启动成功，监听地址: {Url}", ListenUrl);
                LogApiEndpoints();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Web API服务器启动失败");
                throw new Exception($"Web API服务器启动失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 停止WebAPI服务
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!IsRunning)
            {
                return;
            }
            
            try
            {
                _logger.LogInformation("正在停止Web API服务器...");
                
                // 停止核心服务
                await StopCoreServicesAsync();
                
                // 停止WebServer
                _webServer?.Dispose();
                _webServer = null;
                IsRunning = false;
                
                _logger.LogInformation("Web API服务器已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止Web API服务器时出错");
                throw;
            }
        }
        
        /// <summary>
        /// 启动核心服务
        /// </summary>
        private async Task StartCoreServicesAsync()
        {
            // 启动Whisper服务
            var whisperService = WhisperService.Instance;
            whisperService.Start();
            _logger.LogInformation("Whisper服务已启动");
            
            // 启动转录队列服务
            var queueService = TranscriptionQueueService.Instance;
            queueService.Start();
            _logger.LogInformation("转录队列服务已启动");
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 停止核心服务
        /// </summary>
        private async Task StopCoreServicesAsync()
        {
            // 停止转录队列服务
            var queueService = TranscriptionQueueService.Instance;
            queueService.Stop();
            _logger.LogInformation("转录队列服务已停止");
            
            // 停止Whisper服务
            var whisperService = WhisperService.Instance;
            whisperService.Stop();
            _logger.LogInformation("Whisper服务已停止");
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// 记录API端点信息
        /// </summary>
        private void LogApiEndpoints()
        {
            _logger.LogInformation("模型路径: {ModelPath}", _pathService.GetModelsPath());
            _logger.LogInformation("日志路径: {LogPath}", _pathService.GetLogsPath());
            _logger.LogInformation("可用的API端点:");
            _logger.LogInformation("  GET  /                    - API信息");
            _logger.LogInformation("  GET  /api/model/status    - 模型状态");
            _logger.LogInformation("  POST /api/transcribe      - 提交转录任务");
            _logger.LogInformation("  GET  /api/tasks           - 获取任务列表");
            _logger.LogInformation("  GET  /api/tasks/{{taskId}}  - 获取任务详情");
            _logger.LogInformation("  POST /api/tasks/{{taskId}}/cancel - 取消任务");
        }
        
        /// <summary>
        /// 获取配置的监听地址
        /// </summary>
        private static string GetConfiguredHost()
        {
            return Environment.GetEnvironmentVariable("OWHISPER_HOST") ?? DEFAULT_HOST;
        }
        
        /// <summary>
        /// 获取配置的监听端口
        /// </summary>
        private static int GetConfiguredPort()
        {
            var portStr = Environment.GetEnvironmentVariable("OWHISPER_PORT");
            if (int.TryParse(portStr, out int port) && port > 0 && port <= 65535)
            {
                return port;
            }
            return DEFAULT_PORT;
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    StopAsync().Wait(5000); // 等待最多5秒
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "释放WebAPI服务时出错");
                }
                
                _disposed = true;
            }
        }
    }
} 
