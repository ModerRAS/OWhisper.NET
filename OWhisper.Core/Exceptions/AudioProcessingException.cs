using System;

namespace OWhisper.Core.Services
{
    public class AudioProcessingException : Exception
    {
        public string ErrorCode { get; }

        public AudioProcessingException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }

        public AudioProcessingException(string errorCode, string message, Exception innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
} 