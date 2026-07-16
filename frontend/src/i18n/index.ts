import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

// ===== COMMON =====
import commonVi from './locales/vi/common.json'
import commonEn from './locales/en/common.json'

// ===== AUTH =====
import authVi from './locales/vi/auth.json'
import authEn from './locales/en/auth.json'

// ===== ERRORS =====
import errorsVi from './locales/vi/errors.json'
import errorsEn from './locales/en/errors.json'

// ===== LANDING (Job Board) =====
import landingVi from './locales/vi/landing.json'
import landingEn from './locales/en/landing.json'

// ===== JOBS (Job Posting) =====
import jobsVi from './locales/vi/jobs.json'
import jobsEn from './locales/en/jobs.json'

// ===== INTERVIEW =====
import interviewVi from './locales/vi/interview.json'
import interviewEn from './locales/en/interview.json'

// ===== MODULES: CANDIDATE =====
import candidateVi from './locales/vi/modules/candidate/index.json'
import candidateEn from './locales/en/modules/candidate/index.json'

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

export const resources = {
  vi: {
    // Common namespaces (keep existing structure)
    common: commonVi,
    auth: authVi,
    errors: errorsVi,
    landing: landingVi,
    jobs: jobsVi,
    interview: interviewVi,

    // Candidate module
    'modules/candidate': candidateVi,

    // Recruiter module
    'modules/recruiter/dashboard': recruiterDashboardVi,
    'modules/recruiter/jobs': recruiterJobsVi,
    'modules/recruiter/candidates': recruiterCandidatesVi,
    'modules/recruiter/interviews': recruiterInterviewsVi,
    'modules/recruiter/evaluations': recruiterEvaluationsVi,
    'modules/recruiter/settings': recruiterSettingsVi,

    // HR module
    'modules/hr/dashboard': hrDashboardVi,
    'modules/hr/jobs': hrJobsVi,
    'modules/hr/candidates': hrCandidatesVi,
    'modules/hr/evaluations': hrEvaluationsVi,
    'modules/hr/reports': hrReportsVi,
    'modules/hr/playbooks': hrPlaybooksVi,
    'modules/hr/team': hrTeamVi,
    'modules/hr/settings': hrSettingsVi,
  },
  en: {
    // Common namespaces (keep existing structure)
    common: commonEn,
    auth: authEn,
    errors: errorsEn,
    landing: landingEn,
    jobs: jobsEn,
    interview: interviewEn,

    // Candidate module
    'modules/candidate': candidateEn,

    // Recruiter module
    'modules/recruiter/dashboard': recruiterDashboardEn,
    'modules/recruiter/jobs': recruiterJobsEn,
    'modules/recruiter/candidates': recruiterCandidatesEn,
    'modules/recruiter/interviews': recruiterInterviewsEn,
    'modules/recruiter/evaluations': recruiterEvaluationsEn,
    'modules/recruiter/settings': recruiterSettingsEn,

    // HR module
    'modules/hr/dashboard': hrDashboardEn,
    'modules/hr/jobs': hrJobsEn,
    'modules/hr/candidates': hrCandidatesEn,
    'modules/hr/evaluations': hrEvaluationsEn,
    'modules/hr/reports': hrReportsEn,
    'modules/hr/playbooks': hrPlaybooksEn,
    'modules/hr/team': hrTeamEn,
    'modules/hr/settings': hrSettingsEn,
  },
}

export const supportedLanguages = [
  { code: 'vi', name: 'Tiếng Việt', flag: '🇻🇳' },
  { code: 'en', name: 'English', flag: '🇬🇧' },
] as const

export type SupportedLanguage = (typeof supportedLanguages)[number]['code']

export const defaultLanguage: SupportedLanguage = 'vi'

// All namespaces for i18next
const allNamespaces = [
  // Common namespaces (keep existing structure for backward compatibility)
  'common',
  'auth',
  'errors',
  'landing',
  'jobs',
  'interview',
  // Candidate module
  'modules/candidate',
  // Recruiter module
  'modules/recruiter/dashboard',
  'modules/recruiter/jobs',
  'modules/recruiter/candidates',
  'modules/recruiter/interviews',
  'modules/recruiter/evaluations',
  'modules/recruiter/settings',
  // HR module
  'modules/hr/dashboard',
  'modules/hr/jobs',
  'modules/hr/candidates',
  'modules/hr/evaluations',
  'modules/hr/reports',
  'modules/hr/playbooks',
  'modules/hr/team',
  'modules/hr/settings',
]

i18n.use(initReactI18next).init({
  resources,
  lng: defaultLanguage,
  fallbackLng: defaultLanguage,
  defaultNS: 'common',
  ns: allNamespaces,
  interpolation: {
    escapeValue: false,
  },
  react: {
    useSuspense: false,
  },
})

export default i18n
