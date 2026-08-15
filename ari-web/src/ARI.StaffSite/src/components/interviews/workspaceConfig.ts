/**
 * Khác biệt giữa hai khu vực HR và Recruiter cho màn Phỏng vấn.
 *
 * CHỈ chứa khác biệt về CÂU CHỮ và ĐƯỜNG DẪN. Mọi khác biệt về HÀNH VI đã được giải quyết dứt điểm
 * khi gộp hai file, không tham số hoá:
 *  - lọc ca đích theo sức chứa: lấy bản HR (bản Recruiter không lọc, để nhân sự chọn rồi mới báo lỗi);
 *  - đọc kết quả trả về của server: lấy bản HR (bản Recruiter đếm mọi lần gọi không ném là thành công,
 *    nên báo "đã dời" cả khi server từ chối);
 *  - banner lý do từ chối: lấy bản Recruiter (bản HR thiếu hẳn).
 *
 * Câu chữ ở đây là **khoá i18n** (`modules/staff/interviews`), không phải chuỗi hiển thị: file này
 * là module thuần, không gọi được hook `useTranslation` — component nhận khoá rồi tự dịch.
 */
export type WorkspaceVariant = 'hr' | 'recruiter'

export interface WorkspaceConfig {
  /** Khoá i18n cho phần câu chữ riêng của từng khu vực. */
  titleKey: string
  descriptionKey: string
  jobsStatKey: string
  searchPlaceholderKey: string
  emptyTitleKey: string
  emptyDescriptionKey: string
  /** Trang quản lý tin. */
  jobHref: (jobId: string) => string
  evaluationHref: (evaluationId: string) => string
  candidateHref: (applicationId: string) => string
}

export const WORKSPACES: Record<WorkspaceVariant, WorkspaceConfig> = {
  hr: {
    titleKey: 'workspace.hr.title',
    descriptionKey: 'workspace.hr.description',
    jobsStatKey: 'workspace.hr.jobsStat',
    searchPlaceholderKey: 'workspace.hr.searchPlaceholder',
    emptyTitleKey: 'workspace.hr.emptyTitle',
    emptyDescriptionKey: 'workspace.hr.emptyDescription',
    jobHref: (id) => `/hr/jobs/${id}`,
    evaluationHref: (id) => `/hr/evaluations?evaluationId=${id}`,
    candidateHref: (id) => `/hr/candidates/${id}`,
  },
  recruiter: {
    titleKey: 'workspace.recruiter.title',
    descriptionKey: 'workspace.recruiter.description',
    jobsStatKey: 'workspace.recruiter.jobsStat',
    searchPlaceholderKey: 'workspace.recruiter.searchPlaceholder',
    emptyTitleKey: 'workspace.recruiter.emptyTitle',
    emptyDescriptionKey: 'workspace.recruiter.emptyDescription',
    // `/recruiter/jobs/:id` KHÔNG tồn tại (App.tsx chỉ có `/recruiter/jobs/create`) — hai nút cũ
    // trên màn này dẫn thẳng vào trang 404. Đường đúng là `/recruiter/my-jobs/:id`.
    jobHref: (id) => `/recruiter/my-jobs/${id}`,
    evaluationHref: (id) => `/recruiter/evaluations?id=${id}`,
    candidateHref: (id) => `/recruiter/candidates/${id}`,
  },
}

/** Namespace i18n dùng chung cho toàn bộ màn Phỏng vấn (cả hai khu vực). */
export const INTERVIEWS_NS = 'modules/staff/interviews'
