using System;

namespace OWhisper.NET.Models {
    /// <summary>
    /// 音频处理业务异常
    /// </summary>
    public class AudioProcessingException : Exception {
        public string ErrorCode { get; }

        public AudioProcessingException(string code, string message)
            : base(message) {
            ErrorCode = code;
        }

        public AudioProcessingException(string code, string message, Exception innerException)
            : base(message, innerException) {
            ErrorCode = code;
        }
    }
}