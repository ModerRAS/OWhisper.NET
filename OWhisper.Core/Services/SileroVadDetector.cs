using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;
using OWhisper.Core.Models;
using Serilog;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// Silero VAD 语音活动检测器
    /// </summary>
    public class SileroVadDetector : IDisposable
    {
        private readonly SileroVadModel _model;
        private readonly float _threshold;
        private readonly float _negThreshold;
        private readonly int _samplingRate;
        private readonly int _windowSizeSample;
        private readonly float _minSpeechSamples;
        private readonly float _speechPadSamples;
        private readonly float _maxSpeechSamples;
        private readonly float _minSilenceSamples;
        private readonly float _minSilenceSamplesAtMaxSpeech;
        private int _audioLengthSamples;
        private const float THRESHOLD_GAP = 0.15f;
        private const int SAMPLING_RATE_8K = 8000;
        private const int SAMPLING_RATE_16K = 16000;

        /// <summary>
        /// 初始化 Silero VAD 检测器
        /// </summary>
        /// <param name="onnxModelPath">ONNX 模型文件路径</param>
        /// <param name="threshold">语音检测阈值 (0.1-0.9，默认 0.5)</param>
        /// <param name="samplingRate">采样率 (8000 或 16000)</param>
        /// <param name="minSpeechDurationMs">最小语音时长（毫秒）</param>
        /// <param name="maxSpeechDurationSeconds">最大语音时长（秒）</param>
        /// <param name="minSilenceDurationMs">最小静音时长（毫秒）</param>
        /// <param name="speechPadMs">语音段前后填充时间（毫秒）</param>
        public SileroVadDetector(string onnxModelPath, float threshold = 0.5f, int samplingRate = 16000,
            int minSpeechDurationMs = 250, float maxSpeechDurationSeconds = 30.0f,
            int minSilenceDurationMs = 100, int speechPadMs = 30)
        {
            if (samplingRate != SAMPLING_RATE_8K && samplingRate != SAMPLING_RATE_16K)
            {
                throw new ArgumentException("采样率不支持，只支持 [8000, 16000]");
            }

            _model = new SileroVadModel(onnxModelPath);
            _samplingRate = samplingRate;
            _threshold = threshold;
            _negThreshold = threshold - THRESHOLD_GAP;
            _windowSizeSample = samplingRate == SAMPLING_RATE_16K ? 512 : 256;
            _minSpeechSamples = samplingRate * minSpeechDurationMs / 1000f;
            _speechPadSamples = samplingRate * speechPadMs / 1000f;
            _maxSpeechSamples = samplingRate * maxSpeechDurationSeconds - _windowSizeSample - 2 * _speechPadSamples;
            _minSilenceSamples = samplingRate * minSilenceDurationMs / 1000f;
            _minSilenceSamplesAtMaxSpeech = samplingRate * 98 / 1000f;
            
            Reset();
            
            Log.Information("Silero VAD 检测器初始化完成 - 阈值: {Threshold}, 采样率: {SamplingRate}Hz, 窗口大小: {WindowSize}样本", 
                threshold, samplingRate, _windowSizeSample);
        }

        /// <summary>
        /// 重置内部状态
        /// </summary>
        public void Reset()
        {
            _model.ResetStates();
        }

        /// <summary>
        /// 从WAV文件获取语音段列表
        /// </summary>
        /// <param name="wavFile">WAV文件</param>
        /// <returns>语音段列表</returns>
        public List<SileroSpeechSegment> GetSpeechSegmentList(FileInfo wavFile)
        {
            Reset();

            using (var audioFile = new AudioFileReader(wavFile.FullName))
            {
                List<float> speechProbList = new List<float>();
                _audioLengthSamples = (int)(audioFile.Length / 2);
                float[] buffer = new float[_windowSizeSample];

                while (audioFile.Read(buffer, 0, buffer.Length) > 0)
                {
                    float speechProb = _model.Call(new[] { buffer }, _samplingRate)[0];
                    speechProbList.Add(speechProb);
                }

                return CalculateProb(speechProbList);
            }
        }

        /// <summary>
        /// 从音频数据获取语音段列表
        /// </summary>
        /// <param name="audioData">16kHz 单声道音频数据</param>
        /// <returns>语音段列表</returns>
        public List<SileroSpeechSegment> GetSpeechSegmentList(float[] audioData)
        {
            Reset();

            List<float> speechProbList = new List<float>();
            _audioLengthSamples = audioData.Length;

            // 按窗口大小处理音频数据
            for (int i = 0; i < audioData.Length; i += _windowSizeSample)
            {
                int remainingSamples = Math.Min(_windowSizeSample, audioData.Length - i);
                float[] buffer = new float[_windowSizeSample];
                
                // 复制音频数据到缓冲区
                Array.Copy(audioData, i, buffer, 0, remainingSamples);
                
                // 如果不够一个窗口，用零填充
                if (remainingSamples < _windowSizeSample)
                {
                    for (int j = remainingSamples; j < _windowSizeSample; j++)
                    {
                        buffer[j] = 0f;
                    }
                }

                float speechProb = _model.Call(new[] { buffer }, _samplingRate)[0];
                speechProbList.Add(speechProb);
            }

            return CalculateProb(speechProbList);
        }

        private List<SileroSpeechSegment> CalculateProb(List<float> speechProbList)
        {
            List<SileroSpeechSegment> result = new List<SileroSpeechSegment>();
            bool triggered = false;
            int tempEnd = 0, prevEnd = 0, nextStart = 0;
            SileroSpeechSegment segment = new SileroSpeechSegment();

            for (int i = 0; i < speechProbList.Count; i++)
            {
                float speechProb = speechProbList[i];
                
                if (speechProb >= _threshold && (tempEnd != 0))
                {
                    tempEnd = 0;
                    if (nextStart < prevEnd)
                    {
                        nextStart = _windowSizeSample * i;
                    }
                }

                if (speechProb >= _threshold && !triggered)
                {
                    triggered = true;
                    segment.StartOffset = _windowSizeSample * i;
                    continue;
                }

                if (triggered && (_windowSizeSample * i) - segment.StartOffset > _maxSpeechSamples)
                {
                    if (prevEnd != 0)
                    {
                        segment.EndOffset = prevEnd;
                        result.Add(segment);
                        segment = new SileroSpeechSegment();
                        if (nextStart < prevEnd)
                        {
                            triggered = false;
                        }
                        else
                        {
                            segment.StartOffset = nextStart;
                        }

                        prevEnd = 0;
                        nextStart = 0;
                        tempEnd = 0;
                    }
                    else
                    {
                        segment.EndOffset = _windowSizeSample * i;
                        result.Add(segment);
                        segment = new SileroSpeechSegment();
                        prevEnd = 0;
                        nextStart = 0;
                        tempEnd = 0;
                        triggered = false;
                        continue;
                    }
                }

                if (speechProb < _negThreshold && triggered)
                {
                    if (tempEnd == 0)
                    {
                        tempEnd = _windowSizeSample * i;
                    }

                    if (((_windowSizeSample * i) - tempEnd) > _minSilenceSamplesAtMaxSpeech)
                    {
                        prevEnd = tempEnd;
                    }

                    if ((_windowSizeSample * i) - tempEnd < _minSilenceSamples)
                    {
                        continue;
                    }
                    else
                    {
                        segment.EndOffset = tempEnd;
                        if ((segment.EndOffset - segment.StartOffset) > _minSpeechSamples)
                        {
                            result.Add(segment);
                        }

                        segment = new SileroSpeechSegment();
                        prevEnd = 0;
                        nextStart = 0;
                        tempEnd = 0;
                        triggered = false;
                        continue;
                    }
                }
            }

            if (segment.StartOffset != null && (_audioLengthSamples - segment.StartOffset) > _minSpeechSamples)
            {
                segment.EndOffset = _audioLengthSamples;
                result.Add(segment);
            }

            // 添加前后填充并合并相邻段
            for (int i = 0; i < result.Count; i++)
            {
                SileroSpeechSegment item = result[i];
                if (i == 0)
                {
                    item.StartOffset = (int)Math.Max(0, item.StartOffset.Value - _speechPadSamples);
                }

                if (i != result.Count - 1)
                {
                    SileroSpeechSegment nextItem = result[i + 1];
                    int silenceDuration = nextItem.StartOffset.Value - item.EndOffset.Value;
                    if (silenceDuration < 2 * _speechPadSamples)
                    {
                        item.EndOffset = item.EndOffset + (silenceDuration / 2);
                        nextItem.StartOffset = Math.Max(0, nextItem.StartOffset.Value - (silenceDuration / 2));
                    }
                    else
                    {
                        item.EndOffset = (int)Math.Min(_audioLengthSamples, item.EndOffset.Value + _speechPadSamples);
                        nextItem.StartOffset = (int)Math.Max(0, nextItem.StartOffset.Value - _speechPadSamples);
                    }
                }
                else
                {
                    item.EndOffset = (int)Math.Min(_audioLengthSamples, item.EndOffset.Value + _speechPadSamples);
                }
            }

            return MergeListAndCalculateSecond(result, _samplingRate);
        }

        private List<SileroSpeechSegment> MergeListAndCalculateSecond(List<SileroSpeechSegment> original, int samplingRate)
        {
            List<SileroSpeechSegment> result = new List<SileroSpeechSegment>();
            if (original == null || original.Count == 0)
            {
                return result;
            }

            int left = original[0].StartOffset.Value;
            int right = original[0].EndOffset.Value;
            
            if (original.Count > 1)
            {
                original.Sort((a, b) => a.StartOffset.Value.CompareTo(b.StartOffset.Value));
                for (int i = 1; i < original.Count; i++)
                {
                    SileroSpeechSegment segment = original[i];

                    if (segment.StartOffset > right)
                    {
                        result.Add(new SileroSpeechSegment(left, right,
                            CalculateSecondByOffset(left, samplingRate), CalculateSecondByOffset(right, samplingRate)));
                        left = segment.StartOffset.Value;
                        right = segment.EndOffset.Value;
                    }
                    else
                    {
                        right = Math.Max(right, segment.EndOffset.Value);
                    }
                }

                result.Add(new SileroSpeechSegment(left, right,
                    CalculateSecondByOffset(left, samplingRate), CalculateSecondByOffset(right, samplingRate)));
            }
            else
            {
                result.Add(new SileroSpeechSegment(left, right,
                    CalculateSecondByOffset(left, samplingRate), CalculateSecondByOffset(right, samplingRate)));
            }

            return result;
        }

        private float CalculateSecondByOffset(int offset, int samplingRate)
        {
            float secondValue = offset * 1.0f / samplingRate;
            return (float)Math.Floor(secondValue * 1000.0f) / 1000.0f;
        }

        public void Dispose()
        {
            _model?.Dispose();
        }
    }
} 