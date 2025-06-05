using System;

namespace OWhisper.Core.Models
{
    /// <summary>
    /// 文本润色结果模型
    /// </summary>
    public class TextPolishingResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 润色后的文本
        /// </summary>
        public string PolishedText { get; set; } = string.Empty;

        /// <summary>
        /// 原始文本
        /// </summary>
        public string OriginalText { get; set; } = string.Empty;

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 使用的模型
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// 使用的模板名称
        /// </summary>
        public string TemplateName { get; set; } = string.Empty;

        /// <summary>
        /// 消耗的Token数
        /// </summary>
        public int TokensUsed { get; set; }

        /// <summary>
        /// 处理耗时（毫秒）
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static TextPolishingResult Success(string originalText, string polishedText, string model, string templateName, int tokensUsed, long processingTimeMs)
        {
            return new TextPolishingResult
            {
                IsSuccess = true,
                OriginalText = originalText,
                PolishedText = polishedText,
                Model = model,
                TemplateName = templateName,
                TokensUsed = tokensUsed,
                ProcessingTimeMs = processingTimeMs
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static TextPolishingResult Failure(string originalText, string errorMessage, string model = "", string templateName = "")
        {
            return new TextPolishingResult
            {
                IsSuccess = false,
                OriginalText = originalText,
                ErrorMessage = errorMessage,
                Model = model,
                TemplateName = templateName
            };
        }
    }
} 