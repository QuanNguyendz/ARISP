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

export interface MatchBucket {
  label: string
  count: number
}

export interface TrendPoint {
  label: string
  count: number
}

export interface RecruiterStat {
  name: string
  jobs: number
  applicants: number
  hired: number
}

export interface VacancyJob {
  id: string
  title: string
  vacancies: number
  hired: number
}

export interface HrAnalytics {
  matchBuckets: MatchBucket[]
  analyzedCount: number
  avgMatch: number | null
  trend: TrendPoint[]
  recruiters: RecruiterStat[]
  totalVacancies: number
  totalHired: number
  jobsWithQuota: VacancyJob[]
}

export interface DashboardJob {
  id: string
  title: string
  department?: string | null
  createdByName?: string | null
  applicantCount: number
  status: string
}

export interface PendingJob {
  id: string
  title: string
  createdByName?: string | null
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
  analytics: HrAnalytics
  topJobs: DashboardJob[]
  pendingJobs: PendingJob[]
  pendingJobsCount: number
}

export const dashboardService = {
  async getHrOverview(): Promise<HrDashboard> {
    const { data } = await apiClient.get<HrDashboard>('/dashboard/hr')
    return data
  },
}

export default dashboardService
