using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OWhisper.Core.Models;
using Scriban;
using Serilog;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// 模板管理服务实现 - 基于文件夹和文件名的模板系统
    /// </summary>
    public class TemplateManagerService : ITemplateManagerService
    {
        private readonly IPlatformPathService _pathService;
        private readonly ConcurrentDictionary<string, PolishingTemplate> _templateCache = new();
        private readonly object _loadLock = new object();
        private DateTime _lastLoadTime = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5); // 缓存5分钟

        public TemplateManagerService(IPlatformPathService pathService)
        {
            _pathService = pathService;
        }

        /// <summary>
        /// 获取模板目录路径
        /// </summary>
        public string GetTemplatesDirectory()
        {
            var appDataPath = _pathService.GetApplicationDataPath();
            var templatesDir = Path.Combine(appDataPath, "PolishingTemplates");
            
            if (!Directory.Exists(templatesDir))
            {
                Directory.CreateDirectory(templatesDir);
                // 创建默认模板
                _ = Task.Run(CreateDefaultTemplatesAsync);
            }
            
            return templatesDir;
        }

        /// <summary>
        /// 加载所有模板
        /// </summary>
        public async Task<List<PolishingTemplate>> LoadTemplatesAsync()
        {
            // 检查缓存是否过期
            if (_templateCache.Any() && DateTime.Now - _lastLoadTime < _cacheExpiry)
            {
                return _templateCache.Values.Where(t => t.IsEnabled).OrderBy(t => t.Category).ThenBy(t => t.DisplayName).ToList();
            }

            bool shouldLoad = false;
            lock (_loadLock)
            {
                // 双重检查
                if (_templateCache.Any() && DateTime.Now - _lastLoadTime < _cacheExpiry)
                {
                    return _templateCache.Values.Where(t => t.IsEnabled).OrderBy(t => t.Category).ThenBy(t => t.DisplayName).ToList();
                }

                shouldLoad = true;
            }

            if (shouldLoad)
            {
                try
                {
                    _templateCache.Clear();
                    var templatesDir = GetTemplatesDirectory();

                    // 扫描所有分类文件夹
                    var categoryDirs = Directory.GetDirectories(templatesDir);
                    
                    Log.Information("正在加载润色模板，找到 {Count} 个分类文件夹", categoryDirs.Length);

                    // 处理根目录下的模板文件（未分类）
                    await LoadTemplatesFromDirectory(templatesDir, "通用");

                    // 处理各个分类文件夹
                    foreach (var categoryDir in categoryDirs)
                    {
                        var categoryName = Path.GetFileName(categoryDir);
                        await LoadTemplatesFromDirectory(categoryDir, categoryName);
                    }

                    lock (_loadLock)
                    {
                        _lastLoadTime = DateTime.Now;
                    }
                    
                    Log.Information("模板加载完成，共加载 {Count} 个有效模板", _templateCache.Count);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "加载模板时发生错误");
                    throw;
                }
            }

            return _templateCache.Values.Where(t => t.IsEnabled).OrderBy(t => t.Category).ThenBy(t => t.DisplayName).ToList();
        }

        /// <summary>
        /// 从指定目录加载模板
        /// </summary>
        private async Task LoadTemplatesFromDirectory(string directory, string category)
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
                            var templateKey = $"{category}:{fileName}";
                            _templateCache.TryAdd(templateKey, template);
                            Log.Debug("已加载模板: {TemplateName} ({Category})", template.DisplayName, template.Category);
                        }
                    }
                    else
                    {
                        Log.Warning("模板文件内容为空: {TemplateFile}", templateFile);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "加载模板文件失败: {TemplateFile}", templateFile);
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
                IsEnabled = true,
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
            return category.ToLower() switch
            {
                "学术" or "academic" => "你是一个专业的学术文本润色专家。请对用户提供的文本进行学术化润色，要求：1. 使用更加正式和准确的学术用词 2. 完善逻辑结构和论证链条 3. 提升文本的专业性和严谨性 4. 确保术语使用的准确性和一致性 5. 优化段落结构和语句衔接 6. 保持客观中性的学术语调。请直接返回润色后的文本，保持学术写作的规范性。",
                
                "商务" or "business" => "你是一个商务沟通专家。请对用户提供的商务文本进行润色，要求：1. 使用专业且友好的商务语言 2. 确保信息传达清晰明确 3. 优化说服力和影响力 4. 保持恰当的商务礼仪和语调 5. 突出重点信息和行动要求 6. 确保文本简洁有力，易于理解。请直接返回润色后的文本，确保符合商务沟通标准。",
                
                "法律" or "legal" => "你是一个法律文档润色专家。请对用户提供的法律相关文本进行专业润色，要求：1. 使用准确的法律术语 2. 确保逻辑严密和表达准确 3. 保持法律文档的正式性 4. 优化条款结构和语言表达 5. 确保符合法律文书的规范。请直接返回润色后的文本，保持法律文档的专业性和严谨性。",
                
                "医疗" or "medical" => "你是一个医疗文档润色专家。请对用户提供的医疗相关文本进行专业润色，要求：1. 使用准确的医学术语 2. 确保表达的严谨性和科学性 3. 保持医疗文档的专业格式 4. 纠正医学术语的使用错误 5. 提升文本的可读性和专业性。请直接返回润色后的文本，保持医疗文档的专业性。",
                
                "技术" or "technical" => "你是一个技术文档润色专家。请对用户提供的技术文档进行专业润色，要求：1. 使用准确的技术术语 2. 确保技术描述的精确性 3. 优化技术流程的表达 4. 保持技术文档的专业性 5. 提升文档的可读性和实用性。请直接返回润色后的文本，确保技术内容的准确性。",
                
                _ => "你是一个专业的文本润色助手。请对用户提供的文本进行润色和优化，要求：1. 保持原文的核心意思和信息不变 2. 改善语法、用词和表达方式 3. 提高文本的可读性和流畅性 4. 纠正错别字和标点符号错误 5. 适当调整句式结构，使表达更加清晰。请直接返回润色后的文本，不要添加额外的解释或说明。"
            };
        }

        /// <summary>
        /// 根据名称获取模板
        /// </summary>
        public async Task<PolishingTemplate?> GetTemplateAsync(string templateName)
        {
            if (string.IsNullOrEmpty(templateName))
            {
                return null;
            }

            // 确保模板已加载
            await LoadTemplatesAsync();
            
            _templateCache.TryGetValue(templateName, out var template);
            return template?.IsEnabled == true ? template : null;
        }

        /// <summary>
        /// 刷新模板缓存
        /// </summary>
        public async Task RefreshCacheAsync()
        {
            _templateCache.Clear();
            _lastLoadTime = DateTime.MinValue;
            await LoadTemplatesAsync();
        }

        /// <summary>
        /// 渲染模板
        /// </summary>
        public async Task<string> RenderTemplateAsync(string template, Dictionary<string, object> parameters)
        {
            try
            {
                var scribanTemplate = Template.Parse(template);
                if (scribanTemplate.HasErrors)
                {
                    var errors = string.Join("; ", scribanTemplate.Messages.Select(m => m.Message));
                    throw new InvalidOperationException($"模板语法错误: {errors}");
                }

                // 创建模板上下文
                var templateContext = new TemplateContext();
                var scriptObject = new Scriban.Runtime.ScriptObject();
                
                // 添加参数到上下文
                foreach (var parameter in parameters)
                {
                    scriptObject[parameter.Key] = parameter.Value;
                }
                
                templateContext.PushGlobal(scriptObject);
                
                // 渲染模板
                var result = await scribanTemplate.RenderAsync(templateContext);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "渲染模板失败");
                throw;
            }
        }

        /// <summary>
        /// 验证模板语法
        /// </summary>
        public (bool isValid, string errorMessage) ValidateTemplate(string templateContent)
        {
            try
            {
                var template = Template.Parse(templateContent);
                if (template.HasErrors)
                {
                    var errors = string.Join("; ", template.Messages.Select(m => m.Message));
                    return (false, errors);
                }
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 创建默认模板
        /// </summary>
        private async Task CreateDefaultTemplatesAsync()
        {
            var templatesDir = GetTemplatesDirectory();
            
            // 创建分类文件夹和默认模板
            await CreateDefaultTemplate(templatesDir, "通用", "通用润色", @"请对以下文本进行润色和优化：

{{ original_text }}

要求：
1. 保持原文的核心意思和信息不变
2. 改善语法、用词和表达方式
3. 提高文本的可读性和流畅性
4. 纠正错别字和标点符号错误
5. 适当调整句式结构，使表达更加清晰");

            await CreateDefaultTemplate(templatesDir, "学术", "学术论文润色", @"---
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

            await CreateDefaultTemplate(templatesDir, "商务", "商务沟通润色", @"---
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

            await CreateDefaultTemplate(templatesDir, "法律", "法律文档润色", @"---
标题: 法律文档润色
描述: 适用于法律文档的专业润色模板
---
请对以下法律文本进行专业润色：

{{ original_text }}

要求：
1. 使用准确的法律术语
2. 确保逻辑严密和表达准确
3. 保持法律文档的正式性
4. 优化条款结构和语言表达
5. 确保符合法律文书的规范");

            await CreateDefaultTemplate(templatesDir, "医疗", "医疗文档润色", @"---
标题: 医疗文档润色
描述: 适用于医疗相关文档的专业润色模板
---
请对以下医疗文本进行专业润色：

{{ original_text }}

要求：
1. 使用准确的医学术语
2. 确保表达的严谨性和科学性
3. 保持医疗文档的专业格式
4. 纠正医学术语的使用错误
5. 提升文本的可读性和专业性");

            await CreateDefaultTemplate(templatesDir, "技术", "技术文档润色", @"---
标题: 技术文档润色
描述: 适用于技术文档的专业润色模板
---
请对以下技术文档进行专业润色：

{{ original_text }}

要求：
1. 使用准确的技术术语
2. 确保技术描述的精确性
3. 优化技术流程的表达
4. 保持技术文档的专业性
5. 提升文档的可读性和实用性");

            Log.Information("默认润色模板创建完成");
        }

        /// <summary>
        /// 创建默认模板文件
        /// </summary>
        private async Task CreateDefaultTemplate(string templatesDir, string category, string templateName, string content)
        {
            try
            {
                var categoryDir = Path.Combine(templatesDir, category);
                if (!Directory.Exists(categoryDir))
                {
                    Directory.CreateDirectory(categoryDir);
                }

                var templateFile = Path.Combine(categoryDir, $"{templateName}.txt");
                if (!File.Exists(templateFile))
                {
                    File.WriteAllText(templateFile, content);
                    Log.Information("已创建默认模板: {TemplateFile}", templateFile);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存模板失败: {TemplateName}", templateName);
            }
        }
    }
} 