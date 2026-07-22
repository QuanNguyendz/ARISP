import i18n, { type Resource } from 'i18next'
import { initReactI18next } from 'react-i18next'

export const supportedLanguages = [
  { code: 'vi', name: 'Tiếng Việt', flag: '🇻🇳' },
  { code: 'en', name: 'English', flag: '🇬🇧' },
] as const

export type SupportedLanguage = (typeof supportedLanguages)[number]['code']

export const defaultLanguage: SupportedLanguage = 'vi'

const STORAGE_KEY = 'arisp-language'

export function getStoredLanguage(): SupportedLanguage {
  if (typeof window === 'undefined') return defaultLanguage
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored === 'vi' || stored === 'en') return stored
  } catch {
    // localStorage unavailable
  }
  return defaultLanguage
}

export function storeLanguage(lang: SupportedLanguage): void {
  if (typeof window === 'undefined') return
  try {
    localStorage.setItem(STORAGE_KEY, lang)
  } catch {
    // localStorage unavailable
  }
}

/**
 * Khởi tạo i18next một lần cho mỗi site. Mỗi site truyền `resources` đã merge
 * (sharedResources + namespace riêng của site) và danh sách `namespaces`.
 */
export function initI18n(resources: Resource, namespaces: string[]): typeof i18n {
  i18n.use(initReactI18next).init({
    resources,
    lng: getStoredLanguage(),
    fallbackLng: defaultLanguage,
    defaultNS: 'common',
    ns: namespaces,
    interpolation: {
      escapeValue: false,
    },
    react: {
      useSuspense: false,
    },
  })
  return i18n
}

export default i18n
