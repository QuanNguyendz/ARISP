import { apiClient } from '../apiClient'

// Timeout cho các call có phân tích Gemini đồng bộ (upload CV, CV-match): model có thể
// mất ~18s/lượt + retry backoff khi 503/429, vượt timeout mặc định 30s của apiClient.
const AI_REQUEST_TIMEOUT_MS = 120000

export interface ExperienceItem {
  title: string
  organization: string
  period: string
  description?: string
}

export interface EducationItem {
  school: string
  degree: string
  period: string
  note?: string
}

export interface CvReview {
  isValidCv: boolean
  overallScore: number
  verdict: string
  summary: string
  strengths: string[]
  improvements: string[]
  missingSections: string[]
  reviewedAt?: string | null
  reviewedBy?: string | null
}

export interface CandidateProfile {
  id: string
  email: string
  fullName: string
  headline?: string | null
  phone?: string | null
  location?: string | null
  provinceCode?: number | null
  provinceName?: string | null
  wardCode?: number | null
  wardName?: string | null
  dateOfBirth?: string | null
  about?: string | null
  linkedinUrl?: string | null
  githubUrl?: string | null
  portfolioUrl?: string | null
  profileCvUrl?: string | null
  cvFileName?: string | null
  cvDownloadUrl?: string | null
  emailVerified: boolean
  hasPassword: boolean
  cvReview?: CvReview | null
  skills: string[]
  experience: ExperienceItem[]
  education: EducationItem[]
}

export interface CvUploadResult {
  profileCvUrl: string
  cvFileName?: string | null
  cvDownloadUrl?: string | null
  review: CvReview | null
  aiAvailable: boolean
  aiMessage?: string | null
}

export interface CvMatchAnalysis {
  matchScore: number
  summary: string
  skillsMatched: string[]
  skillsGaps: string[]
  experienceRelevance: string
  overallRecommendation: string
  reviewedBy?: string | null
}

export interface CvMatchResult {
  hasCv: boolean
  cvFileName?: string | null
  cvUrl?: string | null
  cvDownloadUrl?: string | null
  aiAvailable: boolean
  message?: string | null
  analysis?: CvMatchAnalysis | null
  /** none | processing | completed | failed — FE poll tiếp khi "processing". */
  status?: string
}

export interface CandidateProfileUpdate {
  fullName?: string
  headline?: string | null
  phone?: string | null
  provinceCode?: number | null
  provinceName?: string | null
  wardCode?: number | null
  wardName?: string | null
  dateOfBirth?: string | null
  about?: string | null
  linkedinUrl?: string | null
  githubUrl?: string | null
  portfolioUrl?: string | null
  skills?: string[]
  experience?: ExperienceItem[]
  education?: EducationItem[]
}

export const profileService = {
  async getProfile(): Promise<CandidateProfile> {
    const { data } = await apiClient.get<CandidateProfile>('/portal/profile')
    return data
  },

  async updateProfile(payload: CandidateProfileUpdate): Promise<CandidateProfile> {
    const { data } = await apiClient.put<CandidateProfile>('/portal/profile', payload)
    return data
  },

  async uploadCv(file: File): Promise<CvUploadResult> {
    const formData = new FormData()
    formData.append('cvFile', file)
    const { data } = await apiClient.post<CvUploadResult>('/portal/profile/cv', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      // Phân tích Gemini chạy đồng bộ + retry backoff (503/429) → vượt timeout mặc định 30s.
      timeout: AI_REQUEST_TIMEOUT_MS,
    })
    return data
  },

  async changePassword(payload: {
    currentPassword?: string
    newPassword: string
  }): Promise<{ message: string; hasPassword: boolean }> {
    const { data } = await apiClient.post<{ message: string; hasPassword: boolean }>(
      '/portal/profile/change-password',
      payload
    )
    return data
  },

  // Phân tích độ phù hợp CV–JD dùng CV trong hồ sơ ứng viên cho 1 tin tuyển dụng.
  async getCvMatch(jobId: string): Promise<CvMatchResult> {
    const { data } = await apiClient.get<CvMatchResult>(`/portal/jobs/${jobId}/cv-match`, {
      timeout: AI_REQUEST_TIMEOUT_MS,
    })
    return data
  },
}
