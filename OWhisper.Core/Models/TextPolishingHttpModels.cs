using System;
using System.Collections.Generic;

namespace OWhisper.Core.Models
{
    /// <summary>
    /// 文本润色 HTTP 请求模型
    /// </summary>
    public class TextPolishingHttpRequest
    {
        /// <summary>
        /// 原始 SRT 文本内容
        /// </summary>
        public string SrtContent { get; set; } = string.Empty;

        /// <summary>
        /// 模板内容（JSON 字符串）
        /// </summary>
        public string TemplateContent { get; set; } = string.Empty;

        /// <summary>
        /// API Key
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// API 提供商（deepseek, openai, azure, 等）
        /// </summary>
        public string ApiProvider { get; set; } = "deepseek";

        /// <summary>
        /// API 基地址
        /// </summary>
        public string ApiBaseUrl { get; set; } = "https://api.deepseek.com/v1";

        /// <summary>
        /// 使用的模型
        /// </summary>
        public string Model { get; set; } = "deepseek-chat";

        /// <summary>
        /// 最大 Token 数
        /// </summary>
        public int MaxTokens { get; set; } = 4000;

        /// <summary>
        /// 温度参数
        /// </summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>
        /// 滑窗大小（字符数）
        /// </summary>
        public int WindowSize { get; set; } = 2000;

        /// <summary>
        /// 滑窗重叠大小（字符数）
        /// </summary>
        public int OverlapSize { get; set; } = 200;

        /// <summary>
        /// 自定义模板参数
        /// </summary>
        public Dictionary<string, object> CustomParameters { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 文本润色 HTTP 响应模型
    /// </summary>
    public class TextPolishingHttpResponse
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 润色后的 SRT 内容
        /// </summary>
        public string PolishedSrtContent { get; set; } = string.Empty;
        
        /// <summary>
        /// 润色后的纯文本内容
        /// </summary>
        public string PolishedText { get; set; } = string.Empty;
        
        /// <summary>
        /// 原始文本内容
        /// </summary>
        public string OriginalText { get; set; } = string.Empty;

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// 总处理时间（毫秒）
        /// </summary>
        public int ProcessingTimeMs { get; set; }

        /// <summary>
        /// 处理统计信息
        /// </summary>
        public PolishingStatistics Statistics { get; set; } = new PolishingStatistics();
    }

    /// <summary>
    /// 润色统计信息
    /// </summary>
    public class PolishingStatistics
    {
        /// <summary>
        /// 总处理时间（毫秒）
        /// </summary>
        public long TotalProcessingTimeMs { get; set; }

        /// <summary>
        /// 总消耗 Token 数
        /// </summary>
        public int TotalTokensUsed { get; set; }
        
        /// <summary>
        /// 总窗口数
        /// </summary>
        public int TotalWindows { get; set; }
        
        /// <summary>
        /// 已处理窗口数
        /// </summary>
        public int ProcessedWindows { get; set; }

        /// <summary>
        /// 处理的窗口数量
        /// </summary>
        public int WindowsProcessed { get; set; }

        /// <summary>
        /// 成功处理的窗口数
        /// </summary>
        public int SuccessfulWindows { get; set; }

        /// <summary>
        /// 失败的窗口数
        /// </summary>
        public int FailedWindows { get; set; }

        /// <summary>
        /// 使用的模型
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// 使用的 API 提供商
        /// </summary>
        public string ApiProvider { get; set; } = string.Empty;

        /// <summary>
        /// 详细的窗口处理信息
        /// </summary>
        public List<WindowProcessingInfo> WindowDetails { get; set; } = new List<WindowProcessingInfo>();
    }

    /// <summary>
    /// 窗口处理信息
    /// </summary>
    public class WindowProcessingInfo
    {
        /// <summary>
        /// 窗口索引
        /// </summary>
        public int WindowIndex { get; set; }

        /// <summary>
        /// 窗口开始位置
        /// </summary>
        public int StartPosition { get; set; }

        /// <summary>
        /// 窗口结束位置
        /// </summary>
        public int EndPosition { get; set; }

        /// <summary>
        /// 处理是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 处理时间（毫秒）
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// 消耗的 Token 数
        /// </summary>
        public int TokensUsed { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// API 连接测试请求
    /// </summary>
    public class ApiConnectionTestRequest
    {
        /// <summary>
        /// API Key
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// API 提供商
        /// </summary>
        public string ApiProvider { get; set; } = "deepseek";

        /// <summary>
        /// API 基地址
        /// </summary>
        public string ApiBaseUrl { get; set; } = "https://api.deepseek.com/v1";

        /// <summary>
        /// 测试模型
        /// </summary>
        public string Model { get; set; } = "deepseek-chat";
    }

    /// <summary>
    /// API 连接测试响应
    /// </summary>
    public class ApiConnectionTestResponse
    {
        /// <summary>
        /// 连接是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// 消息内容
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 响应时间（毫秒）
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// 支持的模型列表
        /// </summary>
        public List<string> SupportedModels { get; set; } = new List<string>();

        /// <summary>
        /// 响应预览
        /// </summary>
        public string ResponsePreview { get; set; } = string.Empty;
    }
} 