using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NAudio.Wave;
using OWhisper.Core.Models;
using Serilog;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// 基于 Silero VAD 的音频处理器
    /// </summary>
    public static class SileroVadAudioProcessor
    {
        /// <summary>
        /// 使用 Silero VAD 对音频进行分段处理
        /// </summary>
        /// <param name="audioData">16kHz单声道PCM音频数据</param>
        /// <param name="enableVad">是否启用VAD分段</param>
        /// <param name="vadSettings">VAD配置参数（兼容性，实际使用内置参数）</param>
        /// <returns>音频分段列表</returns>
        public static List<AudioSegment> ProcessAudioWithSileroVad(byte[] audioData, bool enableVad = true, VadSettings vadSettings = null)
        {
            if (audioData == null || audioData.Length == 0)
            {
                throw new ArgumentException("音频数据不能为空");
            }

            var segments = new List<AudioSegment>();

            if (!enableVad)
            {
                // 不使用VAD，返回完整音频作为单个分段
                Log.Information("Silero VAD未启用，返回完整音频作为单个分段");
                
                TimeSpan totalDuration;
                try
                {
                    using var stream = new MemoryStream(audioData);
                    using var reader = new WaveFileReader(stream);
                    totalDuration = reader.TotalTime;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "无法获取音频时长，使用默认值");
                    totalDuration = TimeSpan.FromSeconds(60); // 默认值
                }

                segments.Add(new AudioSegment
                {
                    StartTime = TimeSpan.Zero,
                    EndTime = totalDuration,
                    AudioData = audioData,
                    HasSpeech = true,
                    AverageEnergy = 0.5 // 默认能量值
                });

                return segments;
            }

            try
            {
                // 获取模型文件路径
                var modelPath = GetSileroVadModelPath();
                if (!File.Exists(modelPath))
                {
                    Log.Warning("Silero VAD 模型文件不存在，回退到不使用VAD: {ModelPath}", modelPath);
                    return ProcessAudioWithSileroVad(audioData, false, null);
                }

                // 将WAV字节数据转换为float数组
                var floatAudioData = ConvertWavBytesToFloat(audioData);
                
                using var vadDetector = new SileroVadDetector(modelPath, threshold: 0.5f, samplingRate: 16000);
                var speechSegments = vadDetector.GetSpeechSegmentList(floatAudioData);

                Log.Information("Silero VAD检测完成 - 共检测到 {Count} 个语音段", speechSegments.Count);

                // 转换为AudioSegment格式
                foreach (var speechSegment in speechSegments)
                {
                    if (speechSegment.StartSecond.HasValue && speechSegment.EndSecond.HasValue)
                    {
                        var startTime = TimeSpan.FromSeconds(speechSegment.StartSecond.Value);
                        var endTime = TimeSpan.FromSeconds(speechSegment.EndSecond.Value);
                        
                        // 提取对应的音频数据段
                        var segmentAudioData = ExtractAudioSegment(audioData, startTime, endTime);
                        
                        if (segmentAudioData != null && segmentAudioData.Length > 0)
                        {
                            segments.Add(new AudioSegment
                            {
                                StartTime = startTime,
                                EndTime = endTime,
                                AudioData = segmentAudioData,
                                HasSpeech = true,
                                AverageEnergy = 0.8 // Silero检测到的都是语音段，设置较高能量值
                            });
                        }
                    }
                }

                // 如果没有检测到语音段，返回原始音频
                if (segments.Count == 0)
                {
                    Log.Warning("Silero VAD未检测到任何语音段，回退到完整音频");
                    return ProcessAudioWithSileroVad(audioData, false, null);
                }

                Log.Information("Silero VAD处理完成 - 最终生成 {Count} 个音频段", segments.Count);
                
                // 打印分段信息
                for (int i = 0; i < segments.Count; i++)
                {
                    Log.Information("语音段 {Index}: {Segment}", i + 1, segments[i]);
                }

                return segments;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Silero VAD处理失败，回退到不使用VAD");
                return ProcessAudioWithSileroVad(audioData, false, null);
            }
        }

        /// <summary>
        /// 获取 Silero VAD 模型文件路径
        /// </summary>
        private static string GetSileroVadModelPath()
        {
            // 首先尝试从嵌入资源提取模型文件
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "OWhisper.Core.Resources.silero_vad_16k.onnx";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    // 创建临时文件
                    var tempPath = Path.Combine(Path.GetTempPath(), "silero_vad_16k.onnx");
                    
                    // 如果临时文件不存在或大小不对，重新创建
                    if (!File.Exists(tempPath) || new FileInfo(tempPath).Length != stream.Length)
                    {
                        using (var fileStream = File.Create(tempPath))
                        {
                            stream.CopyTo(fileStream);
                        }
                        Log.Information("从嵌入资源提取 Silero VAD 模型到: {TempPath}", tempPath);
                    }
                    
                    return tempPath;
                }
            }

            // 回退到文件系统路径
            var assemblyDir = Path.GetDirectoryName(assembly.Location);
            var modelPath = Path.Combine(assemblyDir, "Resources", "silero_vad_16k.onnx");
            
            if (File.Exists(modelPath))
            {
                return modelPath;
            }

            // 尝试项目路径
            var projectPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "OWhisper.Core", "Resources", "silero_vad_16k.onnx");
            if (File.Exists(projectPath))
            {
                return Path.GetFullPath(projectPath);
            }

            // 尝试当前工作目录
            var workingDirPath = Path.Combine(Directory.GetCurrentDirectory(), "OWhisper.Core", "Resources", "silero_vad_16k.onnx");
            if (File.Exists(workingDirPath))
            {
                return workingDirPath;
            }

            // 最后尝试程序目录
            var resourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "silero_vad_16k.onnx");
            return resourcePath;
        }

        /// <summary>
        /// 将WAV字节数据转换为float数组
        /// </summary>
        private static float[] ConvertWavBytesToFloat(byte[] wavBytes)
        {
            using var stream = new MemoryStream(wavBytes);
            using var reader = new WaveFileReader(stream);

            // 验证音频格式
            if (reader.WaveFormat.SampleRate != 16000)
            {
                Log.Warning("音频采样率不是16kHz: {SampleRate}Hz，Silero VAD需要16kHz音频", reader.WaveFormat.SampleRate);
            }

            if (reader.WaveFormat.Channels != 1)
            {
                Log.Warning("音频不是单声道: {Channels}声道，Silero VAD需要单声道音频", reader.WaveFormat.Channels);
            }

            // 读取音频数据并转换为float
            var samples = new List<float>();
            var buffer = new byte[reader.WaveFormat.BlockAlign * 1024]; // 1024个采样点的缓冲区
            int bytesRead;
            
            reader.Position = 0;
            while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                // 将字节数据转换为float (假设是16位PCM)
                for (int i = 0; i < bytesRead; i += 2)
                {
                    if (i + 1 < bytesRead)
                    {
                        short sample = BitConverter.ToInt16(buffer, i);
                        float floatSample = sample / 32768.0f; // 转换为 -1.0 到 1.0 的范围
                        samples.Add(floatSample);
                    }
                }
            }

            return samples.ToArray();
        }

        /// <summary>
        /// 从完整音频中提取指定时间段的音频数据
        /// </summary>
        private static byte[] ExtractAudioSegment(byte[] audioData, TimeSpan startTime, TimeSpan endTime)
        {
            try
            {
                using var stream = new MemoryStream(audioData);
                using var reader = new WaveFileReader(stream);

                var sampleRate = reader.WaveFormat.SampleRate;
                var bytesPerSample = reader.WaveFormat.BitsPerSample / 8;
                var channels = reader.WaveFormat.Channels;

                // 计算起始和结束的字节位置
                var startSample = (long)(startTime.TotalSeconds * sampleRate) * channels;
                var endSample = (long)(endTime.TotalSeconds * sampleRate) * channels;

                // 查找data chunk的位置
                var dataChunkOffset = FindDataChunkOffset(audioData);
                if (dataChunkOffset == -1)
                {
                    Log.Error("未找到WAV文件的data chunk");
                    return null;
                }

                var startOffset = dataChunkOffset + startSample * bytesPerSample;
                var endOffset = dataChunkOffset + endSample * bytesPerSample;

                // 边界检查
                startOffset = Math.Max(dataChunkOffset, Math.Min(startOffset, audioData.Length));
                endOffset = Math.Max(startOffset, Math.Min(endOffset, audioData.Length));

                var segmentLength = (int)(endOffset - startOffset);
                if (segmentLength <= 0)
                {
                    return null;
                }

                // 创建新的WAV文件
                return CreateWavFile(audioData, (int)startOffset, segmentLength, sampleRate, bytesPerSample, channels);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "提取音频段失败: {StartTime} - {EndTime}", startTime, endTime);
                return null;
            }
        }

        /// <summary>
        /// 查找WAV文件中data chunk的偏移位置
        /// </summary>
        private static int FindDataChunkOffset(byte[] audioData)
        {
            for (int i = 12; i < audioData.Length - 8; i++)
            {
                if (audioData[i] == 'd' && audioData[i + 1] == 'a' && 
                    audioData[i + 2] == 't' && audioData[i + 3] == 'a')
                {
                    return i + 8; // 跳过"data"标识符和size字段
                }
            }
            return -1;
        }

        /// <summary>
        /// 创建新的WAV文件
        /// </summary>
        private static byte[] CreateWavFile(byte[] originalAudio, int audioStartOffset, int audioLength, 
            int sampleRate, int bytesPerSample, int channels)
        {
            var bitsPerSample = bytesPerSample * 8;
            var blockAlign = channels * bytesPerSample;
            var byteRate = sampleRate * blockAlign;

            // 创建WAV文件头 (44字节标准头)
            var wavFile = new byte[44 + audioLength];
            
            // RIFF头 (12字节)
            Encoding.ASCII.GetBytes("RIFF").CopyTo(wavFile, 0);
            BitConverter.GetBytes(36 + audioLength).CopyTo(wavFile, 4);
            Encoding.ASCII.GetBytes("WAVE").CopyTo(wavFile, 8);

            // Format chunk (24字节)
            Encoding.ASCII.GetBytes("fmt ").CopyTo(wavFile, 12);
            BitConverter.GetBytes(16).CopyTo(wavFile, 16);
            BitConverter.GetBytes((short)1).CopyTo(wavFile, 20); // PCM格式
            BitConverter.GetBytes((short)channels).CopyTo(wavFile, 22);
            BitConverter.GetBytes(sampleRate).CopyTo(wavFile, 24);
            BitConverter.GetBytes(byteRate).CopyTo(wavFile, 28);
            BitConverter.GetBytes((short)blockAlign).CopyTo(wavFile, 32);
            BitConverter.GetBytes((short)bitsPerSample).CopyTo(wavFile, 34);

            // Data chunk头 (8字节)
            Encoding.ASCII.GetBytes("data").CopyTo(wavFile, 36);
            BitConverter.GetBytes(audioLength).CopyTo(wavFile, 40);

            // 复制音频数据
            Array.Copy(originalAudio, audioStartOffset, wavFile, 44, audioLength);

            return wavFile;
        }
    }
}