namespace ARI.Application.Interfaces
{
    /// <summary>
    /// Nhóm thư mục trong storage. Mỗi loại file nằm một nhánh riêng để tra cứu/backup/dọn dẹp
    /// theo loại được (trước đây mọi thứ — CV, JD, video ghi hình, playbook — đổ chung vào "cv/").
    /// Giá trị chuỗi thật nằm ở <see cref="StorageFolderExtensions.ToSegment"/> để cả
    /// LocalFileStorageService lẫn S3FileStorageService dùng chung một bảng ánh xạ.
    /// </summary>
    public enum StorageFolder
    {
        /// <summary>CV ứng viên (nộp hồ sơ + CV hồ sơ cá nhân).</summary>
        Cv,

        /// <summary>File JD của tin tuyển dụng, gồm cả bản JD đã ký duyệt.</summary>
        Jd,

        /// <summary>Video ghi hình buổi phỏng vấn thật (ADR-052, xoá sau retention).</summary>
        Recording,

        /// <summary>Playbook phỏng vấn nội bộ (ADR-025).</summary>
        Playbook,

        /// <summary>Ảnh đại diện ứng viên (ảnh tự tải lên; ảnh Google lưu thẳng URL, không qua đây).</summary>
        Avatar
    }

    public static class StorageFolderExtensions
    {
        /// <summary>Tên thư mục thật trong bucket/đĩa. Đổi giá trị ở đây là đổi cả 2 provider.</summary>
        public static string ToSegment(this StorageFolder folder) => folder switch
        {
            StorageFolder.Cv => "cv",
            StorageFolder.Jd => "jd",
            StorageFolder.Recording => "recordings",
            StorageFolder.Playbook => "playbooks",
            StorageFolder.Avatar => "avatars",
            _ => "misc"
        };
    }
}
