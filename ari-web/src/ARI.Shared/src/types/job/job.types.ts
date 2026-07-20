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
}

export interface CreateJobPostingRequest {
  title: string
  department?: string
  jobDescription: string
  jdFileUrl?: string
  jdFileName?: string
  jdFileFormat?: string
  interviewMode: 'remote' | 'onsite' | 'both'
  isPublicListing: boolean
  languageRequirement?: string
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
  isValidJd: boolean
  jdFileUrl: string
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
