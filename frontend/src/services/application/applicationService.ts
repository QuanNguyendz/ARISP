import { apiClient } from '../apiClient';
import type { Application, ApplicationDetail, CreateApplicationRequest } from '../../types/application';

interface ApplicationFilters {
  status?: Application['status'];
  jobPostingId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const applicationService = {
  async getApplications(filters?: ApplicationFilters): Promise<PaginatedResponse<Application>> {
    const { data } = await apiClient.get<PaginatedResponse<Application>>('/applications', {
      params: filters,
    });
    return data;
  },

  async getApplicationById(id: string): Promise<ApplicationDetail> {
    const { data } = await apiClient.get<ApplicationDetail>(`/applications/${id}`);
    return data;
  },

  async createApplication(request: CreateApplicationRequest): Promise<Application> {
    const formData = new FormData();
    formData.append('jobPostingId', request.jobPostingId);
    if (request.cvFile) {
      formData.append('cvFile', request.cvFile);
    }

    const { data } = await apiClient.post<Application>('/applications', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },

  async updateApplicationStatus(id: string, status: Application['status']): Promise<Application> {
    const { data } = await apiClient.patch<Application>(`/applications/${id}/status`, { status });
    return data;
  },

  async sendInvite(applicationId: string): Promise<void> {
    await apiClient.post(`/applications/${applicationId}/send-invite`);
  },

  async hasPracticeSession(applicationId: string): Promise<{ available: boolean }> {
    const { data } = await apiClient.get<{ available: boolean }>(`/applications/${applicationId}/practice`);
    return data;
  },

  async getMyApplications(): Promise<Application[]> {
    const { data } = await apiClient.get<Application[]>('/candidate/applications');
    return data;
  },
};
