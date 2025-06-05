using System;
using OWhisper.Core.Models;
using OWhisper.Core.Services;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Whisper.net;
using Whisper.net.Ggml;
using Serilog;

namespace OWhisper.Core.Services
{
    public sealed class WhisperService : IDisposable
    {
        private static readonly Lazy<WhisperService> _instance =
            new Lazy<WhisperService>(() => new WhisperService());

        public static WhisperService Instance => _instance.Value;

        private readonly IPlatformPathService _platformPathService;

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
        public event EventHandler<float> ProgressChanged;

        private WhisperService()
        {
            _platformPathService = new PlatformPathService();
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

        private bool IsValidAac(byte[] audioData)
        {
            try
            {
                // AAC文件头检查: 常见的AAC格式标识
                // ADTS格式: 0xFF 0xF1 或 0xFF 0xF9
                // M4A格式: "ftyp" 在偏移4处
                return audioData != null &&
                       audioData.Length > 8 &&
                       (
                           // ADTS AAC
                           (audioData[0] == 0xFF && (audioData[1] == 0xF1 || audioData[1] == 0xF9)) ||
                           // M4A/MP4 AAC - 检查 'ftyp' 标识
                           (audioData.Length > 8 &&
                            audioData[4] == 'f' && audioData[5] == 't' &&
                            audioData[6] == 'y' && audioData[7] == 'p')
                       );
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
            bool isAacOrM4a = IsValidAac(audioData);
            Log.Information("音频格式检测 - MP3: {IsMp3}, WAV: {IsWav}, AAC/M4A: {IsAacOrM4a}", isMp3, isWav, isAacOrM4a);

            if (!isMp3 && !isWav && !isAacOrM4a)
            {
                Log.Error("不支持的音频格式");
                throw new AudioProcessingException("INVALID_AUDIO_FORMAT", "不支持的音频格式");
            }

            // 创建临时文件以获取音频时长
            string tempFilePath = Path.GetTempFileName();
            double totalMs = 0;
            try
            {
                File.WriteAllBytes(tempFilePath, audioData);
                TimeSpan totalDuration = AudioProcessor.GetAudioDuration(tempFilePath);
                totalMs = totalDuration.TotalMilliseconds;
                Log.Information("音频总时长: {TotalMs}ms", totalMs);

                var startTime = DateTime.UtcNow;
                using var whisperManager = new WhisperManager(_platformPathService);
                var (srtContent, plainText) = await whisperManager.Transcribe(audioData, (progress) =>
                {
                    // 触发进度事件
                    ProgressChanged?.Invoke(this, progress);
                }, totalMs);

                return new TranscriptionResult
                {
                    Success = true,
                    Text = plainText,
                    SrtContent = srtContent,
                    ProcessingTime = (DateTime.UtcNow - startTime).TotalSeconds
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "转写过程中发生错误");
                throw;
            }
            finally
            {
                // 删除临时文件
                try
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "删除临时文件失败");
                }
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
} 