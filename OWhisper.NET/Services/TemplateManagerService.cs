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
    /// 模板管理服务（客户端）
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
                var templateFiles = Directory.GetFiles(_templatesDirectory, "*.json", SearchOption.TopDirectoryOnly);

                foreach (var templateFile in templateFiles)
                {
                    try
                    {
                        var template = await LoadTemplateFromFileAsync(templateFile);
                        if (template != null)
                        {
                            templates.Add(template);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "加载模板文件失败: {TemplateFile}", templateFile);
                    }
                }

                Log.Information("成功加载 {Count} 个模板", templates.Count);
                return templates.OrderBy(t => t.Category).ThenBy(t => t.Name).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "获取所有模板失败");
                return new List<PolishingTemplate>();
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

                // 从文件系统查找
                var templateFile = Path.Combine(_templatesDirectory, $"{templateName}.json");
                if (File.Exists(templateFile))
                {
                    var template = await LoadTemplateFromFileAsync(templateFile);
                    if (template != null)
                    {
                        _templateCache[templateName] = template;
                        return template;
                    }
                }

                Log.Warning("未找到模板: {TemplateName}", templateName);
                return null;
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
                if (template == null || string.IsNullOrWhiteSpace(template.Name))
                {
                    Log.Warning("模板对象或名称为空，无法保存");
                    return false;
                }

                var templateFile = Path.Combine(_templatesDirectory, $"{template.Name}.json");
                var jsonContent = JsonConvert.SerializeObject(template, Formatting.Indented);

                File.WriteAllText(templateFile, jsonContent);

                // 更新缓存
                _templateCache[template.Name] = template;

                Log.Information("成功保存模板: {TemplateName}", template.Name);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存模板失败: {TemplateName}", template?.Name);
                return false;
            }
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

                var templateFile = Path.Combine(_templatesDirectory, $"{templateName}.json");
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
        /// 将模板序列化为JSON字符串
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
                var defaultTemplates = GetDefaultTemplates();

                foreach (var template in defaultTemplates)
                {
                    var templateFile = Path.Combine(_templatesDirectory, $"{template.Name}.json");
                    if (!File.Exists(templateFile))
                    {
                        var jsonContent = JsonConvert.SerializeObject(template, Formatting.Indented);
                        File.WriteAllText(templateFile, jsonContent);
                        Log.Information("创建默认模板: {TemplateName}", template.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "创建默认模板失败");
            }
        }

        /// <summary>
        /// 从文件加载模板
        /// </summary>
        /// <param name="templateFile">模板文件路径</param>
        /// <returns>模板对象</returns>
        private async Task<PolishingTemplate> LoadTemplateFromFileAsync(string templateFile)
        {
            try
            {
                var jsonContent = File.ReadAllText(templateFile);
                var template = JsonConvert.DeserializeObject<PolishingTemplate>(jsonContent);
                
                if (template != null && string.IsNullOrWhiteSpace(template.Name))
                {
                    // 如果模板没有名称，使用文件名作为名称
                    template.Name = Path.GetFileNameWithoutExtension(templateFile);
                }

                return template;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "从文件加载模板失败: {TemplateFile}", templateFile);
                return null;
            }
        }

        /// <summary>
        /// 获取默认模板列表
        /// </summary>
        /// <returns>默认模板列表</returns>
        private List<PolishingTemplate> GetDefaultTemplates()
        {
            return new List<PolishingTemplate>
            {
                new PolishingTemplate
                {
                    Name = "通用润色",
                    Category = "通用",
                    Description = "适用于大部分场景的通用文本润色模板",
                    SystemPromptTemplate = "你是一个专业的文本润色助手。请对用户提供的文本进行润色，使其更加流畅、准确、易读。保持原意不变，但可以适当调整表达方式。",
                    UserMessageTemplate = "请润色以下文本：\n\n{{original_text}}",
                    Parameters = new List<TemplateParameter>
                    {
                        new TemplateParameter
                        {
                            Name = "original_text",
                            Type = "string",
                            Description = "需要润色的原始文本"
                        }
                    }
                },
                new PolishingTemplate
                {
                    Name = "学术专业",
                    Category = "学术",
                    Description = "适用于学术论文、研究报告等专业文档的润色模板",
                    SystemPromptTemplate = "你是一个专业的学术文本编辑助手。请对用户提供的学术文本进行润色，确保：1. 用词准确、专业 2. 逻辑清晰、结构严谨 3. 符合学术写作规范 4. 保持学术性和客观性。",
                    UserMessageTemplate = "请对以下学术文本进行专业润色：\n\n{{original_text}}",
                    Parameters = new List<TemplateParameter>
                    {
                        new TemplateParameter
                        {
                            Name = "original_text",
                            Type = "string",
                            Description = "需要润色的学术文本"
                        }
                    }
                },
                new PolishingTemplate
                {
                    Name = "商务沟通",
                    Category = "商务",
                    Description = "适用于商务邮件、报告、提案等商务沟通场景的润色模板",
                    SystemPromptTemplate = "你是一个专业的商务沟通顾问。请对用户提供的商务文本进行润色，确保：1. 语言正式、礼貌 2. 表达清晰、简洁 3. 突出重点、易于理解 4. 符合商务沟通礼仪。",
                    UserMessageTemplate = "请对以下商务文本进行润色：\n\n{{original_text}}",
                    Parameters = new List<TemplateParameter>
                    {
                        new TemplateParameter
                        {
                            Name = "original_text",
                            Type = "string",
                            Description = "需要润色的商务文本"
                        }
                    }
                }
            };
        }

        #endregion
    }
} 