using System;
using System.Collections.Generic;

namespace OWhisper.Core.Models
{
    /// <summary>
    /// 文本润色请求模型
    /// </summary>
    public class TextPolishingRequest
    {
        /// <summary>
        /// 原始文本
        /// </summary>
        public string OriginalText { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用润色
        /// </summary>
        public bool EnablePolishing { get; set; }

        /// <summary>
        /// 使用的润色模型
        /// </summary>
        public string Model { get; set; } = "deepseek-chat";

        /// <summary>
        /// 使用的模板名称
        /// </summary>
        public string TemplateName { get; set; } = "default";

        /// <summary>
        /// 自定义参数（用于模板渲染）
        /// </summary>
        public Dictionary<string, object> CustomParameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// API Key
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// API 基地址
        /// </summary>
        public string ApiBaseUrl { get; set; } = "https://api.deepseek.com/v1";

        /// <summary>
        /// 最大Token数
        /// </summary>
        public int MaxTokens { get; set; } = 4000;

        /// <summary>
        /// 温度参数
        /// </summary>
        public double Temperature { get; set; } = 0.7;
    }
} 