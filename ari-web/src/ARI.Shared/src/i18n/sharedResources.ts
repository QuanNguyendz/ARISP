// Namespace dùng chung cho mọi site (CandidateSite, StaffSite).
// Site tự merge object này với namespace riêng rồi gọi initI18n (xem core.ts).

import commonVi from './locales/vi/common.json'
import commonEn from './locales/en/common.json'
import authVi from './locales/vi/auth.json'
import authEn from './locales/en/auth.json'
import errorsVi from './locales/vi/errors.json'
import errorsEn from './locales/en/errors.json'
import jobsVi from './locales/vi/jobs.json'
import jobsEn from './locales/en/jobs.json'
import interviewVi from './locales/vi/interview.json'
import interviewEn from './locales/en/interview.json'
import landingVi from './locales/vi/landing.json'
import landingEn from './locales/en/landing.json'
import sharedNotificationsVi from './locales/vi/modules/shared/notifications.json'
import sharedNotificationsEn from './locales/en/modules/shared/notifications.json'
import sharedLayoutVi from './locales/vi/modules/shared/layout.json'
import sharedLayoutEn from './locales/en/modules/shared/layout.json'
import sharedNavVi from './locales/vi/modules/shared/nav.json'
import sharedNavEn from './locales/en/modules/shared/nav.json'

export const sharedNamespaces = [
  'common',
  'auth',
  'errors',
  'jobs',
  'interview',
  'landing',
  'modules/shared/notifications',
  'modules/shared/layout',
  'modules/shared/nav',
]

export const sharedResources = {
  vi: {
    common: commonVi,
    auth: authVi,
    errors: errorsVi,
    jobs: jobsVi,
    interview: interviewVi,
    landing: landingVi,
    'modules/shared/notifications': sharedNotificationsVi,
    'modules/shared/layout': sharedLayoutVi,
    'modules/shared/nav': sharedNavVi,
  },
  en: {
    common: commonEn,
    auth: authEn,
    errors: errorsEn,
    jobs: jobsEn,
    interview: interviewEn,
    landing: landingEn,
    'modules/shared/notifications': sharedNotificationsEn,
    'modules/shared/layout': sharedLayoutEn,
    'modules/shared/nav': sharedNavEn,
  },
}
