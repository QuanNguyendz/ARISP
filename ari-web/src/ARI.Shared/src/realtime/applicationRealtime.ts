import type { QueryClient } from '@tanstack/react-query'

/**
 * Sự kiện DOM phát khi dữ liệu HỒ SƠ ứng tuyển đổi (trạng thái, lịch, bài thi, điểm CV chấm nền xong…).
 * Dành cho các màn nhân sự còn giữ state cục bộ (không qua react-query) — chúng lắng nghe bằng
 * {@link onApplicationsChanged} và tải lại NGẦM, không bật lại khung tải.
 */
export const STAFF_APPLICATIONS_REFRESH_EVENT = 'staff-applications:refresh'

/**
 * Phạm vi của một đợt thay đổi. Danh sách rỗng nghĩa là "không biết hồ sơ/tin nào" — nơi nghe phải
 * coi như có liên quan tới mình, vì bỏ sót một lần cập nhật tệ hơn nạp thừa một lần.
 */
export interface ApplicationsChangedDetail {
  /** Listener vừa nối lại DB (ADR-057) hoặc không rõ phạm vi — mọi màn phải nạp lại. */
  all: boolean
  jobPostingIds: string[]
  applicationIds: string[]
}

/**
 * Huỷ hiệu lực MỌI query đọc dữ liệu hồ sơ — một chỗ duy nhất.
 *
 * Trước đây mỗi nhánh realtime tự liệt kê khoá, và màn tin của Hiring Manager dùng một khoá không
 * nhánh nào nhắc tới: chấm CV nền xong (ADR-070) thì màn đó đứng ở "Đang chấm CV" cho tới khi F5.
 * Thêm màn mới đọc hồ sơ thì thêm khoá của nó VÀO ĐÂY, không rải ra từng nhánh sự kiện.
 *
 * Chỉ query đang được hiển thị mới thực sự tải lại, nên huỷ rộng vẫn rẻ.
 */
export function invalidateApplicationQueries(
  queryClient: QueryClient,
  detail: ApplicationsChangedDetail
): void {
  // Danh sách không lọc theo tin / hồ sơ.
  for (const key of [
    'applications',
    'recruiter-candidates',
    'recruiter-my-applications',
    'global-search-apps',
    'my-jobs',
    'hm-jobs',
    'hr-dashboard',
  ]) {
    queryClient.invalidateQueries({ queryKey: [key] })
  }

  const jobIds = detail.all ? [] : detail.jobPostingIds
  if (jobIds.length > 0) {
    for (const jobId of jobIds) {
      queryClient.invalidateQueries({ queryKey: ['job', jobId, 'applications'] })
      // Thẻ bộ tiêu chí đếm số hồ sơ còn chờ chấm theo bộ hiện hành.
      queryClient.invalidateQueries({ queryKey: ['job-cv-rubric', jobId] })
    }
  } else {
    queryClient.invalidateQueries({
      predicate: (q) => q.queryKey[0] === 'job' && q.queryKey[2] === 'applications',
    })
    queryClient.invalidateQueries({ queryKey: ['job-cv-rubric'] })
  }

  const appIds = detail.all ? [] : detail.applicationIds
  if (appIds.length > 0) {
    for (const appId of appIds) {
      queryClient.invalidateQueries({ queryKey: ['application', appId] })
      queryClient.invalidateQueries({ queryKey: ['hr-application', appId] })
    }
  } else {
    queryClient.invalidateQueries({ queryKey: ['application'] })
    queryClient.invalidateQueries({ queryKey: ['hr-application'] })
  }
}

export interface ApplicationChangeBatcher {
  /** Ghi nhận một thay đổi. Không truyền gì = không rõ phạm vi → nạp lại tất cả. */
  push: (change?: { jobPostingId?: string | null; applicationId?: string | null }) => void
  /** Phát ngay phần đang gom (dùng khi huỷ kết nối). */
  flush: () => void
  dispose: () => void
}

/**
 * Gom các thay đổi hồ sơ đến dồn dập thành MỘT lần nạp lại.
 *
 * Lưu bộ tiêu chí mới là chấm lại mọi hồ sơ của tin (ADR-070): mỗi hồ sơ chấm xong là một sự kiện, và
 * `invalidateQueries` mặc định huỷ lượt tải đang chạy để tải lại — không gom thì N hồ sơ là N yêu cầu
 * nối đuôi nhau. Chờ `delayMs` sau sự kiện cuối, nhưng không quá `maxWaitMs` kể từ sự kiện đầu, để
 * một đợt dài vẫn thấy điểm hiện dần chứ không đứng im tới khi đợt kết thúc.
 */
export function createApplicationChangeBatcher(
  queryClient: QueryClient,
  options: { delayMs?: number; maxWaitMs?: number; target?: EventTarget | null } = {}
): ApplicationChangeBatcher {
  const delayMs = options.delayMs ?? 300
  const maxWaitMs = options.maxWaitMs ?? 1500
  const target = options.target === undefined ? globalThis.window ?? null : options.target

  let all = false
  let jobIds = new Set<string>()
  let appIds = new Set<string>()
  let pending = false
  let timer: ReturnType<typeof setTimeout> | null = null
  let firstAt = 0

  const flush = () => {
    if (timer) clearTimeout(timer)
    timer = null
    if (!pending) return

    const detail: ApplicationsChangedDetail = {
      all,
      jobPostingIds: [...jobIds],
      applicationIds: [...appIds],
    }
    all = false
    jobIds = new Set()
    appIds = new Set()
    pending = false

    invalidateApplicationQueries(queryClient, detail)
    target?.dispatchEvent(new CustomEvent<ApplicationsChangedDetail>(STAFF_APPLICATIONS_REFRESH_EVENT, { detail }))
  }

  const push: ApplicationChangeBatcher['push'] = (change) => {
    const jobId = change?.jobPostingId || null
    const appId = change?.applicationId || null
    if (!jobId && !appId) all = true
    if (jobId) jobIds.add(jobId)
    if (appId) appIds.add(appId)

    const now = Date.now()
    if (!pending) firstAt = now
    pending = true

    if (timer) clearTimeout(timer)
    const wait = Math.max(0, Math.min(delayMs, firstAt + maxWaitMs - now))
    timer = setTimeout(flush, wait)
  }

  const dispose = () => {
    if (timer) clearTimeout(timer)
    timer = null
    pending = false
  }

  return { push, flush, dispose }
}

/** Màn có liên quan tới đợt thay đổi này không. Không rõ phạm vi = có. */
export function affectsJob(detail: ApplicationsChangedDetail, jobPostingId?: string | null): boolean {
  return detail.all || detail.jobPostingIds.length === 0 || (!!jobPostingId && detail.jobPostingIds.includes(jobPostingId))
}

export function affectsApplication(detail: ApplicationsChangedDetail, applicationId?: string | null): boolean {
  return (
    detail.all || detail.applicationIds.length === 0 || (!!applicationId && detail.applicationIds.includes(applicationId))
  )
}

/**
 * Đăng ký nghe thay đổi hồ sơ cho màn dùng state cục bộ. Trả về hàm huỷ — gọi trong cleanup của
 * `useEffect`.
 */
export function onApplicationsChanged(handler: (detail: ApplicationsChangedDetail) => void): () => void {
  const listener = (event: Event) => {
    const detail = (event as CustomEvent<ApplicationsChangedDetail>).detail
    handler(detail ?? { all: true, jobPostingIds: [], applicationIds: [] })
  }
  window.addEventListener(STAFF_APPLICATIONS_REFRESH_EVENT, listener)
  return () => window.removeEventListener(STAFF_APPLICATIONS_REFRESH_EVENT, listener)
}
