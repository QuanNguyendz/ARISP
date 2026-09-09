import { useEffect, useState } from 'react'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { User, Bell, Lock, Loader2, AlertCircle, CheckCircle2 } from 'lucide-react'
import { PageHeader, PasswordToggle } from '@ari/shared/ui'
import { useAuthStore } from '@ari/shared/store/auth'
import {
  profileService,
  type StaffProfile,
  type StaffSettings,
} from '@/fservices/profile/profileService'

type TabId = 'profile' | 'notifications' | 'security'

/**
 * Màn Cài đặt dùng chung cho HR Lead và Recruiter — trước đây là hai file 190 dòng giống hệt
 * nhau, và ở cả hai thì tab Hồ sơ vẽ cứng tên/email mẫu còn tab Bảo mật là hai ô mật khẩu
 * không nối đi đâu (nút "Đổi mật khẩu" không có onClick). Chỉ tab Thông báo là chạy thật.
 */
export default function StaffSettingsView() {
  const { t } = useTranslation('modules/shared/settings')
  const [activeTab, setActiveTab] = useState<TabId>('profile')

  const [settings, setSettings] = useState<StaffSettings>({ receiveEmail: true, receivePush: true })
  const [loadingSettings, setLoadingSettings] = useState(true)

  const [profile, setProfile] = useState<StaffProfile | null>(null)
  const [fullName, setFullName] = useState('')
  const [department, setDepartment] = useState('')
  const [savingProfile, setSavingProfile] = useState(false)
  const [profileError, setProfileError] = useState('')
  const [profileSaved, setProfileSaved] = useState(false)

  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [showCurrent, setShowCurrent] = useState(false)
  const [showNew, setShowNew] = useState(false)
  const [changingPassword, setChangingPassword] = useState(false)
  const [passwordError, setPasswordError] = useState('')
  const [passwordDone, setPasswordDone] = useState('')

  // Tên hiển thị ở topbar/sidebar đọc từ store — đổi họ tên phải cập nhật luôn, nếu không
  // người dùng lưu xong vẫn thấy tên cũ cho tới lần đăng nhập sau.
  const updateUser = useAuthStore((state) => state.updateUser)

  useEffect(() => {
    let active = true
    profileService
      .getSettings()
      .then((res) => {
        if (!active) return
        setSettings(res)
      })
      .catch(() => {})
      .finally(() => {
        if (active) setLoadingSettings(false)
      })

    profileService
      .getProfile()
      .then((res) => {
        if (!active) return
        setProfile(res)
        setFullName(res.fullName)
        setDepartment(res.department ?? '')
      })
      .catch(() => {
        if (active) setProfileError(t('profile.loadError'))
      })

    return () => {
      active = false
    }
  }, [t])

  const toggleSetting = async (key: keyof StaffSettings) => {
    const previous = settings
    const next = { ...settings, [key]: !settings[key] }
    setSettings(next)
    try {
      await profileService.updateSettings(next)
    } catch {
      setSettings(previous)
    }
  }

  const saveProfile = async () => {
    setProfileError('')
    setProfileSaved(false)
    if (!fullName.trim()) {
      setProfileError(t('profile.nameRequired'))
      return
    }
    setSavingProfile(true)
    try {
      const saved = await profileService.updateProfile({
        fullName: fullName.trim(),
      })
      setProfile(saved)
      setFullName(saved.fullName)
      setDepartment(saved.department ?? '')
      updateUser({ name: saved.fullName })
      setProfileSaved(true)
    } catch (err) {
      setProfileError(err instanceof Error ? err.message : t('profile.saveError'))
    } finally {
      setSavingProfile(false)
    }
  }

  const submitPassword = async () => {
    setPasswordError('')
    setPasswordDone('')
    if (newPassword !== confirmPassword) {
      setPasswordError(t('security.mismatch'))
      return
    }
    setChangingPassword(true)
    try {
      const res = await profileService.changePassword({
        // Tài khoản Google chưa có mật khẩu → không gửi trường này, backend hiểu là đặt lần đầu.
        currentPassword: profile?.hasPassword ? currentPassword : undefined,
        newPassword,
      })
      setPasswordDone(res.message)
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
      setProfile((prev) => (prev ? { ...prev, hasPassword: true } : prev))
    } catch (err) {
      setPasswordError(err instanceof Error ? err.message : t('security.changeError'))
    } finally {
      setChangingPassword(false)
    }
  }

  const tabs: { id: TabId; label: string; icon: typeof User }[] = [
    { id: 'profile', label: t('tabs.profile'), icon: User },
    { id: 'notifications', label: t('tabs.notifications'), icon: Bell },
    { id: 'security', label: t('tabs.security'), icon: Lock },
  ]

  const inputClass =
    'w-full px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder-ink-400 focus:outline-none focus:border-brand-400 transition-colors disabled:opacity-60'

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader title={t('title')} description={t('description')} />

      <div className="flex flex-col gap-4 lg:flex-row lg:gap-8">
        <div className="-mx-4 sm:mx-0 lg:w-64 lg:shrink-0">
          <nav
            aria-label={t('tabsLabel')}
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
                  {t('profile.title')}
                </h3>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('profile.fullName')}
                    </label>
                    <input
                      type="text"
                      value={fullName}
                      onChange={(e) => setFullName(e.target.value)}
                      disabled={!profile}
                      maxLength={120}
                      className={inputClass}
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('profile.department')}
                    </label>
                    {/* CHỈ ĐỌC (ADR-065). Trước đây ô này sửa được, nên ô "đội" khoá cứng trên phiếu
                        yêu cầu tuyển dụng chỉ là hình thức: Hiring Manager của đội A vào đây đổi
                        sang đội B rồi quay ra lập phiếu. Đội nay do Super Admin gán. */}
                    <input type="text" value={department || '—'} disabled className={inputClass} />
                    <p className="mt-1.5 text-xs text-ink-400">{t('profile.departmentLocked')}</p>
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('profile.email')}
                    </label>
                    {/* Email là danh tính đăng nhập (khớp allowed_email_domains + tài khoản
                        Google) nên khoá hẳn; đổi được là mở đường chiếm tài khoản. */}
                    <input type="email" value={profile?.email ?? ''} disabled className={inputClass} />
                    <p className="mt-1.5 text-xs text-ink-400">{t('profile.emailLocked')}</p>
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('profile.role')}
                    </label>
                    <input
                      type="text"
                      value={profile ? t(`roles.${profile.role.toLowerCase()}`, profile.role) : ''}
                      disabled
                      className={inputClass}
                    />
                    <p className="mt-1.5 text-xs text-ink-400">{t('profile.roleLocked')}</p>
                  </div>
                </div>

                {profileError && (
                  <div className="flex items-center gap-2 rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 p-3 text-sm text-red-700 dark:text-red-400">
                    <AlertCircle className="h-4 w-4 shrink-0" />
                    {profileError}
                  </div>
                )}
                {profileSaved && (
                  <div className="flex items-center gap-2 rounded-xl border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/10 p-3 text-sm text-emerald-700 dark:text-emerald-400">
                    <CheckCircle2 className="h-4 w-4 shrink-0" />
                    {t('profile.saved')}
                  </div>
                )}

                <button
                  onClick={saveProfile}
                  disabled={savingProfile || !profile}
                  className="inline-flex items-center gap-2 px-6 py-3 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 text-white font-medium hover:opacity-90 transition-opacity disabled:opacity-50"
                >
                  {savingProfile && <Loader2 className="h-4 w-4 animate-spin" />}
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
                  {(
                    [
                      { key: 'receiveEmail' as const, group: 'email' },
                      { key: 'receivePush' as const, group: 'browser' },
                    ]
                  ).map(({ key, group }) => (
                    <div
                      key={key}
                      className="flex items-center justify-between p-4 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5"
                    >
                      <div>
                        <p className="text-sm font-medium text-ink-900 dark:text-white">
                          {t(`notifications.${group}.title`)}
                        </p>
                        <p className="text-xs text-ink-500 dark:text-ink-400">
                          {t(`notifications.${group}.description`)}
                        </p>
                      </div>
                      <button
                        disabled={loadingSettings}
                        onClick={() => toggleSetting(key)}
                        aria-pressed={settings[key]}
                        aria-label={t(`notifications.${group}.title`)}
                        className={`relative w-12 h-6 rounded-full transition-colors disabled:opacity-50 ${settings[key] ? 'bg-brand-600' : 'bg-ink-300 dark:bg-white/20'}`}
                      >
                        <span
                          className={`absolute top-1 w-4 h-4 rounded-full bg-white transition-all ${settings[key] ? 'right-1' : 'left-1'}`}
                        />
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {activeTab === 'security' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
                  {profile && !profile.hasPassword ? t('security.setTitle') : t('security.title')}
                </h3>

                {profile && !profile.hasPassword && (
                  <div className="rounded-xl border border-blue-200 dark:border-blue-500/30 bg-blue-50 dark:bg-blue-500/10 p-3 text-sm text-blue-700 dark:text-blue-400">
                    {t('security.googleOnlyHint')}
                  </div>
                )}

                <div className="space-y-4">
                  {profile?.hasPassword && (
                    <div>
                      <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                        {t('security.currentPassword')}
                      </label>
                      <div className="relative">
                        <input
                          type={showCurrent ? 'text' : 'password'}
                          value={currentPassword}
                          onChange={(e) => setCurrentPassword(e.target.value)}
                          autoComplete="current-password"
                          placeholder="••••••••"
                          className={`${inputClass} pr-12`}
                        />
                        <span className="absolute right-3 top-1/2 -translate-y-1/2">
                          <PasswordToggle
                            visible={showCurrent}
                            onToggle={() => setShowCurrent(!showCurrent)}
                          />
                        </span>
                      </div>
                    </div>
                  )}
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('security.newPassword')}
                    </label>
                    <div className="relative">
                      <input
                        type={showNew ? 'text' : 'password'}
                        value={newPassword}
                        onChange={(e) => setNewPassword(e.target.value)}
                        autoComplete="new-password"
                        placeholder="••••••••"
                        className={`${inputClass} pr-12`}
                      />
                      <span className="absolute right-3 top-1/2 -translate-y-1/2">
                        <PasswordToggle visible={showNew} onToggle={() => setShowNew(!showNew)} />
                      </span>
                    </div>
                    <p className="mt-1.5 text-xs text-ink-400">{t('security.rule')}</p>
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      {t('security.confirmPassword')}
                    </label>
                    <input
                      type={showNew ? 'text' : 'password'}
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      autoComplete="new-password"
                      placeholder="••••••••"
                      className={inputClass}
                    />
                  </div>
                </div>

                {passwordError && (
                  <div className="flex items-center gap-2 rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 p-3 text-sm text-red-700 dark:text-red-400">
                    <AlertCircle className="h-4 w-4 shrink-0" />
                    {passwordError}
                  </div>
                )}
                {passwordDone && (
                  <div className="flex items-center gap-2 rounded-xl border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/10 p-3 text-sm text-emerald-700 dark:text-emerald-400">
                    <CheckCircle2 className="h-4 w-4 shrink-0" />
                    {passwordDone}
                  </div>
                )}

                <button
                  onClick={submitPassword}
                  disabled={
                    changingPassword ||
                    !newPassword ||
                    !confirmPassword ||
                    (Boolean(profile?.hasPassword) && !currentPassword)
                  }
                  className="inline-flex items-center gap-2 px-6 py-3 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 text-white font-medium hover:opacity-90 transition-opacity disabled:opacity-50"
                >
                  {changingPassword && <Loader2 className="h-4 w-4 animate-spin" />}
                  {profile && !profile.hasPassword
                    ? t('security.setPassword')
                    : t('security.changePassword')}
                </button>
              </div>
            )}
          </motion.div>
        </div>
      </div>
    </div>
  )
}
