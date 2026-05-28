using System.Net;
using System;

namespace EWova.NetService
{
    /// <summary>
    /// API 異常 - 包含 HTTP 狀態碼和伺服器回應內容
    /// </summary>
    public class ApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public string ResponseText { get; }

        public ApiException(
            HttpStatusCode statusCode,
            string message,
            string responseText = null,
            Exception inner = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
            ResponseText = responseText;
        }
    }
    /// <summary>
    /// 未授權異常 - HTTP 401，表示請求需要用戶驗證
    /// </summary>
    public class UnauthorizedException : ApiException
    {
        public UnauthorizedException(string responseText = null)
            : base(
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                responseText)
        {
        }
    }
    /// <summary>
    /// 驗證異常 - HTTP 400，表示請求無效或缺少必要參數
    /// </summary>
    public class ValidationException : ApiException
    {
        public ValidationException(string responseText = null)
            : base(
                HttpStatusCode.BadRequest,
                "Validation Failed",
                responseText)
        {
        }
    }
    /// <summary>
    /// 伺服器異常 - HTTP 5xx，表示伺服器內部錯誤或暫時無法處理請求
    /// </summary>
    public class ServerException : ApiException
    {
        public ServerException(
            HttpStatusCode statusCode,
            string responseText = null)
            : base(
                statusCode,
                "Server Error",
                responseText)
        {
        }
    }
    /// <summary>
    /// 禁止訪問異常 - HTTP 403，表示用戶沒有權限訪問該資源
    /// </summary>
    public class ForbiddenException : ApiException
    {
        public ForbiddenException(string responseText = null)
            : base(
                HttpStatusCode.Forbidden,
                "Forbidden",
                responseText)
        {
        }
    }
    /// <summary>
    /// 未找到異常 - HTTP 404，表示請求的資源不存在或已被刪除
    /// </summary>
    public class NotFoundException : ApiException
    {
        public NotFoundException(string responseText = null)
            : base(
                HttpStatusCode.NotFound,
                "Not Found",
                responseText)
        {
        }
    }
    /// <summary>
    /// 速率限制異常 - HTTP 429，表示請求過於頻繁，超過了伺服器設定的速率限制
    /// </summary>
    public class RateLimitException : ApiException
    {
        public RateLimitException(string responseText = null)
            : base(
                HttpStatusCode.TooManyRequests,
                "Too Many Requests",
                responseText)
        {
        }
    }
}
