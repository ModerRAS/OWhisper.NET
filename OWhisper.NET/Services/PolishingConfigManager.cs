using System;
using System.IO;
using Newtonsoft.Json;
using OWhisper.Core.Services;

namespace OWhisper.NET.Services
{
    /// <summary>
    /// 文本润色配置管理器
    /// </summary>
    public class PolishingConfigManager
    {
        private readonly string _configFilePath;

        public PolishingConfigManager()
        {
            var pathService = new PlatformPathService();
            var appDataPath = pathService.GetApplicationDataPath();
            _configFilePath = Path.Combine(appDataPath, "polishing_config.json");
        }

        /// <summary>
        /// 保存润色配置
        /// </summary>
        public void SaveConfig(PolishingConfig config)
        {
            try
            {
                var directory = Path.GetDirectoryName(_configFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                // 忽略保存错误，不影响主功能
                Console.WriteLine($"保存润色配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载润色配置
        /// </summary>
        public PolishingConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    return JsonConvert.DeserializeObject<PolishingConfig>(json) ?? new PolishingConfig();
                }
            }
            catch (Exception ex)
            {
                // 忽略加载错误，返回默认配置
                Console.WriteLine($"加载润色配置失败: {ex.Message}");
            }

            return new PolishingConfig();
        }
    }

    /// <summary>
    /// 文本润色配置
    /// </summary>
    public class PolishingConfig
    {
        /// <summary>
        /// 是否启用润色
        /// </summary>
        public bool EnablePolishing { get; set; } = false;

        /// <summary>
        /// API Key（加密存储）
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// 选择的模型
        /// </summary>
        public string SelectedModel { get; set; } = "deepseek-chat";

        /// <summary>
        /// 选择的模板名称
        /// </summary>
        public string SelectedTemplateName { get; set; } = "default";

        /// <summary>
        /// API基地址
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