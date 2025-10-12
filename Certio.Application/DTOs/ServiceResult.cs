namespace Certio.Application.DTOs
{
    /// <summary>
    /// Generic result wrapper for service operations
    /// </summary>
    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }
        public Dictionary<string, string[]>? ValidationErrors { get; set; }

        public static ServiceResult<T> SuccessResult(T data)
        {
            return new ServiceResult<T>
            {
                Success = true,
                Data = data
            };
        }

        public static ServiceResult<T> FailureResult(string errorMessage, string errorCode = "ERROR")
        {
            return new ServiceResult<T>
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }

        public static ServiceResult<T> ValidationFailureResult(Dictionary<string, string[]> validationErrors)
        {
            return new ServiceResult<T>
            {
                Success = false,
                ErrorCode = "VALIDATION_ERROR",
                ValidationErrors = validationErrors
            };
        }
    }

    /// <summary>
    /// Result for operations that don't return data
    /// </summary>
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }

        public static ServiceResult SuccessResult()
        {
            return new ServiceResult { Success = true };
        }

        public static ServiceResult FailureResult(string errorMessage, string errorCode = "ERROR")
        {
            return new ServiceResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }
}

