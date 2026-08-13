import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  Palette,
  Bell,
  Lock,
  MonitorSmartphone,
  Sun,
  Moon,
  Monitor,
  ChevronRight,
  Check,
  Laptop,
  Loader2,
  Download,
} from 'lucide-react'
import { useAuthStore } from '@ari/shared/store/auth'
import { authService } from '@ari/shared/fservices/auth'
import { settingsService } from '@/fservices/settings/settingsService'
import type { CandidateSettings, NotificationChannelPref } from '@/fservices/settings/settingsService'
import { Skeleton } from '@ari/shared/ui/Skeleton'
import Select from '@ari/shared/ui/Select'

type ThemeMode = 'light' | 'dark' | 'system'

function currentThemeMode(): ThemeMode {
  const t = localStorage.getItem('theme')
  if (t === 'dark') return 'dark'
  if (t === 'light') return 'light'
  return 'system'
}

function applyTheme(mode: ThemeMode) {
  const root = document.documentElement
  if (mode === 'dark') {
    root.classList.add('dark')
    localStorage.setItem('theme', 'dark')
  } else if (mode === 'light') {
    root.classList.remove('dark')
    localStorage.setItem('theme', 'light')
  } else {
    localStorage.removeItem('theme')
    root.classList.toggle('dark', window.matchMedia('(prefers-color-scheme: dark)').matches)
  }
}

const SECTION_NAV = [
  { id: 'appearance', labelKey: 'settings.nav.appearance', Icon: Palette },
  { id: 'notifications', labelKey: 'settings.nav.notifications', Icon: Bell },
  { id: 'privacy', labelKey: 'settings.nav.privacy', Icon: Lock },
  { id: 'sessions', labelKey: 'settings.nav.sessions', Icon: MonitorSmartphone },
]

function Toggle({ on, onChange }: { on: boolean; onChange: (v: boolean) => void }) {
  return (
    <button
      type="button"
      onClick={() => onChange(!on)}
      className={`relative h-6 w-11 shrink-0 rounded-full transition ${on ? 'bg-brand-600' : 'bg-ink-200'}`}
    >
      <span
        className={`absolute left-0.5 top-0.5 h-5 w-5 rounded-full bg-white shadow transition ${
          on ? 'translate-x-5' : ''
        }`}
      />
    </button>
  )
}

export default function SettingsPage() {
  const { t } = useTranslation('modules/candidate/settings')
  const navigate = useNavigate()
  const { logout } = useAuthStore()
  const [settings, setSettings] = useState<CandidateSettings | null>(null)
  const [loading, setLoading] = useState(true)
  const [theme, setTheme] = useState<ThemeMode>(currentThemeMode)
  const [active, setActive] = useState('appearance')
  const [saving, setSaving] = useState(false)
  const [exporting, setExporting] = useState(false)
  const [loggingOut, setLoggingOut] = useState(false)

  useEffect(() => {
    let on = true
    settingsService
      .get()
      .then((d) => on && setSettings(d))
      .catch(() => {})
      .finally(() => on && setLoading(false))
    return () => {
      on = false
    }
  }, [])

  const save = async (next: CandidateSettings) => {
    setSettings(next)
    setSaving(true)
    try {
      await settingsService.update(next)
    } catch {
      /* nuốt lỗi — cập nhật lạc quan */
    } finally {
      setSaving(false)
    }
  }

  const setChannel = (key: keyof CandidateSettings, channel: keyof NotificationChannelPref, v: boolean) => {
    if (!settings) return
    const pref = settings[key] as NotificationChannelPref
    save({ ...settings, [key]: { ...pref, [channel]: v } })
  }

  const onTheme = (mode: ThemeMode) => {
    setTheme(mode)
    applyTheme(mode)
  }

  const onExport = async () => {
    setExporting(true)
    try {
      await settingsService.exportData()
    } catch {
      /* ignore */
    } finally {
      setExporting(false)
    }
  }

  const onLogoutAll = async () => {
    if (!window.confirm(t('settings.sessions.logoutAllConfirm'))) return
    setLoggingOut(true)
    try {
      await settingsService.logoutAllDevices()
      await authService.logout().catch(() => {})
    } catch {
      /* ignore */
    } finally {
      logout()
      navigate('/auth/candidate-login')
    }
  }

  const themeOptions = [
    { mode: 'light' as const, label: t('settings.appearance.themeOptions.light'), Icon: Sun, iconCls: 'text-amber-500', bar: 'from-ink-50 to-white ring-ink-200' },
    { mode: 'dark' as const, label: t('settings.appearance.themeOptions.dark'), Icon: Moon, iconCls: 'text-brand-500', bar: 'from-ink-900 to-ink-700 ring-ink-700' },
    { mode: 'system' as const, label: t('settings.appearance.themeOptions.system'), Icon: Monitor, iconCls: 'text-ink-500', bar: 'from-white to-ink-900 ring-ink-200' },
  ]

  const notifRows = [
    { key: 'interviewInvite' as const, title: t('settings.notifications.types.interviewInvite'), desc: t('settings.notifications.types.interviewInviteDesc') },
    { key: 'result' as const, title: t('settings.notifications.types.result'), desc: t('settings.notifications.types.resultDesc') },
    { key: 'applicationUpdate' as const, title: t('settings.notifications.types.applicationUpdate'), desc: t('settings.notifications.types.applicationUpdateDesc') },
    { key: 'jobSuggestion' as const, title: t('settings.notifications.types.jobSuggestion'), desc: t('settings.notifications.types.jobSuggestionDesc') },
  ]

  return (
    <div className="mx-auto max-w-6xl px-6 py-6">
      <div className="mb-4 flex items-center gap-2 text-sm text-ink-400">
        <Link to="/jobs" className="hover:text-brand-600">
          {t('settings.home')}
        </Link>
        <ChevronRight className="h-4 w-4" />
        <span className="font-medium text-ink-600">{t('settings.title')}</span>
      </div>

      <div className="grid gap-8 lg:grid-cols-[220px_1fr]">
        <aside className="self-start lg:sticky lg:top-24">
          <nav className="rounded-2xl border border-ink-200 bg-white p-2 text-sm shadow-card">
            {SECTION_NAV.map(({ id, labelKey, Icon }) => (
              <a
                key={id}
                href={`#${id}`}
                onClick={() => setActive(id)}
                className={`flex items-center gap-3 rounded-xl px-3 py-2.5 ${
                  active === id
                    ? 'bg-brand-50 font-semibold text-brand-700'
                    : 'text-ink-600 hover:bg-ink-100'
                }`}
              >
                <Icon className={`h-4 w-4 ${active === id ? '' : 'text-ink-400'}`} /> {t(labelKey)}
              </a>
            ))}
          </nav>
        </aside>

        <div className="space-y-6">
          <h1 className="font-display text-2xl font-extrabold text-ink-900">{t('settings.title')}</h1>

          {/* Appearance */}
          <section
            id="appearance"
            className="scroll-mt-24 rounded-2xl border border-ink-200 bg-white p-4 shadow-card sm:p-6"
          >
            <h2 className="mb-4 flex items-center gap-2 font-display text-lg font-bold text-ink-900">
              <Palette className="h-5 w-5 text-brand-600" /> {t('settings.appearance.sectionTitle')}
            </h2>

            <div className="mb-5">
              <div className="mb-2 text-sm font-medium text-ink-600">{t('settings.appearance.theme')}</div>
              <div className="grid grid-cols-3 gap-3">
                {themeOptions.map(({ mode, label, Icon, iconCls, bar }) => (
                  <button
                    key={mode}
                    onClick={() => onTheme(mode)}
                    className={`rounded-xl border-2 p-3 text-left ${
                      theme === mode
                        ? 'border-brand-500 ring-2 ring-brand-100'
                        : 'border-ink-200 hover:border-brand-300'
                    }`}
                  >
                    <div className={`mb-2 h-10 rounded-lg bg-gradient-to-br ring-1 ${bar}`} />
                    <span className="flex items-center gap-1.5 text-sm font-semibold text-ink-800">
                      <Icon className={`h-4 w-4 ${iconCls}`} /> {label}
                    </span>
                  </button>
                ))}
              </div>
            </div>

            <div className="flex items-center justify-between border-t border-ink-100 pt-4">
              <div>
                <div className="text-sm font-semibold text-ink-800">{t('settings.appearance.displayLanguage')}</div>
                <div className="text-xs text-ink-400">
                  {t('settings.appearance.displayLanguageHint')}
                </div>
              </div>
              <Select
                value={settings?.language ?? 'vi'}
                disabled={!settings}
                onChange={(v) => settings && save({ ...settings, language: v })}
                options={[
                  { value: 'vi', label: 'Tiếng Việt' },
                  { value: 'en', label: 'English' },
                ]}
                align="right"
                className="min-w-[9rem]"
              />
            </div>
          </section>

          {/* Notifications */}
          <section
            id="notifications"
            className="scroll-mt-24 rounded-2xl border border-ink-200 bg-white p-4 shadow-card sm:p-6"
          >
            <h2 className="mb-1 flex items-center gap-2 font-display text-lg font-bold text-ink-900">
              <Bell className="h-5 w-5 text-brand-600" /> {t('settings.notifications.sectionTitle')}
            </h2>
            <p className="mb-4 text-sm text-ink-400">{t('settings.notifications.sectionDesc')}</p>

            <div className="overflow-hidden rounded-xl border border-ink-200">
              <div className="grid grid-cols-[1fr_auto_auto] items-center gap-4 border-b border-ink-100 bg-ink-50 px-4 py-2.5 text-xs font-semibold text-ink-500">
                <span>{t('settings.notifications.columns.type')}</span>
                <span className="w-12 text-center">{t('settings.notifications.columns.email')}</span>
                <span className="w-12 text-center">{t('settings.notifications.columns.push')}</span>
              </div>
              <div className="divide-y divide-ink-100">
                {loading || !settings
                  ? Array.from({ length: 4 }).map((_, i) => (
                      <div key={i} className="grid grid-cols-[1fr_auto_auto] items-center gap-4 px-4 py-3">
                        <Skeleton className="h-8 w-3/4" />
                        <Skeleton className="h-6 w-11 rounded-full" />
                        <Skeleton className="h-6 w-11 rounded-full" />
                      </div>
                    ))
                  : notifRows.map(({ key, title, desc }) => {
                      const pref = settings[key] as NotificationChannelPref
                      return (
                        <div
                          key={key}
                          className="grid grid-cols-[1fr_auto_auto] items-center gap-4 px-4 py-3"
                        >
                          <div>
                            <div className="text-sm font-medium text-ink-800">{title}</div>
                            <div className="text-xs text-ink-400">{desc}</div>
                          </div>
                          <div className="flex w-12 justify-center">
                            <Toggle on={pref.email} onChange={(v) => setChannel(key, 'email', v)} />
                          </div>
                          <div className="flex w-12 justify-center">
                            <Toggle on={pref.push} onChange={(v) => setChannel(key, 'push', v)} />
                          </div>
                        </div>
                      )
                    })}
              </div>
            </div>
          </section>

          {/* Privacy */}
          <section
            id="privacy"
            className="scroll-mt-24 rounded-2xl border border-ink-200 bg-white p-4 shadow-card sm:p-6"
          >
            <h2 className="mb-4 flex items-center gap-2 font-display text-lg font-bold text-ink-900">
              <Lock className="h-5 w-5 text-brand-600" /> {t('settings.privacy.sectionTitle')}
            </h2>
            <div className="divide-y divide-ink-100">
              <div className="flex items-center justify-between py-3">
                <div className="pr-4">
                  <div className="text-sm font-semibold text-ink-800">{t('settings.privacy.allowHrViewProfile')}</div>
                  <div className="text-xs text-ink-400">
                    {t('settings.privacy.allowHrViewProfileDesc')}
                  </div>
                </div>
                <Toggle
                  on={settings?.allowHrViewProfile ?? true}
                  onChange={(v) => settings && save({ ...settings, allowHrViewProfile: v })}
                />
              </div>
              <div className="flex items-center justify-between py-3">
                <div className="pr-4">
                  <div className="text-sm font-semibold text-ink-800">{t('settings.privacy.allowRecording')}</div>
                  <div className="text-xs text-ink-400">
                    {t('settings.privacy.allowRecordingDesc')}
                  </div>
                </div>
                <Toggle
                  on={settings?.allowRecording ?? true}
                  onChange={(v) => settings && save({ ...settings, allowRecording: v })}
                />
              </div>
              <div className="flex items-center justify-between py-3">
                <div className="pr-4">
                  <div className="text-sm font-semibold text-ink-800">{t('settings.privacy.marketingEmail')}</div>
                  <div className="text-xs text-ink-400">{t('settings.privacy.marketingEmailDesc')}</div>
                </div>
                <Toggle
                  on={settings?.marketingEmail ?? false}
                  onChange={(v) => settings && save({ ...settings, marketingEmail: v })}
                />
              </div>
              <div className="flex items-center justify-between py-3">
                <div className="pr-4">
                  <div className="text-sm font-semibold text-ink-800">{t('settings.privacy.downloadData')}</div>
                  <div className="text-xs text-ink-400">
                    {t('settings.privacy.downloadDataDesc')}
                  </div>
                </div>
                <button
                  onClick={onExport}
                  disabled={exporting}
                  className="inline-flex shrink-0 items-center gap-2 rounded-xl border border-ink-200 px-4 py-2 text-sm font-semibold text-ink-700 hover:bg-ink-50 disabled:opacity-50"
                >
                  {exporting ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <Download className="h-4 w-4" />
                  )}
                  {t('settings.privacy.downloadData')}
                </button>
              </div>
            </div>
          </section>

          {/* Sessions */}
          <section
            id="sessions"
            className="scroll-mt-24 rounded-2xl border border-ink-200 bg-white p-4 shadow-card sm:p-6"
          >
            <h2 className="mb-4 flex items-center gap-2 font-display text-lg font-bold text-ink-900">
              <MonitorSmartphone className="h-5 w-5 text-brand-600" /> {t('settings.sessions.sectionTitle')}
            </h2>
            <div className="space-y-3">
              <div className="flex items-center gap-3 rounded-xl border border-emerald-200 bg-emerald-50/50 p-3">
                <div className="grid h-10 w-10 shrink-0 place-items-center rounded-lg bg-white text-emerald-600 ring-1 ring-emerald-200">
                  <Laptop className="h-5 w-5" />
                </div>
                <div className="min-w-0 flex-1">
                  <div className="text-sm font-semibold text-ink-800">
                    {t('settings.sessions.currentSession')}
                    <span className="ml-1 rounded-full bg-emerald-100 px-2 py-0.5 text-[11px] font-semibold text-emerald-700">
                      {t('settings.sessions.currentSessionBadge')}
                    </span>
                  </div>
                  <div className="text-xs text-ink-400">{t('settings.sessions.status')}</div>
                </div>
              </div>
            </div>
            <button
              onClick={onLogoutAll}
              disabled={loggingOut}
              className="mt-4 inline-flex w-full items-center justify-center gap-2 rounded-xl border border-red-200 px-4 py-2.5 text-sm font-semibold text-red-600 hover:bg-red-50 disabled:opacity-50"
            >
              {loggingOut && <Loader2 className="h-4 w-4 animate-spin" />}
              {t('settings.sessions.logoutAll')}
            </button>
          </section>

          {/* Save bar */}
          <div className="sticky bottom-4 z-10 flex items-center justify-between gap-3 rounded-2xl border border-ink-200 bg-white/90 px-5 py-3 shadow-card backdrop-blur">
            <span className="flex items-center gap-2 text-sm text-ink-500">
              {saving ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin text-brand-500" /> {t('settings.saveBar.saving')}
                </>
              ) : (
                <>
                  <Check className="h-4 w-4 text-emerald-500" /> {t('settings.saveBar.saved')}
                </>
              )}
            </span>
            <Link to="/candidate/profile" className="text-sm font-semibold text-brand-600 hover:underline">
              {t('settings.saveBar.manageAccount')}
            </Link>
          </div>
        </div>
      </div>
    </div>
  )
}
