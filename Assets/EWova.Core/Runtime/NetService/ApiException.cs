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
        public HttpStatusCode? StatusCode { get; }
        public string EndPoint { get; }
        public string ResponseBody { get; }

        public bool IsNetworkError => StatusCode != null;
        public bool IsServerError => (int?)StatusCode >= 500;
        public bool IsClientError => StatusCode >= (HttpStatusCode)400 && StatusCode < (HttpStatusCode)500;

        public ApiException(
            ApiErrorCode errorCode,
            HttpStatusCode? statusCode,
            string endPoint,
            string responseBody,
            string message,
            Exception inner)
            : base(message, inner)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
            EndPoint = endPoint;
            ResponseBody = responseBody;
        }
    }
}
