using System.Net;
using System;

namespace EWova.NetService
{
    public enum ApiErrorCode
    {
        Unknown = 0,
        BadRequest = 400,
        Unauthorized = 401,
        Forbidden = 403,
        NotFound = 404,
        TooManyRequests = 429,
        ServerError = 500,

        DeserializationError = 1001,
        NetworkError = 1002
    }

    /// <summary>
    /// API 異常 - 包含 HTTP 狀態碼和伺服器回應內容
    /// </summary>
    public class ApiException : Exception
    {
        public ApiErrorCode ErrorCode { get; }
        public HttpStatusCode HttpStatusCode { get; }
        public string ResponseText { get; }

        public ApiException(
            ApiErrorCode errorCode,
            HttpStatusCode httpStatusCode,
            string message,
            string responseText = null,
            Exception inner = null)
            : base(message, inner)
        {
            ErrorCode = errorCode;
            HttpStatusCode = httpStatusCode;
            ResponseText = responseText;
        }
    }
}
