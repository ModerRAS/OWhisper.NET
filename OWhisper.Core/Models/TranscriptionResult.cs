using System;

namespace OWhisper.Core.Models
{
    public class TranscriptionResult
    {
        public bool Success { get; set; }
        public string? Text { get; set; }
        public string? Language { get; set; }
        public TimeSpan Duration { get; set; }
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? FileName { get; set; }
        public long FileSizeBytes { get; set; }
        public string? Model { get; set; }
        public string SrtContent { get; set; } = string.Empty;
        public double ProcessingTime { get; set; }

        // 文本润色相关属性
        public bool PolishingEnabled { get; set; }
        public string? PolishedText { get; set; }
        public string? PolishingSrtContent { get; set; }
        public TextPolishingResult? PolishingResult { get; set; }
        public string? PolishingTemplateName { get; set; }
        public string? PolishingModel { get; set; }
        public double PolishingProcessingTime { get; set; }
    }
} 