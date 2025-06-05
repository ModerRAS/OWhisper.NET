using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Chat;
using OWhisper.Core.Models;
using Serilog;
using System.Diagnostics;
using System.ClientModel;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// 文本润色服务实现
    /// </summary>
    public class TextPolishingService : ITextPolishingService
    {
        private readonly ITemplateManagerService _templateManager;
        private readonly List<string> _supportedModels = new()
        {
            "deepseek-chat",
            "deepseek-coder"
        };

        public TextPolishingService(ITemplateManagerService templateManager)
        {
            _templateManager = templateManager;
        }

        /// <summary>
        /// 对文本进行润色
        /// </summary>
        public async Task<TextPolishingResult> PolishTextAsync(TextPolishingRequest request, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // 参数验证
                if (request == null)
                {
                    return TextPolishingResult.Failure("", "请求参数不能为空");
                }

                if (string.IsNullOrWhiteSpace(request.OriginalText))
                {
                    return TextPolishingResult.Failure(request.OriginalText, "原始文本不能为空");
                }

                if (!request.EnablePolishing)
                {
                    // 不启用润色，直接返回原文
                    return TextPolishingResult.Success(request.OriginalText, request.OriginalText, request.Model, request.TemplateName, 0, stopwatch.ElapsedMilliseconds);
                }

                if (string.IsNullOrWhiteSpace(request.ApiKey))
                {
                    return TextPolishingResult.Failure(request.OriginalText, "API Key不能为空");
                }

                // 获取模板
                var template = await _templateManager.GetTemplateAsync(request.TemplateName);
                if (template == null)
                {
                    return TextPolishingResult.Failure(request.OriginalText, $"找不到模板: {request.TemplateName}");
                }

                Log.Information("开始润色文本，使用模板: {TemplateName}, 模型: {Model}", template.DisplayName, request.Model);

                // 准备模板参数
                var templateParams = new Dictionary<string, object>(request.CustomParameters)
                {
                    ["original_text"] = request.OriginalText
                };

                // 渲染系统提示词和用户消息
                var systemPrompt = await _templateManager.RenderTemplateAsync(template.SystemPromptTemplate, templateParams);
                var userMessage = await _templateManager.RenderTemplateAsync(template.UserMessageTemplate, templateParams);

                // 创建OpenAI客户端
                var client = new OpenAIClient(new ApiKeyCredential(request.ApiKey), new OpenAIClientOptions
                {
                    Endpoint = new Uri(request.ApiBaseUrl)
                });

                // 准备聊天消息
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userMessage)
                };

                Log.Debug("发送润色请求到 {BaseUrl}, 模型: {Model}, 最大Token: {MaxTokens}", 
                    request.ApiBaseUrl, request.Model, request.MaxTokens);

                var response = await client.GetChatClient(request.Model).CompleteChatAsync(messages, cancellationToken: cancellationToken);

                if (response?.Value?.Content?.Any() != true)
                {
                    return TextPolishingResult.Failure(request.OriginalText, "AI服务返回空响应");
                }

                var polishedText = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
                var tokensUsed = 0; // 简化token统计

                stopwatch.Stop();

                Log.Information("文本润色完成，耗时: {ElapsedMs}ms, 使用Token: {TokensUsed}", 
                    stopwatch.ElapsedMilliseconds, tokensUsed);

                return TextPolishingResult.Success(
                    request.OriginalText, 
                    polishedText.Trim(), 
                    request.Model, 
                    request.TemplateName, 
                    tokensUsed, 
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Log.Error(ex, "文本润色失败，耗时: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" 内部错误: {ex.InnerException.Message}";
                }

                return TextPolishingResult.Failure(request.OriginalText, errorMessage, request.Model, request.TemplateName);
            }
        }

        /// <summary>
        /// 获取所有可用的润色模板
        /// </summary>
        public async Task<List<PolishingTemplate>> GetAvailableTemplatesAsync()
        {
            return await _templateManager.LoadTemplatesAsync();
        }

        /// <summary>
        /// 根据名称获取模板
        /// </summary>
        public async Task<PolishingTemplate?> GetTemplateAsync(string templateName)
        {
            return await _templateManager.GetTemplateAsync(templateName);
        }

        /// <summary>
        /// 重新加载模板
        /// </summary>
        public async Task ReloadTemplatesAsync()
        {
            await _templateManager.RefreshCacheAsync();
        }

        /// <summary>
        /// 验证API连接
        /// </summary>
        public async Task<(bool isSuccess, string errorMessage)> ValidateApiConnectionAsync(string apiKey, string baseUrl, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return (false, "API Key不能为空");
                }

                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    return (false, "API基地址不能为空");
                }

                Log.Information("验证API连接: {BaseUrl}", baseUrl);

                var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
                {
                    Endpoint = new Uri(baseUrl)
                });

                // 发送一个简单的测试请求
                var testMessages = new List<ChatMessage>
                {
                    new UserChatMessage("测试连接")
                };

                var response = await client.GetChatClient("deepseek-chat").CompleteChatAsync(testMessages, cancellationToken: cancellationToken);

                if (response?.Value != null)
                {
                    Log.Information("API连接验证成功");
                    return (true, string.Empty);
                }
                else
                {
                    return (false, "API返回空响应");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "API连接验证失败");
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 获取支持的模型列表
        /// </summary>
        public List<string> GetSupportedModels()
        {
            return new List<string>(_supportedModels);
        }
    }
} 