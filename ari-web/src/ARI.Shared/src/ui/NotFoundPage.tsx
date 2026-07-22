import { useTranslation } from 'react-i18next'

export default function NotFoundPage() {
  const { t } = useTranslation('common')

  return (
    <div className="flex min-h-screen flex-col items-center justify-center p-6 text-center">
      <p className="text-7xl font-bold text-gray-300 md:text-9xl">404</p>
      <h1 className="mt-4 text-2xl font-semibold text-gray-700">{t('notFoundPage.title')}</h1>
      <a
        href="/"
        className="mt-8 inline-flex rounded-xl bg-brand-600 px-5 py-3 text-sm font-semibold text-white transition hover:bg-brand-700"
      >
        {t('notFoundPage.backToHome')}
      </a>
    </div>
  )
}
