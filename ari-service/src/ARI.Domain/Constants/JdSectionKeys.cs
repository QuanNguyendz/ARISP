using System;
using System.Collections.Generic;
using System.Linq;

namespace ARI.Domain.Constants
{
    /// <summary>
    /// Khoá các mục của bản mô tả công việc (ADR-064).
    ///
    /// <b>Đây là khoá BẤT BIẾN, không phải nhãn hiển thị.</b> Nội dung đã soạn trong
    /// <c>JdDocument.SectionsJson</c> tra theo đúng những khoá này, nên đổi khoá là làm mất nội dung
    /// của mọi bản JD đã có mà không sinh ra lỗi nào. HR Leader đổi được tiêu đề, gợi ý, thứ tự và
    /// bật/tắt — riêng khoá thì không.
    /// </summary>
    public static class JdSectionKeys
    {
        public const string Description = "description";
        public const string Requirements = "requirements";
        public const string NiceToHave = "niceToHave";
        public const string Benefits = "benefits";
        public const string WorkingTime = "workingTime";
        public const string Process = "process";
        public const string Contact = "contact";

        public static readonly string[] All =
        {
            Description, Requirements, NiceToHave, Benefits, WorkingTime, Process, Contact,
        };

        public static bool IsKnown(string? key) =>
            key != null && All.Contains(key.Trim(), StringComparer.Ordinal);
    }

    /// <summary>Một mục trong mẫu JD — hình dạng của mỗi phần tử trong <c>JdTemplate.SectionsJson</c>.</summary>
    public sealed class JdTemplateSection
    {
        public string Key { get; set; } = string.Empty;

        /// <summary>Tiêu đề in ra file. HR Leader sửa được.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Gợi ý hiển thị cho Recruiter trong trình soạn — không in ra file.</summary>
        public string? Hint { get; set; }

        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Bộ mục mặc định khi công ty chưa cấu hình gì. Hai mục đầu khớp đúng hai ô mà Hiring
        /// Manager đã điền trên phiếu (<c>Description</c>, <c>Requirements</c>) nên trình soạn mở ra
        /// là đã có sẵn nội dung để sửa, không phải một trang trắng.
        /// </summary>
        public static List<JdTemplateSection> Defaults() => new()
        {
            new() { Key = JdSectionKeys.Description,  Title = "Mô tả công việc",       Hint = "Công việc hằng ngày, dự án sẽ tham gia", Enabled = true },
            new() { Key = JdSectionKeys.Requirements, Title = "Yêu cầu ứng viên",      Hint = "Kỹ năng và kinh nghiệm bắt buộc",       Enabled = true },
            new() { Key = JdSectionKeys.NiceToHave,   Title = "Ưu tiên nếu có",        Hint = "Không bắt buộc nhưng là lợi thế",       Enabled = true },
            new() { Key = JdSectionKeys.Benefits,     Title = "Quyền lợi",             Hint = "Lương thưởng, bảo hiểm, đào tạo",       Enabled = true },
            new() { Key = JdSectionKeys.WorkingTime,  Title = "Thời gian làm việc",    Hint = "Giờ làm, ngày làm, hình thức",          Enabled = true },
            new() { Key = JdSectionKeys.Process,      Title = "Quy trình tuyển dụng",  Hint = "Các vòng ứng viên sẽ trải qua",         Enabled = false },
            new() { Key = JdSectionKeys.Contact,      Title = "Liên hệ",               Hint = "Đầu mối nhận hồ sơ",                    Enabled = false },
        };
    }
}
