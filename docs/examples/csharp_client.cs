using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OWhisper.Examples
{
    /// <summary>
    /// OWhisper.NET C#客户端示例
    /// 演示如何使用C#调用OWhisper.NET API进行音频转写
    /// </summary>
    public class OWhisperClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public OWhisperClient(string baseUrl = null)
        {
            // 支持环境变量配置
            if (baseUrl == null)
            {
                var host = Environment.GetEnvironmentVariable("OWHISPER_HOST") ?? "localhost";
                var port = Environment.GetEnvironmentVariable("OWHISPER_PORT") ?? "11899";
                baseUrl = $"http://{host}:{port}";
            }
            
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30)
            };
        }

        public async Task<ApiResponse<object>> GetStatusAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/status");
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ApiResponse<object>>(json);
        }

        public async Task<ApiResponse<TranscriptionResult>> TranscribeFileAsync(string filePath)
        {
            using var form = new MultipartFormDataContent();
            var fileBytes = File.ReadAllBytes(filePath);
            var fileContent = new ByteArrayContent(fileBytes);
            
            // 设置Content-Type
            var extension = Path.GetExtension(filePath).ToLower();
            switch (extension)
            {
                case ".mp3":
                    fileContent.Headers.ContentType = 
                        new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
                    break;
                case ".wav":
                    fileContent.Headers.ContentType = 
                        new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                    break;
                case ".aac":
                    fileContent.Headers.ContentType = 
                        new System.Net.Http.Headers.MediaTypeHeaderValue("audio/aac");
                    break;
            }

            form.Add(fileContent, "file", Path.GetFileName(filePath));

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/transcribe", form);
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ApiResponse<TranscriptionResult>>(json);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // 模型类
    public class ApiResponse<T>
    {
        public string Status { get; set; }
        public T Data { get; set; }
        public string Error { get; set; }
        public string ErrorCode { get; set; }
    }

    public class TranscriptionResult
    {
        public string Text { get; set; }
        public string SrtContent { get; set; }
        public double ProcessingTime { get; set; }
    }

    // 使用示例
    class Program
    {
        static async Task Main(string[] args)
        {
            using var client = new OWhisperClient();

            try
            {
                // 检查服务状态
                var status = await client.GetStatusAsync();
                Console.WriteLine($"服务状态: {status.Status}");

                // 转写音频文件
                var result = await client.TranscribeFileAsync("audio.mp3");
                
                if (result.Status == "success")
                {
                    // 保存结果
                    File.WriteAllText("output.txt", result.Data.Text, System.Text.Encoding.UTF8);
                    File.WriteAllText("output.srt", result.Data.SrtContent, System.Text.Encoding.UTF8);
                    
                    Console.WriteLine($"转写完成，耗时: {result.Data.ProcessingTime:F1}秒");
                }
                else
                {
                    Console.WriteLine($"转写失败: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
        }
    }
} 