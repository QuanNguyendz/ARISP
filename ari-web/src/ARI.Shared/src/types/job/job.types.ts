export interface RoundConfig {
  roundNumber: number
  roundType: string // 'screening' | 'technical'
  interviewLanguage?: string
  interviewCodeTtlHours: number
  maxDurationMinutes: number
}

export interface AvailabilitySlot {
  id: string
  jobPostingId?: string
  roundNumber?: number
  startTime: Date | string
  endTime: Date | string
  timezone: string
  /** Số ứng viên tối đa; `null` = KHÔNG giới hạn (vòng trắc nghiệm). */
  capacity: number | null
  bookedCount: number
  /** Còn chỗ trống để ứng viên đặt (không giới hạn thì luôn còn) */
  isAvailable?: boolean
  /** Ứng viên đang giữ chỗ ở ca này — rỗng khi chưa ai đặt (ADR-067). */
  bookings?: SlotBookingBrief[]

  /**
   * Số dòng booking ĐÃ ĐÓNG (báo bận / huỷ) vẫn trỏ vào ca này. Không chiếm chỗ nhưng làm ca
   * **không xoá được** — dòng đó là bằng chứng "ứng viên được mời vào đúng giờ đó rồi báo bận".
   */
  closedBookingCount?: number
  /**
   * Chính các dòng đã đóng đó — của AI, và vì sao. Một con số trần kiểu "2 lượt đã đóng" không trả
   * lời được câu người vận hành hỏi ngay khi nhìn thấy nó: lượt gì, của ai?
   */
  closedBookings?: SlotClosedBooking[]
}

/** Một dòng booking đã đóng (không giữ chỗ) còn trỏ vào ca. */
export interface SlotClosedBooking {
  bookingId: string
  applicationId: string
  candidateName?: string | null
  candidateEmail?: string | null
  /** declined_by_candidate | no_show | rejected_by_staff | expired_no_response | cancelled */
  state: string
  /** Lý do ứng viên tự nhập khi từ chối, hoặc ghi chú của hệ thống. */
  reason?: string | null
}

/** Ứng viên đang giữ một chỗ trong ca — vừa đủ để nhận ra người đó trên màn lịch. */
export interface SlotBookingBrief {
  bookingId: string
  applicationId: string
  candidateName?: string | null
  candidateEmail?: string | null
  /** pending | confirmed | declined — ứng viên đã xác nhận giờ hẹn chưa (ADR-048). */
  confirmationStatus: string
}

export interface JobPosting {
  id: string
  createdByUserId: string
  title: string
  department?: string
  jobDescription: string
  jdFileUrl?: string
  jdFileName?: string
  jdFileFormat?: string
  interviewMode: 'remote' | 'onsite' | 'both'
  status: 'draft' | 'pending' | 'active' | 'paused' | 'rejected' | 'closed' | 'archived'
  isPublicListing: boolean
  detectedLanguage?: string
  languageRequirement?: string
  languageConfirmed: boolean
  roundConfigs: RoundConfig[]
  createdAt: string
  location?: string
  workMode?: string
  salaryMin?: number
  salaryMax?: number
  salaryCurrency?: string
  salaryIsNegotiable: boolean
  employmentType?: string
  experienceLevel?: string
  skills: string[]
  jobCategory?: string
  applicationDeadline?: string
  isUrgent: boolean
  /** Số lượng cần tuyển (chỉ tiêu). Null/0 = không giới hạn. */
  vacancies?: number | null
  scoringRubric?: unknown
  /**
   * Điểm sàn để AI kết luận "đạt" ở buổi phỏng vấn (0–100, mặc định 70) — ADR-060.
   * Chỉ áp dụng khi tin đã khai bộ tiêu chí chấm phỏng vấn trong Playbook.
   */
  interviewPassScore?: number
  rescheduleDeadlineHours?: number
  inviteTokenTtlHours?: number
  /** Số ứng viên đã ứng tuyển — trả về từ GET /jobs/admin */
  applicantCount?: number
  publishedAt?: string
  /** Tên người tạo tin (Recruiter/HR) — trả về từ GET /jobs/admin */
  createdByName?: string
  /** Lý do từ chối (khi status = 'rejected') */
  rejectionReason?: string
  /** ===== Phê duyệt của HR Leader ===== */
  approvedByUserId?: string
  approvedAt?: string
  /** Tên người duyệt (snapshot) */
  approverName?: string
  /** URL file JD đã đóng dấu duyệt (đã resolve) — chỉ có khi đã duyệt & JD là PDF */
  signedJdFileUrl?: string

  /**
   * ===== Cổng của Hiring Manager (ADR-061) =====
   * Server CHỈ điền các trường này cho nhân sự nội bộ. Ứng viên xem cùng một tin trên Job Board
   * nhận về `undefined` — nên đừng dựa vào chúng để dựng gì ở CandidateSite.
   */
  /**
   * pending | approved | rejected | bypassed. Không có = tin chưa từng gửi Hiring Manager ký.
   * `rejected` = HM yêu cầu sửa → tin đồng thời về `status = 'rejected'` để Recruiter sửa và gửi lại (ADR-068).
   */
  hmSignOffStatus?: HmSignOffStatus | null
  /** Góp ý của HM khi yêu cầu sửa, hoặc lý do quản trị viên đăng vượt cổng — nội bộ, không cho ứng viên xem. */
  hmSignOffReason?: string | null
  /**
   * Vị trí Hiring Manager chính (ADR-068 — mọi tin luôn có một người): `active`, hoặc `inactive` (bị khoá /
   * đổi vai trò → HR Leader cần chuyển), hoặc `missing` (tin cũ chưa gán → HR Leader cần gán). Khác `active`
   * thì mọi cổng duyệt của tin đang ĐÓNG.
   */
  hiringManagerState?: HiringManagerState | null
  hiringManagerUserId?: string | null
  hiringManagerName?: string | null
  /** Phiếu yêu cầu tuyển dụng của tin — để mở lại trình soạn JD khi HM yêu cầu sửa. Chỉ nhân sự nhận được. */
  recruitmentRequestId?: string | null
  /** Tin đã có bộ tiêu chí chấm CV chưa (ADR-070) — thiếu thì không gửi duyệt / đăng được. Chỉ nhân sự nhận được. */
  hasCvRubric?: boolean | null
  /**
   * Ngân hàng đề trắc nghiệm so với số câu mỗi bài — `questionCount < questionsPerTest` thì không gửi duyệt /
   * đăng được. Null khi tin không có vòng trắc nghiệm. Chỉ nhân sự nhận được.
   */
  onlineTestBank?: OnlineTestBankStatus | null
}

export interface OnlineTestBankStatus {
  questionCount: number
  questionsPerTest: number
}

export type HmSignOffStatus = 'pending' | 'approved' | 'rejected' | 'bypassed'
export type HiringManagerState = 'active' | 'inactive' | 'missing'

export interface CreateJobPostingRequest {
  /**
   * Phiếu yêu cầu tuyển dụng đã được HR Leader duyệt (ADR-063). BẮT BUỘC khi tạo tin mới —
   * server từ chối tin không gắn phiếu. Bỏ trống khi sửa tin đã có.
   */
  recruitmentRequestId?: string

  title: string
  department?: string
  jobDescription: string
  jdFileUrl?: string
  jdFileName?: string
  jdFileFormat?: string
  interviewMode: 'remote' | 'onsite' | 'both'
  isPublicListing: boolean
  languageRequirement?: string
  /**
   * Điểm sàn để AI kết luận "đạt" ở buổi phỏng vấn (0–100, mặc định 70) — ADR-060.
   * Chỉ áp dụng khi tin đã khai bộ tiêu chí chấm phỏng vấn trong Playbook.
   */
  interviewPassScore?: number
  rescheduleDeadlineHours?: number
  inviteTokenTtlHours?: number
  roundConfigs: RoundConfig[]
  location?: string
  workMode?: string
  salaryMin?: number
  salaryMax?: number
  salaryCurrency?: string
  salaryIsNegotiable: boolean
  employmentType?: string
  experienceLevel?: string
  skills: string[]
  jobCategory?: string
  applicationDeadline?: string
  isUrgent: boolean
  vacancies?: number | null
  personaName?: string
  personaVoiceId?: string
  personaStyle?: string
}

/** Kết quả phân tích JD (POST /jobs/analyze-jd) — auto-fill form + metadata file đã lưu. */
export interface AnalyzeJdResult {
  /** true = AI không phân tích được (lỗi/quá thời gian) — KHÁC với "đọc được nhưng không phải JD". */
  analysisFailed?: boolean
  /** true = PDF không rút được chữ nào (bản scan / xuất từ slide ảnh). */
  scannedPdf?: boolean
  isValidJd: boolean
  /** storageKey — gửi NGUYÊN TRẠNG trong payload tạo tin, không dùng để hiển thị. */
  jdFileUrl: string
  /** URL xem được trên trình duyệt (presigned với R2). Chỉ dùng để mở file, không gửi lên lại. */
  jdFileViewUrl?: string
  jdFileName: string
  jdFileFormat: string
  title?: string
  department?: string
  jobDescription?: string
  jobCategory?: string
  experienceLevel?: string
  employmentType?: string
  workMode?: string
  location?: string
  skills: string[]
  languageRequirement?: string
  /** Ngôn ngữ nên dùng cho các vòng phỏng vấn AI (`vi` | `en`), AI đọc từ JD — điền sẵn ô ngôn ngữ từng vòng. */
  interviewLanguage?: 'vi' | 'en' | null
  salaryMin?: number
  salaryMax?: number
}
