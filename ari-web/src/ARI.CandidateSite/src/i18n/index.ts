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

// ===== LANDING (site-specific) =====
import landingHomeVi from './locales/vi/modules/landing/home.json'
import landingHomeEn from './locales/en/modules/landing/home.json'

// ===== INTERVIEW MODULES =====
import interviewRoomVi from './locales/vi/modules/interview/room.json'
import interviewRoomEn from './locales/en/modules/interview/room.json'
import interviewPracticeVi from './locales/vi/modules/interview/practice.json'
import interviewPracticeEn from './locales/en/modules/interview/practice.json'

// ===== MODULES: CANDIDATE =====
import candidateVi from './locales/vi/modules/candidate/index.json'
import candidateEn from './locales/en/modules/candidate/index.json'
import candidateApplicationDetailVi from './locales/vi/modules/candidate/applicationDetail.json'
import candidateApplicationDetailEn from './locales/en/modules/candidate/applicationDetail.json'
import candidateInterviewScheduleVi from './locales/vi/modules/candidate/interviewSchedule.json'
import candidateInterviewScheduleEn from './locales/en/modules/candidate/interviewSchedule.json'
import candidateNotificationsVi from './locales/vi/modules/candidate/notifications.json'
import candidateNotificationsEn from './locales/en/modules/candidate/notifications.json'

// ===== MODULES: RECRUITER =====
import recruiterDashboardVi from './locales/vi/modules/recruiter/dashboard.json'
import recruiterDashboardEn from './locales/en/modules/recruiter/dashboard.json'
import recruiterJobsVi from './locales/vi/modules/recruiter/jobs.json'
import recruiterJobsEn from './locales/en/modules/recruiter/jobs.json'
import recruiterCandidatesVi from './locales/vi/modules/recruiter/candidates.json'
import recruiterCandidatesEn from './locales/en/modules/recruiter/candidates.json'
import recruiterInterviewsVi from './locales/vi/modules/recruiter/interviews.json'
import recruiterInterviewsEn from './locales/en/modules/recruiter/interviews.json'
import recruiterEvaluationsVi from './locales/vi/modules/recruiter/evaluations.json'
import recruiterEvaluationsEn from './locales/en/modules/recruiter/evaluations.json'
import recruiterSettingsVi from './locales/vi/modules/recruiter/settings.json'
import recruiterSettingsEn from './locales/en/modules/recruiter/settings.json'
import recruiterJobDetailVi from './locales/vi/modules/recruiter/jobDetail.json'
import recruiterJobDetailEn from './locales/en/modules/recruiter/jobDetail.json'
import recruiterCandidateDetailVi from './locales/vi/modules/recruiter/candidateDetail.json'
import recruiterCandidateDetailEn from './locales/en/modules/recruiter/candidateDetail.json'
import recruiterInterviewCodeVi from './locales/vi/modules/recruiter/interviewCode.json'
import recruiterInterviewCodeEn from './locales/en/modules/recruiter/interviewCode.json'
import recruiterScheduleConfigVi from './locales/vi/modules/recruiter/scheduleConfig.json'
import recruiterScheduleConfigEn from './locales/en/modules/recruiter/scheduleConfig.json'
import recruiterCreateJobVi from './locales/vi/modules/recruiter/createJob.json'
import recruiterCreateJobEn from './locales/en/modules/recruiter/createJob.json'

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
import hrPlaybooksVi from './locales/vi/modules/hr/playbooks.json'
import hrPlaybooksEn from './locales/en/modules/hr/playbooks.json'
import hrTeamVi from './locales/vi/modules/hr/team.json'
import hrTeamEn from './locales/en/modules/hr/team.json'
import hrSettingsVi from './locales/vi/modules/hr/settings.json'
import hrSettingsEn from './locales/en/modules/hr/settings.json'
import hrCandidateDetailVi from './locales/vi/modules/hr/candidateDetail.json'
import hrCandidateDetailEn from './locales/en/modules/hr/candidateDetail.json'
import hrInterviewSessionsVi from './locales/vi/modules/hr/interviewSessions.json'
import hrInterviewSessionsEn from './locales/en/modules/hr/interviewSessions.json'
import hrJobPostingDetailVi from './locales/vi/modules/hr/jobPostingDetail.json'
import hrJobPostingDetailEn from './locales/en/modules/hr/jobPostingDetail.json'

// ===== MODULES: LEGAL =====
import legalPrivacyPolicyVi from './locales/vi/modules/legal/privacyPolicy.json'
import legalPrivacyPolicyEn from './locales/en/modules/legal/privacyPolicy.json'
import legalTermsVi from './locales/vi/modules/legal/terms.json'
import legalTermsEn from './locales/en/modules/legal/terms.json'

// ===== MODULES: SUPER-ADMIN =====
import superAdminDashboardVi from './locales/vi/modules/super-admin/dashboard.json'
import superAdminDashboardEn from './locales/en/modules/super-admin/dashboard.json'
import superAdminSettingsVi from './locales/vi/modules/super-admin/settings.json'
import superAdminSettingsEn from './locales/en/modules/super-admin/settings.json'
import superAdminUsersVi from './locales/vi/modules/super-admin/users.json'
import superAdminUsersEn from './locales/en/modules/super-admin/users.json'
import superAdminPendingUsersVi from './locales/vi/modules/super-admin/pendingUsers.json'
import superAdminPendingUsersEn from './locales/en/modules/super-admin/pendingUsers.json'
import superAdminAuditLogsVi from './locales/vi/modules/super-admin/auditLogs.json'
import superAdminAuditLogsEn from './locales/en/modules/super-admin/auditLogs.json'

// ===== MODULES: JOB-BOARD =====
import applyVi from './locales/vi/modules/job-board/apply.json'
import applyEn from './locales/en/modules/job-board/apply.json'

// ===== PAGES (top-level page translations) =====
import candidateApplyVi from './locales/vi/pages/candidateApply.json'
import candidateApplyEn from './locales/en/pages/candidateApply.json'

export const resources = {
  vi: {
    ...sharedResources.vi,
    'modules/landing/home': landingHomeVi,

    // Interview module
    'modules/interview/room': interviewRoomVi,
    'modules/interview/practice': interviewPracticeVi,

    // Candidate module
    'modules/candidate': candidateVi,
    'modules/candidate/applicationDetail': candidateApplicationDetailVi,
    'modules/candidate/interviewSchedule': candidateInterviewScheduleVi,
    'modules/candidate/notifications': candidateNotificationsVi,

    // Recruiter module
    'modules/recruiter/dashboard': recruiterDashboardVi,
    'modules/recruiter/jobs': recruiterJobsVi,
    'modules/recruiter/candidates': recruiterCandidatesVi,
    'modules/recruiter/interviews': recruiterInterviewsVi,
    'modules/recruiter/evaluations': recruiterEvaluationsVi,
    'modules/recruiter/settings': recruiterSettingsVi,
    'modules/recruiter/jobDetail': recruiterJobDetailVi,
    'modules/recruiter/candidateDetail': recruiterCandidateDetailVi,
    'modules/recruiter/interviewCode': recruiterInterviewCodeVi,
    'modules/recruiter/scheduleConfig': recruiterScheduleConfigVi,
    'modules/recruiter/createJob': recruiterCreateJobVi,

    // HR module
    'modules/hr/dashboard': hrDashboardVi,
    'modules/hr/jobs': hrJobsVi,
    'modules/hr/candidates': hrCandidatesVi,
    'modules/hr/evaluations': hrEvaluationsVi,
    'modules/hr/reports': hrReportsVi,
    'modules/hr/playbooks': hrPlaybooksVi,
    'modules/hr/team': hrTeamVi,
    'modules/hr/settings': hrSettingsVi,
    'modules/hr/candidateDetail': hrCandidateDetailVi,
    'modules/hr/interviewSessions': hrInterviewSessionsVi,
    'modules/hr/jobPostingDetail': hrJobPostingDetailVi,

    // Legal module
    'modules/legal/privacyPolicy': legalPrivacyPolicyVi,
    'modules/legal/terms': legalTermsVi,

    // Super-Admin module
    'modules/super-admin/dashboard': superAdminDashboardVi,
    'modules/super-admin/settings': superAdminSettingsVi,
    'modules/super-admin/users': superAdminUsersVi,
    'modules/super-admin/pendingUsers': superAdminPendingUsersVi,
    'modules/super-admin/auditLogs': superAdminAuditLogsVi,

    // Job-Board module
    'modules/job-board/apply': applyVi,

    // Pages (top-level)
    'pages/candidateApply': candidateApplyVi,
  },
  en: {
    ...sharedResources.en,
    'modules/landing/home': landingHomeEn,

    // Interview module
    'modules/interview/room': interviewRoomEn,
    'modules/interview/practice': interviewPracticeEn,

    // Candidate module
    'modules/candidate': candidateEn,
    'modules/candidate/applicationDetail': candidateApplicationDetailEn,
    'modules/candidate/interviewSchedule': candidateInterviewScheduleEn,
    'modules/candidate/notifications': candidateNotificationsEn,

    // Recruiter module
    'modules/recruiter/dashboard': recruiterDashboardEn,
    'modules/recruiter/jobs': recruiterJobsEn,
    'modules/recruiter/candidates': recruiterCandidatesEn,
    'modules/recruiter/interviews': recruiterInterviewsEn,
    'modules/recruiter/evaluations': recruiterEvaluationsEn,
    'modules/recruiter/settings': recruiterSettingsEn,
    'modules/recruiter/jobDetail': recruiterJobDetailEn,
    'modules/recruiter/candidateDetail': recruiterCandidateDetailEn,
    'modules/recruiter/interviewCode': recruiterInterviewCodeEn,
    'modules/recruiter/scheduleConfig': recruiterScheduleConfigEn,
    'modules/recruiter/createJob': recruiterCreateJobEn,

    // HR module
    'modules/hr/dashboard': hrDashboardEn,
    'modules/hr/jobs': hrJobsEn,
    'modules/hr/candidates': hrCandidatesEn,
    'modules/hr/evaluations': hrEvaluationsEn,
    'modules/hr/reports': hrReportsEn,
    'modules/hr/playbooks': hrPlaybooksEn,
    'modules/hr/team': hrTeamEn,
    'modules/hr/settings': hrSettingsEn,
    'modules/hr/candidateDetail': hrCandidateDetailEn,
    'modules/hr/interviewSessions': hrInterviewSessionsEn,
    'modules/hr/jobPostingDetail': hrJobPostingDetailEn,

    // Legal module
    'modules/legal/privacyPolicy': legalPrivacyPolicyEn,
    'modules/legal/terms': legalTermsEn,

    // Super-Admin module
    'modules/super-admin/dashboard': superAdminDashboardEn,
    'modules/super-admin/settings': superAdminSettingsEn,
    'modules/super-admin/users': superAdminUsersEn,
    'modules/super-admin/pendingUsers': superAdminPendingUsersEn,
    'modules/super-admin/auditLogs': superAdminAuditLogsEn,

    // Job-Board module
    'modules/job-board/apply': applyEn,

    // Pages (top-level)
    'pages/candidateApply': candidateApplyEn,
  },
}

// Namespace riêng của site (namespace dùng chung nằm trong sharedNamespaces).
const siteNamespaces = [
  'modules/landing/home',
  // Interview module
  'modules/interview/room',
  'modules/interview/practice',
  // Candidate module
  'modules/candidate',
  'modules/candidate/applicationDetail',
  'modules/candidate/interviewSchedule',
  'modules/candidate/notifications',
  // Recruiter module
  'modules/recruiter/dashboard',
  'modules/recruiter/jobs',
  'modules/recruiter/candidates',
  'modules/recruiter/interviews',
  'modules/recruiter/evaluations',
  'modules/recruiter/settings',
  'modules/recruiter/jobDetail',
  'modules/recruiter/candidateDetail',
  'modules/recruiter/interviewCode',
  'modules/recruiter/scheduleConfig',
  'modules/recruiter/createJob',
  // HR module
  'modules/hr/dashboard',
  'modules/hr/jobs',
  'modules/hr/candidates',
  'modules/hr/evaluations',
  'modules/hr/reports',
  'modules/hr/playbooks',
  'modules/hr/team',
  'modules/hr/settings',
  'modules/hr/candidateDetail',
  'modules/hr/interviewSessions',
  'modules/hr/jobPostingDetail',
  // Legal module
  'modules/legal/privacyPolicy',
  'modules/legal/terms',
  // Super-Admin module
  'modules/super-admin/dashboard',
  'modules/super-admin/settings',
  'modules/super-admin/users',
  'modules/super-admin/pendingUsers',
  'modules/super-admin/auditLogs',
  // Job-Board module
  'modules/job-board/apply',
  // Pages (top-level)
  'pages/candidateApply',
]

const allNamespaces = [...sharedNamespaces, ...siteNamespaces]

const i18n = initI18n(resources, allNamespaces)

export { supportedLanguages, defaultLanguage, getStoredLanguage, storeLanguage }
export type { SupportedLanguage }
export default i18n
