namespace OWhisper.NET.Models
{
    public class ApiResponse<T>
    {
        public string Status { get; set; }
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
}