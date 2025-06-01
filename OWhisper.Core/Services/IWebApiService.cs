using System;
using System.Threading;
using System.Threading.Tasks;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// WebAPI服务接口
    /// </summary>
    public interface IWebApiService : IDisposable
    {
        /// <summary>
        /// 是否正在运行
        /// </summary>
        bool IsRunning { get; }
        
        /// <summary>
        /// 监听URL
        /// </summary>
        string ListenUrl { get; }
        
        /// <summary>
        /// 启动WebAPI服务
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 停止WebAPI服务
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);
    }
} 