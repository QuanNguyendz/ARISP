import { apiClient } from '../apiClient'

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
    })
    return data
  },

  async changePassword(payload: {
    currentPassword?: string
    newPassword: string
  }): Promise<{ message: string; hasPassword: boolean }> {
    const { data } = await apiClient.post<{ message: string; hasPassword: boolean }>(
      '/portal/profile/change-password',
      payload,
    )
    return data
  },
}
