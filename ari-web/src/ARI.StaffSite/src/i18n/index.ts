import {
  initI18n,
  supportedLanguages,
  defaultLanguage,
  getStoredLanguage,
  storeLanguage,
  sharedResources,
  sharedNamespaces,
  type SupportedLanguage,
} from '@ari/shared/i18n'

// ===== MODULES: HR =====
import hrDashboardVi from './locales/vi/modules/hr/dashboard.json'
import hrDashboardEn from './locales/en/modules/hr/dashboard.json'
import hrJobsVi from './locales/vi/modules/hr/jobs.json'
import hrJobsEn from './locales/en/modules/hr/jobs.json'
import hrCandidatesVi from './locales/vi/modules/hr/candidates.json'
import hrCandidatesEn from './locales/en/modules/hr/candidates.json'
import hrEvaluationsVi from './locales/vi/modules/hr/evaluations.json'
import hrEvaluationsEn from './locales/en/modules/hr/evaluations.json'
import hrReportsVi from './locales/vi/modules/hr/reports.json'
import hrReportsEn from './locales/en/modules/hr/reports.json'
import hmDashboardVi from './locales/vi/modules/hm/dashboard.json'
import hmDashboardEn from './locales/en/modules/hm/dashboard.json'
import hmJobsVi from './locales/vi/modules/hm/jobs.json'
import hmJobsEn from './locales/en/modules/hm/jobs.json'
import hmInterviewRoomsVi from './locales/vi/modules/hm/interviewRooms.json'
import hmInterviewRoomsEn from './locales/en/modules/hm/interviewRooms.json'
import hmCandidateDetailVi from './locales/vi/modules/hm/candidateDetail.json'
import hmCandidateDetailEn from './locales/en/modules/hm/candidateDetail.json'
import sharedSettingsVi from './locales/vi/modules/shared/settings.json'
import sharedSettingsEn from './locales/en/modules/shared/settings.json'
import sharedHelpVi from './locales/vi/modules/shared/help.json'
import sharedHelpEn from './locales/en/modules/shared/help.json'
import hrPlaybooksVi from './locales/vi/modules/hr/playbooks.json'
import hrPlaybooksEn from './locales/en/modules/hr/playbooks.json'
import hrRecruitersVi from './locales/vi/modules/hr/recruiters.json'
import hrRecruitersEn from './locales/en/modules/hr/recruiters.json'
import hrTeamVi from './locales/vi/modules/hr/team.json'
import hrTeamEn from './locales/en/modules/hr/team.json'
import hrCandidateDetailVi from './locales/vi/modules/hr/candidateDetail.json'
import hrCandidateDetailEn from './locales/en/modules/hr/candidateDetail.json'
import staffInterviewsVi from './locales/vi/modules/staff/interviews.json'
import staffInterviewsEn from './locales/en/modules/staff/interviews.json'
import staffRecruitmentRequestsVi from './locales/vi/modules/staff/recruitmentRequests.json'
import staffRecruitmentRequestsEn from './locales/en/modules/staff/recruitmentRequests.json'
import staffJdComposerVi from './locales/vi/modules/staff/jdComposer.json'
import staffJdComposerEn from './locales/en/modules/staff/jdComposer.json'
import hrJdTemplateVi from './locales/vi/modules/hr/jdTemplate.json'
import hrJdTemplateEn from './locales/en/modules/hr/jdTemplate.json'
import staffHiringVi from './locales/vi/modules/staff/hiring.json'
import staffHiringEn from './locales/en/modules/staff/hiring.json'
import staffOffersVi from './locales/vi/modules/staff/offers.json'
import staffOffersEn from './locales/en/modules/staff/offers.json'
import hrJobPostingDetailVi from './locales/vi/modules/hr/jobPostingDetail.json'
import hrJobPostingDetailEn from './locales/en/modules/hr/jobPostingDetail.json'

// ===== MODULES: RECRUITER =====
import recruiterDashboardVi from './locales/vi/modules/recruiter/dashboard.json'
import recruiterDashboardEn from './locales/en/modules/recruiter/dashboard.json'
import recruiterJobsVi from './locales/vi/modules/recruiter/jobs.json'
import recruiterJobsEn from './locales/en/modules/recruiter/jobs.json'
import recruiterJobDetailVi from './locales/vi/modules/recruiter/jobDetail.json'
import recruiterJobDetailEn from './locales/en/modules/recruiter/jobDetail.json'
import recruiterCandidateDetailVi from './locales/vi/modules/recruiter/candidateDetail.json'
import recruiterCandidateDetailEn from './locales/en/modules/recruiter/candidateDetail.json'
import recruiterScheduleConfigVi from './locales/vi/modules/recruiter/scheduleConfig.json'
import recruiterScheduleConfigEn from './locales/en/modules/recruiter/scheduleConfig.json'
import recruiterCreateJobVi from './locales/vi/modules/recruiter/createJob.json'
import recruiterCreateJobEn from './locales/en/modules/recruiter/createJob.json'
import recruiterOnlineTestVi from './locales/vi/modules/recruiter/onlineTest.json'
import recruiterOnlineTestEn from './locales/en/modules/recruiter/onlineTest.json'

// ===== MODULES: SUPER-ADMIN =====
import superAdminDashboardVi from './locales/vi/modules/super-admin/dashboard.json'
import superAdminDashboardEn from './locales/en/modules/super-admin/dashboard.json'
import superAdminSettingsVi from './locales/vi/modules/super-admin/settings.json'
import superAdminSettingsEn from './locales/en/modules/super-admin/settings.json'
import superAdminUsersVi from './locales/vi/modules/super-admin/users.json'
import superAdminUsersEn from './locales/en/modules/super-admin/users.json'
import superAdminPendingUsersVi from './locales/vi/modules/super-admin/pendingUsers.json'
import superAdminPendingUsersEn from './locales/en/modules/super-admin/pendingUsers.json'
import candidatePipelineVi from './locales/vi/modules/staff/candidatePipeline.json'
import candidatePipelineEn from './locales/en/modules/staff/candidatePipeline.json'
import superAdminDepartmentsVi from './locales/vi/modules/super-admin/departments.json'
import superAdminDepartmentsEn from './locales/en/modules/super-admin/departments.json'
import superAdminAuditLogsVi from './locales/vi/modules/super-admin/auditLogs.json'
import superAdminAuditLogsEn from './locales/en/modules/super-admin/auditLogs.json'

export const resources = {
  vi: {
    ...sharedResources.vi,
    // Hiring Manager module (ADR-061)
    'modules/hm/dashboard': hmDashboardVi,
    'modules/hm/jobs': hmJobsVi,
    'modules/hm/interviewRooms': hmInterviewRoomsVi,
    'modules/hm/candidateDetail': hmCandidateDetailVi,

    // HR module
    'modules/hr/dashboard': hrDashboardVi,
    'modules/hr/jobs': hrJobsVi,
    'modules/hr/candidates': hrCandidatesVi,
    'modules/hr/evaluations': hrEvaluationsVi,
    'modules/hr/reports': hrReportsVi,
    'modules/shared/settings': sharedSettingsVi,
    'modules/shared/help': sharedHelpVi,
    'modules/hr/playbooks': hrPlaybooksVi,
    'modules/hr/recruiters': hrRecruitersVi,
    'modules/hr/team': hrTeamVi,
    'modules/hr/candidateDetail': hrCandidateDetailVi,
    'modules/staff/interviews': staffInterviewsVi,
    'modules/staff/recruitmentRequests': staffRecruitmentRequestsVi,
    'modules/staff/jdComposer': staffJdComposerVi,
    'modules/hr/jdTemplate': hrJdTemplateVi,
    'modules/staff/hiring': staffHiringVi,
    'modules/staff/offers': staffOffersVi,
    'modules/hr/jobPostingDetail': hrJobPostingDetailVi,
    // Recruiter module
    'modules/recruiter/dashboard': recruiterDashboardVi,
    'modules/recruiter/jobs': recruiterJobsVi,
    'modules/recruiter/jobDetail': recruiterJobDetailVi,
    'modules/recruiter/candidateDetail': recruiterCandidateDetailVi,
    'modules/recruiter/scheduleConfig': recruiterScheduleConfigVi,
    'modules/recruiter/createJob': recruiterCreateJobVi,
    'modules/recruiter/onlineTest': recruiterOnlineTestVi,
    // Super-Admin module
    'modules/super-admin/dashboard': superAdminDashboardVi,
    'modules/super-admin/settings': superAdminSettingsVi,
    'modules/super-admin/users': superAdminUsersVi,
    'modules/super-admin/pendingUsers': superAdminPendingUsersVi,
    'modules/staff/candidatePipeline': candidatePipelineVi,
    'modules/super-admin/departments': superAdminDepartmentsVi,
    'modules/super-admin/auditLogs': superAdminAuditLogsVi,
  },
  en: {
    ...sharedResources.en,
    // Hiring Manager module (ADR-061)
    'modules/hm/dashboard': hmDashboardEn,
    'modules/hm/jobs': hmJobsEn,
    'modules/hm/interviewRooms': hmInterviewRoomsEn,
    'modules/hm/candidateDetail': hmCandidateDetailEn,

    // HR module
    'modules/hr/dashboard': hrDashboardEn,
    'modules/hr/jobs': hrJobsEn,
    'modules/hr/candidates': hrCandidatesEn,
    'modules/hr/evaluations': hrEvaluationsEn,
    'modules/hr/reports': hrReportsEn,
    'modules/shared/settings': sharedSettingsEn,
    'modules/shared/help': sharedHelpEn,
    'modules/hr/playbooks': hrPlaybooksEn,
    'modules/hr/recruiters': hrRecruitersEn,
    'modules/hr/team': hrTeamEn,
    'modules/hr/candidateDetail': hrCandidateDetailEn,
    'modules/staff/interviews': staffInterviewsEn,
    'modules/staff/recruitmentRequests': staffRecruitmentRequestsEn,
    'modules/staff/jdComposer': staffJdComposerEn,
    'modules/hr/jdTemplate': hrJdTemplateEn,
    'modules/staff/hiring': staffHiringEn,
    'modules/staff/offers': staffOffersEn,
    'modules/hr/jobPostingDetail': hrJobPostingDetailEn,
    // Recruiter module
    'modules/recruiter/dashboard': recruiterDashboardEn,
    'modules/recruiter/jobs': recruiterJobsEn,
    'modules/recruiter/jobDetail': recruiterJobDetailEn,
    'modules/recruiter/candidateDetail': recruiterCandidateDetailEn,
    'modules/recruiter/scheduleConfig': recruiterScheduleConfigEn,
    'modules/recruiter/createJob': recruiterCreateJobEn,
    'modules/recruiter/onlineTest': recruiterOnlineTestEn,
    // Super-Admin module
    'modules/super-admin/dashboard': superAdminDashboardEn,
    'modules/super-admin/settings': superAdminSettingsEn,
    'modules/super-admin/users': superAdminUsersEn,
    'modules/super-admin/pendingUsers': superAdminPendingUsersEn,
    'modules/staff/candidatePipeline': candidatePipelineEn,
    'modules/super-admin/departments': superAdminDepartmentsEn,
    'modules/super-admin/auditLogs': superAdminAuditLogsEn,
  },
}

// Namespace riêng của StaffSite (namespace dùng chung nằm trong sharedNamespaces).
const siteNamespaces = [
  'modules/hm/dashboard',
  'modules/hm/jobs',
  'modules/hm/interviewRooms',
  'modules/hm/candidateDetail',
  'modules/hr/dashboard',
  'modules/hr/jobs',
  'modules/hr/candidates',
  'modules/hr/evaluations',
  'modules/hr/reports',
  'modules/shared/settings',
  'modules/shared/help',
  'modules/hr/playbooks',
  'modules/hr/recruiters',
  'modules/hr/team',
  'modules/hr/candidateDetail',
  'modules/staff/interviews',
  'modules/staff/recruitmentRequests',
  'modules/staff/jdComposer',
  'modules/hr/jdTemplate',
  'modules/staff/hiring',
  'modules/staff/offers',
  'modules/hr/jobPostingDetail',
  'modules/recruiter/dashboard',
  'modules/recruiter/jobs',
  'modules/recruiter/jobDetail',
  'modules/recruiter/candidateDetail',
  'modules/recruiter/scheduleConfig',
  'modules/recruiter/createJob',
  'modules/recruiter/onlineTest',
  'modules/super-admin/dashboard',
  'modules/super-admin/settings',
  'modules/super-admin/users',
  'modules/super-admin/pendingUsers',
  'modules/staff/candidatePipeline',
  'modules/super-admin/departments',
  'modules/super-admin/auditLogs',
]

const allNamespaces = [...sharedNamespaces, ...siteNamespaces]

const i18n = initI18n(resources, allNamespaces)

export { supportedLanguages, defaultLanguage, getStoredLanguage, storeLanguage }
export type { SupportedLanguage }
export default i18n
