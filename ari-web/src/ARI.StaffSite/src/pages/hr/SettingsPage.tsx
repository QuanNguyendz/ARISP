import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { User, Bell, Lock } from 'lucide-react'
import { useState, useEffect } from 'react'
import { PageHeader } from '@ari/shared/ui'
import { profileService, StaffSettings } from '../../fservices/profile/profileService'

export default function HrSettingsPage() {
  const { t } = useTranslation('modules/hr/settings')

  const [activeTab, setActiveTab] = useState('profile')
  const [settings, setSettings] = useState<StaffSettings>({ receiveEmail: true, receivePush: true })
  const [loadingSettings, setLoadingSettings] = useState(true)

  useEffect(() => {
    profileService.getSettings().then((res) => {
      setSettings(res)
      setLoadingSettings(false)
    })
  }, [])

  const toggleSetting = async (key: keyof StaffSettings) => {
    const newSettings = { ...settings, [key]: !settings[key] }
    setSettings(newSettings)
    try {
      await profileService.updateSettings(newSettings)
    } catch {
      setSettings(settings) // revert on fail
    }
  }

  const tabs = [
    { id: 'profile', label: t('tabs.profile'), icon: User },
    { id: 'notifications', label: t('tabs.notifications'), icon: Bell },
    { id: 'security', label: t('tabs.security'), icon: Lock },
  ]

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader title={t('title')} description={t('description')} />

      <div className="flex flex-col lg:flex-row gap-8">
        {/* Tabs Sidebar */}
        <div className="lg:w-64 shrink-0">
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-2 shadow-card">
            {tabs.map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium transition-all ${
                  activeTab === tab.id
                    ? 'bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400'
                    : 'text-ink-600 dark:text-ink-400 hover:bg-ink-50 dark:hover:bg-white/5'
                }`}
              >
                <tab.icon className="w-5 h-5" />
                {tab.label}
              </button>
            ))}
          </div>
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
                    <button 
                      disabled={loadingSettings}
                      onClick={() => toggleSetting('receiveEmail')}
                      className={`relative w-12 h-6 rounded-full transition-colors ${settings.receiveEmail ? 'bg-brand-600' : 'bg-ink-300 dark:bg-white/20'}`}
                    >
                      <span className={`absolute top-1 w-4 h-4 rounded-full bg-white transition-transform ${settings.receiveEmail ? 'right-1' : 'left-1'}`} />
                    </button>
                  </div>
                  <div className="flex items-center justify-between p-4 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5">
                    <div>
                      <p className="text-sm font-medium text-ink-900 dark:text-white">
                        {t('notifications.browser.title', 'Nhận thông báo hệ thống')}
                      </p>
                      <p className="text-xs text-ink-500 dark:text-ink-400">
                        {t('notifications.browser.description', 'Nhận thông báo trên biểu tượng chuông của hệ thống')}
                      </p>
                    </div>
                    <button 
                      disabled={loadingSettings}
                      onClick={() => toggleSetting('receivePush')}
                      className={`relative w-12 h-6 rounded-full transition-colors ${settings.receivePush ? 'bg-brand-600' : 'bg-ink-300 dark:bg-white/20'}`}
                    >
                      <span className={`absolute top-1 w-4 h-4 rounded-full bg-white transition-transform ${settings.receivePush ? 'right-1' : 'left-1'}`} />
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
