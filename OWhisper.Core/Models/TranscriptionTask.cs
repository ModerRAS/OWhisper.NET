using System;
using OWhisper.Core.Models;

namespace OWhisper.Core.Models
{
    public class TranscriptionTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public string? Language { get; set; }
        public string? Model { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TaskStatus Status { get; set; } = TaskStatus.Pending;
        public int Progress { get; set; } = 0;
        public TranscriptionResult? Result { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public enum TaskStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Cancelled
    }
} 