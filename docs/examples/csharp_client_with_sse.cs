using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace OWhisper.Examples
{
    // API响应包装类
    public class ApiResponse<T>
    {
        public string Status { get; set; }
        public T Data { get; set; }
        public string Error { get; set; }
        public string ErrorCode { get; set; }
    }

    public class TaskCreationResponse
    {
        public string TaskId { get; set; }
        public int QueuePosition { get; set; }
    }

    public class TranscriptionResult
    {
        public string Text { get; set; }
        public string SrtContent { get; set; }
        public double ProcessingTime { get; set; }
    }

    public class TranscriptionProgress
    {
        public string TaskId { get; set; }
        public string Status { get; set; }
        public float Progress { get; set; }
        public int QueuePosition { get; set; }
        public string Message { get; set; }
        public TranscriptionResult Result { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class OWhisperApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public OWhisperApiClient(string baseUrl = "http://localhost:11899")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(60);
        }

        /// <summary>
        /// 提交音频文件进行转录
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <returns>任务创建响应</returns>
        public async Task<TaskCreationResponse> SubmitTranscriptionTaskAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"音频文件不存在: {filePath}");

            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var fileName = Path.GetFileName(filePath);

            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            
            // 根据文件扩展名设置Content-Type
            var extension = Path.GetExtension(fileName).ToLower();
            fileContent.Headers.ContentType = extension switch
            {
                ".mp3" => new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg"),
                ".wav" => new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav"),
                ".aac" => new System.Net.Http.Headers.MediaTypeHeaderValue("audio/aac"),
                ".m4a" => new System.Net.Http.Headers.MediaTypeHeaderValue("audio/aac"),
                _ => new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream")
            };

            form.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/transcribe", form);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"提交任务失败: {response.StatusCode}\n{responseContent}");
            }

            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<TaskCreationResponse>>(responseContent);
            if (apiResponse?.Status != "success")
            {
                var error = apiResponse?.Error ?? apiResponse?.ErrorCode ?? "未知错误";
                throw new Exception($"API返回错误: {error}");
            }

            return apiResponse.Data;
        }

        /// <summary>
        /// 获取任务详情
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>任务详情</returns>
        public async Task<TranscriptionProgress> GetTaskAsync(string taskId)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tasks/{taskId}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"获取任务失败: {response.StatusCode}\n{responseContent}");
            }

            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<dynamic>>(responseContent);
            if (apiResponse?.Status != "success")
            {
                var error = apiResponse?.Error ?? apiResponse?.ErrorCode ?? "未知错误";
                throw new Exception($"API返回错误: {error}");
            }

            var data = apiResponse.Data;
            return new TranscriptionProgress
            {
                TaskId = data.id?.ToString(),
                Status = data.status?.ToString(),
                Progress = data.progress ?? 0,
                QueuePosition = data.queuePosition ?? 0,
                Result = data.result != null ? JsonConvert.DeserializeObject<TranscriptionResult>(data.result.ToString()) : null,
                ErrorMessage = data.errorMessage?.ToString()
            };
        }

        /// <summary>
        /// 使用SSE监听任务进度
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="progressCallback">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>最终结果</returns>
        public async Task<TranscriptionResult> MonitorTaskProgressAsync(
            string taskId, 
            Action<TranscriptionProgress> progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            using var sseClient = new HttpClient();
            sseClient.Timeout = TimeSpan.FromMinutes(60);

            var response = await sseClient.GetAsync($"{_baseUrl}/api/tasks/{taskId}/progress", 
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"无法连接到SSE服务: {response.StatusCode}");
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                if (line.StartsWith("data: "))
                {
                    var jsonData = line.Substring(6);
                    try
                    {
                        var progress = JsonConvert.DeserializeObject<TranscriptionProgress>(jsonData);
                        progressCallback?.Invoke(progress);

                        // 如果任务完成，返回结果
                        if (progress.Status == "Completed" && progress.Result != null)
                        {
                            return progress.Result;
                        }
                        else if (progress.Status == "Failed")
                        {
                            throw new Exception($"任务处理失败: {progress.ErrorMessage}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        // 忽略心跳消息等非进度数据
                        Console.WriteLine($"跳过非进度消息: {ex.Message}");
                    }
                }
            }

            throw new OperationCanceledException("任务监听被取消");
        }

        /// <summary>
        /// 取消任务
        /// </summary>
        /// <param name="taskId">任务ID</param>
        public async Task<bool> CancelTaskAsync(string taskId)
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/tasks/{taskId}/cancel", null);
            return response.IsSuccessStatusCode;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: program <音频文件路径> [输出文件路径]");
                return;
            }

            var audioFilePath = args[0];
            var outputFilePath = args.Length > 1 ? args[1] : Path.ChangeExtension(audioFilePath, ".srt");

            using var client = new OWhisperApiClient();

            try
            {
                Console.WriteLine($"提交转录任务: {audioFilePath}");
                
                // 提交任务
                var taskResponse = await client.SubmitTranscriptionTaskAsync(audioFilePath);
                Console.WriteLine($"任务已提交，ID: {taskResponse.TaskId}，队列位置: {taskResponse.QueuePosition}");

                // 监听进度
                var result = await client.MonitorTaskProgressAsync(taskResponse.TaskId, progress =>
                {
                    switch (progress.Status)
                    {
                        case "Queued":
                            Console.WriteLine($"队列中等待，位置: {progress.QueuePosition}");
                            break;
                        case "Processing":
                            Console.WriteLine($"处理中... {progress.Progress:F1}%");
                            break;
                        case "Completed":
                            Console.WriteLine("转录完成!");
                            break;
                        case "Failed":
                            Console.WriteLine($"处理失败: {progress.ErrorMessage}");
                            break;
                    }
                });

                // 保存结果
                var content = Path.GetExtension(outputFilePath).ToLower() == ".srt" 
                    ? result.SrtContent 
                    : result.Text;

                await File.WriteAllTextAsync(outputFilePath, content, Encoding.UTF8);
                Console.WriteLine($"转录完成，耗时: {result.ProcessingTime:F1}秒");
                Console.WriteLine($"结果已保存到: {outputFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
        }
    }
} 