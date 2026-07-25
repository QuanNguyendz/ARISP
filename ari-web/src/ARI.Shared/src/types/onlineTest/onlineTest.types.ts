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
  passScore: number
  durationMinutes: number
  totalQuestions: number
  questions: CandidateTestQuestion[]
  alreadySubmitted: boolean
  score: number | null
  isPassed: boolean | null
  submittedAt: string | null
  /** Hồ sơ đã qua vòng duyệt CV chưa — chưa pass thì không được làm bài (chỉ hiện "Chờ duyệt CV"). */
  cvPassed: boolean
}

/** Kết quả chấm bài trắc nghiệm. */
export interface OnlineTestResult {
  score: number
  isPassed: boolean
  passScore: number
  correctCount: number
  totalQuestions: number
  submittedAt: string
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
