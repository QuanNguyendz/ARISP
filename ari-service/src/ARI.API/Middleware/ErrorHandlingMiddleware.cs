using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ARI.API.Middleware
{
    /// <summary>
    /// Lưới cuối cho ngoại lệ chưa ai bắt. Ba thứ ở đây đều là chỗ đã từng gây lỗi thật:
    ///
    /// <b>1. Tên trường phải là <c>message</c> (camelCase).</b> Trước đây body serialize bằng
    /// <c>JsonSerializer.Serialize</c> không kèm option, ra <c>Message</c> hoa đầu — mà toàn bộ
    /// frontend đọc <c>error.message</c>. Không khớp thì nó rơi về chuỗi dự phòng và người dùng nhìn
    /// thấy đúng hai chữ <c>HTTP 500</c>. Lỗi có mô tả tử tế ở server nhưng không bao giờ tới được màn hình.
    ///
    /// <b>2. Không đẩy <c>exception.Message</c> ra ngoài ở môi trường thật.</b> Câu đó viết cho lập
    /// trình viên (tên bảng, tên cột, chuỗi kết nối trong thông báo của Npgsql), không viết cho người
    /// dùng — và nó nằm trong response gửi cho bất kỳ ai gọi được API. Development thì giữ để còn gỡ lỗi.
    ///
    /// <b>3. Luôn kèm <c>code</c>.</b> Giao diện dịch theo MÃ chứ không theo câu chữ, nên mọi thân lỗi
    /// của hệ thống đều phải có mã — kể cả lỗi không lường trước (<see cref="InternalErrorCode"/>).
    /// </summary>
    public class ErrorHandlingMiddleware
    {
        /// <summary>Mã cho ngoại lệ chưa phân loại — frontend dịch mã này ra câu xin lỗi chung.</summary>
        public const string InternalErrorCode = "internal_error";

        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        public ErrorHandlingMiddleware(
            RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during the request pipeline.");
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Response đã bắt đầu gửi thì không ghi đè được nữa — cố ghi sẽ ném thêm một ngoại lệ
            // thứ hai và che mất ngoại lệ gốc vừa log.
            if (context.Response.HasStarted) return Task.CompletedTask;

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var body = new
            {
                statusCode = context.Response.StatusCode,
                code = InternalErrorCode,
                message = "Hệ thống gặp sự cố ngoài dự kiến. Vui lòng thử lại; nếu vẫn lỗi hãy báo quản trị viên.",
                // Chi tiết kỹ thuật CHỈ ở máy phát triển.
                detail = _environment.IsDevelopment() ? exception.Message : null,
            };

            return context.Response.WriteAsync(JsonSerializer.Serialize(body));
        }
    }
}
