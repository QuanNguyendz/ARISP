using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using ARI.Domain.Constants;
using ARI.Domain.Entities;

namespace ARI.Application.JdDocuments
{
    /// <summary>
    /// Bố cục đã tính sẵn của một bản mô tả công việc (ADR-064) — **nguồn sự thật duy nhất** cho cả
    /// bản .docx lẫn bản .pdf.
    ///
    /// Vì sao tách ra khỏi renderer: hai bộ xuất viết riêng sẽ trôi khỏi nhau ngay lần sửa thứ hai
    /// (thêm một mục ở docx, quên ở pdf — và không có gì báo). Ở đây quyết định "in cái gì, theo thứ
    /// tự nào"; renderer chỉ còn việc "vẽ ra sao". Nhờ vậy phần logic này test được mà không cần
    /// chạm tới PdfSharpCore hay OpenXML.
    /// </summary>
    public sealed class JdLayout
    {
        public string CompanyName { get; init; } = string.Empty;
        public string? CompanyAddress { get; init; }
        public string? CompanyWebsite { get; init; }
        public string? CompanyEmail { get; init; }

        public string AccentColor { get; init; } = "#4F46E5";
        public string FontFamily { get; init; } = "Arial";
        public string? FooterNote { get; init; }

        /// <summary>Tên vị trí — dùng đặt tên file; trong văn bản nó là dòng đầu của bảng thông tin.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>Tiêu đề canh giữa của văn bản, vd "THÔNG TIN TUYỂN DỤNG".</summary>
        public string DocumentTitle { get; init; } = "THÔNG TIN TUYỂN DỤNG";

        /// <summary>
        /// Bảng thông tin đầu văn bản (Vị trí · Số lượng · Thời gian · Địa chỉ…). Trường trống bị bỏ
        /// hẳn, không in "—": một dòng "Địa chỉ: —" trông như dữ liệu bị mất chứ không phải cố ý trống.
        /// </summary>
        public IReadOnlyList<JdFact> Facts { get; init; } = Array.Empty<JdFact>();

        /// <summary>Các mục có bật VÀ có nội dung, theo đúng thứ tự mẫu quy định.</summary>
        public IReadOnlyList<JdSection> Sections { get; init; } = Array.Empty<JdSection>();

        public sealed record JdFact(string Label, string Value);

        /// <summary>Một mục. <paramref name="Lines"/> là từng dòng đã tách — renderer in thành gạch đầu dòng.</summary>
        public sealed record JdSection(string Title, IReadOnlyList<string> Lines);

        // ---------------------------------------------------------------------------------

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        /// <summary>Đọc danh sách mục của mẫu; hỏng hoặc rỗng thì rơi về bộ mặc định.</summary>
        public static List<JdTemplateSection> ParseSections(string? sectionsJson)
        {
            if (string.IsNullOrWhiteSpace(sectionsJson)) return JdTemplateSection.Defaults();

            try
            {
                var parsed = JsonSerializer.Deserialize<List<JdTemplateSection>>(sectionsJson, Json);
                return parsed is { Count: > 0 } ? parsed : JdTemplateSection.Defaults();
            }
            catch (JsonException)
            {
                // Cấu hình hỏng KHÔNG được làm chết việc dựng JD — rơi về mặc định để vẫn ra được file.
                return JdTemplateSection.Defaults();
            }
        }

        /// <summary>Đọc nội dung từng mục của bản JD đã soạn.</summary>
        public static Dictionary<string, string> ParseContent(string? contentJson)
        {
            if (string.IsNullOrWhiteSpace(contentJson)) return new Dictionary<string, string>();

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(contentJson, Json)
                       ?? new Dictionary<string, string>();
            }
            catch (JsonException)
            {
                return new Dictionary<string, string>();
            }
        }

        public static JdLayout Build(JdTemplate template, JdDocument document)
        {
            var sections = ParseSections(template.SectionsJson);
            var content = ParseContent(document.SectionsJson);

            var facts = new List<JdFact>();
            void Fact(string label, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value)) facts.Add(new JdFact(label, value.Trim()));
            }

            // Thứ tự bám khuôn JD doanh nghiệp vẫn dùng: Vị trí → Số lượng → Thời gian → Địa chỉ,
            // rồi mới tới phần bổ sung. Tên vị trí là DÒNG ĐẦU của bảng chứ không phải một tiêu đề
            // to riêng — đó là điểm khác dễ thấy nhất so với bố cục cũ.
            Fact("Vị trí", document.Title);
            if (document.Vacancies is > 0) Fact("Số lượng", document.Vacancies.ToString());
            Fact("Thời gian", DisplayLabel(EmploymentTypeLabels, document.EmploymentType));
            Fact("Địa chỉ", document.Location);
            Fact("Bộ phận", document.Department);
            Fact("Cấp bậc", DisplayLabel(ExperienceLevelLabels, document.ExperienceLevel));
            Fact("Hình thức làm việc", DisplayLabel(WorkModeLabels, document.WorkMode));
            Fact("Mức lương", FormatSalary(document.SalaryMin, document.SalaryMax, document.SalaryCurrency));
            if (document.ApplicationDeadline is { } deadline)
                // Giờ Việt Nam: container chạy UTC nên ToLocalTime() là lệnh rỗng và in ra lệch một
                // ngày — đúng lỗi đã phải vá ở thư mời nhận việc.
                Fact("Hạn nộp hồ sơ", deadline.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));

            var built = new List<JdSection>();
            foreach (var section in sections.Where(s => s.Enabled))
            {
                if (!content.TryGetValue(section.Key, out var raw) || string.IsNullOrWhiteSpace(raw))
                    continue;   // mục bật nhưng chưa viết gì thì không in tiêu đề trống

                var lines = raw
                    .Replace("\r\n", "\n")
                    .Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0)
                    .Select(l => l.TrimStart('-', '•', '*', ' ').Trim())
                    .Where(l => l.Length > 0)
                    .ToList();

                if (lines.Count > 0)
                    built.Add(new JdSection(string.IsNullOrWhiteSpace(section.Title) ? section.Key : section.Title.Trim(), lines));
            }

            return new JdLayout
            {
                CompanyName = string.IsNullOrWhiteSpace(template.CompanyName) ? "Công ty" : template.CompanyName.Trim(),
                CompanyAddress = Clean(template.CompanyAddress),
                CompanyWebsite = Clean(template.CompanyWebsite),
                CompanyEmail = Clean(template.CompanyEmail),
                AccentColor = NormalizeHex(template.AccentColor),
                FontFamily = string.IsNullOrWhiteSpace(template.FontFamily) ? "Arial" : template.FontFamily.Trim(),
                FooterNote = Clean(template.FooterNote),
                Title = string.IsNullOrWhiteSpace(document.Title) ? "Bản mô tả công việc" : document.Title.Trim(),
                DocumentTitle = string.IsNullOrWhiteSpace(template.DocumentTitle)
                    ? "THÔNG TIN TUYỂN DỤNG"
                    : template.DocumentTitle.Trim(),
                Facts = facts,
                Sections = built,
            };
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        // ===== Nhãn hiển thị của các trường phân loại =====
        //
        // DB lưu khoá máy (`full_time`, `onsite`, `middle`), nhưng file JD là văn bản gửi ra ngoài
        // công ty — in thẳng khoá máy vào đó thì ứng viên đọc được "Thời gian: full_time".
        //
        // Phải ánh xạ Ở ĐÂY chứ không phải ở frontend: file do server dựng, không đọc được bảng nhãn
        // bên TypeScript (`@ari/shared/utils/jobOptions`). Hai bảng phải khớp nhau về nội dung — chú
        // thích ở cả hai phía nhắc nhau điều đó.

        private static readonly Dictionary<string, string> EmploymentTypeLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["full_time"] = "Toàn thời gian",
            ["part_time"] = "Bán thời gian",
            ["contract"] = "Hợp đồng",
            ["internship"] = "Thực tập",
            ["freelance"] = "Freelance",
        };

        private static readonly Dictionary<string, string> WorkModeLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["onsite"] = "On-site",
            ["hybrid"] = "Hybrid",
            ["remote"] = "Remote",
        };

        private static readonly Dictionary<string, string> ExperienceLevelLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["intern"] = "Intern",
            ["fresher"] = "Fresher",
            ["junior"] = "Junior",
            ["middle"] = "Middle",
            ["senior"] = "Senior",
            ["lead"] = "Lead",
            ["manager"] = "Manager",
        };

        /// <summary>
        /// Nhãn hiển thị của một khoá. Khoá lạ thì TRẢ LẠI NGUYÊN GIÁ TRỊ chứ không bỏ trống — dữ
        /// liệu cũ hoặc giá trị nhập tay vẫn hiện ra để người đọc thấy, thay vì biến mất âm thầm.
        /// </summary>
        public static string DisplayLabel(IReadOnlyDictionary<string, string> labels, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var key = value.Trim();
            return labels.TryGetValue(key, out var label) ? label : key;
        }

        /// <summary>
        /// Định dạng số kiểu Việt Nam, khai TƯỜNG MINH thay vì tra <c>CultureInfo("vi-VN")</c>.
        ///
        /// Culture của hệ điều hành không phải thứ đáng tin ở đây: container Linux (ADR-055) có thể
        /// chạy ở chế độ globalization-invariant, khi đó <c>vi-VN</c> âm thầm rơi về invariant và
        /// tiền lương in ra <c>20,000,000</c> — file JD gửi ra ngoài công ty với dấu phân cách sai.
        /// </summary>
        private static readonly NumberFormatInfo VietnameseNumber = new()
        {
            NumberGroupSeparator = ".",
            NumberDecimalSeparator = ",",
            NumberGroupSizes = new[] { 3 },
        };

        public static string FormatSalary(decimal? min, decimal? max, string? currency)
        {
            if (min is null && max is null) return string.Empty;

            var unit = string.IsNullOrWhiteSpace(currency) ? "VND" : currency.Trim();
            string N(decimal v) => v.ToString("#,##0", VietnameseNumber);

            if (min is { } lo && max is { } hi) return $"{N(lo)} – {N(hi)} {unit}";
            return $"{N((min ?? max)!.Value)} {unit}";
        }

        /// <summary>
        /// Đưa màu về dạng <c>#RRGGBB</c>. Giá trị rác rơi về màu mặc định thay vì ném lỗi — người
        /// dùng gõ nhầm một ký tự không đáng làm hỏng cả file JD.
        /// </summary>
        public static string NormalizeHex(string? value)
        {
            const string fallback = "#4F46E5";
            if (string.IsNullOrWhiteSpace(value)) return fallback;

            var v = value.Trim();
            if (!v.StartsWith('#')) v = "#" + v;
            if (v.Length != 7) return fallback;

            return v[1..].All(Uri.IsHexDigit) ? v.ToUpperInvariant() : fallback;
        }

        /// <summary>Tách <c>#RRGGBB</c> thành ba thành phần màu cho PdfSharpCore.</summary>
        public static (byte R, byte G, byte B) HexToRgb(string hex)
        {
            var v = NormalizeHex(hex);
            return (Convert.ToByte(v.Substring(1, 2), 16),
                    Convert.ToByte(v.Substring(3, 2), 16),
                    Convert.ToByte(v.Substring(5, 2), 16));
        }
    }
}
