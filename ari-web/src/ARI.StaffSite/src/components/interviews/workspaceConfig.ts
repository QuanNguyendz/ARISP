/**
 * Khác biệt giữa hai khu vực HR và Recruiter cho màn Phỏng vấn.
 *
 * CHỈ chứa khác biệt về CÂU CHỮ và ĐƯỜNG DẪN. Mọi khác biệt về HÀNH VI đã được giải quyết dứt điểm
 * khi gộp hai file, không tham số hoá:
 *  - lọc ca đích theo sức chứa: lấy bản HR (bản Recruiter không lọc, để nhân sự chọn rồi mới báo lỗi);
 *  - đọc kết quả trả về của server: lấy bản HR (bản Recruiter đếm mọi lần gọi không ném là thành công,
 *    nên báo "đã dời" cả khi server từ chối);
 *  - banner lý do từ chối: lấy bản Recruiter (bản HR thiếu hẳn).
 */
export type WorkspaceVariant = 'hr' | 'recruiter'

export interface WorkspaceConfig {
  title: string
  description: string
  jobsStatLabel: string
  searchPlaceholder: string
  emptyTitleNoJobs: string
  emptyDescriptionNoJobs: string
  /** Trang quản lý tin. */
  jobHref: (jobId: string) => string
  evaluationHref: (evaluationId: string) => string
  candidateHref: (applicationId: string) => string
}

export const WORKSPACES: Record<WorkspaceVariant, WorkspaceConfig> = {
  hr: {
    title: 'Quản Lý Lịch Phỏng Vấn',
    description: 'Theo dõi các ca phỏng vấn, cấp mã thi và quản lý ứng viên trên toàn hệ thống',
    jobsStatLabel: 'Vị trí tuyển dụng',
    searchPlaceholder: 'Tìm kiếm vị trí tuyển dụng...',
    emptyTitleNoJobs: 'Chưa có vị trí tuyển dụng nào',
    emptyDescriptionNoJobs: 'Tạo vị trí tuyển dụng và thiết lập khung giờ phỏng vấn để bắt đầu.',
    jobHref: (id) => `/hr/jobs/${id}`,
    evaluationHref: (id) => `/hr/evaluations?evaluationId=${id}`,
    candidateHref: (id) => `/hr/candidates/${id}`,
  },
  recruiter: {
    title: 'Quản Lý Lịch Phỏng Vấn',
    description: 'Quản lý các ca phỏng vấn, cấp mã thi trực tiếp và theo dõi ứng viên cho các vị trí do bạn phụ trách',
    jobsStatLabel: 'Vị trí của tôi',
    searchPlaceholder: 'Tìm kiếm vị trí tuyển dụng của tôi...',
    emptyTitleNoJobs: 'Chưa có vị trí tuyển dụng nào do bạn phụ trách',
    emptyDescriptionNoJobs: 'Hãy tạo vị trí tuyển dụng mới và thiết lập khung giờ phỏng vấn.',
    // `/recruiter/jobs/:id` KHÔNG tồn tại (App.tsx chỉ có `/recruiter/jobs/create`) — hai nút cũ
    // trên màn này dẫn thẳng vào trang 404. Đường đúng là `/recruiter/my-jobs/:id`.
    jobHref: (id) => `/recruiter/my-jobs/${id}`,
    evaluationHref: (id) => `/recruiter/evaluations?id=${id}`,
    candidateHref: (id) => `/recruiter/candidates/${id}`,
  },
}
