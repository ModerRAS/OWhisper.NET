using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Chat;
using OWhisper.Core.Models;
using Scriban;
using Serilog;
using System.ClientModel;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// 滑窗文本润色服务实现
    /// </summary>
    public class SlidingWindowPolishingService : ISlidingWindowPolishingService
    {
        private readonly ILogger _logger = Log.ForContext<SlidingWindowPolishingService>();

        public async Task<TextPolishingHttpResponse> PolishSrtAsync(TextPolishingHttpRequest request, CancellationToken cancellationToken = default)
        {
            var statistics = new PolishingStatistics();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 验证输入
                if (string.IsNullOrWhiteSpace(request.SrtContent))
                {
                    return new TextPolishingHttpResponse
                    {
                        Success = false,
                        ErrorMessage = "SRT 内容不能为空"
                    };
                }

                // 解析模板
                var template = ParseTemplate(request.TemplateContent);
                if (template == null)
                {
                    return new TextPolishingHttpResponse
                    {
                        Success = false,
                        ErrorMessage = "模板解析失败"
                    };
                }

                var result = await ProcessText(request, template, statistics, cancellationToken);
                
                result.Statistics = statistics;
                result.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "文本润色过程中发生错误");
                return new TextPolishingHttpResponse
                {
                    Success = false,
                    ErrorMessage = $"润色失败: {ex.Message}",
                    Statistics = statistics,
                    ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
        }

        public async Task<ApiConnectionTestResponse> TestApiConnectionAsync(ApiConnectionTestRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var client = CreateApiClient(request.ApiKey, request.ApiBaseUrl);
                
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("测试连接"),
                    new UserChatMessage("你好")
                };

                var response = await client.GetChatClient(request.Model).CompleteChatAsync(messages, cancellationToken: cancellationToken);

                return new ApiConnectionTestResponse
                {
                    Success = true,
                    Message = "API 连接测试成功",
                    ResponsePreview = response?.Value?.Content?.FirstOrDefault()?.Text ?? "无响应内容"
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "API 连接测试失败");
                return new ApiConnectionTestResponse
                {
                    Success = false,
                    Message = $"API 连接测试失败: {ex.Message}"
                };
            }
        }

        public string[] GetSupportedProviders()
        {
            var providers = new[]
            {
                "Deepseek",
                "OpenAI",
                "Azure"
            };
            return providers;
        }

        public string[] GetSupportedModels(string provider)
        {
            return provider?.ToLowerInvariant() switch
            {
                "deepseek" => new[] { "deepseek-chat" },
                "openai" => new[] { "gpt-4o", "gpt-4o-mini", "gpt-3.5-turbo" },
                "azure" => new[] { "gpt-4", "gpt-35-turbo" },
                _ => Array.Empty<string>()
            };
        }

        private async Task<TextPolishingHttpResponse> ProcessText(TextPolishingHttpRequest request, PolishingTemplate template, PolishingStatistics statistics, CancellationToken cancellationToken)
        {
            // 简化实现 - 将整个SRT作为一个文本块处理
            var client = CreateApiClient(request.ApiKey, request.ApiBaseUrl);
            
            var systemPrompt = template.SystemPromptTemplate?.Replace("{{original_text}}", request.SrtContent) ?? "请润色以下文本";
            var userMessage = template.UserMessageTemplate?.Replace("{{original_text}}", request.SrtContent) ?? request.SrtContent;

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userMessage)
            };

            var response = await client.GetChatClient(request.Model).CompleteChatAsync(messages, cancellationToken: cancellationToken);

            if (response?.Value?.Content?.Any() == true)
            {
                var polishedText = response.Value.Content.FirstOrDefault()?.Text ?? "";
                
                statistics.TotalWindows = 1;
                statistics.ProcessedWindows = 1;
                statistics.TotalTokensUsed = 0; // 简化token计数

                return new TextPolishingHttpResponse
                {
                    Success = true,
                    PolishedSrtContent = polishedText,
                    OriginalText = request.SrtContent,
                    PolishedText = polishedText
                };
            }

            return new TextPolishingHttpResponse
            {
                Success = false,
                ErrorMessage = "未能从 API 获取有效响应"
            };
        }

        private PolishingTemplate ParseTemplate(string templateContent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(templateContent))
                {
                    return CreateDefaultTemplate();
                }

                var template = JsonConvert.DeserializeObject<PolishingTemplate>(templateContent);
                return template ?? CreateDefaultTemplate();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "模板解析失败，使用默认模板");
                return CreateDefaultTemplate();
            }
        }

        private PolishingTemplate CreateDefaultTemplate()
        {
            return new PolishingTemplate
            {
                Name = "默认模板",
                Category = "通用",
                SystemPromptTemplate = "你是一个专业的文本润色专家。请对以下文本进行润色，使其更加通顺、准确和专业。保持原文的意思不变，只改进表达方式。",
                UserMessageTemplate = "请润色以下文本：\n\n{{original_text}}",
                Parameters = new List<TemplateParameter>()
            };
        }

        private OpenAIClient CreateApiClient(string apiKey, string baseUrl)
        {
            var options = new OpenAIClientOptions();
            
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                options.Endpoint = new Uri(baseUrl);
            }

            return new OpenAIClient(new ApiKeyCredential(apiKey), options);
        }

        // 保留原有的详细提供商信息方法（用于内部使用）
        private IReadOnlyList<ApiProvider> GetDetailedProviders()
        {
            return new List<ApiProvider>
            {
                new ApiProvider { Name = "Deepseek", BaseUrl = "https://api.deepseek.com", SupportedModels = new[] { "deepseek-chat" } },
                new ApiProvider { Name = "OpenAI", BaseUrl = "https://api.openai.com", SupportedModels = new[] { "gpt-4o", "gpt-4o-mini", "gpt-3.5-turbo" } },
                new ApiProvider { Name = "Azure", BaseUrl = "https://your-resource.openai.azure.com", SupportedModels = new[] { "gpt-4", "gpt-35-turbo" } }
            };
        }
    }

    public class ApiProvider
    {
        public string Name { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string[] SupportedModels { get; set; } = Array.Empty<string>();
    }
} 