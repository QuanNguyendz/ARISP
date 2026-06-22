import { useEffect, useMemo, useState } from 'react'
import { motion } from 'framer-motion'
import { Shield, Webhook, Plus, X, Save, Loader2, CheckCircle2, Info } from 'lucide-react'
import { PageHeader, ErrorAlert } from '@components/shared'
import { adminService, type SystemSettingItem } from '@services/admin'
import { SettingsSkeleton } from './_skeletons'

type TabId = 'auth' | 'integrations'

const SETTING_DESCRIPTIONS: Record<string, string> = {
  allowed_email_domains:
    'Danh sách miền email công ty được phép đăng nhập (Google OAuth + pre-provisioning).',
  ats_webhook_url: 'URL webhook gửi sự kiện tuyển dụng sang hệ thống ATS.',
  ats_webhook_secret: 'Khóa bí mật ký payload webhook ATS.',
  slack_webhook_url: 'Incoming webhook để gửi thông báo tới Slack.',
  teams_webhook_url: 'Incoming webhook để gửi thông báo tới Microsoft Teams.',
}

export default function SuperAdminSettingsPage() {
  const [tab, setTab] = useState<TabId>('auth')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [saved, setSaved] = useState(false)

  const [domains, setDomains] = useState<string[]>([])
  const [domainInput, setDomainInput] = useState('')
  const [values, setValues] = useState<Record<string, string>>({})

  useEffect(() => {
    ;(async () => {
      setLoading(true)
      setError('')
      try {
        const settings = await adminService.getSettings()
        const map: Record<string, string> = {}
        settings.forEach((s) => (map[s.key] = s.value))
        setValues(map)
        setDomains(
          (map['allowed_email_domains'] || '')
            .split(',')
            .map((d) => d.trim())
            .filter(Boolean)
        )
      } catch (e: any) {
        setError(e?.response?.data?.message || 'Không tải được cài đặt hệ thống.')
      } finally {
        setLoading(false)
      }
    })()
  }, [])

  const addDomain = () => {
    const d = domainInput.trim().toLowerCase().replace(/^@/, '')
    if (!d) return
    if (!/^[a-z0-9.-]+\.[a-z]{2,}$/.test(d)) {
      setError(`"${d}" không phải miền hợp lệ.`)
      return
    }
    if (!domains.includes(d)) setDomains([...domains, d])
    setDomainInput('')
    setError('')
  }

  const buildPayload = (): SystemSettingItem[] => {
    const items: SystemSettingItem[] = [
      {
        key: 'allowed_email_domains',
        value: domains.join(','),
        description: SETTING_DESCRIPTIONS['allowed_email_domains'],
      },
    ]
    ;['ats_webhook_url', 'ats_webhook_secret', 'slack_webhook_url', 'teams_webhook_url'].forEach(
      (k) => {
        items.push({ key: k, value: values[k] || '', description: SETTING_DESCRIPTIONS[k] })
      }
    )
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
      setError(e?.response?.data?.message || 'Không thể lưu cài đặt.')
    } finally {
      setSaving(false)
    }
  }

  const tabs = useMemo(
    () => [
      { id: 'auth' as const, label: 'Đăng nhập & miền email', icon: Shield },
      { id: 'integrations' as const, label: 'Tích hợp & Webhook', icon: Webhook },
    ],
    []
  )

  const inputClass =
    'w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400'

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Cài đặt hệ thống"
        description="Cấu hình toàn cục cho nền tảng ARISP (single-tenant)"
        actions={[
          {
            label: saving ? 'Đang lưu...' : 'Lưu thay đổi',
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
          <CheckCircle2 className="h-4 w-4" /> Đã lưu cài đặt hệ thống.
        </div>
      )}

      {loading ? (
        <SettingsSkeleton />
      ) : (
        <div className="flex flex-col gap-6 lg:flex-row lg:gap-8">
          {/* Tabs */}
          <div className="lg:w-64 lg:shrink-0">
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-2 shadow-card">
              {tabs.map((t) => (
                <button
                  key={t.id}
                  onClick={() => setTab(t.id)}
                  className={`flex w-full items-center gap-3 rounded-xl px-4 py-3 text-sm font-medium transition-colors ${
                    tab === t.id
                      ? 'bg-brand-50 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400'
                      : 'text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10'
                  }`}
                >
                  <t.icon className="h-5 w-5" />
                  <span className="text-left">{t.label}</span>
                </button>
              ))}
            </div>
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
                    Miền email được phép
                  </h3>
                  <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">
                    {SETTING_DESCRIPTIONS['allowed_email_domains']}
                  </p>
                </div>

                <div className="flex gap-2.5 rounded-xl border border-brand-200 bg-brand-50 p-3.5 text-xs leading-relaxed text-ink-600 dark:border-brand-500/30 dark:bg-brand-500/10 dark:text-ink-300">
                  <Info className="mt-0.5 h-4 w-4 shrink-0 text-brand-600 dark:text-brand-400" />
                  <div className="space-y-1">
                    <p>
                      Chỉ áp dụng cho{' '}
                      <b className="text-ink-800 dark:text-ink-100">
                        đăng nhập Google của nhân viên
                      </b>{' '}
                      (HR · Recruiter · Super Admin). Đăng nhập bằng email + mật khẩu và tài khoản
                      ứng viên không bị ràng buộc bởi danh sách này.
                    </p>
                    <p>
                      Để trống = cho phép mọi miền. Sau khi qua cửa miền, tài khoản vẫn phải được
                      cấp trước trong hệ thống.
                    </p>
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
                    placeholder="vd: congty.com"
                    className={inputClass}
                  />
                  <button
                    onClick={addDomain}
                    className="flex shrink-0 items-center gap-1.5 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-brand-700"
                  >
                    <Plus className="h-4 w-4" /> Thêm
                  </button>
                </div>

                {domains.length === 0 ? (
                  <p className="rounded-xl border border-dashed border-ink-200 dark:border-white/10 px-4 py-6 text-center text-sm text-ink-400">
                    Chưa có miền nào. Khi trống, mọi miền email sẽ bị chặn đăng nhập qua Google.
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
                          aria-label={`Xóa ${d}`}
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
                    Webhook & tích hợp
                  </h3>
                  <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">
                    Cấu hình điểm gửi sự kiện tới ATS và kênh thông báo nội bộ.
                  </p>
                </div>

                {[
                  {
                    key: 'ats_webhook_url',
                    label: 'ATS Webhook URL',
                    placeholder: 'https://ats.example.com/hooks/arisp',
                  },
                  {
                    key: 'ats_webhook_secret',
                    label: 'ATS Webhook Secret',
                    placeholder: '••••••••',
                    type: 'password',
                  },
                  {
                    key: 'slack_webhook_url',
                    label: 'Slack Incoming Webhook',
                    placeholder: 'https://hooks.slack.com/services/...',
                  },
                  {
                    key: 'teams_webhook_url',
                    label: 'Microsoft Teams Webhook',
                    placeholder: 'https://outlook.office.com/webhook/...',
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
                    <p className="mt-1 text-xs text-ink-400">{SETTING_DESCRIPTIONS[f.key]}</p>
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
