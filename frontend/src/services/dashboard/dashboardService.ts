import { apiClient } from '../apiClient'

export interface FunnelStep {
  label: string
  value: number
}

export interface RecentCandidate {
  id: string
  candidateName: string
  jobTitle?: string
  status: string
  matchScore?: number | null
  latestRound?: number | null
  latestVerdict?: string | null
}

export interface HrDashboard {
  activeJobs: number
  draftJobs: number
  totalApplications: number
  aiInterviews: number
  pendingReviews: number
  hired: number
  funnel: FunnelStep[]
  recentCandidates: RecentCandidate[]
}

export const dashboardService = {
  async getHrOverview(): Promise<HrDashboard> {
    const { data } = await apiClient.get<HrDashboard>('/dashboard/hr')
    return data
  },
}

export default dashboardService
