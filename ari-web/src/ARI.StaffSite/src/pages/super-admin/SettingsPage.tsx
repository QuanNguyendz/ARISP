import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { motion } from 'framer-motion'
import { Shield, Webhook, Plus, X, Save, Loader2, CheckCircle2, Info } from 'lucide-react'
import { PageHeader, ErrorAlert } from '@ari/shared/ui'
import { adminService, type SystemSettingItem } from '@/fservices/admin'
import { SettingsSkeleton } from './_skeletons'

type TabId = 'auth' | 'integrations'

const SETTING_KEYS = {
  allowedEmailDomains: 'allowed_email_domains',
  atsWebhookUrl: 'ats_webhook_url',
  atsWebhookSecret: 'ats_webhook_secret',
  slackWebhookUrl: 'slack_webhook_url',
  teamsWebhookUrl: 'teams_webhook_url',
} as const

export default function SuperAdminSettingsPage() {
  const { t } = useTranslation('modules/super-admin/settings')
  const [tab, setTab] = useState<TabId>('auth')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [saved, setSaved] = useState(false)

  const [domains, setDomains] = useState<string[]>([])
  const [domainInput, setDomainInput] = useState('')
  const [values, setValues] = useState<Record<string, string>>({})

  useEffect(() => {
    (async () => {
      setLoading(true)
      setError('')
      try {
        const settings = await adminService.getSettings()
        const map: Record<string, string> = {}
        settings.forEach((s) => (map[s.key] = s.value))
        setValues(map)
        setDomains(
          (map[SETTING_KEYS.allowedEmailDomains] || '')
            .split(',')
            .map((d) => d.trim())
            .filter(Boolean)
        )
      } catch (e: any) {
        setError(e?.response?.data?.message || t('errors.loadFailed'))
      } finally {
        setLoading(false)
      }
    })()
  }, [t])

  const addDomain = () => {
    const d = domainInput.trim().toLowerCase().replace(/^@/, '')
    if (!d) return
    if (!/^[a-z0-9.-]+\.[a-z]{2,}$/.test(d)) {
      setError(t('auth.invalidDomain', { domain: d }))
      return
    }
    if (!domains.includes(d)) setDomains([...domains, d])
    setDomainInput('')
    setError('')
  }

  const buildPayload = (): SystemSettingItem[] => {
    const items: SystemSettingItem[] = [
      {
        key: SETTING_KEYS.allowedEmailDomains,
        value: domains.join(','),
        description: t('auth.description'),
      },
    ]
    ;[
      SETTING_KEYS.atsWebhookUrl,
      SETTING_KEYS.atsWebhookSecret,
      SETTING_KEYS.slackWebhookUrl,
      SETTING_KEYS.teamsWebhookUrl,
    ].forEach((k) => {
      items.push({
        key: k,
        value: values[k] || '',
        description: t(`integrations.descriptions.${k}`),
      })
    })
    return items
  }

  const handleSave = async () => {
    setSaving(true)
    setError('')
    setSaved(false)
    try {
      await adminService.updateSettings(buildPayload())
      setSaved(true)
      setTimeout(() => setSaved(false), 2500)
    } catch (e: any) {
      setError(e?.response?.data?.message || t('errors.saveFailed'))
    } finally {
      setSaving(false)
    }
  }

  const tabs = [
    { id: 'auth' as const, label: t('tabs.auth'), icon: Shield },
    { id: 'integrations' as const, label: t('tabs.integrations'), icon: Webhook },
  ]

  const inputClass =
    'w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400'

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title={t('title')}
        description={t('description')}
        actions={[
          {
            label: saving ? t('saving') : t('saveChanges'),
            onClick: handleSave,
            variant: 'primary',
            icon: saving ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Save className="h-4 w-4" />
            ),
          },
        ]}
      />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}
      {saved && (
        <div className="mb-6 flex items-center gap-2 rounded-xl border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/10 px-4 py-3 text-sm text-emerald-700 dark:text-emerald-400">
          <CheckCircle2 className="h-4 w-4" /> {t('saveSuccess')}
        </div>
      )}

      {loading ? (
        <SettingsSkeleton />
      ) : (
        <div className="flex flex-col gap-6 lg:flex-row lg:gap-8">
          {/* Tabs — horizontal scroll trên mobile, sidebar dọc từ lg */}
          <div className="-mx-4 sm:mx-0 lg:w-64 lg:shrink-0">
            <nav
              aria-label="Settings tabs"
              className="flex gap-2 overflow-x-auto px-4 py-1 sm:flex-wrap sm:px-0 lg:flex-col lg:gap-0 lg:overflow-visible lg:rounded-2xl lg:border lg:border-ink-200 lg:bg-white lg:p-2 lg:shadow-card dark:lg:border-white/10 dark:lg:bg-white/5"
            >
              {tabs.map((tb) => {
                const TabIcon = tb.icon
                return (
                  <button
                    key={tb.id}
                    onClick={() => setTab(tb.id)}
                    className={`inline-flex shrink-0 items-center gap-2 whitespace-nowrap rounded-full border px-3.5 py-1.5 text-sm font-medium transition-colors lg:w-full lg:whitespace-normal lg:rounded-xl lg:border-0 lg:px-4 lg:py-3 ${
                      tab === tb.id
                        ? 'border-brand-200 bg-brand-50 text-brand-700 dark:border-brand-500/30 dark:bg-brand-500/20 dark:text-brand-400'
                        : 'border-ink-200 bg-white text-ink-600 hover:bg-ink-50 dark:border-white/10 dark:bg-white/5 dark:text-ink-400 dark:hover:bg-white/10'
                    }`}
                  >
                    <TabIcon className="h-4 w-4" /> {tb.label}
                  </button>
                )
              })}
            </nav>
          </div>

          {/* Content */}
          <motion.div
            key={tab}
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            className="flex-1 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
          >
            {tab === 'auth' && (
              <div className="space-y-5">
                <div>
                  <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
                    {t('auth.title')}
                  </h3>
                  <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">
                    {t('auth.description')}
                  </p>
                </div>

                <div className="flex gap-2.5 rounded-xl border border-brand-200 bg-brand-50 p-3.5 text-xs leading-relaxed text-ink-600 dark:border-brand-500/30 dark:bg-brand-500/10 dark:text-ink-300">
                  <Info className="mt-0.5 h-4 w-4 shrink-0 text-brand-600 dark:text-brand-400" />
                  <div className="space-y-1">
                    <p>
                      {t('auth.infoLine1')}{' '}
                      <b className="text-ink-800 dark:text-ink-100">{t('auth.infoHighlight')}</b>{' '}
                      {t('auth.infoLine1End')}
                    </p>
                    <p>{t('auth.infoLine2')}</p>
                  </div>
                </div>

                <div className="flex gap-2">
                  <input
                    value={domainInput}
                    onChange={(e) => setDomainInput(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') {
                        e.preventDefault()
                        addDomain()
                      }
                    }}
                    placeholder={t('auth.placeholder')}
                    className={inputClass}
                  />
                  <button
                    onClick={addDomain}
                    className="flex shrink-0 items-center gap-1.5 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-brand-700"
                  >
                    <Plus className="h-4 w-4" /> {t('auth.addButton')}
                  </button>
                </div>

                {domains.length === 0 ? (
                  <p className="rounded-xl border border-dashed border-ink-200 dark:border-white/10 px-4 py-6 text-center text-sm text-ink-400">
                    {t('auth.empty')}
                  </p>
                ) : (
                  <div className="flex flex-wrap gap-2">
                    {domains.map((d) => (
                      <span
                        key={d}
                        className="inline-flex items-center gap-2 rounded-full border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 px-3 py-1.5 text-sm text-ink-700 dark:text-ink-200"
                      >
                        @{d}
                        <button
                          onClick={() => setDomains(domains.filter((x) => x !== d))}
                          className="text-ink-400 hover:text-red-500"
                          aria-label={t('auth.removeDomainAria', { domain: d })}
                        >
                          <X className="h-3.5 w-3.5" />
                        </button>
                      </span>
                    ))}
                  </div>
                )}
              </div>
            )}

            {tab === 'integrations' && (
              <div className="space-y-6">
                <div>
                  <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
                    {t('integrations.title')}
                  </h3>
                  <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">
                    {t('integrations.description')}
                  </p>
                </div>

                {[
                  {
                    key: SETTING_KEYS.atsWebhookUrl,
                    label: t('integrations.atsWebhookUrl'),
                    placeholder: t('integrations.placeholders.atsWebhookUrl'),
                  },
                  {
                    key: SETTING_KEYS.atsWebhookSecret,
                    label: t('integrations.atsWebhookSecret'),
                    placeholder: t('integrations.placeholders.atsWebhookSecret'),
                    type: 'password',
                  },
                  {
                    key: SETTING_KEYS.slackWebhookUrl,
                    label: t('integrations.slackWebhookUrl'),
                    placeholder: t('integrations.placeholders.slackWebhookUrl'),
                  },
                  {
                    key: SETTING_KEYS.teamsWebhookUrl,
                    label: t('integrations.teamsWebhookUrl'),
                    placeholder: t('integrations.placeholders.teamsWebhookUrl'),
                  },
                ].map((f) => (
                  <div key={f.key}>
                    <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">
                      {f.label}
                    </label>
                    <input
                      type={f.type || 'text'}
                      value={values[f.key] || ''}
                      onChange={(e) => setValues({ ...values, [f.key]: e.target.value })}
                      placeholder={f.placeholder}
                      className={inputClass}
                    />
                    <p className="mt-1 text-xs text-ink-400">
                      {t(`integrations.descriptions.${f.key}`)}
                    </p>
                  </div>
                ))}
              </div>
            )}
          </motion.div>
        </div>
      )}
    </div>
  )
}
