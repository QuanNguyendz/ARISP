import type { CandidateState, SlotCandidate } from '@ari/shared/fservices/interview'

/**
 * Nguồn sự thật DUY NHẤT cho nhãn trạng thái ứng viên + việc bật/tắt từng nút.
 *
 * Trước đây màn hình có BA bản vị từ khác nhau (dòng ứng viên, "chọn tất cả", "chọn người chưa có
 * mã") và cả ba đều suy trạng thái bằng cách dò chuỗi tiếng Việt trong `declineReason`:
 *
 *     c.declineReason?.includes('loại')
 *
 * Hai hậu quả thật:
 *  1. Hệ thống tự huỷ lịch vì quá hạn xác nhận ghi lý do "[Hệ thống] Ứng viên không xác nhận lịch
 *     trong thời hạn." — không chứa chữ 'loại' — nên hiện thành "Từ chối (báo bận)", tức đổ lỗi
 *     cho ứng viên một việc họ không làm.
 *  2. Lý do từ chối là văn bản ứng viên TỰ NHẬP: chỉ cần gõ chữ "loại" vào là tự khoá nút của
 *     chính mình.
 *
 * Nay server trả thẳng `candidateState`, và mọi quyết định hiển thị đọc từ bảng dưới đây.
 *
 * Nhãn ở đây là **khoá i18n** (`modules/staff/interviews`) — file này là module thuần, không gọi
 * được hook dịch; component nhận khoá rồi tự dịch.
 */
export interface CandidateStateStyle {
  labelKey: string
  /** Lớp CSS cho chip trạng thái. */
  chip: string
  /** Lịch đã đóng (không còn giữ chỗ, không nhắc lịch / cấp mã được nữa). */
  closed: boolean
}

export const CANDIDATE_STATE: Record<CandidateState, CandidateStateStyle> = {
  pending: {
    labelKey: 'state.pending',
    chip: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400',
    closed: false,
  },
  confirmed: {
    labelKey: 'state.confirmed',
    chip: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400',
    closed: false,
  },
  declined_by_candidate: {
    labelKey: 'state.declined_by_candidate',
    chip: 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400',
    closed: true,
  },
  expired_no_response: {
    labelKey: 'state.expired_no_response',
    chip: 'bg-orange-100 text-orange-700 dark:bg-orange-500/20 dark:text-orange-400',
    closed: true,
  },
  no_show: {
    labelKey: 'state.no_show',
    chip: 'bg-orange-100 text-orange-700 dark:bg-orange-500/20 dark:text-orange-400',
    closed: true,
  },
  rejected_by_staff: {
    labelKey: 'state.rejected_by_staff',
    chip: 'bg-ink-200 text-ink-600 dark:bg-white/10 dark:text-ink-300',
    closed: true,
  },
  cancelled: {
    labelKey: 'state.cancelled',
    chip: 'bg-ink-200 text-ink-600 dark:bg-white/10 dark:text-ink-300',
    closed: true,
  },
}

const FALLBACK: CandidateStateStyle = {
  labelKey: 'state.unknown',
  chip: 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300',
  closed: true,
}

export const stateOf = (c: SlotCandidate): CandidateStateStyle =>
  CANDIDATE_STATE[c.candidateState] ?? FALLBACK

/** Hồ sơ đã bị nhân sự loại — trạng thái cuối, không thao tác gì thêm được. */
export const isRejected = (c: SlotCandidate) => c.candidateState === 'rejected_by_staff'

/**
 * Dời lịch CHỈ dành cho ứng viên đã chủ động báo bận (ADR-059): họ đã phản hồi và nêu lý do nên
 * nhân sự biết đường xếp ca khác. Người chưa phản hồi thì chưa có gì để dời; người không tham dự
 * thì hồ sơ đã dừng lại. Server chặn y hệt — đây chỉ là lớp giao diện.
 */
export const canReschedule = (c: SlotCandidate) => c.candidateState === 'declined_by_candidate'

/**
 * Chọn được để thao tác hàng loạt. CỐ Ý bao gồm cả người đã báo bận / quá hạn: dời lịch cho họ
 * chính là công dụng chính của nút "Dời lịch".
 */
export const isSelectable = (c: SlotCandidate) => !isRejected(c)

/** Cần cấp mã: lịch còn hiệu lực và chưa có mã nào còn hạn. */
export const needsCode = (c: SlotCandidate) => !stateOf(c).closed && !c.interviewCode

/** Khoá i18n cho nhãn khối lý do bên dưới dòng ứng viên. */
export function declineReasonLabelKey(c: SlotCandidate): string | null {
  switch (c.candidateState) {
    case 'rejected_by_staff':
      return 'candidate.reasonRejected'
    case 'declined_by_candidate':
      return 'candidate.reasonDeclined'
    case 'expired_no_response':
      // Không lặp lại nguyên câu "[Hệ thống] …" của backend — nói thẳng chuyện gì đã xảy ra.
      return null
    default:
      return null
  }
}
