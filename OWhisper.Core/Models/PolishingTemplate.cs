using System;
using System.Collections.Generic;

namespace OWhisper.Core.Models
{
    /// <summary>
    /// 润色模板模型
    /// </summary>
    public class PolishingTemplate
    {
        /// <summary>
        /// 模板名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 模板显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 模板描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 模板分类（如：通用、医疗、法律、技术等）
        /// </summary>
        public string Category { get; set; } = "通用";

        /// <summary>
        /// 系统提示词模板
        /// </summary>
        public string SystemPromptTemplate { get; set; } = string.Empty;

        /// <summary>
        /// 用户消息模板
        /// </summary>
        public string UserMessageTemplate { get; set; } = string.Empty;

        /// <summary>
        /// 模板参数定义
        /// </summary>
        public List<TemplateParameter> Parameters { get; set; } = new List<TemplateParameter>();

        /// <summary>
        /// 模板版本
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// 模板参数定义
    /// </summary>
    public class TemplateParameter
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 参数显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 参数描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 参数类型（string, int, bool, enum等）
        /// </summary>
        public string Type { get; set; } = "string";

        /// <summary>
        /// 默认值
        /// </summary>
        public string DefaultValue { get; set; } = string.Empty;

        /// <summary>
        /// 是否必需
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// 可选值（用于enum类型）
        /// </summary>
        public List<string> Options { get; set; } = new List<string>();
    }
} 