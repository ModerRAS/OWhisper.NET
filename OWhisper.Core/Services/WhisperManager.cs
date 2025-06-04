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
using Downloader;
using Newtonsoft.Json;

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
                // 使用 FileShare.Read 允许其他进程同时读取文件
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
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
                // 对于 large-v3-turbo 模型，使用 R2 直接下载
                if (modelType == GgmlType.LargeV3Turbo)
                {
                    await DownloadFromR2Async(targetModelsDir, downloadStartTime);
                }
                else
                {
                    // 其他模型使用原来的下载方式
                    await DownloadFromWhisperGgmlAsync(modelType, targetModelsDir, downloadStartTime);
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

        /// <summary>
        /// 从 R2 存储下载 large-v3-turbo 模型（使用 Downloader 库实现多线程下载和断点续传）
        /// </summary>
        private async Task DownloadFromR2Async(string targetModelsDir, DateTime downloadStartTime)
        {
            const string R2_BASE_URL = "https://velopack.miaostay.com";
            var modelUrl = $"{R2_BASE_URL}/models/{ModelName}";
            var tempFilePath = Path.Combine(targetModelsDir, $"{ModelName}.tmp");
            var finalPath = Path.Combine(targetModelsDir, ModelName);
            var packageFilePath = Path.Combine(targetModelsDir, $"{ModelName}.download"); // 断点续传包文件

            Log.Information("从 R2 存储下载模型 (使用 Downloader 库): {ModelUrl}", modelUrl);

            try
            {
                // 配置下载选项 - 适配 Downloader 3.1.2
                var downloadOpt = new DownloadConfiguration()
                {
                    ChunkCount = 8, // 使用8个分片并行下载
                    ParallelDownload = true, // 启用并行下载
                    ParallelCount = 4, // 并发下载数
                    MaximumBytesPerSecond = 0, // 不限制下载速度
                    BufferBlockSize = 8192, // 8KB 缓冲区
                    MaxTryAgainOnFailover = 5, // 最大重试次数
                    MinimumSizeOfChunking = 1024 * 1024, // 最小分片大小 1MB
                    ReserveStorageSpaceBeforeStartingDownload = true, // 预分配存储空间
                    ClearPackageOnCompletionWithFailure = true, // 失败时清理包文件
                    Timeout = (int)TimeSpan.FromMinutes(15).TotalMilliseconds, // 15分钟超时，使用毫秒
                    RequestConfiguration = new RequestConfiguration()
                    {
                        UserAgent = "OWhisper.NET/1.0 (Downloader)",
                        Accept = "*/*",
                        KeepAlive = true,
                        ProtocolVersion = new Version(1, 1),
                        Timeout = (int)TimeSpan.FromMinutes(2).TotalMilliseconds // 单个请求2分钟超时，转换为毫秒
                    }
                };

                // 如果存在代理设置，使用代理
                var proxyHandler = HttpClientHelper.CreateProxyHandler();
                if (proxyHandler.Proxy != null)
                {
                    downloadOpt.RequestConfiguration.Proxy = proxyHandler.Proxy;
                    Log.Information("使用代理: {ProxyAddress}", proxyHandler.Proxy.GetProxy(new Uri(modelUrl)));
                }

                // 检查是否存在未完成的下载包（断点续传）
                DownloadPackage? package = null;
                if (File.Exists(packageFilePath))
                {
                    try
                    {
#if NET8_0_OR_GREATER
                        var packageJson = await File.ReadAllTextAsync(packageFilePath);
#else
                        var packageJson = File.ReadAllText(packageFilePath);
#endif
                        package = Newtonsoft.Json.JsonConvert.DeserializeObject<DownloadPackage>(packageJson);
                        if (package != null && package.Urls?.FirstOrDefault() == modelUrl)
                        {
                            Log.Information("发现未完成的下载，将继续断点续传...");
                        }
                        else
                        {
                            package = null; // URL不匹配，不使用断点续传
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "读取下载包文件失败，将重新开始下载");
                        package = null;
                    }
                }

                // 执行下载 - 缩小 using 作用域到只有下载阶段
                DownloadPackage? resultPackage = null;
                var lastProgressTime = downloadStartTime;
                var lastDownloadedBytes = 0L;

                using (var downloader = new DownloadService(downloadOpt))
                {
                    // 设置进度事件处理
                    downloader.DownloadProgressChanged += (sender, e) =>
                    {
                        var now = DateTime.UtcNow;
                        
                        // 每2秒报告一次进度
                        if (now - lastProgressTime > TimeSpan.FromSeconds(2))
                        {
                            var currentSpeed = (e.ReceivedBytesSize - lastDownloadedBytes) / (now - lastProgressTime).TotalSeconds / (1024 * 1024);
                            var avgSpeed = e.AverageBytesPerSecondSpeed / (1024 * 1024);
                            var eta = e.AverageBytesPerSecondSpeed > 0 ? 
                                TimeSpan.FromSeconds((e.TotalBytesToReceive - e.ReceivedBytesSize) / e.AverageBytesPerSecondSpeed) : 
                                TimeSpan.Zero;

                            Log.Information("下载进度: {DownloadedMB}MB / {TotalMB}MB ({Progress:F1}%) - 当前速度: {CurrentSpeed:F1}MB/s - 平均速度: {AvgSpeed:F1}MB/s - 活跃分片: {ActiveChunks} - 预计剩余: {ETA}", 
                                e.ReceivedBytesSize / (1024 * 1024),
                                e.TotalBytesToReceive / (1024 * 1024),
                                e.ProgressPercentage,
                                currentSpeed,
                                avgSpeed,
                                e.ActiveChunks,
                                eta.ToString(@"mm\:ss"));

                            lastProgressTime = now;
                            lastDownloadedBytes = e.ReceivedBytesSize;
                        }
                    };

                    downloader.DownloadStarted += (sender, e) =>
                    {
                        Log.Information("开始下载: 文件大小 {TotalMB}MB, 支持分片: {SupportsRange}, 分片数: {ChunkCount}",
                            e.TotalBytesToReceive / (1024 * 1024),
                            e.TotalBytesToReceive > 0,
                            downloadOpt.ChunkCount);
                    };

                    downloader.DownloadFileCompleted += (sender, e) =>
                    {
                        if (e.Error != null)
                        {
                            Log.Error(e.Error, "下载完成但有错误");
                        }
                        else
                        {
                            // 在 Downloader 3.1.2 中，AsyncCompletedEventArgs 没有 FileName 属性
                            Log.Information("下载成功完成");
                        }
                    };

                    downloader.ChunkDownloadProgressChanged += (sender, e) =>
                    {
                        // 可选：记录单个分片的详细进度（仅在调试时启用）
                        if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                        {
                            // 在 Downloader 3.1.2 中，DownloadProgressChangedEventArgs 没有 Id 属性，使用其他标识符
                            Log.Debug("分片进度: {Progress:F1}% ({ReceivedMB}MB / {TotalMB}MB) - 速度: {Speed:F1}KB/s",
                                e.ProgressPercentage, 
                                e.ReceivedBytesSize / (1024 * 1024), 
                                e.TotalBytesToReceive / (1024 * 1024),
                                e.AverageBytesPerSecondSpeed / 1024);
                        }
                    };

                    // 开始下载（支持断点续传）
                    // 注意：在 Downloader 3.1.2 中，DownloadFileTaskAsync 返回 void 而不是 DownloadPackage
                    if (package != null)
                    {
                        Log.Information("继续断点续传下载...");
                        await downloader.DownloadFileTaskAsync(package);
                        // 由于返回类型是 void，我们需要重新获取 package 信息
                        resultPackage = downloader.Package;
                    }
                    else
                    {
                        Log.Information("开始新的多线程下载...");
                        await downloader.DownloadFileTaskAsync(modelUrl, tempFilePath);
                        // 获取下载后的 package 信息
                        resultPackage = downloader.Package;
                    }
                } // DownloadService 在这里被释放

                // 保存下载包信息（用于断点续传）
                if (resultPackage != null)
                {
                    try
                    {
                        var packageJson = Newtonsoft.Json.JsonConvert.SerializeObject(resultPackage, Newtonsoft.Json.Formatting.Indented);
#if NET8_0_OR_GREATER
                        await File.WriteAllTextAsync(packageFilePath, packageJson);
#else
                        File.WriteAllText(packageFilePath, packageJson);
#endif
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "保存下载包信息失败，将无法使用断点续传");
                    }
                }

                var elapsed = DateTime.UtcNow - downloadStartTime;
                var fileInfo = new FileInfo(tempFilePath);
                var avgSpeed = fileInfo.Length / elapsed.TotalSeconds / (1024 * 1024);

                Log.Information("下载完成: 大小 {TotalMB}MB, 耗时 {Elapsed}, 平均速度: {Speed:F1}MB/s",
                    fileInfo.Length / (1024 * 1024), elapsed.ToString(@"mm\:ss"), avgSpeed);

                // 下载成功，删除包文件
                if (File.Exists(packageFilePath))
                {
                    File.Delete(packageFilePath);
                }

                await CompleteDownload(tempFilePath, finalPath, fileInfo.Length, downloadStartTime);
            }
            catch (Exception ex) when (ex is not AudioProcessingException)
            {
                // 清理临时文件，但保留包文件用于断点续传
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
                
                Log.Error(ex, "使用 Downloader 库下载失败，尝试单线程下载");
                
                // 降级到单线程下载
                await DownloadSingleThreadedFallbackAsync(modelUrl, tempFilePath, finalPath, downloadStartTime);
            }
        }

        /// <summary>
        /// 单线程下载（备用方案）
        /// </summary>
        private async Task DownloadSingleThreadedFallbackAsync(string modelUrl, string tempFilePath, string finalPath, DateTime downloadStartTime)
        {
            Log.Information("使用单线程下载作为备用方案");
            
            using var httpClient = HttpClientHelper.CreateProxyHttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(10);

            using var response = await httpClient.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            Log.Information("文件大小: {TotalMB}MB", totalBytes / (1024 * 1024));

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileWriter = File.Create(tempFilePath);
            
            var buffer = new byte[64 * 1024]; // 64KB buffer
            long downloadedBytes = 0;
            int bytesRead;
            var lastProgressTime = downloadStartTime;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileWriter.WriteAsync(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;

                // 每3秒报告一次进度
                if (DateTime.UtcNow - lastProgressTime > TimeSpan.FromSeconds(3))
                {
                    var progressPercent = totalBytes > 0 ? (downloadedBytes * 100.0 / totalBytes) : 0;
                    var speed = downloadedBytes / (DateTime.UtcNow - downloadStartTime).TotalSeconds / (1024 * 1024);
                    Log.Information("单线程下载进度: {DownloadedMB}MB / {TotalMB}MB ({Progress:F1}%) - 速度: {Speed:F1}MB/s", 
                        downloadedBytes / (1024 * 1024), 
                        totalBytes / (1024 * 1024), 
                        progressPercent,
                        speed);
                    lastProgressTime = DateTime.UtcNow;
                }
            }

            await CompleteDownload(tempFilePath, finalPath, downloadedBytes, downloadStartTime);
        }

        /// <summary>
        /// 完成下载，验证并移动文件
        /// </summary>
        private async Task CompleteDownload(string tempFilePath, string finalPath, long totalBytes, DateTime downloadStartTime)
        {
            var elapsed = DateTime.UtcNow - downloadStartTime;
            var avgSpeed = totalBytes / elapsed.TotalSeconds / (1024 * 1024);
            
            Log.Information("模型下载完成，总大小: {TotalMB}MB, 耗时: {Elapsed}, 平均速度: {Speed:F1}MB/s", 
                totalBytes / (1024 * 1024), elapsed.ToString(@"mm\:ss"), avgSpeed);
            
            // 验证下载文件的SHA256
            Log.Information("正在验证下载文件的SHA256...");
            if (!VerifyModelSha256(tempFilePath))
            {
                Log.Error("下载的模型文件SHA256校验失败，删除损坏的文件");
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
                throw new AudioProcessingException("MODEL_VERIFICATION_FAILED", "下载的模型文件SHA256校验失败，可能文件已损坏");
            }
            
            // 移动临时文件到最终位置
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }
            File.Move(tempFilePath, finalPath);
            
            Log.Information("模型下载完成并校验通过，保存到: {ModelPath}", finalPath);
        }

        /// <summary>
        /// 使用原来的 WhisperGgmlDownloader 下载其他模型
        /// </summary>
        private async Task DownloadFromWhisperGgmlAsync(GgmlType modelType, string targetModelsDir, DateTime downloadStartTime)
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

        /// <summary>
        /// 检查是否有未完成的下载
        /// </summary>
        public bool HasIncompleteDownload()
        {
            var packageFilePath = Path.Combine(_modelDir, $"{ModelName}.download");
            return File.Exists(packageFilePath);
        }

        /// <summary>
        /// 清理未完成的下载文件
        /// </summary>
        public void CleanupIncompleteDownload()
        {
            try
            {
                var packageFilePath = Path.Combine(_modelDir, $"{ModelName}.download");
                var tempFilePath = Path.Combine(_modelDir, $"{ModelName}.tmp");

                if (File.Exists(packageFilePath))
                {
                    File.Delete(packageFilePath);
                    Log.Information("已清理下载包文件: {PackageFile}", packageFilePath);
                }

                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                    Log.Information("已清理临时文件: {TempFile}", tempFilePath);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "清理未完成下载文件时出错");
            }
        }

        /// <summary>
        /// 获取未完成下载的进度信息
        /// </summary>
        public async Task<(bool hasIncomplete, long downloadedBytes, long totalBytes, double progressPercent)> GetIncompleteDownloadProgressAsync()
        {
            try
            {
                var packageFilePath = Path.Combine(_modelDir, $"{ModelName}.download");
                if (!File.Exists(packageFilePath))
                {
                    return (false, 0, 0, 0);
                }

#if NET8_0_OR_GREATER
                var packageJson = await File.ReadAllTextAsync(packageFilePath);
#else
                var packageJson = File.ReadAllText(packageFilePath);
#endif
                var package = Newtonsoft.Json.JsonConvert.DeserializeObject<DownloadPackage>(packageJson);
                
                if (package?.Chunks != null)
                {
                    var downloadedBytes = package.Chunks.Sum(chunk => chunk.Length);
                    var totalBytes = package.TotalFileSize;
                    var progressPercent = totalBytes > 0 ? (downloadedBytes * 100.0 / totalBytes) : 0;
                    
                    return (true, downloadedBytes, totalBytes, progressPercent);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "获取下载进度信息失败");
            }

            return (false, 0, 0, 0);
        }

        public void Dispose()
        {
            _processor?.Dispose();
            _processor = null;
        }
    }
} 
