using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OWhisper.Core.Services;
using Whisper.net;
using Whisper.net.Ggml;
using Serilog;
using System.Security.Cryptography;

namespace OWhisper.Core.Services
{
    public class WhisperManager : IDisposable
    {
        private WhisperProcessor _processor;
        private const string ModelName = "ggml-large-v3-turbo.bin";
        private const string ModelSha256 = "1fc70f774d38eb169993ac391eea357ef47c88757ef72ee5943879b7e8e2bc69";
        const GgmlType ggmlType = GgmlType.LargeV3Turbo;
        private readonly string _modelDir;
        private readonly string _modelPath;
        private readonly object _lock = new object();
        private readonly IPlatformPathService _pathService;

        public WhisperManager(IPlatformPathService? pathService = null)
        {
            _pathService = pathService ?? new PlatformPathService();
            _modelDir = _pathService.GetModelsPath();
            _modelPath = Path.Combine(_modelDir, ModelName);
        }

        /// <summary>
        /// 计算文件的SHA256哈希值
        /// </summary>
        private static string CalculateFileSha256(string filePath)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "计算文件SHA256失败: {FilePath}", filePath);
                return string.Empty;
            }
        }

        /// <summary>
        /// 验证模型文件的SHA256哈希值
        /// </summary>
        private bool VerifyModelSha256(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            var actualSha256 = CalculateFileSha256(filePath);
            var isValid = string.Equals(actualSha256, ModelSha256, StringComparison.OrdinalIgnoreCase);
            
            if (!isValid)
            {
                Log.Warning("模型文件SHA256校验失败. 预期: {Expected}, 实际: {Actual}", ModelSha256, actualSha256);
            }
            
            return isValid;
        }

        public (bool exists, bool valid, long size, string path) CheckModelStatus()
        {
            try
            {
                var exists = File.Exists(_modelPath);
                var valid = false;
                long size = 0;

                if (exists)
                {
                    var fileInfo = new FileInfo(_modelPath);
                    size = fileInfo.Length;
                    
                    // 验证文件大小和SHA256
                    var sizeValid = size > 10 * 1024 * 1024; // 至少10MB
                    var sha256Valid = VerifyModelSha256(_modelPath);
                    
                    valid = sizeValid && sha256Valid;
                    
                    Log.Information("模型文件校验 - 大小: {Size}字节, 大小有效: {SizeValid}, SHA256有效: {Sha256Valid}", 
                        size, sizeValid, sha256Valid);
                }

                return (exists, valid, size, _modelPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "检查模型状态失败");
                return (false, false, 0, _modelPath);
            }
        }

        public async Task DownloadModelAsync(GgmlType modelType, string targetModelsDir)
        {
            if (!Directory.Exists(targetModelsDir))
            {
                try
                {
                    Directory.CreateDirectory(targetModelsDir);
                    Log.Information("创建模型目录: {ModelDir}", targetModelsDir);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "创建模型目录失败");
                    throw new AudioProcessingException("MODEL_DIR_CREATE_FAILED", $"无法创建模型目录: {ex.Message}");
                }
            }

            Log.Information("开始下载模型: {ModelName}...", ModelName);
            var downloadStartTime = DateTime.UtcNow;

            try
            {
                // 创建下载器实例并下载模型
                using var httpClient = HttpClientHelper.CreateProxyHttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(10);
                var downloader = new WhisperGgmlDownloader(httpClient);
                using var modelStream = await downloader.GetGgmlModelAsync(modelType);

                var tempFilePath = Path.Combine(targetModelsDir, $"{ModelName}.tmp");

                try
                {
                    using (var fileWriter = File.OpenWrite(tempFilePath))
                    {
                        var buffer = new byte[8192];
                        long totalBytes = 0;
                        int bytesRead;

                        while ((bytesRead = await modelStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileWriter.WriteAsync(buffer, 0, bytesRead);
                            totalBytes += bytesRead;

                            if (DateTime.UtcNow - downloadStartTime > TimeSpan.FromSeconds(5))
                            {
                                Log.Information("已下载: {DownloadedMB}MB", totalBytes / (1024 * 1024));
                                downloadStartTime = DateTime.UtcNow;
                            }
                        }
                    }

                    var finalPath = Path.Combine(targetModelsDir, ModelName);
                    if (File.Exists(finalPath))
                    {
                        File.Delete(finalPath);
                    }
                    File.Move(tempFilePath, finalPath);
                    
                    // 验证下载文件的SHA256
                    Log.Information("正在验证下载文件的SHA256...");
                    if (!VerifyModelSha256(finalPath))
                    {
                        Log.Error("下载的模型文件SHA256校验失败，删除损坏的文件");
                        if (File.Exists(finalPath))
                        {
                            File.Delete(finalPath);
                        }
                        throw new AudioProcessingException("MODEL_VERIFICATION_FAILED", "下载的模型文件SHA256校验失败，可能文件已损坏");
                    }
                    
                    Log.Information("模型下载完成并校验通过，保存到: {ModelPath}", finalPath);
                }
                catch (IOException ex)
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                    Log.Error(ex, "文件写入失败");
                    throw new AudioProcessingException("MODEL_WRITE_FAILED", $"模型文件写入失败: {ex.Message}");
                }
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                Log.Error(ex, "网络请求失败");
                throw new AudioProcessingException("NETWORK_ERROR", $"下载模型失败: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                Log.Error(ex, "下载超时");
                throw new AudioProcessingException("DOWNLOAD_TIMEOUT", "模型下载超时");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "下载过程中发生未知错误");
                throw new AudioProcessingException("DOWNLOAD_FAILED", $"模型下载失败: {ex.Message}");
            }
        }

        public async Task<(string srtContent, string plainText)> Transcribe(byte[] audioData)
        {
            return await Transcribe(audioData, null, 0);
        }

        public async Task<(string srtContent, string plainText)> Transcribe(byte[] audioData, Action<float> progressCallback, double totalMs)
        {
            TimeSpan timeTaken;
            var startTime = DateTime.UtcNow;

            // 确保目录存在
            _pathService.EnsureDirectoriesExist();

            Log.Information("检查模型文件: {ModelPath}", _modelPath);
            var modelStatus = CheckModelStatus();
            Log.Information("模型状态 - 存在: {Exists}, 有效: {Valid}, 大小: {Size}字节",
                modelStatus.exists, modelStatus.valid, modelStatus.size);

            if (!modelStatus.exists || !modelStatus.valid)
            {
                try
                {
                    Log.Information("开始下载模型文件...");
                    await DownloadModelAsync(ggmlType, _modelDir);
                    timeTaken = DateTime.UtcNow - startTime;
                    Log.Information("下载耗时: {Seconds}秒", timeTaken.TotalSeconds);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "模型下载失败");
                    throw new Exception("模型下载失败", ex);
                }
            }

            using var whisperFactory = WhisperFactory.FromPath(_modelPath);

            // This section creates the processor object which is used to process the audio file, it uses language `auto` to detect the language of the audio file.
            await using var processor = whisperFactory.CreateBuilder()
                                                      .WithThreads(16)
                                                      //.WithLanguage("zh")
                                                      .WithLanguageDetection()
                                                      //.WithPrompt(prompt)
                                                      .Build();

            timeTaken = DateTime.UtcNow - startTime;
            Log.Information("Whisper初始化耗时: {TimeTaken}", timeTaken);

            using var wavStream = new MemoryStream(audioData);
            wavStream.Seek(0, SeekOrigin.Begin);

            Log.Information("⟫ Starting Whisper processing...");

            startTime = DateTime.UtcNow;

            var srtSegments = new List<string>();
            var plainTextSegments = new List<string>();

            // This section processes the audio file and prints the results (start time, end time and text) to the console.
            var startId = 1;
            string lastText = string.Empty;
            var repeatCount = 0;
            var cts = new CancellationTokenSource();
            var token = cts.Token;
            try
            {
                await foreach (var result in processor.ProcessAsync(wavStream, token))
                {
                    timeTaken = DateTime.UtcNow - startTime;
                    Log.Information("{Start}-->{End}: {Text} [{TimeTaken}]",
                        result.Start, result.End, result.Text, timeTaken);

                    // 构建SRT格式
                    srtSegments.Add($"{startId}");
                    srtSegments.Add($"{result.Start.ToString(@"hh\:mm\:ss\,fff")} --> {result.End.ToString(@"hh\:mm\:ss\,fff")}");
                    srtSegments.Add($"{result.Text}");
                    srtSegments.Add(""); // SRT需要空行分隔

                    // 收集纯文本
                    plainTextSegments.Add(result.Text.Trim());

                    startId++;

                    // 计算并报告进度
                    if (progressCallback != null && totalMs > 0)
                    {
                        float progress = (float)(result.End.TotalMilliseconds / totalMs) * 100;
                        progressCallback(progress);
                    }

                    startTime = DateTime.UtcNow;
                    if (lastText.Equals(result.Text.Trim()))
                    {
                        repeatCount++;
                    }
                    else
                    {
                        repeatCount = 0;
                        lastText = result.Text.Trim();
                    }
                    if (repeatCount > 100)
                    {
                        cts.Cancel();
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                Log.Error(ex, "处理过程中发生错误");
            }

            Log.Information("⟫ Completed Whisper processing...");

            var srtContent = string.Join("\n", srtSegments);
            var plainText = string.Join(" ", plainTextSegments);

            return (srtContent, plainText);
        }

        public void Dispose()
        {
            _processor?.Dispose();
            _processor = null;
        }
    }
} 
