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

// ===== MODULES: LEGAL =====
import legalPrivacyPolicyVi from './locales/vi/modules/legal/privacyPolicy.json'
import legalPrivacyPolicyEn from './locales/en/modules/legal/privacyPolicy.json'
import legalTermsVi from './locales/vi/modules/legal/terms.json'
import legalTermsEn from './locales/en/modules/legal/terms.json'

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
    // Legal module
    'modules/legal/privacyPolicy': legalPrivacyPolicyVi,
    'modules/legal/terms': legalTermsVi,
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
    // Legal module
    'modules/legal/privacyPolicy': legalPrivacyPolicyEn,
    'modules/legal/terms': legalTermsEn,
    // Job-Board module
    'modules/job-board/apply': applyEn,
    // Pages (top-level)
    'pages/candidateApply': candidateApplyEn,
  },
}

// Namespace riêng của site (namespace dùng chung nằm trong sharedNamespaces).
const siteNamespaces = [
  'modules/landing/home',
  'modules/interview/room',
  'modules/interview/practice',
  'modules/candidate',
  'modules/candidate/applicationDetail',
  'modules/candidate/interviewSchedule',
  'modules/candidate/notifications',
  'modules/legal/privacyPolicy',
  'modules/legal/terms',
  'modules/job-board/apply',
  'pages/candidateApply',
]

const allNamespaces = [...sharedNamespaces, ...siteNamespaces]

const i18n = initI18n(resources, allNamespaces)

export { supportedLanguages, defaultLanguage, getStoredLanguage, storeLanguage }
export type { SupportedLanguage }
export default i18n
