/**
 * Khoá react-query của màn Phỏng vấn — khai báo Ở MỘT NƠI.
 *
 * Trước đây danh sách ca và danh sách ứng viên nằm trong `useState` thuần, nạp đúng một lần lúc mở
 * rộng thẻ. Hệ quả: sửa sức chứa ở màn cấu hình lịch xong thì modal "Dời lịch" vẫn hiện số cũ, và
 * không có cache nào để realtime huỷ hiệu lực. Đưa vào react-query để hai đường làm mới (thao tác
 * trong cùng tab + sự kiện realtime từ tab/máy khác) cùng chạm được tới một chỗ.
 *
 * `useAppNotifications` huỷ hiệu lực theo TIỀN TỐ (`['job-slots']`, `['slot-candidates']`) vì payload
 * NOTIFY chỉ mang bộ khoá cố định, không có `availability_slot_id`.
 */
export const interviewKeys = {
  /** Danh sách tin có ca phỏng vấn (đã lọc theo quyền ở server). */
  jobs: ['interview-jobs'] as const,
  /** Các ca của một tin — nguồn dữ liệu cho cả modal "Dời lịch". */
  slots: (jobId: string) => ['job-slots', jobId] as const,
  /** Ứng viên trong một ca. */
  candidates: (slotId: string) => ['slot-candidates', slotId] as const,
}
