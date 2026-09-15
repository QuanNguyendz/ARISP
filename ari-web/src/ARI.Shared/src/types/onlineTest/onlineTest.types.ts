// Types cho luồng Online Test (thi trắc nghiệm) — khớp DTO backend (ARI.Application/OnlineTest).

export type OnlineTestQuestionType = 'single' | 'multiple'

/** Câu hỏi trắc nghiệm — góc nhìn HR/Recruiter (kèm đáp án đúng). */
export interface OnlineTestQuestion {
  id: string
  questionText: string
  options: string[]
  questionType: OnlineTestQuestionType
  correctOptions: number[]
}

/** Ngân hàng câu hỏi + cấu hình bài thi của một job — góc nhìn HR/Recruiter. */
export interface OnlineTestBank {
  jobPostingId: string
  jobTitle: string
  passScore: number
  questionsPerTest: number
  durationMinutes: number
  questions: OnlineTestQuestion[]
  /**
   * Ngôn ngữ đề thi lấy từ cấu hình vòng trắc nghiệm ('vi' | 'en'); null khi tin chưa có vòng
   * trắc nghiệm. Đề nhập lên phải khớp giá trị này (BE kiểm tra khi import Excel).
   */
  language?: 'vi' | 'en' | null
}

/** Câu hỏi hiển thị cho ứng viên — KHÔNG có đáp án đúng. */
export interface CandidateTestQuestion {
  id: string
  questionText: string
  options: string[]
  questionType: OnlineTestQuestionType
}

/** Đề thi cho ứng viên (đã bốc ngẫu nhiên) + trạng thái đã nộp (nếu có). */
export interface CandidateOnlineTest {
  applicationId: string
  jobPostingId: string
  jobTitle: string
  roundNumber: number
  durationMinutes: number
  totalQuestions: number
  questions: CandidateTestQuestion[]
  alreadySubmitted: boolean
  score: number | null
  isPassed: boolean | null
  submittedAt: string | null
  /** Hồ sơ đã qua vòng duyệt CV chưa — chưa pass thì không được làm bài (chỉ hiện "Chờ duyệt CV"). */
  cvPassed: boolean

  /** Giờ hẹn làm bài; `null` khi nhân sự chưa xếp lịch. */
  opensAt?: string | null
  /** Hết giờ được phép VÀO thi (= `opensAt` + 1 tiếng). */
  closesAt?: string | null
  /** Có được bắt đầu lúc này không — sai thì `questions` về RỖNG từ server. */
  canStart?: boolean
  /**
   * Hết hạn mà ứng viên không vào làm: cửa vào đã đóng khi chưa có bài, hoặc bài hiện có là hệ thống
   * nộp thay. Kiểm TRƯỚC `alreadySubmitted` — bài hệ thống nộp thay không phải "đã nộp bài".
   */
  expired?: boolean
}

/**
 * Biên nhận nộp bài — thứ DUY NHẤT ứng viên nhận lại sau khi bấm nộp.
 *
 * Bài vẫn được chấm ngay và tự động (nhân sự xem được liền), nhưng điểm và kết quả không đi kèm
 * phản hồi này: công bố tại chỗ là công bố trước khi vòng chốt.
 */
export interface OnlineTestSubmitAck {
  submittedAt: string
  totalQuestions: number
}

/** Kết quả chấm bài — chỉ dành cho PHÍA NHÂN SỰ (`getStaffResult`). */
export interface OnlineTestResult {
  score: number
  isPassed: boolean
  passScore: number
  correctCount: number
  totalQuestions: number
  submittedAt: string
  /** Bài do hệ thống nộp thay khi hết hạn — ứng viên không vào làm bài. */
  expired?: boolean
}

/** Một dòng điểm ứng viên trong bảng tổng hợp theo job. */
export interface OnlineTestScoreRow {
  applicationId: string
  candidateName: string
  candidateEmail: string
  roundNumber: number
  score: number
  isPassed: boolean
  correctCount: number
  totalQuestions: number
  submittedAt: string
  /** Số lần ứng viên rời khỏi bài thi (chuyển tab / mất focus) — chống gian lận nhẹ. > 0 → cờ nghi vấn. */
  tabSwitchCount: number
  /** Bài do hệ thống nộp thay khi hết hạn — ứng viên không vào làm bài. */
  expired?: boolean
}

/** Bảng tổng hợp điểm bài trắc nghiệm của toàn bộ ứng viên trong một job. */
export interface OnlineTestJobResults {
  jobPostingId: string
  jobTitle: string
  passScore: number
  totalQuestions: number
  submissionCount: number
  passedCount: number
  notPassedCount: number
  averageScore: number
  highestScore: number
  lowestScore: number
  rows: OnlineTestScoreRow[]
  /**
   * Trong `submissionCount`, bao nhiêu bài là hệ thống nộp thay khi hết hạn. Vẫn tính vào mọi con
   * số (0 điểm, chưa đạt) — con số này cho biết điểm trung bình bị kéo xuống vì người không vào thi.
   */
  expiredCount?: number
}

/** Payload tạo / sửa câu hỏi. */
export interface UpsertOnlineTestQuestion {
  questionText: string
  options: string[]
  questionType: OnlineTestQuestionType
  correctOptions: number[]
}

/** Payload cập nhật cấu hình bài thi. */
export interface OnlineTestSettings {
  passScore: number
  questionsPerTest: number
  durationMinutes: number
}

/** Một dòng lỗi khi import câu hỏi từ Excel. */
export interface OnlineTestImportRowError {
  row: number
  message: string
}

/** Kết quả import ngân hàng câu hỏi từ file Excel. */
export interface OnlineTestImportResult {
  imported: number
  failed: number
  errors: OnlineTestImportRowError[]
}

/**
 * Một câu trong bài làm của ứng viên — kèm ĐÁP ÁN ĐÚNG, nên chỉ có ở đường NHÂN SỰ.
 *
 * `selectedOptions` rỗng nghĩa là **bỏ trắng**, khác với chọn sai — phân biệt được hai thứ này là
 * một nửa lý do màn xem bài tồn tại.
 */
export interface OnlineTestAnswerReviewItem {
  questionId: string
  questionText: string
  options: string[]
  questionType: string
  selectedOptions: number[]
  correctOptions: number[]
  isCorrect: boolean
}

/** Toàn bộ bài làm của một ứng viên — điểm tổng + từng câu. */
export interface OnlineTestAnswerSheet {
  applicationId: string
  candidateName: string
  roundNumber: number
  score: number
  isPassed: boolean
  passScore: number
  correctCount: number
  totalQuestions: number
  submittedAt: string
  /** Số lần rời khỏi bài thi — tín hiệu chống gian lận nhẹ. */
  tabSwitchCount: number
  items: OnlineTestAnswerReviewItem[]
  /** Bài do hệ thống nộp thay khi hết hạn — ứng viên không vào làm, mọi câu đều trống. */
  expired?: boolean
}
