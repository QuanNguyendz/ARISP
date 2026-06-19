import { apiClient } from '../apiClient';
import type {
  Application,
  ApplicationDetail,
  CreateApplicationRequest,
  HrApplicationItem,
  MyApplicationItem,
} from '../../types/application';

export const applicationService = {
  // HR/Recruiter: lấy toàn bộ hồ sơ ứng tuyển (GET /applications trả về mảng phẳng)
  async getApplications(): Promise<HrApplicationItem[]> {
    const { data } = await apiClient.get<HrApplicationItem[]>('/applications');
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

  // Candidate: danh sách hồ sơ ứng tuyển của chính mình (GET /api/portal/applications)
  async getMyApplications(): Promise<MyApplicationItem[]> {
    const { data } = await apiClient.get<MyApplicationItem[]>('/portal/applications');
    return data;
  },
};
