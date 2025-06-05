using System;
using System.Threading;
using System.Threading.Tasks;
using OWhisper.Core.Models;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// 滑窗文本润色服务接口
    /// </summary>
    public interface ISlidingWindowPolishingService
    {
        /// <summary>
        /// 使用滑窗方式润色 SRT 文本
        /// </summary>
        /// <param name="request">润色请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>润色响应</returns>
        Task<TextPolishingHttpResponse> PolishSrtAsync(TextPolishingHttpRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 测试 API 连接
        /// </summary>
        /// <param name="request">连接测试请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接测试响应</returns>
        Task<ApiConnectionTestResponse> TestApiConnectionAsync(ApiConnectionTestRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取支持的 API 提供商列表
        /// </summary>
        /// <returns>支持的提供商列表</returns>
        string[] GetSupportedProviders();

        /// <summary>
        /// 获取指定提供商支持的模型列表
        /// </summary>
        /// <param name="provider">API 提供商</param>
        /// <returns>支持的模型列表</returns>
        string[] GetSupportedModels(string provider);
    }
} 