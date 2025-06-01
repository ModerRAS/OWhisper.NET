namespace OWhisper.Core.Models
{
    public class ApiResponse<T>
    {
        public string Status { get; set; } = string.Empty;
        public T Data { get; set; }
        public string Error { get; set; }
        public string ErrorCode { get; set; }

        public static ApiResponse<T> Success(T data)
        {
            return new ApiResponse<T>
            {
                Status = "success",
                Data = data
            };
        }

        public static ApiResponse<T> CreateError(string code, string message)
        {
            return new ApiResponse<T>
            {
                Status = "error",
                Error = message,
                ErrorCode = code
            };
        }
    }

    public class TaskCreationResponse
    {
        public string TaskId { get; set; } = string.Empty;
        public int QueuePosition { get; set; }
    }

    public class TranscriptionProgress
    {
        public string TaskId { get; set; } = string.Empty;
        public TaskStatus Status { get; set; }
        public float Progress { get; set; }
        public int QueuePosition { get; set; }
        public string Message { get; set; } = string.Empty;
        public TranscriptionResult Result { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
} 