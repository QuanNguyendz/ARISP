using Microsoft.Extensions.Configuration;

namespace ARI.Application.Common
{
    /// <summary>
    /// Gốc URL của hai site frontend, dùng cho MỌI đường dẫn đặt vào email.
    ///
    /// <b>Vì sao gom về một chỗ.</b> Trước đây 12 chỗ tự đọc cấu hình, và chỗ nào cũng có sẵn
    /// <c>?? "http://localhost:3000"</c>. Thiếu biến môi trường lúc deploy thì thư vẫn gửi đi bình
    /// thường — chỉ là nút bấm dẫn về máy của lập trình viên. Không lỗi, không log, không ai biết cho
    /// tới khi ứng viên báo link hỏng. Một mặc định "trông có vẻ vô hại" lại đi thẳng ra ngoài công ty,
    /// đúng kiểu đã gặp với <c>full_time</c> in vào file JD.
    ///
    /// <b>Không có mặc định localhost ở đây.</b> Giá trị cho môi trường phát triển do
    /// <c>ARI.API</c> nạp vào cấu hình lúc khởi động, và môi trường non-Development thì **chặn ngay ở
    /// bước boot** nếu thiếu (cùng cách đang làm với <c>JWT:Secret</c> và Google OAuth). Nhờ vậy sai
    /// cấu hình lộ ra lúc triển khai — nơi có người đang nhìn — chứ không lúc 2 giờ sáng trong một lá thư.
    ///
    /// Hai site tách biệt theo ADR-046: ứng viên và nhân sự ở hai origin khác nhau, nên link gửi cho
    /// ứng viên mà lấy gốc của cổng nhân sự là dẫn tới một trang họ không đăng nhập được.
    /// </summary>
    public static class FrontendUrls
    {
        /// <summary>Cổng ứng viên (ARI.CandidateSite) — job board, portal, xác thực email.</summary>
        public const string CandidateKey = "Frontend:CandidateBaseUrl";

        /// <summary>Cổng nhân sự (ARI.StaffSite).</summary>
        public const string StaffKey = "Authentication:AdminFrontendUrl";

        /// <summary>Tên cũ của <see cref="StaffKey"/>, còn dùng ở vài môi trường.</summary>
        public const string LegacyStaffKey = "Auth:AdminFrontendUrl";

        /// <summary>Giá trị dev, ARI.API nạp vào cấu hình khi chạy Development.</summary>
        public const string DevCandidate = "http://localhost:3000";
        public const string DevStaff = "http://localhost:3001";

        /// <summary>
        /// Gốc cổng ứng viên. Trả về chuỗi rỗng khi chưa cấu hình — trạng thái mà bước kiểm lúc boot
        /// đã loại trừ ở mọi môi trường thật, nên ở đây không ném lỗi để một lá thư thiếu link không
        /// kéo đổ cả thao tác nghiệp vụ đang chạy.
        /// </summary>
        public static string Candidate(IConfiguration configuration) =>
            Read(configuration, CandidateKey) ?? Read(configuration, StaffKey) ?? string.Empty;

        /// <summary>Gốc cổng nhân sự.</summary>
        public static string Staff(IConfiguration configuration) =>
            Read(configuration, StaffKey) ?? Read(configuration, LegacyStaffKey) ?? string.Empty;

        /// <summary>Bỏ dấu <c>/</c> cuối để nơi gọi ghép đường dẫn mà không sinh ra <c>//</c>.</summary>
        private static string? Read(IConfiguration configuration, string key)
        {
            var value = configuration[key];
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');
        }
    }
}
