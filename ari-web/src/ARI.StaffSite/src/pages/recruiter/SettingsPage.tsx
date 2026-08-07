import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { User, Bell, Lock } from 'lucide-react'
import { useState, useEffect } from 'react'
import { PageHeader } from '@ari/shared/ui'
import { profileService, StaffSettings } from '../../fservices/profile/profileService'

export default function RecruiterSettingsPage() {
  const { t } = useTranslation('modules/recruiter/settings')

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
    { id: 'profile', label: t('tabs.profile', 'Hồ sơ'), icon: User },
    { id: 'notifications', label: t('tabs.notifications', 'Thông báo'), icon: Bell },
    { id: 'security', label: t('tabs.security', 'Bảo mật'), icon: Lock },
  ]

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader title={t('title', 'Cài đặt')} description={t('description', 'Quản lý hồ sơ và cài đặt cá nhân')} />

      <div className="flex flex-col gap-4 lg:flex-row lg:gap-8">
        {/* Tabs */}
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
            className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card"
          >
            {activeTab === 'profile' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
                  {t('profile.title', 'Hồ sơ')}
                </h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('profile.fullName', 'Họ tên')}
                    </label>
                    <input
                      type="text"
                      defaultValue="Recruiter"
                      className="w-full px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder-ink-400 focus:outline-none focus:border-brand-400 transition-colors"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('profile.email', 'Email')}
                    </label>
                    <input
                      type="email"
                      defaultValue="recruiter@arisp.com"
                      className="w-full px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder-ink-400 focus:outline-none focus:border-brand-400 transition-colors"
                    />
                  </div>
                </div>
                <button className="px-6 py-3 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 text-white font-medium hover:opacity-90 transition-opacity">
                  {t('profile.saveChanges', 'Lưu thay đổi')}
                </button>
              </div>
            )}

            {activeTab === 'notifications' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
                  {t('notifications.title', 'Cài đặt thông báo')}
                </h3>
                <div className="space-y-4">
                  <div className="flex items-center justify-between p-4 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5">
                    <div>
                      <p className="text-sm font-medium text-ink-900 dark:text-white">
                        {t('notifications.email.title', 'Nhận email thông báo')}
                      </p>
                      <p className="text-xs text-ink-500 dark:text-ink-400">
                        {t('notifications.email.description', 'Nhận thông báo qua email cho các sự kiện quan trọng')}
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
                  {t('security.title', 'Đổi mật khẩu')}
                </h3>
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('security.currentPassword', 'Mật khẩu hiện tại')}
                    </label>
                    <input
                      type="password"
                      placeholder="••••••••"
                      className="w-full px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder-ink-400 focus:outline-none focus:border-brand-400 transition-colors"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('security.newPassword', 'Mật khẩu mới')}
                    </label>
                    <input
                      type="password"
                      placeholder="••••••••"
                      className="w-full px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder-ink-400 focus:outline-none focus:border-brand-400 transition-colors"
                    />
                  </div>
                </div>
                <button className="px-6 py-3 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 text-white font-medium hover:opacity-90 transition-opacity">
                  {t('security.changePassword', 'Đổi mật khẩu')}
                </button>
              </div>
            )}
          </motion.div>
        </div>
      </div>
    </div>
  )
}
