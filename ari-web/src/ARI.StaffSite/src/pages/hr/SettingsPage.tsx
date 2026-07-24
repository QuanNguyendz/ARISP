import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { User, Bell, Lock } from 'lucide-react'
import { useState } from 'react'
import { PageHeader } from '@ari/shared/ui'

export default function HrSettingsPage() {
  const { t } = useTranslation('modules/hr/settings')

  const [activeTab, setActiveTab] = useState('profile')

  const tabs = [
    { id: 'profile', label: t('tabs.profile'), icon: User },
    { id: 'notifications', label: t('tabs.notifications'), icon: Bell },
    { id: 'security', label: t('tabs.security'), icon: Lock },
  ]

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader title={t('title')} description={t('description')} />

      <div className="flex flex-col gap-4 lg:flex-row lg:gap-8">
        {/* Tabs — horizontal scroll trên mobile, sidebar dọc từ lg */}
        <div className="-mx-4 sm:mx-0 lg:w-64 lg:shrink-0">
          <nav
            aria-label="Settings tabs"
            className="flex gap-2 overflow-x-auto px-4 py-1 sm:flex-wrap sm:px-0 lg:flex-col lg:gap-0 lg:overflow-visible lg:rounded-2xl lg:border lg:border-ink-200 lg:bg-white lg:p-2 lg:shadow-card dark:lg:border-white/10 dark:lg:bg-white/5"
          >
            {tabs.map((tab) => {
              const TabIcon = tab.icon
              return (
                <button
                  key={tab.id}
                  onClick={() => setActiveTab(tab.id)}
                  className={`inline-flex shrink-0 items-center gap-2 whitespace-nowrap rounded-full border px-3.5 py-1.5 text-sm font-medium transition-colors lg:w-full lg:whitespace-normal lg:rounded-xl lg:border-0 lg:px-4 lg:py-3 ${
                    activeTab === tab.id
                      ? 'border-brand-200 bg-brand-100 text-brand-700 dark:border-brand-500/30 dark:bg-brand-500/20 dark:text-brand-400'
                      : 'border-ink-200 bg-white text-ink-600 hover:bg-ink-50 dark:border-white/10 dark:bg-white/5 dark:text-ink-400 dark:hover:bg-white/10'
                  }`}
                >
                  <TabIcon className="h-4 w-4" /> {tab.label}
                </button>
              )
            })}
          </nav>
        </div>

        {/* Content */}
        <div className="flex-1">
          <motion.div
            key={activeTab}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
          >
            {activeTab === 'profile' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
                  {t('profile.title')}
                </h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('profile.fullName')}
                    </label>
                    <input
                      type="text"
                      defaultValue="HR Admin"
                      className="w-full px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder-ink-400 focus:outline-none focus:border-brand-400 transition-colors"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('profile.email')}
                    </label>
                    <input
                      type="email"
                      defaultValue="hr@arisp.com"
                      className="w-full px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder-ink-400 focus:outline-none focus:border-brand-400 transition-colors"
                    />
                  </div>
                </div>
                <button className="px-6 py-3 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 text-white font-medium hover:opacity-90 transition-opacity">
                  {t('profile.saveChanges')}
                </button>
              </div>
            )}

            {activeTab === 'notifications' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
                  {t('notifications.title')}
                </h3>
                <div className="space-y-4">
                  <div className="flex items-center justify-between p-4 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5">
                    <div>
                      <p className="text-sm font-medium text-ink-900 dark:text-white">
                        {t('notifications.email.title')}
                      </p>
                      <p className="text-xs text-ink-500 dark:text-ink-400">
                        {t('notifications.email.description')}
                      </p>
                    </div>
                    <button className="relative w-12 h-6 rounded-full bg-brand-600">
                      <span className="absolute right-1 top-1 w-4 h-4 rounded-full bg-white transition-transform" />
                    </button>
                  </div>
                  <div className="flex items-center justify-between p-4 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5">
                    <div>
                      <p className="text-sm font-medium text-ink-900 dark:text-white">
                        {t('notifications.browser.title')}
                      </p>
                      <p className="text-xs text-ink-500 dark:text-ink-400">
                        {t('notifications.browser.description')}
                      </p>
                    </div>
                    <button className="relative w-12 h-6 rounded-full bg-brand-600">
                      <span className="absolute right-1 top-1 w-4 h-4 rounded-full bg-white transition-transform" />
                    </button>
                  </div>
                </div>
              </div>
            )}

            {activeTab === 'security' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
                  {t('security.title')}
                </h3>
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('security.currentPassword')}
                    </label>
                    <input
                      type="password"
                      placeholder="••••••••"
                      className="w-full px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder-ink-400 focus:outline-none focus:border-brand-400 transition-colors"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('security.newPassword')}
                    </label>
                    <input
                      type="password"
                      placeholder="••••••••"
                      className="w-full px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder-ink-400 focus:outline-none focus:border-brand-400 transition-colors"
                    />
                  </div>
                </div>
                <button className="px-6 py-3 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 text-white font-medium hover:opacity-90 transition-opacity">
                  {t('security.changePassword')}
                </button>
              </div>
            )}
          </motion.div>
        </div>
      </div>
    </div>
  )
}
