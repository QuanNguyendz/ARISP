import { useState, useRef, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Globe, Check } from 'lucide-react'
import { supportedLanguages, SupportedLanguage, storeLanguage } from '@ari/shared/i18n'

export function LanguageSwitcher() {
  const { t, i18n } = useTranslation('modules/shared/layout')
  const [isOpen, setIsOpen] = useState(false)
  const dropdownRef = useRef<HTMLDivElement>(null)

  // Short code display like VI/EN
  const shortCode = i18n.language.toUpperCase().slice(0, 2)

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false)
      }
    }

    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  const changeLanguage = (langCode: SupportedLanguage) => {
    storeLanguage(langCode)
    i18n.changeLanguage(langCode)
    setIsOpen(false)
  }

  return (
    <div className="relative" ref={dropdownRef}>
      <button
        onClick={() => setIsOpen(!isOpen)}
        aria-label={t('changeLanguage')}
        className="flex items-center gap-1.5 h-10 px-3 rounded-xl text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10 transition-colors"
      >
        <Globe className="w-4 h-4" />
        <span className="text-sm font-medium">{shortCode}</span>
      </button>

      {isOpen && (
        <div className="absolute right-0 mt-2 w-48 py-2 bg-white dark:bg-ink-900 rounded-xl shadow-lg border border-ink-200 dark:border-white/10 z-50">
          {supportedLanguages.map((lang) => (
            <button
              key={lang.code}
              onClick={() => changeLanguage(lang.code)}
              className={`w-full flex items-center justify-between px-4 py-2.5 text-sm transition-colors ${
                i18n.language === lang.code
                  ? 'text-brand-600 dark:text-brand-400 font-medium'
                  : 'text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/5'
              }`}
            >
              <span className="flex items-center gap-2">
                <span>{lang.flag}</span>
                <span>{lang.name}</span>
              </span>
              {i18n.language === lang.code && (
                <Check className="w-4 h-4 text-brand-500 dark:text-brand-400" />
              )}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
