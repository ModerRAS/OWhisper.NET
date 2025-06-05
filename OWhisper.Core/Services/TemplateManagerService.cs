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
    /// 模板管理服务实现
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

            lock (_loadLock)
            {
                // 双重检查
                if (_templateCache.Any() && DateTime.Now - _lastLoadTime < _cacheExpiry)
                {
                    return _templateCache.Values.Where(t => t.IsEnabled).OrderBy(t => t.Category).ThenBy(t => t.DisplayName).ToList();
                }

                try
                {
                    _templateCache.Clear();
                    var templatesDir = GetTemplatesDirectory();
                    var templateFiles = Directory.GetFiles(templatesDir, "*.json", SearchOption.AllDirectories);

                    Log.Information("正在加载润色模板，找到 {Count} 个模板文件", templateFiles.Length);

                    foreach (var templateFile in templateFiles)
                    {
                        try
                        {
                            var jsonContent = File.ReadAllText(templateFile);
                            var template = JsonConvert.DeserializeObject<PolishingTemplate>(jsonContent);
                            
                            if (template != null && !string.IsNullOrEmpty(template.Name))
                            {
                                _templateCache.TryAdd(template.Name, template);
                                Log.Debug("已加载模板: {TemplateName} ({Category})", template.DisplayName, template.Category);
                            }
                            else
                            {
                                Log.Warning("模板文件格式无效: {TemplateFile}", templateFile);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "加载模板文件失败: {TemplateFile}", templateFile);
                        }
                    }

                    _lastLoadTime = DateTime.Now;
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
            
            // 默认通用模板
            var defaultTemplate = new PolishingTemplate
            {
                Name = "default",
                DisplayName = "通用润色",
                Description = "适用于大多数场景的通用文本润色模板",
                Category = "通用",
                SystemPromptTemplate = @"你是一个专业的文本润色助手。请对用户提供的文本进行润色和优化，要求：

1. 保持原文的核心意思和信息不变
2. 改善语法、用词和表达方式
3. 提高文本的可读性和流畅性  
4. 纠正错别字和标点符号错误
5. 适当调整句式结构，使表达更加清晰

请直接返回润色后的文本，不要添加额外的解释或说明。",
                UserMessageTemplate = "请对以下文本进行润色：\n\n{{ original_text }}",
                Parameters = new List<TemplateParameter>
                {
                    new TemplateParameter
                    {
                        Name = "original_text",
                        DisplayName = "原始文本",
                        Description = "需要润色的原始文本",
                        Type = "string",
                        IsRequired = true
                    }
                }
            };

            await SaveTemplateAsync(templatesDir, defaultTemplate);

            // 专业/学术模板
            var academicTemplate = new PolishingTemplate
            {
                Name = "academic",
                DisplayName = "学术/专业润色",
                Description = "适用于学术论文、专业报告等正式文档的润色模板",
                Category = "学术",
                SystemPromptTemplate = @"你是一个专业的学术文本润色专家。请对用户提供的文本进行学术化润色，要求：

1. 使用更加正式和准确的学术用词
2. 完善逻辑结构和论证链条
3. 提升文本的专业性和严谨性
4. 确保术语使用的准确性和一致性
5. 优化段落结构和语句衔接
6. 保持客观中性的学术语调

{{ if style }}
写作风格要求：{{ style }}
{{ end }}

请直接返回润色后的文本，保持学术写作的规范性。",
                UserMessageTemplate = "请对以下{{ domain || '学术' }}文本进行专业润色：\n\n{{ original_text }}",
                Parameters = new List<TemplateParameter>
                {
                    new TemplateParameter
                    {
                        Name = "original_text",
                        DisplayName = "原始文本",
                        Description = "需要润色的原始文本",
                        Type = "string",
                        IsRequired = true
                    },
                    new TemplateParameter
                    {
                        Name = "domain",
                        DisplayName = "专业领域",
                        Description = "文本所属的专业领域（如：计算机科学、医学、法学等）",
                        Type = "string",
                        DefaultValue = "学术"
                    },
                    new TemplateParameter
                    {
                        Name = "style",
                        DisplayName = "写作风格",
                        Description = "特定的写作风格要求",
                        Type = "enum",
                        Options = new List<string> { "简洁明快", "详细严谨", "论证充分", "数据驱动" }
                    }
                }
            };

            await SaveTemplateAsync(templatesDir, academicTemplate);

            // 商务模板
            var businessTemplate = new PolishingTemplate
            {
                Name = "business",
                DisplayName = "商务沟通润色",
                Description = "适用于商务邮件、报告、提案等商业文档的润色模板",
                Category = "商务",
                SystemPromptTemplate = @"你是一个商务沟通专家。请对用户提供的商务文本进行润色，要求：

1. 使用专业且友好的商务语言
2. 确保信息传达清晰明确
3. 优化说服力和影响力
4. 保持恰当的商务礼仪和语调
5. 突出重点信息和行动要求
6. 确保文本简洁有力，易于理解

{{ if tone }}
语调要求：{{ tone }}
{{ end }}

{{ if audience }}
目标受众：{{ audience }}
{{ end }}

请直接返回润色后的文本，确保符合商务沟通标准。",
                UserMessageTemplate = "请对以下商务文本进行润色：\n\n{{ original_text }}",
                Parameters = new List<TemplateParameter>
                {
                    new TemplateParameter
                    {
                        Name = "original_text",
                        DisplayName = "原始文本",
                        Description = "需要润色的原始文本",
                        Type = "string",
                        IsRequired = true
                    },
                    new TemplateParameter
                    {
                        Name = "tone",
                        DisplayName = "语调风格",
                        Description = "期望的语调风格",
                        Type = "enum",
                        Options = new List<string> { "正式严肃", "友好亲和", "说服有力", "简洁直接" }
                    },
                    new TemplateParameter
                    {
                        Name = "audience",
                        DisplayName = "目标受众",
                        Description = "文档的目标受众",
                        Type = "string",
                        DefaultValue = "商业伙伴"
                    }
                }
            };

            await SaveTemplateAsync(templatesDir, businessTemplate);

            Log.Information("默认润色模板创建完成");
        }

        /// <summary>
        /// 保存模板到文件
        /// </summary>
        private async Task SaveTemplateAsync(string templatesDir, PolishingTemplate template)
        {
            try
            {
                var categoryDir = Path.Combine(templatesDir, template.Category);
                if (!Directory.Exists(categoryDir))
                {
                    Directory.CreateDirectory(categoryDir);
                }

                var templateFile = Path.Combine(categoryDir, $"{template.Name}.json");
                if (!File.Exists(templateFile))
                {
                    var jsonContent = JsonConvert.SerializeObject(template, Formatting.Indented);
                    File.WriteAllText(templateFile, jsonContent);
                    Log.Information("已创建默认模板: {TemplateFile}", templateFile);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存模板失败: {TemplateName}", template.Name);
            }
        }
    }
} 