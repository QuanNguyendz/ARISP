import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { User, Bell, Lock } from 'lucide-react'
import { useState } from 'react'
import { PageHeader } from '@ari/shared/ui'

export default function RecruiterSettingsPage() {
  const { t } = useTranslation('modules/recruiter/settings')

  const [activeTab, setActiveTab] = useState('profile')

  const tabs = [
    { id: 'profile', label: t('tabs.profile'), icon: User },
    { id: 'notifications', label: t('tabs.notifications'), icon: Bell },
    { id: 'security', label: t('tabs.security'), icon: Lock },
  ]

  return (
    <div className="p-6 lg:p-8">
      <PageHeader title={t('title')} description={t('description')} />

      <div className="flex flex-col gap-4 lg:flex-row lg:gap-8">
        <div className="-mx-4 sm:mx-0 lg:w-64 lg:shrink-0">
          <nav
            aria-label="Settings tabs"
            className="flex gap-2 overflow-x-auto px-4 py-1 sm:flex-wrap sm:px-0 lg:flex-col lg:gap-0 lg:overflow-visible lg:rounded-2xl lg:border lg:border-white/[0.08] lg:bg-white/[0.03] lg:p-2 lg:backdrop-blur-xl"
          >
            {tabs.map((tab) => {
              const TabIcon = tab.icon
              return (
                <button
                  key={tab.id}
                  onClick={() => setActiveTab(tab.id)}
                  className={`inline-flex shrink-0 items-center gap-2 whitespace-nowrap rounded-full border px-3.5 py-1.5 text-sm font-medium transition-colors lg:w-full lg:whitespace-normal lg:rounded-xl lg:border-0 lg:px-4 lg:py-3 ${
                    activeTab === tab.id
                      ? 'border-amber-500/40 bg-amber-500/20 text-amber-400'
                      : 'border-white/10 bg-white/5 text-white/50 hover:bg-white/5 hover:text-white'
                  }`}
                >
                  <TabIcon className="h-4 w-4" /> {tab.label}
                </button>
              )
            })}
          </nav>
        </div>

        <div className="flex-1">
          <motion.div
            key={activeTab}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6"
          >
            {activeTab === 'profile' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-white">{t('profile.title')}</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-white/70 mb-2">
                      {t('profile.fullName')}
                    </label>
                    <input
                      type="text"
                      defaultValue="Recruiter"
                      className="w-full px-4 py-3 rounded-xl bg-white/[0.03] border border-white/10 text-white placeholder-white/30 focus:outline-none focus:border-amber-500/50 transition-colors"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-white/70 mb-2">
                      {t('profile.email')}
                    </label>
                    <input
                      type="email"
                      defaultValue="recruiter@arisp.com"
                      className="w-full px-4 py-3 rounded-xl bg-white/[0.03] border border-white/10 text-white placeholder-white/30 focus:outline-none focus:border-amber-500/50 transition-colors"
                    />
                  </div>
                </div>
                <button className="px-6 py-3 rounded-xl bg-gradient-to-r from-amber-500 to-orange text-white font-medium hover:opacity-90 transition-opacity">
                  {t('profile.saveChanges')}
                </button>
              </div>
            )}

            {activeTab === 'notifications' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-white">{t('notifications.title')}</h3>
                <div className="space-y-4">
                  <div className="flex items-center justify-between p-4 rounded-xl bg-white/[0.02] border border-white/5">
                    <div>
                      <p className="text-sm font-medium text-white">
                        {t('notifications.email.title')}
                      </p>
                      <p className="text-xs text-white/40">
                        {t('notifications.email.description')}
                      </p>
                    </div>
                    <button className="relative w-12 h-6 rounded-full bg-amber-500">
                      <span className="absolute right-1 top-1 w-4 h-4 rounded-full bg-white transition-transform" />
                    </button>
                  </div>
                </div>
              </div>
            )}

            {activeTab === 'security' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-white">{t('security.title')}</h3>
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-medium text-white/70 mb-2">
                      {t('security.currentPassword')}
                    </label>
                    <input
                      type="password"
                      placeholder="••••••••"
                      className="w-full px-4 py-3 rounded-xl bg-white/[0.03] border border-white/10 text-white placeholder-white/30 focus:outline-none focus:border-amber-500/50 transition-colors"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-white/70 mb-2">
                      {t('security.newPassword')}
                    </label>
                    <input
                      type="password"
                      placeholder="••••••••"
                      className="w-full px-4 py-3 rounded-xl bg-white/[0.03] border border-white/10 text-white placeholder-white/30 focus:outline-none focus:border-amber-500/50 transition-colors"
                    />
                  </div>
                </div>
                <button className="px-6 py-3 rounded-xl bg-gradient-to-r from-amber-500 to-orange text-white font-medium hover:opacity-90 transition-opacity">
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
