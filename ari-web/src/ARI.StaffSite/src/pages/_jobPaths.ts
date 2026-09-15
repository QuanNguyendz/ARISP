/**
 * Đường dẫn tới các màn của MỘT tin, theo khu làm việc đang đứng.
 *
 * Ba vai dùng chung mấy trang này (ngân hàng đề, bảng điểm) nhưng mỗi vai có tiền tố route riêng:
 * `/hr/jobs/:id`, `/recruiter/my-jobs/:id`, `/hm/jobs/:id`. Trước đây mỗi trang tự viết
 * `isHr ? '/hr/...' : '/recruiter/...'` — một phép chọn HAI nhánh, nên khu Hiring Manager vừa được
 * mở là mọi nút "quay lại" trong đó đá người dùng sang khu Recruiter (nơi họ không có quyền).
 *
 * Cùng loại lỗi với nhãn vòng `type === 'technical' ? ... : ...`: hai nhánh cho một tập ba giá trị.
 */
export type JobWorkspace = 'hr' | 'recruiter' | 'hm'

export function workspaceOf(pathname: string): JobWorkspace {
  if (pathname.startsWith('/hr')) return 'hr'
  if (pathname.startsWith('/hm')) return 'hm'
  return 'recruiter'
}

/** Gốc đường dẫn của một tin trong khu đang đứng. */
export function jobBasePath(pathname: string, jobId: string | undefined): string {
  const id = jobId ?? ''
  switch (workspaceOf(pathname)) {
    case 'hr':
      return `/hr/jobs/${id}`
    case 'hm':
      return `/hm/jobs/${id}`
    default:
      return `/recruiter/my-jobs/${id}`
  }
}

/** Các màn con của tin — luôn dựng từ cùng một gốc để không lệch khu. */
export function jobPaths(pathname: string, jobId: string | undefined) {
  const base = jobBasePath(pathname, jobId)
  return {
    detail: base,
    onlineTestBank: `${base}/online-test`,
    onlineTestResults: `${base}/online-test/results`,
  }
}

/** Màn hồ sơ ứng viên trong khu đang đứng. */
export function candidatePath(pathname: string, applicationId: string): string {
  switch (workspaceOf(pathname)) {
    case 'hr':
      return `/hr/candidates/${applicationId}`
    case 'hm':
      return `/hm/candidates/${applicationId}`
    default:
      return `/recruiter/candidates/${applicationId}`
  }
}
