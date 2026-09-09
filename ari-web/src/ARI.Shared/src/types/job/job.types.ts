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
  capacity: number
  bookedCount: number
  /** Còn chỗ trống để ứng viên đặt (BookedCount < Capacity) */
  isAvailable?: boolean
  /** Ứng viên đang giữ chỗ ở ca này — rỗng khi chưa ai đặt (ADR-067). */
  bookings?: SlotBookingBrief[]
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
  /** pending | approved | rejected. Không có = tin chưa gán Hiring Manager, không có cổng nào. */
  hmSignOffStatus?: string | null
  /** Góp ý của Hiring Manager khi yêu cầu sửa mô tả công việc — nội bộ, không cho ứng viên xem. */
  hmSignOffReason?: string | null
  /** Tin có cổng duyệt của Hiring Manager không (suy ra từ đội tuyển dụng, không phải cột bật/tắt). */
  requiresHmApproval?: boolean
  hiringManagerUserId?: string | null
  hiringManagerName?: string | null
}

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
  salaryMin?: number
  salaryMax?: number
}
