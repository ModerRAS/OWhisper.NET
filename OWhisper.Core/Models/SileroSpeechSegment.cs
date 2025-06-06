using System;

namespace OWhisper.Core.Models
{
    /// <summary>
    /// Silero VAD 检测到的语音段
    /// </summary>
    public class SileroSpeechSegment
    {
        /// <summary>
        /// 开始偏移量（采样点）
        /// </summary>
        public int? StartOffset { get; set; }
        
        /// <summary>
        /// 结束偏移量（采样点）
        /// </summary>
        public int? EndOffset { get; set; }
        
        /// <summary>
        /// 开始时间（秒）
        /// </summary>
        public float? StartSecond { get; set; }
        
        /// <summary>
        /// 结束时间（秒）
        /// </summary>
        public float? EndSecond { get; set; }

        /// <summary>
        /// 持续时间
        /// </summary>
        public TimeSpan Duration => StartSecond.HasValue && EndSecond.HasValue 
            ? TimeSpan.FromSeconds(EndSecond.Value - StartSecond.Value) 
            : TimeSpan.Zero;

        public SileroSpeechSegment()
        {
        }

        public SileroSpeechSegment(int startOffset, int? endOffset, float? startSecond, float? endSecond)
        {
            StartOffset = startOffset;
            EndOffset = endOffset;
            StartSecond = startSecond;
            EndSecond = endSecond;
        }

        public override string ToString()
        {
            return $"语音段: {StartSecond:F3}s - {EndSecond:F3}s ({Duration.TotalSeconds:F1}s)";
        }
    }
} 