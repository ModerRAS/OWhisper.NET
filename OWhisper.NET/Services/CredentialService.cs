using System;
using CredentialManagement;

namespace OWhisper.NET.Services
{
    /// <summary>
    /// 凭据管理服务，用于安全地存储和读取API Key
    /// </summary>
    public class CredentialService
    {
        private const string APP_NAME = "OWhisper.NET";

        /// <summary>
        /// 保存API Key到Windows凭据管理器
        /// </summary>
        /// <param name="providerName">提供商名称（如DeepSeek、OpenAI等）</param>
        /// <param name="apiKey">API Key</param>
        /// <returns>是否保存成功</returns>
        public static bool SaveApiKey(string providerName, string apiKey)
        {
            try
            {
                var credential = new Credential
                {
                    Target = $"{APP_NAME}_{providerName}_ApiKey",
                    Username = providerName,
                    Password = apiKey,
                    Type = CredentialType.Generic,
                    PersistanceType = PersistanceType.LocalComputer
                };

                return credential.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存API Key失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从Windows凭据管理器读取API Key
        /// </summary>
        /// <param name="providerName">提供商名称（如DeepSeek、OpenAI等）</param>
        /// <returns>API Key，如果未找到则返回null</returns>
        public static string GetApiKey(string providerName)
        {
            try
            {
                var credential = new Credential
                {
                    Target = $"{APP_NAME}_{providerName}_ApiKey"
                };

                if (credential.Load())
                {
                    return credential.Password;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取API Key失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 删除存储的API Key
        /// </summary>
        /// <param name="providerName">提供商名称</param>
        /// <returns>是否删除成功</returns>
        public static bool DeleteApiKey(string providerName)
        {
            try
            {
                var credential = new Credential
                {
                    Target = $"{APP_NAME}_{providerName}_ApiKey"
                };

                return credential.Delete();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"删除API Key失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查是否已存储了指定提供商的API Key
        /// </summary>
        /// <param name="providerName">提供商名称</param>
        /// <returns>是否存在API Key</returns>
        public static bool HasApiKey(string providerName)
        {
            return !string.IsNullOrEmpty(GetApiKey(providerName));
        }
    }
} 