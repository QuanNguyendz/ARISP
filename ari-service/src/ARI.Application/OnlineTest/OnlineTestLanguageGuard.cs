using System;
using System.Collections.Generic;
using System.Linq;

namespace ARI.Application.OnlineTest
{
    /// <summary>
    /// Kiểm tra ngân hàng câu hỏi có đúng ngôn ngữ đã cấu hình cho vòng trắc nghiệm không.
    ///
    /// Vì sao cần: ngôn ngữ vòng trắc nghiệm nằm ở <c>InterviewRoundConfig.InterviewLanguage</c> (cấu
    /// hình lúc tạo tin), còn đề thi nhập sau ở màn Ngân hàng câu hỏi. Không có gì buộc hai thứ khớp
    /// nhau — hậu quả là ứng viên mở bài thi ra thấy đề bằng ngôn ngữ khác với thứ đã thông báo.
    ///
    /// Cách nhận diện KHÔNG đối xứng, và đó là chủ ý:
    ///   - "Đây là tiếng Việt" nhận ra được chắc chắn (có dấu thanh/nguyên âm riêng của tiếng Việt).
    ///   - "Đây là tiếng Anh" thì KHÔNG: một câu tiếng Việt viết không dấu, hay câu chỉ gồm mã nguồn
    ///     (<c>SELECT * FROM users</c>), đều không có dấu.
    /// Nên: cấu hình tiếng Anh mà gặp chữ có dấu → chắc chắn sai, báo đúng dòng đó. Cấu hình tiếng Việt
    /// thì chỉ kết luận khi CẢ FILE không có lấy một chữ có dấu — đủ để bắt trường hợp nhập nhầm file
    /// tiếng Anh, mà không đánh trượt một vài câu lệch chuẩn.
    /// </summary>
    public static class OnlineTestLanguageGuard
    {
        /// <summary>Dưới ngưỡng này thì tín hiệu quá ít để kết luận cả file là sai ngôn ngữ.</summary>
        public const int MinRowsForVietnameseVerdict = 3;

        /// <summary>Nguyên âm + phụ âm mang dấu riêng của tiếng Việt (đủ cả hoa lẫn thường).</summary>
        private const string VietnameseChars =
            "àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ" +
            "ÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠÙÚỤỦŨƯỪỨỰỬỮỲÝỴỶỸĐ";

        private static readonly HashSet<char> VietnameseCharSet = new(VietnameseChars);

        /// <summary>Chuẩn hoá giá trị ngôn ngữ về "vi" / "en"; giá trị lạ hoặc rỗng trả null (bỏ kiểm tra).</summary>
        public static string? Normalize(string? language)
        {
            var v = (language ?? string.Empty).Trim().ToLowerInvariant();
            if (v.StartsWith("vi")) return "vi";
            if (v.StartsWith("en")) return "en";
            return null;
        }

        /// <summary>Chuỗi có chứa ký tự riêng của tiếng Việt không.</summary>
        public static bool HasVietnameseChars(string? text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var ch in text)
                if (VietnameseCharSet.Contains(ch)) return true;
            return false;
        }

        /// <summary>
        /// Với cấu hình tiếng Anh: trả về true nếu dòng này viết bằng tiếng Việt (→ từ chối dòng).
        /// Với cấu hình khác: luôn false (không kết luận được theo từng dòng).
        /// </summary>
        public static bool RowViolatesLanguage(string? language, params string?[] texts)
        {
            if (Normalize(language) != "en") return false;
            return texts.Any(HasVietnameseChars);
        }

        /// <summary>
        /// Kiểm tra ở mức CẢ FILE. Trả về thông báo lỗi nếu file rõ ràng sai ngôn ngữ, null nếu chấp nhận.
        /// </summary>
        /// <param name="language">Ngôn ngữ đã cấu hình cho vòng trắc nghiệm.</param>
        /// <param name="questionTexts">Nội dung câu hỏi của các dòng hợp lệ trong file.</param>
        public static string? CheckFile(string? language, IReadOnlyList<string> questionTexts)
        {
            if (Normalize(language) != "vi") return null;
            if (questionTexts.Count < MinRowsForVietnameseVerdict) return null;
            if (questionTexts.Any(HasVietnameseChars)) return null;

            return "Vòng trắc nghiệm của tin này đang cấu hình ngôn ngữ Tiếng Việt, nhưng không câu hỏi nào "
                 + "trong file có chữ tiếng Việt có dấu. Hãy kiểm tra lại file, hoặc đổi ngôn ngữ của vòng "
                 + "trắc nghiệm trong phần cấu hình tin tuyển dụng.";
        }

        /// <summary>Câu báo lỗi cho một dòng viết sai ngôn ngữ (dùng khi cấu hình là tiếng Anh).</summary>
        public static string RowErrorMessage(string? language) =>
            Normalize(language) == "en"
                ? "Vòng trắc nghiệm đang cấu hình ngôn ngữ Tiếng Anh nhưng dòng này viết bằng tiếng Việt."
                : "Nội dung không đúng ngôn ngữ đã cấu hình cho vòng trắc nghiệm.";
    }
}
