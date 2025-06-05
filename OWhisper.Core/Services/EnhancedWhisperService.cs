using System;
using System.Threading.Tasks;
using OWhisper.Core.Models;
using Serilog;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// 增强的Whisper服务，集成文本润色功能
    /// </summary>
    public class EnhancedWhisperService : IDisposable
    {
        private readonly WhisperService _whisperService;
        private readonly ITextPolishingService _polishingService;

        public EnhancedWhisperService(
            WhisperService whisperService = null, 
            ITextPolishingService polishingService = null)
        {
            _whisperService = whisperService ?? WhisperService.Instance;
            _polishingService = polishingService ?? CreateDefaultPolishingService();
        }

        /// <summary>
        /// 创建默认的文本润色服务
        /// </summary>
        private ITextPolishingService CreateDefaultPolishingService()
        {
            var pathService = new PlatformPathService();
            var templateManager = new TemplateManagerService(pathService);
            return new TextPolishingService(templateManager);
        }

        /// <summary>
        /// 转录音频并可选择进行文本润色
        /// </summary>
        /// <param name="audioData">音频数据</param>
        /// <param name="polishingRequest">润色请求（可选）</param>
        /// <param name="enableVad">是否启用VAD</param>
        /// <param name="vadSettings">VAD设置</param>
        /// <returns>转录结果（包含润色结果）</returns>
        public async Task<TranscriptionResult> TranscribeWithPolishingAsync(
            byte[] audioData, 
            TextPolishingRequest polishingRequest = null,
            bool enableVad = true,
            VadSettings vadSettings = null)
        {
            var overallStartTime = DateTime.UtcNow;
            
            try
            {
                Log.Information("开始转录音频，润色启用: {PolishingEnabled}", polishingRequest?.EnablePolishing == true);

                // 第一步：进行语音转录
                var transcriptionResult = await _whisperService.Transcribe(audioData, enableVad, vadSettings);
                
                if (!transcriptionResult.Success)
                {
                    Log.Error("语音转录失败: {Error}", transcriptionResult.Error);
                    return transcriptionResult;
                }

                Log.Information("语音转录完成，开始处理润色...");

                // 第二步：进行文本润色（如果启用）
                if (polishingRequest != null && polishingRequest.EnablePolishing)
                {
                    await ProcessTextPolishingAsync(transcriptionResult, polishingRequest);
                }
                else
                {
                    Log.Information("未启用文本润色，跳过润色步骤");
                    transcriptionResult.PolishingEnabled = false;
                }

                var totalTime = (DateTime.UtcNow - overallStartTime).TotalSeconds;
                Log.Information("转录和润色完成，总耗时: {TotalTime}秒", totalTime);

                return transcriptionResult;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "转录和润色过程中发生错误");
                return new TranscriptionResult
                {
                    Success = false,
                    Error = ex.Message,
                    ProcessingTime = (DateTime.UtcNow - overallStartTime).TotalSeconds
                };
            }
        }

        /// <summary>
        /// 处理文本润色
        /// </summary>
        private async Task ProcessTextPolishingAsync(TranscriptionResult transcriptionResult, TextPolishingRequest polishingRequest)
        {
            var polishingStartTime = DateTime.UtcNow;

            try
            {
                // 设置润色请求的原始文本
                polishingRequest.OriginalText = transcriptionResult.Text ?? string.Empty;

                // 进行文本润色
                var polishingResult = await _polishingService.PolishTextAsync(polishingRequest);

                // 更新转录结果
                transcriptionResult.PolishingEnabled = true;
                transcriptionResult.PolishingResult = polishingResult;
                transcriptionResult.PolishingTemplateName = polishingRequest.TemplateName;
                transcriptionResult.PolishingModel = polishingRequest.Model;
                transcriptionResult.PolishingProcessingTime = (DateTime.UtcNow - polishingStartTime).TotalSeconds;

                if (polishingResult.IsSuccess)
                {
                    transcriptionResult.PolishedText = polishingResult.PolishedText;
                    
                    // 生成润色后的SRT内容
                    if (!string.IsNullOrEmpty(transcriptionResult.SrtContent))
                    {
                        transcriptionResult.PolishingSrtContent = await GeneratePolishedSrtAsync(
                            transcriptionResult.SrtContent, 
                            transcriptionResult.Text, 
                            polishingResult.PolishedText);
                    }

                    Log.Information("文本润色成功，原文长度: {OriginalLength}, 润色后长度: {PolishedLength}, 使用Token: {TokensUsed}",
                        polishingRequest.OriginalText.Length,
                        polishingResult.PolishedText.Length,
                        polishingResult.TokensUsed);
                }
                else
                {
                    Log.Warning("文本润色失败: {ErrorMessage}", polishingResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "处理文本润色时发生错误");
                transcriptionResult.PolishingEnabled = true;
                transcriptionResult.PolishingResult = TextPolishingResult.Failure(
                    polishingRequest.OriginalText, 
                    ex.Message, 
                    polishingRequest.Model, 
                    polishingRequest.TemplateName);
                transcriptionResult.PolishingProcessingTime = (DateTime.UtcNow - polishingStartTime).TotalSeconds;
            }
        }

        /// <summary>
        /// 生成润色后的SRT内容
        /// </summary>
        private async Task<string> GeneratePolishedSrtAsync(string originalSrt, string originalText, string polishedText)
        {
            try
            {
                // 如果润色后的文本与原文相似，直接替换SRT中的文本内容
                if (IsSimilarStructure(originalText, polishedText))
                {
                    return await ReplaceSimpleSrtTextAsync(originalSrt, originalText, polishedText);
                }
                
                // 如果结构差异较大，需要重新分配时间戳
                return await RegenerateSrtWithTimestampsAsync(originalSrt, polishedText);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "生成润色SRT失败，使用原始SRT");
                return originalSrt;
            }
        }

        /// <summary>
        /// 检查两个文本的结构是否相似
        /// </summary>
        private bool IsSimilarStructure(string originalText, string polishedText)
        {
            // 简单的相似度检查：句子数量和长度比较
            var originalSentences = SplitIntoSentences(originalText);
            var polishedSentences = SplitIntoSentences(polishedText);
            
            var lengthRatio = (double)polishedText.Length / originalText.Length;
            var sentenceRatio = (double)polishedSentences.Count / originalSentences.Count;
            
            // 如果长度比在0.5-2.0之间，句子数量比在0.7-1.5之间，认为结构相似
            return lengthRatio >= 0.5 && lengthRatio <= 2.0 && 
                   sentenceRatio >= 0.7 && sentenceRatio <= 1.5;
        }

        /// <summary>
        /// 将文本分割成句子
        /// </summary>
        private List<string> SplitIntoSentences(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();
            
            // 使用正则表达式分割句子
            var sentences = Regex.Split(text, @"[。！？!?]\s*")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            
            return sentences;
        }

        /// <summary>
        /// 简单替换SRT文本内容
        /// </summary>
        private async Task<string> ReplaceSimpleSrtTextAsync(string originalSrt, string originalText, string polishedText)
        {
            // 这是一个简化的实现，实际应用中可能需要更复杂的逻辑
            var lines = originalSrt.Split('\n');
            var result = new List<string>();
            
            var originalSentences = SplitIntoSentences(originalText);
            var polishedSentences = SplitIntoSentences(polishedText);
            
            int sentenceIndex = 0;
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                
                // 跳过序号行和时间戳行
                if (Regex.IsMatch(line, @"^\d+$") || Regex.IsMatch(line, @"\d{2}:\d{2}:\d{2},\d{3} --> \d{2}:\d{2}:\d{2},\d{3}"))
                {
                    result.Add(line);
                }
                // 空行
                else if (string.IsNullOrWhiteSpace(line))
                {
                    result.Add(line);
                }
                // 文本行
                else
                {
                    if (sentenceIndex < polishedSentences.Count)
                    {
                        result.Add(polishedSentences[sentenceIndex]);
                        sentenceIndex++;
                    }
                    else
                    {
                        result.Add(line); // 保持原文本
                    }
                }
            }
            
            return string.Join("\n", result);
        }

        /// <summary>
        /// 重新生成带时间戳的SRT
        /// </summary>
        private async Task<string> RegenerateSrtWithTimestampsAsync(string originalSrt, string polishedText)
        {
            // 提取原始SRT的时间信息
            var timeRanges = ExtractSrtTimeRanges(originalSrt);
            if (!timeRanges.Any()) return originalSrt;
            
            // 分割润色后的文本
            var polishedSentences = SplitIntoSentences(polishedText);
            if (!polishedSentences.Any()) return originalSrt;
            
            // 重新分配时间戳
            var result = new List<string>();
            var totalDuration = timeRanges.Last().EndTime - timeRanges.First().StartTime;
            var averageDurationPerSentence = totalDuration.TotalSeconds / polishedSentences.Count;
            
            for (int i = 0; i < polishedSentences.Count; i++)
            {
                var startTime = timeRanges.First().StartTime.Add(TimeSpan.FromSeconds(i * averageDurationPerSentence));
                var endTime = i < polishedSentences.Count - 1 
                    ? startTime.Add(TimeSpan.FromSeconds(averageDurationPerSentence))
                    : timeRanges.Last().EndTime;
                
                result.Add($"{i + 1}");
                result.Add($"{FormatSrtTimestamp(startTime)} --> {FormatSrtTimestamp(endTime)}");
                result.Add(polishedSentences[i]);
                result.Add("");
            }
            
            return string.Join("\n", result);
        }

        /// <summary>
        /// 提取SRT的时间范围
        /// </summary>
        private List<(TimeSpan StartTime, TimeSpan EndTime)> ExtractSrtTimeRanges(string srtContent)
        {
            var timeRanges = new List<(TimeSpan StartTime, TimeSpan EndTime)>();
            var lines = srtContent.Split('\n');
            
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"(\d{2}:\d{2}:\d{2},\d{3}) --> (\d{2}:\d{2}:\d{2},\d{3})");
                if (match.Success)
                {
                    var startTime = ParseSrtTimestamp(match.Groups[1].Value);
                    var endTime = ParseSrtTimestamp(match.Groups[2].Value);
                    timeRanges.Add((startTime, endTime));
                }
            }
            
            return timeRanges;
        }

        /// <summary>
        /// 解析SRT时间戳
        /// </summary>
        private TimeSpan ParseSrtTimestamp(string timestamp)
        {
            var parts = timestamp.Split(':');
            var secondsAndMs = parts[2].Split(',');
            return new TimeSpan(
                int.Parse(parts[0]),
                int.Parse(parts[1]),
                int.Parse(secondsAndMs[0]))
                .Add(TimeSpan.FromMilliseconds(int.Parse(secondsAndMs[1])));
        }

        /// <summary>
        /// 格式化SRT时间戳
        /// </summary>
        private string FormatSrtTimestamp(TimeSpan timeSpan)
        {
            return $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2},{timeSpan.Milliseconds:D3}";
        }

        /// <summary>
        /// 获取文本润色服务
        /// </summary>
        public ITextPolishingService GetPolishingService()
        {
            return _polishingService;
        }

        /// <summary>
        /// 获取Whisper服务
        /// </summary>
        public WhisperService GetWhisperService()
        {
            return _whisperService;
        }

        public void Dispose()
        {
            _whisperService?.Dispose();
        }
    }
} 