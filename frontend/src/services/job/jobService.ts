import { apiClient } from '../apiClient';
import type { JobPosting, CreateJobPostingRequest } from '@/types/job';

interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

interface JobPostingFilters {
  status?: JobPosting['status'];
  search?: string;
  page?: number;
  pageSize?: number;
}

export const jobService = {
  async getJobPostings(filters?: JobPostingFilters): Promise<PaginatedResponse<JobPosting>> {
    const { data } = await apiClient.get<PaginatedResponse<JobPosting>>('/job-postings', {
      params: filters,
    });
    return data;
  },

  async getJobPostingById(id: string): Promise<JobPosting> {
    const { data } = await apiClient.get<JobPosting>(`/job-postings/${id}`);
    return data;
  },

  async createJobPosting(request: CreateJobPostingRequest): Promise<JobPosting> {
    const { data } = await apiClient.post<JobPosting>('/job-postings', request);
    return data;
  },

  async updateJobPosting(id: string, request: Partial<CreateJobPostingRequest>): Promise<JobPosting> {
    const { data } = await apiClient.put<JobPosting>(`/job-postings/${id}`, request);
    return data;
  },

  async deleteJobPosting(id: string): Promise<void> {
    await apiClient.delete(`/job-postings/${id}`);
  },

  async publishJobPosting(id: string): Promise<JobPosting> {
    const { data } = await apiClient.post<JobPosting>(`/job-postings/${id}/publish`);
    return data;
  },

  async pauseJobPosting(id: string): Promise<JobPosting> {
    const { data } = await apiClient.post<JobPosting>(`/job-postings/${id}/pause`);
    return data;
  },

  async detectLanguageRequirement(jdText: string): Promise<{ language: string; requirement: string; confidence: number } | null> {
    const { data } = await apiClient.post('/job-postings/detect-language', { jdText });
    return data;
  },

  async getPublicJobPostings(filters?: { search?: string; location?: string; page?: number; pageSize?: number }): Promise<PaginatedResponse<JobPosting>> {
    const { data } = await apiClient.get<PaginatedResponse<JobPosting>>('/public/job-postings', {
      params: filters,
    });
    return data;
  },
};
