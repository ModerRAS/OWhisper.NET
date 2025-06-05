using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OWhisper.Core.Models;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// 模板管理服务接口
    /// </summary>
    public interface ITemplateManagerService
    {
        /// <summary>
        /// 加载所有模板
        /// </summary>
        /// <returns>模板列表</returns>
        Task<List<PolishingTemplate>> LoadTemplatesAsync();

        /// <summary>
        /// 根据名称获取模板
        /// </summary>
        /// <param name="templateName">模板名称</param>
        /// <returns>模板信息</returns>
        Task<PolishingTemplate?> GetTemplateAsync(string templateName);

        /// <summary>
        /// 刷新模板缓存
        /// </summary>
        Task RefreshCacheAsync();

        /// <summary>
        /// 获取模板目录路径
        /// </summary>
        /// <returns>模板目录路径</returns>
        string GetTemplatesDirectory();

        /// <summary>
        /// 渲染模板
        /// </summary>
        /// <param name="template">模板内容</param>
        /// <param name="parameters">参数</param>
        /// <returns>渲染结果</returns>
        Task<string> RenderTemplateAsync(string template, Dictionary<string, object> parameters);

        /// <summary>
        /// 验证模板语法
        /// </summary>
        /// <param name="templateContent">模板内容</param>
        /// <returns>验证结果</returns>
        (bool isValid, string errorMessage) ValidateTemplate(string templateContent);
    }
} 