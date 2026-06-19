import { apiClient } from '../apiClient';
import type { JobPosting, CreateJobPostingRequest } from '@/types/job';

export const jobService = {
  // HR Admin: Get all jobs (draft, active, paused, closed)
  async getAdminJobPostings(): Promise<JobPosting[]> {
    const { data } = await apiClient.get<JobPosting[]>('/jobs/admin');
    return data;
  },

  // Public: Get active public jobs
  async getPublicJobPostings(): Promise<JobPosting[]> {
    const { data } = await apiClient.get<JobPosting[]>('/jobs');
    return data;
  },

  // Get job detail by ID (Supports both public candidates and HR staff)
  async getJobPostingById(id: string): Promise<JobPosting> {
    const { data } = await apiClient.get<JobPosting>(`/jobs/${id}`);
    return data;
  },

  // HR Admin: Create a new job posting
  async createJobPosting(request: CreateJobPostingRequest): Promise<JobPosting> {
    const { data } = await apiClient.post<JobPosting>('/jobs', request);
    return data;
  },

  // HR Admin: Update an existing job posting
  async updateJob(id: string, request: CreateJobPostingRequest): Promise<JobPosting> {
    const { data } = await apiClient.put<JobPosting>(`/jobs/${id}`, request);
    return data;
  },

  // HR Admin: Add availability slots for a job posting
  async addAvailabilitySlots(id: string, slots: any[]): Promise<any> {
    const { data } = await apiClient.post(`/jobs/${id}/slots`, slots);
    return data;
  },

  // HR Admin: Approve / reject / change a job posting status.
  // status: 'active' (duyệt) | 'rejected' (từ chối, cần rejectionReason) | 'closed' | 'archived' | 'pending'
  async updateJobStatus(
    id: string,
    status: JobPosting['status'],
    rejectionReason?: string
  ): Promise<void> {
    await apiClient.patch(`/jobs/${id}/status`, { status, rejectionReason });
  }
};
export default jobService;
