using System;

namespace OWhisper.NET.Models {
    public class TranscriptionTask {
        public string Id { get; set; }
        public TaskStatus Status { get; set; }
        public float Progress { get; set; }
        public string FileName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TranscriptionResult Result { get; set; }
        public string ErrorMessage { get; set; }
        public int QueuePosition { get; set; }
    }

    public enum TaskStatus {
        Queued,
        Processing,
        Completed,
        Failed,
        Cancelled
    }

    public class TranscriptionProgress {
        public string TaskId { get; set; }
        public TaskStatus Status { get; set; }
        public float Progress { get; set; }
        public int QueuePosition { get; set; }
        public string Message { get; set; }
        public TranscriptionResult Result { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class TaskCreationResponse {
        public string TaskId { get; set; }
        public int QueuePosition { get; set; }
    }
} 