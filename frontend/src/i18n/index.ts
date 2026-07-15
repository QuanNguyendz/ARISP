import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

import commonVi from './locales/vi/common.json'
import commonEn from './locales/en/common.json'
import authVi from './locales/vi/auth.json'
import authEn from './locales/en/auth.json'
import landingVi from './locales/vi/landing.json'
import landingEn from './locales/en/landing.json'
import jobsVi from './locales/vi/jobs.json'
import jobsEn from './locales/en/jobs.json'
import interviewVi from './locales/vi/interview.json'
import interviewEn from './locales/en/interview.json'
import candidateVi from './locales/vi/candidate.json'
import candidateEn from './locales/en/candidate.json'
import errorsVi from './locales/vi/errors.json'
import errorsEn from './locales/en/errors.json'

export const resources = {
  vi: {
    common: commonVi,
    auth: authVi,
    landing: landingVi,
    jobs: jobsVi,
    interview: interviewVi,
    candidate: candidateVi,
    errors: errorsVi,
  },
  en: {
    common: commonEn,
    auth: authEn,
    landing: landingEn,
    jobs: jobsEn,
    interview: interviewEn,
    candidate: candidateEn,
    errors: errorsEn,
  },
}

export const supportedLanguages = [
  { code: 'vi', name: 'Tiếng Việt', flag: '🇻🇳' },
  { code: 'en', name: 'English', flag: '🇬🇧' },
] as const

export type SupportedLanguage = (typeof supportedLanguages)[number]['code']

export const defaultLanguage: SupportedLanguage = 'vi'

i18n.use(initReactI18next).init({
  resources,
  lng: defaultLanguage,
  fallbackLng: defaultLanguage,
  defaultNS: 'common',
  ns: ['common', 'auth', 'landing', 'jobs', 'interview', 'candidate', 'errors'],
  interpolation: {
    escapeValue: false,
  },
  react: {
    useSuspense: false,
  },
})

export default i18n
