using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NAudio.Wave;
using Serilog;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// 音频分段信息
    /// </summary>
    public class AudioSegment
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public byte[] AudioData { get; set; }
        public bool HasSpeech { get; set; }
        public double AverageEnergy { get; set; }
        
        public override string ToString()
        {
            return $"Segment: {StartTime:mm\\:ss\\.fff} - {EndTime:mm\\:ss\\.fff} ({Duration.TotalSeconds:F1}s, Speech: {HasSpeech}, Energy: {AverageEnergy:F2})";
        }
    }

    /// <summary>
    /// VAD配置参数
    /// </summary>
    public class VadSettings
    {
        /// <summary>
        /// 最小分段长度（秒）
        /// </summary>
        public double MinSegmentDuration { get; set; } = 1.0;
        
        /// <summary>
        /// 最大分段长度（秒）
        /// </summary>
        public double MaxSegmentDuration { get; set; } = 30.0;
        
        /// <summary>
        /// 静音阈值（0-1之间）
        /// </summary>
        public double SilenceThreshold { get; set; } = 0.01;
        
        /// <summary>
        /// 连续静音时间阈值（秒），超过此时间认为是分段边界
        /// </summary>
        public double SilenceDurationThreshold { get; set; } = 0.5;
        
        /// <summary>
        /// 分析窗口大小（毫秒）
        /// </summary>
        public int WindowSizeMs { get; set; } = 20;
        
        /// <summary>
        /// 是否移除静音段
        /// </summary>
        public bool RemoveSilentSegments { get; set; } = true;
        
        /// <summary>
        /// 分段前后的缓冲时间（秒）
        /// </summary>
        public double BufferTime { get; set; } = 0.1;
    }

    /// <summary>
    /// 语音活动检测器 (Voice Activity Detector)
    /// </summary>
    public class VoiceActivityDetector
    {
        private readonly VadSettings _settings;

        public VoiceActivityDetector(VadSettings settings = null)
        {
            _settings = settings ?? new VadSettings();
        }

        /// <summary>
        /// 对音频数据进行VAD分析并分段
        /// </summary>
        /// <param name="audioData">16kHz单声道PCM音频数据</param>
        /// <returns>音频分段列表</returns>
        public List<AudioSegment> SegmentAudio(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
            {
                throw new ArgumentException("音频数据不能为空");
            }

            Log.Information("开始VAD音频分段分析，数据长度: {Length}字节", audioData.Length);

            var segments = new List<AudioSegment>();

            try
            {
                using var stream = new MemoryStream(audioData);
                using var reader = new WaveFileReader(stream);

                // 验证音频格式 (16kHz, 16bit, 单声道)
                if (reader.WaveFormat.SampleRate != 16000 || 
                    reader.WaveFormat.BitsPerSample != 16 || 
                    reader.WaveFormat.Channels != 1)
                {
                    throw new ArgumentException($"不支持的音频格式: {reader.WaveFormat.SampleRate}Hz, {reader.WaveFormat.BitsPerSample}bit, {reader.WaveFormat.Channels}声道");
                }

                var totalDuration = reader.TotalTime;
                Log.Information("音频总时长: {Duration}秒", totalDuration.TotalSeconds);

                // 计算参数
                var bytesPerSample = reader.WaveFormat.BitsPerSample / 8;
                var samplesPerSecond = reader.WaveFormat.SampleRate;
                var windowSizeSamples = (int)(samplesPerSecond * _settings.WindowSizeMs / 1000.0);
                var windowSizeBytes = windowSizeSamples * bytesPerSample;

                // 分析每个窗口的能量
                var energyLevels = AnalyzeEnergyLevels(reader, windowSizeBytes, bytesPerSample);
                
                // 根据能量水平检测语音活动
                var speechActivity = DetectSpeechActivity(energyLevels);
                
                // 根据语音活动创建分段
                segments = CreateSegments(audioData, speechActivity, totalDuration, samplesPerSecond, bytesPerSample);

                Log.Information("VAD分析完成，共生成 {Count} 个分段", segments.Count);
                
                // 打印分段信息
                for (int i = 0; i < segments.Count; i++)
                {
                    Log.Information("分段 {Index}: {Segment}", i + 1, segments[i]);
                }

                return segments;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "VAD音频分段失败");
                throw;
            }
        }

        /// <summary>
        /// 分析音频的能量水平
        /// </summary>
        private List<double> AnalyzeEnergyLevels(WaveFileReader reader, int windowSizeBytes, int bytesPerSample)
        {
            var energyLevels = new List<double>();
            var buffer = new byte[windowSizeBytes];
            
            reader.Position = 44; // 跳过WAV文件头
            
            while (reader.Position < reader.Length)
            {
                var bytesRead = reader.Read(buffer, 0, windowSizeBytes);
                if (bytesRead == 0) break;

                var energy = CalculateRmsEnergy(buffer, bytesRead, bytesPerSample);
                energyLevels.Add(energy);
            }

            Log.Information("能量分析完成，共 {Count} 个窗口", energyLevels.Count);
            
            if (energyLevels.Count > 0)
            {
                var maxEnergy = energyLevels.Max();
                var avgEnergy = energyLevels.Average();
                Log.Information("能量统计 - 最大: {Max:F4}, 平均: {Avg:F4}, 阈值: {Threshold:F4}", 
                    maxEnergy, avgEnergy, _settings.SilenceThreshold);
            }

            return energyLevels;
        }

        /// <summary>
        /// 计算RMS能量
        /// </summary>
        private double CalculateRmsEnergy(byte[] buffer, int length, int bytesPerSample)
        {
            if (length == 0) return 0;

            double sum = 0;
            int sampleCount = length / bytesPerSample;

            for (int i = 0; i < sampleCount; i++)
            {
                var sampleValue = BitConverter.ToInt16(buffer, i * bytesPerSample);
                // 使用long来避免乘法溢出
                long longValue = sampleValue;
                sum += longValue * longValue;
            }

            var rms = Math.Sqrt(sum / sampleCount);
            return rms / 32768.0; // 归一化到0-1范围
        }

        /// <summary>
        /// 根据能量水平检测语音活动
        /// </summary>
        private List<bool> DetectSpeechActivity(List<double> energyLevels)
        {
            var speechActivity = new List<bool>();
            
            // 简单的阈值检测
            foreach (var energy in energyLevels)
            {
                speechActivity.Add(energy > _settings.SilenceThreshold);
            }

            // 应用中值滤波减少噪声
            speechActivity = ApplyMedianFilter(speechActivity, 3);

            // 应用形态学操作（开运算+闭运算）
            speechActivity = ApplyMorphologicalOperations(speechActivity);

            return speechActivity;
        }

        /// <summary>
        /// 应用中值滤波
        /// </summary>
        private List<bool> ApplyMedianFilter(List<bool> input, int windowSize)
        {
            if (windowSize <= 1) return input;
            
            var result = new List<bool>(input);
            var halfWindow = windowSize / 2;

            for (int i = halfWindow; i < input.Count - halfWindow; i++)
            {
                var window = input.Skip(i - halfWindow).Take(windowSize).ToList();
                var trueCount = window.Count(x => x);
                result[i] = trueCount > windowSize / 2;
            }

            return result;
        }

        /// <summary>
        /// 应用形态学操作
        /// </summary>
        private List<bool> ApplyMorphologicalOperations(List<bool> input)
        {
            // 开运算：先腐蚀后膨胀，去除小的噪声
            var eroded = ApplyErosion(input, 2);
            var opened = ApplyDilation(eroded, 2);

            // 闭运算：先膨胀后腐蚀，填补小的空洞
            var dilated = ApplyDilation(opened, 3);
            var closed = ApplyErosion(dilated, 3);

            return closed;
        }

        /// <summary>
        /// 腐蚀操作
        /// </summary>
        private List<bool> ApplyErosion(List<bool> input, int kernelSize)
        {
            var result = new List<bool>(input);
            var halfKernel = kernelSize / 2;

            for (int i = halfKernel; i < input.Count - halfKernel; i++)
            {
                bool allTrue = true;
                for (int j = -halfKernel; j <= halfKernel; j++)
                {
                    if (!input[i + j])
                    {
                        allTrue = false;
                        break;
                    }
                }
                result[i] = allTrue;
            }

            return result;
        }

        /// <summary>
        /// 膨胀操作
        /// </summary>
        private List<bool> ApplyDilation(List<bool> input, int kernelSize)
        {
            var result = new List<bool>(input);
            var halfKernel = kernelSize / 2;

            for (int i = halfKernel; i < input.Count - halfKernel; i++)
            {
                bool anyTrue = false;
                for (int j = -halfKernel; j <= halfKernel; j++)
                {
                    if (input[i + j])
                    {
                        anyTrue = true;
                        break;
                    }
                }
                result[i] = anyTrue;
            }

            return result;
        }

        /// <summary>
        /// 根据语音活动创建音频分段
        /// </summary>
        private List<AudioSegment> CreateSegments(byte[] audioData, List<bool> speechActivity, 
            TimeSpan totalDuration, int samplesPerSecond, int bytesPerSample)
        {
            var segments = new List<AudioSegment>();
            var windowDurationMs = _settings.WindowSizeMs;
            var windowDuration = TimeSpan.FromMilliseconds(windowDurationMs);

            // 找到语音活动的边界
            var boundaries = FindSpeechBoundaries(speechActivity);

            Log.Information("检测到 {Count} 个语音边界", boundaries.Count);

            // 为每个边界创建分段
            for (int i = 0; i < boundaries.Count; i += 2)
            {
                if (i + 1 >= boundaries.Count) break;

                var startWindow = boundaries[i];
                var endWindow = boundaries[i + 1];

                var startTime = TimeSpan.FromMilliseconds(startWindow * windowDurationMs);
                var endTime = TimeSpan.FromMilliseconds(endWindow * windowDurationMs);

                // 应用缓冲时间
                startTime = TimeSpan.FromSeconds(Math.Max(0, startTime.TotalSeconds - _settings.BufferTime));
                endTime = TimeSpan.FromSeconds(Math.Min(totalDuration.TotalSeconds, endTime.TotalSeconds + _settings.BufferTime));

                // 检查分段长度限制
                var duration = endTime - startTime;
                if (duration.TotalSeconds < _settings.MinSegmentDuration)
                {
                    Log.Information("跳过过短分段: {Duration}秒", duration.TotalSeconds);
                    continue;
                }

                // 如果分段太长，进行分割
                if (duration.TotalSeconds > _settings.MaxSegmentDuration)
                {
                    var subSegments = SplitLongSegment(audioData, startTime, endTime, 
                        samplesPerSecond, bytesPerSample, totalDuration);
                    segments.AddRange(subSegments);
                }
                else
                {
                    var segment = CreateSegment(audioData, startTime, endTime, 
                        samplesPerSecond, bytesPerSample, true);
                    if (segment != null)
                    {
                        segments.Add(segment);
                    }
                }
            }

            return segments;
        }

        /// <summary>
        /// 找到语音活动的边界
        /// </summary>
        private List<int> FindSpeechBoundaries(List<bool> speechActivity)
        {
            var boundaries = new List<int>();
            bool inSpeech = false;

            for (int i = 0; i < speechActivity.Count; i++)
            {
                if (speechActivity[i] && !inSpeech)
                {
                    // 语音开始
                    boundaries.Add(i);
                    inSpeech = true;
                }
                else if (!speechActivity[i] && inSpeech)
                {
                    // 检查是否是真正的语音结束（连续静音）
                    var silenceLength = CountConsecutiveSilence(speechActivity, i);
                    var silenceDuration = silenceLength * _settings.WindowSizeMs / 1000.0;
                    
                    if (silenceDuration >= _settings.SilenceDurationThreshold)
                    {
                        // 语音结束
                        boundaries.Add(i);
                        inSpeech = false;
                    }
                }
            }

            // 如果最后还在语音中，添加结束边界
            if (inSpeech)
            {
                boundaries.Add(speechActivity.Count - 1);
            }

            return boundaries;
        }

        /// <summary>
        /// 计算连续静音的长度
        /// </summary>
        private int CountConsecutiveSilence(List<bool> speechActivity, int startIndex)
        {
            int count = 0;
            for (int i = startIndex; i < speechActivity.Count && !speechActivity[i]; i++)
            {
                count++;
            }
            return count;
        }

        /// <summary>
        /// 分割过长的分段
        /// </summary>
        private List<AudioSegment> SplitLongSegment(byte[] audioData, TimeSpan startTime, TimeSpan endTime,
            int samplesPerSecond, int bytesPerSample, TimeSpan totalDuration)
        {
            var segments = new List<AudioSegment>();
            var currentStart = startTime;
            var maxDuration = TimeSpan.FromSeconds(_settings.MaxSegmentDuration);

            while (currentStart < endTime)
            {
                var currentEnd = TimeSpan.FromSeconds(Math.Min(endTime.TotalSeconds, 
                    currentStart.TotalSeconds + maxDuration.TotalSeconds));

                var segment = CreateSegment(audioData, currentStart, currentEnd, 
                    samplesPerSecond, bytesPerSample, true);
                if (segment != null)
                {
                    segments.Add(segment);
                }

                currentStart = currentEnd;
            }

            Log.Information("长分段已分割为 {Count} 个子分段", segments.Count);
            return segments;
        }

        /// <summary>
        /// 创建音频分段
        /// </summary>
        private AudioSegment CreateSegment(byte[] audioData, TimeSpan startTime, TimeSpan endTime,
            int samplesPerSecond, int bytesPerSample, bool hasSpeech)
        {
            try
            {
                // 查找WAV文件中data chunk的位置
                int dataChunkOffset = FindDataChunkOffset(audioData);
                if (dataChunkOffset == -1)
                {
                    Log.Error("未找到WAV文件的data chunk");
                    return null;
                }

                // 计算音频数据中的字节偏移量（相对于data chunk开始）
                var startSample = (long)(startTime.TotalSeconds * samplesPerSecond);
                var endSample = (long)(endTime.TotalSeconds * samplesPerSecond);
                
                var startOffset = dataChunkOffset + startSample * bytesPerSample;
                var endOffset = dataChunkOffset + endSample * bytesPerSample;

                // 边界检查
                startOffset = Math.Max(dataChunkOffset, Math.Min(startOffset, audioData.Length));
                endOffset = Math.Max(startOffset, Math.Min(endOffset, audioData.Length));

                var segmentAudioLength = (int)(endOffset - startOffset);
                if (segmentAudioLength <= 0) return null;

                // 创建新的WAV文件
                var segmentData = CreateWavFile(audioData, (int)startOffset, segmentAudioLength, 
                    samplesPerSecond, bytesPerSample);

                // 计算平均能量 (新创建的WAV文件，data在偏移44处开始)
                var avgEnergy = CalculateAverageEnergy(segmentData, 44, segmentAudioLength, bytesPerSample);

                return new AudioSegment
                {
                    StartTime = startTime,
                    EndTime = endTime,
                    AudioData = segmentData,
                    HasSpeech = hasSpeech,
                    AverageEnergy = avgEnergy
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "创建音频分段失败: {StartTime} - {EndTime}", startTime, endTime);
                return null;
            }
        }

        /// <summary>
        /// 查找WAV文件中data chunk的偏移位置
        /// </summary>
        private int FindDataChunkOffset(byte[] audioData)
        {
            // WAV文件格式: RIFF header (12字节) + format chunk + data chunk
            // 查找 "data" 标识符
            for (int i = 12; i < audioData.Length - 8; i++)
            {
                if (audioData[i] == 'd' && audioData[i + 1] == 'a' && 
                    audioData[i + 2] == 't' && audioData[i + 3] == 'a')
                {
                    // 返回data chunk数据部分的起始位置 (跳过"data"标识符和size字段)
                    return i + 8;
                }
            }
            return -1;
        }

        /// <summary>
        /// 创建新的WAV文件
        /// </summary>
        private byte[] CreateWavFile(byte[] originalAudio, int audioStartOffset, int audioLength, 
            int sampleRate, int bytesPerSample)
        {
            // 计算通道数（假设是单声道或立体声）
            int channels = bytesPerSample == 2 ? 1 : 2; // 简化假设
            if (bytesPerSample == 4) channels = 2; // 立体声16位
            
            // 重新计算确保正确
            int bitsPerSample = (bytesPerSample / channels) * 8;
            int blockAlign = channels * (bitsPerSample / 8);
            int byteRate = sampleRate * blockAlign;

            // 创建WAV文件头 (44字节标准头)
            var wavFile = new byte[44 + audioLength];
            
            // RIFF头 (12字节)
            Encoding.ASCII.GetBytes("RIFF").CopyTo(wavFile, 0);
            BitConverter.GetBytes(36 + audioLength).CopyTo(wavFile, 4); // 文件大小 - 8
            Encoding.ASCII.GetBytes("WAVE").CopyTo(wavFile, 8);

            // Format chunk (24字节)
            Encoding.ASCII.GetBytes("fmt ").CopyTo(wavFile, 12);
            BitConverter.GetBytes(16).CopyTo(wavFile, 16); // format chunk大小
            BitConverter.GetBytes((short)1).CopyTo(wavFile, 20); // PCM格式
            BitConverter.GetBytes((short)channels).CopyTo(wavFile, 22); // 通道数
            BitConverter.GetBytes(sampleRate).CopyTo(wavFile, 24); // 采样率
            BitConverter.GetBytes(byteRate).CopyTo(wavFile, 28); // 字节率
            BitConverter.GetBytes((short)blockAlign).CopyTo(wavFile, 32); // 块对齐
            BitConverter.GetBytes((short)bitsPerSample).CopyTo(wavFile, 34); // 位深

            // Data chunk头 (8字节)
            Encoding.ASCII.GetBytes("data").CopyTo(wavFile, 36);
            BitConverter.GetBytes(audioLength).CopyTo(wavFile, 40); // 音频数据大小

            // 复制音频数据
            Array.Copy(originalAudio, audioStartOffset, wavFile, 44, audioLength);

            return wavFile;
        }

        /// <summary>
        /// 计算平均能量
        /// </summary>
        private double CalculateAverageEnergy(byte[] audioData, int offset, int length, int bytesPerSample)
        {
            if (length == 0) return 0;

            double sum = 0;
            int sampleCount = length / bytesPerSample;

            for (int i = 0; i < sampleCount; i++)
            {
                var sampleValue = BitConverter.ToInt16(audioData, offset + i * bytesPerSample);
                // 防止Int16.MinValue溢出：使用long来避免溢出
                sum += Math.Abs((long)sampleValue);
            }

            return sum / sampleCount / 32768.0; // 归一化
        }

        /// <summary>
        /// 获取推荐的VAD设置
        /// </summary>
        public static VadSettings GetRecommendedSettings(double audioLengthSeconds)
        {
            var settings = new VadSettings();

            // 根据音频长度调整参数
            if (audioLengthSeconds > 300) // 超过5分钟
            {
                settings.MaxSegmentDuration = 20.0;
                settings.SilenceDurationThreshold = 1.0;
                settings.RemoveSilentSegments = true;
            }
            else if (audioLengthSeconds > 60) // 超过1分钟
            {
                settings.MaxSegmentDuration = 30.0;
                settings.SilenceDurationThreshold = 0.7;
            }
            else // 短音频
            {
                settings.MaxSegmentDuration = audioLengthSeconds;
                settings.SilenceDurationThreshold = 0.3;
                settings.RemoveSilentSegments = false;
            }

            return settings;
        }
    }
} 