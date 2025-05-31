using System;
using OWhisper.NET.Models;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Whisper.net;
using Whisper.net.Ggml;
using Serilog;

namespace OWhisper.NET
{
    public sealed class WhisperService : IDisposable
    {
        private static readonly Lazy<WhisperService> _instance =
            new Lazy<WhisperService>(() => new WhisperService());
        
        public static WhisperService Instance => _instance.Value;

        public enum ServiceStatus
        {
            Stopped,
            Starting,
            Running,
            Stopping
        }

        private ServiceStatus _status = ServiceStatus.Stopped;
        private readonly object _lock = new object();

        public event EventHandler<ServiceStatus> StatusChanged;

        private WhisperService()
        {
        }

        public ServiceStatus GetStatus() => _status;

        private bool IsValidMp3(byte[] audioData)
        {
            try
            {
                // MP3文件头检查: 0xFF 0xFB或0xFF 0xF3
                return audioData != null &&
                       audioData.Length > 2 &&
                      ((audioData[0] == 0xFF && audioData[1] == 0xFB) ||
                       (audioData[0] == 0xFF && audioData[1] == 0xF3));
            }
            catch
            {
                return false;
            }
        }

        private bool IsValidWav(byte[] audioData)
        {
            try
            {
                // WAV文件头检查: "RIFF"标记
                return audioData != null &&
                       audioData.Length > 12 &&
                       audioData[0] == 'R' &&
                       audioData[1] == 'I' &&
                       audioData[2] == 'F' &&
                       audioData[3] == 'F' &&
                       audioData[8] == 'W' &&
                       audioData[9] == 'A' &&
                       audioData[10] == 'V' &&
                       audioData[11] == 'E';
            }
            catch
            {
                return false;
            }
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_status != ServiceStatus.Stopped) return;
                
                Log.Information("服务状态变更: {OldStatus} -> {NewStatus}", _status, ServiceStatus.Starting);
                _status = ServiceStatus.Starting;
                StatusChanged?.Invoke(this, _status);
                Log.Information("启动服务线程...");
                
            }
        }

        /// <summary>
        /// 转录音频数据为文本
        /// </summary>
        /// <param name="audioData">音频数据</param>
        /// <returns>转写文本</returns>
        public async Task<TranscriptionResult> Transcribe(byte[] audioData)
        {
            // 前置音频验证
            Log.Information("验证音频数据格式...");
            bool isMp3 = IsValidMp3(audioData);
            bool isWav = IsValidWav(audioData);
            Log.Information("音频格式检测 - MP3: {IsMp3}, WAV: {IsWav}", isMp3, isWav);
            
            if (!isMp3 && !isWav)
            {
                Log.Error("不支持的音频格式");
                throw new AudioProcessingException("INVALID_AUDIO_FORMAT", "不支持的音频格式");
            }

            var startTime = DateTime.UtcNow;
            try
            {
                using var whisperManager = new WhisperManager();
                var text = await whisperManager.Transcribe(audioData);
                return new TranscriptionResult
                {
                    Text = text,
                    ProcessingTime = (DateTime.UtcNow - startTime).TotalSeconds
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "转写过程中发生错误");
                throw;
            }
        }

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        

        public void Stop()
        {
            lock (_lock)
            {
                if (_status != ServiceStatus.Running)
                {
                    Log.Information("服务状态为{Status}，无需停止", _status);
                    return;
                }
                
                _status = ServiceStatus.Stopping;
                StatusChanged?.Invoke(this, _status);
                Log.Information("正在停止服务线程...");
                
                _cts.Cancel();
                _status = ServiceStatus.Stopped;
                StatusChanged?.Invoke(this, _status);
                Log.Information("服务已完全停止");
            }
        }

        public void Dispose()
        {
            try
            {
                Stop();
                
                
                _cts?.Dispose();
            }
            catch
            {
                // 记录错误到日志系统
                throw;
            }
            
            GC.SuppressFinalize(this);
        }
    }

    public class WhisperManager : IDisposable
    {
        private WhisperProcessor _processor;
        private const string ModelName = "ggml-large-v3-turbo.bin";
        const GgmlType ggmlType = GgmlType.LargeV3Turbo;
        private readonly string _modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");
        private readonly string _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", ModelName);
        private readonly object _lock = new object();

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
                    // 简单验证模型文件大小(假设有效模型至少10MB)
                    valid = size > 10 * 1024 * 1024;
                }
                
                return (exists, valid, size, _modelPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "检查模型状态失败");
                return (false, false, 0, _modelPath);
            }
        }
        public async Task DownloadModelAsync(GgmlType modelType, string targetModelsDir) {
            if (!Directory.Exists(targetModelsDir)) {
                try {
                    Directory.CreateDirectory(targetModelsDir);
                    Log.Information("创建模型目录: {ModelDir}", targetModelsDir);
                } catch (Exception ex) {
                    Log.Error(ex, "创建模型目录失败");
                    throw new AudioProcessingException("MODEL_DIR_CREATE_FAILED", $"无法创建模型目录: {ex.Message}");
                }
            }

            Log.Information("开始下载模型: {ModelName}...", ModelName);
            var downloadStartTime = DateTime.UtcNow;
            
            try {
                using var client = HttpClientHelper.CreateProxyHttpClient();
                client.Timeout = TimeSpan.FromMinutes(10);
                var downloader = new WhisperGgmlDownloader(client);
                
                using var modelStream = await downloader.GetGgmlModelAsync(modelType);
                var tempFilePath = Path.Combine(targetModelsDir, $"{ModelName}.tmp");
                
                try {
                    using var fileWriter = File.OpenWrite(tempFilePath);
                    var buffer = new byte[8192];
                    long totalBytes = 0;
                    int bytesRead;
                    
                    while ((bytesRead = await modelStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                        await fileWriter.WriteAsync(buffer, 0, bytesRead);
                        totalBytes += bytesRead;
                        
                        if (DateTime.UtcNow - downloadStartTime > TimeSpan.FromSeconds(5)) {
                            Log.Information("已下载: {DownloadedMB}MB", totalBytes / (1024 * 1024));
                            downloadStartTime = DateTime.UtcNow;
                        }
                    }
                    
                    var finalPath = Path.Combine(targetModelsDir, ModelName);
                    if (File.Exists(finalPath)) {
                        File.Delete(finalPath);
                    }
                    File.Move(tempFilePath, finalPath);
                    Log.Information("模型下载完成，保存到: {ModelPath}", Path.Combine(targetModelsDir, ModelName));
                } catch (IOException ex) {
                    if (File.Exists(tempFilePath)) {
                        File.Delete(tempFilePath);
                    }
                    Log.Error(ex, "文件写入失败");
                    throw new AudioProcessingException("MODEL_WRITE_FAILED", $"模型文件写入失败: {ex.Message}");
                }
            } catch (System.Net.Http.HttpRequestException ex) {
                Log.Error(ex, "网络请求失败");
                throw new AudioProcessingException("NETWORK_ERROR", $"下载模型失败: {ex.Message}");
            } catch (TaskCanceledException ex) {
                Log.Error(ex, "下载超时");
                throw new AudioProcessingException("DOWNLOAD_TIMEOUT", "模型下载超时");
            } catch (Exception ex) {
                Log.Error(ex, "下载过程中发生未知错误");
                throw new AudioProcessingException("DOWNLOAD_FAILED", $"模型下载失败: {ex.Message}");
            }
        }

        private bool IsValidMp3(byte[] audioData)
        {
            // MP3文件头检查: 0xFF 0xFB或0xFF 0xF3
            return audioData.Length > 2 &&
                  ((audioData[0] == 0xFF && audioData[1] == 0xFB) ||
                   (audioData[0] == 0xFF && audioData[1] == 0xF3));
        }

        private bool IsValidWav(byte[] audioData)
        {
            // WAV文件头检查: "RIFF"标记
            return audioData.Length > 12 &&
                   audioData[0] == 'R' &&
                   audioData[1] == 'I' &&
                   audioData[2] == 'F' &&
                   audioData[3] == 'F' &&
                   audioData[8] == 'W' &&
                   audioData[9] == 'A' &&
                   audioData[10] == 'V' &&
                   audioData[11] == 'E';
        }

        public async Task<string> Transcribe(byte[] audioData)
        {
            TimeSpan timeTaken;
            var startTime = DateTime.UtcNow;
            
            // 确保模型目录存在
            if (!Directory.Exists(_modelDir))
            {
                Directory.CreateDirectory(_modelDir);
            }
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

            var ToReturn = new List<string>();

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
                    Log.Debug("{Start}-->{End}: {Text} [{TimeTaken}]",
                        result.Start, result.End, result.Text, timeTaken);
                    ToReturn.Add($"{startId}");
                    ToReturn.Add($"{result.Start.ToString(@"hh\:mm\:ss\,fff")} --> {result.End.ToString(@"hh\:mm\:ss\,fff")}");
                    ToReturn.Add($"{result.Text}\n");
                    startId++;
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

            return string.Join("\n", ToReturn);
        }

        public void Dispose()
        {
            _processor?.Dispose();
            _processor = null;
        }
    }
}