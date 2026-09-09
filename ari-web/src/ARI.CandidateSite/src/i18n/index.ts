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
import candidateOfferVi from './locales/vi/modules/candidate/offer.json'
import candidateOfferEn from './locales/en/modules/candidate/offer.json'
import candidateInterviewScheduleVi from './locales/vi/modules/candidate/interviewSchedule.json'
import candidateInterviewScheduleEn from './locales/en/modules/candidate/interviewSchedule.json'
import candidateNotificationsVi from './locales/vi/modules/candidate/notifications.json'
import candidateNotificationsEn from './locales/en/modules/candidate/notifications.json'
import candidateOnlineTestVi from './locales/vi/modules/candidate/onlineTest.json'
import candidateOnlineTestEn from './locales/en/modules/candidate/onlineTest.json'
import candidatePracticeReviewVi from './locales/vi/modules/candidate/practiceReview.json'
import candidatePracticeReviewEn from './locales/en/modules/candidate/practiceReview.json'
import candidateScheduleVi from './locales/vi/modules/candidate/schedule.json'
import candidateScheduleEn from './locales/en/modules/candidate/schedule.json'
import candidateSettingsVi from './locales/vi/modules/candidate/settings.json'
import candidateSettingsEn from './locales/en/modules/candidate/settings.json'
import kioskVi from './locales/vi/modules/kiosk.json'
import kioskEn from './locales/en/modules/kiosk.json'

// ===== MODULES: LEGAL =====
import legalPrivacyPolicyVi from './locales/vi/modules/legal/privacyPolicy.json'
import legalPrivacyPolicyEn from './locales/en/modules/legal/privacyPolicy.json'
import legalTermsVi from './locales/vi/modules/legal/terms.json'
import legalTermsEn from './locales/en/modules/legal/terms.json'

// ===== MODULES: JOB-BOARD =====
import applyVi from './locales/vi/modules/job-board/apply.json'
import applyEn from './locales/en/modules/job-board/apply.json'

export const resources = {
  vi: {
    ...sharedResources.vi,
    // Interview module
    'modules/interview/room': interviewRoomVi,
    'modules/interview/practice': interviewPracticeVi,
    // Candidate module
    'modules/candidate': candidateVi,
    'modules/candidate/applicationDetail': candidateApplicationDetailVi,
    'modules/candidate/offer': candidateOfferVi,
    'modules/candidate/interviewSchedule': candidateInterviewScheduleVi,
    'modules/candidate/notifications': candidateNotificationsVi,
    'modules/candidate/onlineTest': candidateOnlineTestVi,
    'modules/candidate/practiceReview': candidatePracticeReviewVi,
    'modules/candidate/schedule': candidateScheduleVi,
    'modules/candidate/settings': candidateSettingsVi,
    'modules/kiosk': kioskVi,
    // Legal module
    'modules/legal/privacyPolicy': legalPrivacyPolicyVi,
    'modules/legal/terms': legalTermsVi,
    // Job-Board module
    'modules/job-board/apply': applyVi,
  },
  en: {
    ...sharedResources.en,
    // Interview module
    'modules/interview/room': interviewRoomEn,
    'modules/interview/practice': interviewPracticeEn,
    // Candidate module
    'modules/candidate': candidateEn,
    'modules/candidate/applicationDetail': candidateApplicationDetailEn,
    'modules/candidate/offer': candidateOfferEn,
    'modules/candidate/interviewSchedule': candidateInterviewScheduleEn,
    'modules/candidate/notifications': candidateNotificationsEn,
    'modules/candidate/onlineTest': candidateOnlineTestEn,
    'modules/candidate/practiceReview': candidatePracticeReviewEn,
    'modules/candidate/schedule': candidateScheduleEn,
    'modules/candidate/settings': candidateSettingsEn,
    'modules/kiosk': kioskEn,
    // Legal module
    'modules/legal/privacyPolicy': legalPrivacyPolicyEn,
    'modules/legal/terms': legalTermsEn,
    // Job-Board module
    'modules/job-board/apply': applyEn,
  },
}

// Namespace riêng của site (namespace dùng chung nằm trong sharedNamespaces).
const siteNamespaces = [
  'modules/interview/room',
  'modules/interview/practice',
  'modules/candidate',
  'modules/candidate/applicationDetail',
  'modules/candidate/offer',
  'modules/candidate/interviewSchedule',
  'modules/candidate/notifications',
  'modules/candidate/onlineTest',
  'modules/candidate/practiceReview',
  'modules/candidate/schedule',
  'modules/candidate/settings',
  'modules/kiosk',
  'modules/legal/privacyPolicy',
  'modules/legal/terms',
  'modules/job-board/apply',
]

const allNamespaces = [...sharedNamespaces, ...siteNamespaces]

const i18n = initI18n(resources, allNamespaces)

export { supportedLanguages, defaultLanguage, getStoredLanguage, storeLanguage }
export type { SupportedLanguage }
export default i18n
