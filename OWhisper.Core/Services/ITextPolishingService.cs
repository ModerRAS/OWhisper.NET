using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OWhisper.Core.Models;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// 文本润色服务接口
    /// </summary>
    public interface ITextPolishingService
    {
        /// <summary>
        /// 对文本进行润色
        /// </summary>
        /// <param name="request">润色请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>润色结果</returns>
        Task<TextPolishingResult> PolishTextAsync(TextPolishingRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取所有可用的润色模板
        /// </summary>
        /// <returns>模板列表</returns>
        Task<List<PolishingTemplate>> GetAvailableTemplatesAsync();

        /// <summary>
        /// 根据名称获取模板
        /// </summary>
        /// <param name="templateName">模板名称</param>
        /// <returns>模板信息</returns>
        Task<PolishingTemplate?> GetTemplateAsync(string templateName);

        /// <summary>
        /// 重新加载模板
        /// </summary>
        Task ReloadTemplatesAsync();

        /// <summary>
        /// 验证API连接
        /// </summary>
        /// <param name="apiKey">API密钥</param>
        /// <param name="baseUrl">API基地址</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否连接成功</returns>
        Task<(bool isSuccess, string errorMessage)> ValidateApiConnectionAsync(string apiKey, string baseUrl, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取支持的模型列表
        /// </summary>
        /// <returns>模型名称列表</returns>
        List<string> GetSupportedModels();
    }
} 