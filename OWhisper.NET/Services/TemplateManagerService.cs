using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OWhisper.Core.Models;
using Serilog;

namespace OWhisper.NET.Services
{
    /// <summary>
    /// 模板管理服务（客户端）- 基于文件夹和文件名的模板系统
    /// </summary>
    public class TemplateManagerService
    {
        private readonly string _templatesDirectory;
        private readonly Dictionary<string, PolishingTemplate> _templateCache;

        public TemplateManagerService()
        {
            // 模板目录在程序文件夹下的 Templates 子目录
            _templatesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            _templateCache = new Dictionary<string, PolishingTemplate>();
            
            EnsureTemplatesDirectoryExists();
            CreateDefaultTemplatesIfNotExists();
        }

        /// <summary>
        /// 获取所有可用的模板
        /// </summary>
        /// <returns>模板列表</returns>
        public async Task<List<PolishingTemplate>> GetAllTemplatesAsync()
        {
            try
            {
                var templates = new List<PolishingTemplate>();

                // 处理根目录下的模板文件（未分类）
                await LoadTemplatesFromDirectory(_templatesDirectory, "通用", templates);

                // 扫描所有分类文件夹
                var categoryDirs = Directory.GetDirectories(_templatesDirectory);
                foreach (var categoryDir in categoryDirs)
                {
                    var categoryName = Path.GetFileName(categoryDir);
                    await LoadTemplatesFromDirectory(categoryDir, categoryName, templates);
                }

                Log.Information("成功加载 {Count} 个模板", templates.Count);
                return templates.OrderBy(t => t.Category).ThenBy(t => t.DisplayName).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "获取所有模板失败");
                return new List<PolishingTemplate>();
            }
        }

        /// <summary>
        /// 从指定目录加载模板
        /// </summary>
        private async Task LoadTemplatesFromDirectory(string directory, string category, List<PolishingTemplate> templates)
        {
            var templateFiles = Directory.GetFiles(directory, "*.txt", SearchOption.TopDirectoryOnly);

            foreach (var templateFile in templateFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(templateFile);
                    var content = File.ReadAllText(templateFile);
                    
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var template = ParseTemplateContent(fileName, category, content, templateFile);
                        if (template != null)
                        {
                            templates.Add(template);
                            
                            // 更新缓存
                            var templateKey = $"{category}:{fileName}";
                            _templateCache[templateKey] = template;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "加载模板文件失败: {TemplateFile}", templateFile);
                }
            }
        }

        /// <summary>
        /// 解析模板内容，支持元数据头部
        /// </summary>
        private PolishingTemplate ParseTemplateContent(string fileName, string category, string content, string filePath)
        {
            var template = new PolishingTemplate
            {
                Name = $"{category}:{fileName}",
                DisplayName = fileName,
                Category = category,
                CreatedAt = File.GetCreationTime(filePath),
                UpdatedAt = File.GetLastWriteTime(filePath)
            };

            // 检查是否有元数据头部（以 --- 开始和结束）
            if (content.StartsWith("---"))
            {
                var parts = content.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    // 解析元数据
                    var metadata = parts[0].Trim();
                    var templateContent = string.Join("---", parts.Skip(1)).Trim();

                    ParseMetadata(template, metadata);
                    template.UserMessageTemplate = templateContent;
                }
                else
                {
                    template.UserMessageTemplate = content;
                }
            }
            else
            {
                template.UserMessageTemplate = content;
            }

            // 如果没有系统提示词，使用默认的
            if (string.IsNullOrWhiteSpace(template.SystemPromptTemplate))
            {
                template.SystemPromptTemplate = GetDefaultSystemPrompt(category);
            }

            // 确保有基础参数
            if (!template.Parameters.Any(p => p.Name == "original_text"))
            {
                template.Parameters.Insert(0, new TemplateParameter
                {
                    Name = "original_text",
                    DisplayName = "原始文本",
                    Description = "需要润色的原始文本",
                    Type = "string",
                    IsRequired = true
                });
            }

            return template;
        }

        /// <summary>
        /// 解析模板元数据
        /// </summary>
        private void ParseMetadata(PolishingTemplate template, string metadata)
        {
            var lines = metadata.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex == -1) continue;

                var key = line.Substring(0, colonIndex).Trim();
                var value = line.Substring(colonIndex + 1).Trim();

                switch (key.ToLower())
                {
                    case "title":
                    case "标题":
                        template.DisplayName = value;
                        break;
                    case "description":
                    case "描述":
                        template.Description = value;
                        break;
                    case "category":
                    case "分类":
                        template.Category = value;
                        break;
                    case "system_prompt":
                    case "系统提示":
                        template.SystemPromptTemplate = value.Replace("\\n", "\n");
                        break;
                    case "enabled":
                    case "启用":
                        template.IsEnabled = bool.TryParse(value, out var enabled) ? enabled : true;
                        break;
                }
            }
        }

        /// <summary>
        /// 获取默认系统提示词
        /// </summary>
        private string GetDefaultSystemPrompt(string category)
        {
            var lowerCategory = category.ToLower();
            switch (lowerCategory)
            {
                case "学术":
                case "academic":
                    return "你是一个专业的学术文本润色专家。请对用户提供的文本进行学术化润色，要求：1. 使用更加正式和准确的学术用词 2. 完善逻辑结构和论证链条 3. 提升文本的专业性和严谨性 4. 确保术语使用的准确性和一致性 5. 优化段落结构和语句衔接 6. 保持客观中性的学术语调。请直接返回润色后的文本，保持学术写作的规范性。";
                
                case "商务":
                case "business":
                    return "你是一个商务沟通专家。请对用户提供的商务文本进行润色，要求：1. 使用专业且友好的商务语言 2. 确保信息传达清晰明确 3. 优化说服力和影响力 4. 保持恰当的商务礼仪和语调 5. 突出重点信息和行动要求 6. 确保文本简洁有力，易于理解。请直接返回润色后的文本，确保符合商务沟通标准。";
                
                case "法律":
                case "legal":
                    return "你是一个法律文档润色专家。请对用户提供的法律相关文本进行专业润色，要求：1. 使用准确的法律术语 2. 确保逻辑严密和表达准确 3. 保持法律文档的正式性 4. 优化条款结构和语言表达 5. 确保符合法律文书的规范。请直接返回润色后的文本，保持法律文档的专业性和严谨性。";
                
                case "医疗":
                case "medical":
                    return "你是一个医疗文档润色专家。请对用户提供的医疗相关文本进行专业润色，要求：1. 使用准确的医学术语 2. 确保表达的严谨性和科学性 3. 保持医疗文档的专业格式 4. 纠正医学术语的使用错误 5. 提升文本的可读性和专业性。请直接返回润色后的文本，保持医疗文档的专业性。";
                
                case "技术":
                case "technical":
                    return "你是一个技术文档润色专家。请对用户提供的技术文档进行专业润色，要求：1. 使用准确的技术术语 2. 确保技术描述的精确性 3. 优化技术流程的表达 4. 保持技术文档的专业性 5. 提升文档的可读性和实用性。请直接返回润色后的文本，确保技术内容的准确性。";
                
                default:
                    return "你是一个专业的文本润色助手。请对用户提供的文本进行润色和优化，要求：1. 保持原文的核心意思和信息不变 2. 改善语法、用词和表达方式 3. 提高文本的可读性和流畅性 4. 纠正错别字和标点符号错误 5. 适当调整句式结构，使表达更加清晰。请直接返回润色后的文本，不要添加额外的解释或说明。";
            }
        }

        /// <summary>
        /// 根据名称获取模板
        /// </summary>
        /// <param name="templateName">模板名称</param>
        /// <returns>模板对象</returns>
        public async Task<PolishingTemplate> GetTemplateByNameAsync(string templateName)
        {
            try
            {
                // 先从缓存中查找
                if (_templateCache.TryGetValue(templateName, out var cachedTemplate))
                {
                    return cachedTemplate;
                }

                // 如果缓存中没有，重新加载所有模板
                var allTemplates = await GetAllTemplatesAsync();
                var template = allTemplates.FirstOrDefault(t => t.Name == templateName);

                Log.Information(template != null ? "找到模板: {TemplateName}" : "未找到模板: {TemplateName}", templateName);
                return template;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "获取模板失败: {TemplateName}", templateName);
                return null;
            }
        }

        /// <summary>
        /// 保存模板到文件
        /// </summary>
        /// <param name="template">模板对象</param>
        /// <returns>是否保存成功</returns>
        public async Task<bool> SaveTemplateAsync(PolishingTemplate template)
        {
            try
            {
                if (template == null || string.IsNullOrWhiteSpace(template.DisplayName))
                {
                    Log.Warning("模板对象或显示名称为空，无法保存");
                    return false;
                }

                var categoryDir = Path.Combine(_templatesDirectory, template.Category);
                if (!Directory.Exists(categoryDir))
                {
                    Directory.CreateDirectory(categoryDir);
                }

                var templateFile = Path.Combine(categoryDir, $"{template.DisplayName}.txt");
                
                // 构建模板内容
                var content = BuildTemplateContent(template);
                File.WriteAllText(templateFile, content);

                // 更新缓存
                var templateKey = $"{template.Category}:{template.DisplayName}";
                _templateCache[templateKey] = template;

                Log.Information("成功保存模板: {TemplateName}", template.DisplayName);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存模板失败: {TemplateName}", template?.DisplayName);
                return false;
            }
        }

        /// <summary>
        /// 构建模板文件内容
        /// </summary>
        private string BuildTemplateContent(PolishingTemplate template)
        {
            var lines = new List<string>();
            
            // 添加元数据头部
            if (!string.IsNullOrWhiteSpace(template.Description) || 
                !string.IsNullOrWhiteSpace(template.SystemPromptTemplate))
            {
                lines.Add("---");
                
                if (!string.IsNullOrWhiteSpace(template.Description))
                {
                    lines.Add($"描述: {template.Description}");
                }
                
                if (!string.IsNullOrWhiteSpace(template.SystemPromptTemplate))
                {
                    lines.Add($"系统提示: {template.SystemPromptTemplate.Replace("\n", "\\n")}");
                }
                
                lines.Add("---");
            }
            
            // 添加模板内容
            lines.Add(template.UserMessageTemplate ?? "");
            
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// 删除模板
        /// </summary>
        /// <param name="templateName">模板名称</param>
        /// <returns>是否删除成功</returns>
        public bool DeleteTemplate(string templateName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(templateName))
                {
                    return false;
                }

                // 从模板名称解析分类和文件名
                var parts = templateName.Split(':');
                if (parts.Length != 2)
                {
                    Log.Warning("模板名称格式无效: {TemplateName}", templateName);
                    return false;
                }

                var category = parts[0];
                var fileName = parts[1];
                var categoryDir = Path.Combine(_templatesDirectory, category);
                var templateFile = Path.Combine(categoryDir, $"{fileName}.txt");

                if (File.Exists(templateFile))
                {
                    File.Delete(templateFile);
                    _templateCache.Remove(templateName);
                    Log.Information("成功删除模板: {TemplateName}", templateName);
                    return true;
                }

                Log.Warning("模板文件不存在: {TemplateName}", templateName);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "删除模板失败: {TemplateName}", templateName);
                return false;
            }
        }

        /// <summary>
        /// 获取模板目录路径
        /// </summary>
        /// <returns>模板目录路径</returns>
        public string GetTemplatesDirectory()
        {
            return _templatesDirectory;
        }

        /// <summary>
        /// 将模板序列化为JSON字符串（向后兼容）
        /// </summary>
        /// <param name="template">模板对象</param>
        /// <returns>JSON字符串</returns>
        public string SerializeTemplate(PolishingTemplate template)
        {
            try
            {
                if (template == null)
                {
                    return string.Empty;
                }

                return JsonConvert.SerializeObject(template, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.None
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "序列化模板失败");
                return string.Empty;
            }
        }

        /// <summary>
        /// 刷新模板缓存
        /// </summary>
        public void RefreshCache()
        {
            _templateCache.Clear();
            Log.Information("模板缓存已刷新");
        }

        #region 私有方法

        /// <summary>
        /// 确保模板目录存在
        /// </summary>
        private void EnsureTemplatesDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_templatesDirectory))
                {
                    Directory.CreateDirectory(_templatesDirectory);
                    Log.Information("创建模板目录: {TemplatesDirectory}", _templatesDirectory);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "创建模板目录失败: {TemplatesDirectory}", _templatesDirectory);
            }
        }

        /// <summary>
        /// 创建默认模板（如果不存在）
        /// </summary>
        private void CreateDefaultTemplatesIfNotExists()
        {
            try
            {
                // 创建分类文件夹和默认模板
                CreateDefaultTemplate("通用", "通用润色", @"请对以下文本进行润色和优化：

{{ original_text }}

要求：
1. 保持原文的核心意思和信息不变
2. 改善语法、用词和表达方式
3. 提高文本的可读性和流畅性
4. 纠正错别字和标点符号错误
5. 适当调整句式结构，使表达更加清晰");

                CreateDefaultTemplate("学术", "学术论文润色", @"---
标题: 学术论文润色
描述: 适用于学术论文、研究报告等专业文档的润色模板
系统提示: 你是一个专业的学术文本编辑助手。请对用户提供的学术文本进行润色，确保用词准确、专业，逻辑清晰、结构严谨，符合学术写作规范，保持学术性和客观性。
---
请对以下学术文本进行专业润色：

{{ original_text }}

请确保：
1. 使用准确、专业的学术用词
2. 逻辑清晰、结构严谨
3. 符合学术写作规范
4. 保持学术性和客观性");

                CreateDefaultTemplate("商务", "商务沟通润色", @"---
标题: 商务沟通润色
描述: 适用于商务邮件、报告、提案等商务沟通场景的润色模板
---
请对以下商务文本进行润色：

{{ original_text }}

要求：
1. 语言正式、礼貌
2. 表达清晰、简洁
3. 突出重点、易于理解
4. 符合商务沟通礼仪");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "创建默认模板失败");
            }
        }

        /// <summary>
        /// 创建默认模板文件
        /// </summary>
        private void CreateDefaultTemplate(string category, string templateName, string content)
        {
            try
            {
                var categoryDir = Path.Combine(_templatesDirectory, category);
                if (!Directory.Exists(categoryDir))
                {
                    Directory.CreateDirectory(categoryDir);
                }

                var templateFile = Path.Combine(categoryDir, $"{templateName}.txt");
                if (!File.Exists(templateFile))
                {
                    File.WriteAllText(templateFile, content);
                    Log.Information("创建默认模板: {TemplateName}", templateName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存模板失败: {TemplateName}", templateName);
            }
        }

        #endregion
    }
} 