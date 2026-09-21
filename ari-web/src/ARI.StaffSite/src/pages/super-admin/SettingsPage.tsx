import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { motion } from 'framer-motion'
import { Shield, MapPin, Plus, X, Save, Loader2, CheckCircle2, Info } from 'lucide-react'
import { PageHeader, ErrorAlert } from '@ari/shared/ui'
import { adminService, type SystemSettingItem } from '@/fservices/admin'
import { SettingsSkeleton } from './_skeletons'
import { resolveApiError } from '@ari/shared/utils/apiError'
import { useDbTableChanged } from '@ari/shared/realtime/dbTableRealtime'

type TabId = 'auth' | 'interview'

const SETTING_KEYS = {
  allowedEmailDomains: 'allowed_email_domains',
  // Địa điểm phỏng vấn — in vào thư mời gửi ứng viên (single-tenant: một văn phòng dùng chung).
  interviewLocationAddress: 'interview_location_address',
  interviewLocationDirections: 'interview_location_directions',
  interviewLocationMapUrl: 'interview_location_map_url',
  // CỐ Ý không có webhook ATS / Slack / Teams: dự án không tích hợp hệ thống ngoài nào. Bốn ô đó
  // từng nằm ở đây nhưng không endpoint nào đọc tới — một màn cấu hình nhận giá trị rồi không dùng
  // còn tệ hơn là không có màn nào, vì nó khiến người cấu hình tin rằng tích hợp đang chạy.
} as const

/** Dấu vân tay của nội dung màn — so với bản đã lưu để biết còn thay đổi chưa lưu không. */
const snapshotOf = (domains: string[], values: Record<string, string>) =>
  JSON.stringify({ domains, values })

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

  /**
   * Bản đã lưu trên server (tên miền + các ô), để biết màn còn thay đổi chưa lưu hay không. Ô gõ tên
   * miền chưa bấm "Thêm" cũng tính là đang sửa dở.
   */
  const [savedSnapshot, setSavedSnapshot] = useState<string | null>(null)
  const isDirty =
    domainInput.trim() !== '' || (savedSnapshot != null && snapshotOf(domains, values) !== savedSnapshot)

  // Đọc lại sau `await` — closure của lượt render cũ không biết người dùng vừa sửa thêm trong lúc chờ.
  const canReplaceRef = useRef(false)
  canReplaceRef.current = !isDirty && !saving

  /** `silent` = nạp lại ngầm do realtime: không thay cả màn bằng khung tải, không đè lỗi lên màn. */
  const load = useCallback(
    async (silent = false) => {
      if (!silent) {
        setLoading(true)
        setError('')
      }
      try {
        const settings = await adminService.getSettings()
        if (silent && !canReplaceRef.current) return
        const map: Record<string, string> = {}
        settings.forEach((s) => (map[s.key] = s.value))
        const nextDomains = (map[SETTING_KEYS.allowedEmailDomains] || '')
          .split(',')
          .map((d) => d.trim())
          .filter(Boolean)
        setValues(map)
        setDomains(nextDomains)
        setSavedSnapshot(snapshotOf(nextDomains, map))
      } catch (e: unknown) {
        if (!silent) setError(resolveApiError(e, t, 'errors.loadFailed'))
      } finally {
        if (!silent) setLoading(false)
      }
    },
    [t]
  )

  useEffect(() => {
    void load()
  }, [load])

  // Super Admin khác vừa lưu cài đặt (ADR-057). Đang sửa dở thì KHÔNG nạp đè — bấm Lưu sau đó sẽ ghi
  // bản của người này, nhưng ít ra không có chữ nào biến mất dưới tay họ.
  useDbTableChanged(['system_settings'], () => {
    if (canReplaceRef.current) void load(true)
  })

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
      SETTING_KEYS.interviewLocationAddress,
      SETTING_KEYS.interviewLocationDirections,
      SETTING_KEYS.interviewLocationMapUrl,
    ].forEach((k) => {
      items.push({
        key: k,
        value: values[k] || '',
        description: t(`interview.descriptions.${k}`),
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
      // Bản vừa lưu thành mốc "đã lưu" mới — không thì màn bị coi là sửa dở mãi và realtime bị chặn.
      setSavedSnapshot(snapshotOf(domains, values))
      setSaved(true)
      setTimeout(() => setSaved(false), 2500)
    } catch (e: any) {
      setError(resolveApiError(e, t, 'errors.saveFailed'))
    } finally {
      setSaving(false)
    }
  }

  const tabs = [
    { id: 'auth' as const, label: t('tabs.auth'), icon: Shield },
    { id: 'interview' as const, label: t('tabs.interview'), icon: MapPin },
  ]

  const inputClass =
    'w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400'

  return (
    <div className="p-4 sm:p-6 lg:p-8">
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
        <div className="flex flex-col gap-4 lg:flex-row lg:gap-8">
          {/* Tabs — pill ngang scroll-snap trên mobile, sidebar dọc từ lg */}
          <div className="-mx-4 sm:mx-0 lg:w-64 lg:shrink-0">
            <nav
              aria-label="Settings tabs"
              className="flex gap-2 overflow-x-auto scroll-px-4 px-4 py-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden lg:flex-col lg:gap-0 lg:overflow-visible lg:rounded-2xl lg:border lg:border-ink-200 lg:bg-white lg:p-2 lg:shadow-card dark:lg:border-white/10 dark:lg:bg-white/5"
            >
              {tabs.map((tb) => {
                const TabIcon = tb.icon
                return (
                  <button
                    key={tb.id}
                    onClick={() => setTab(tb.id)}
                    className={`inline-flex shrink-0 snap-start items-center gap-2 whitespace-nowrap rounded-full border px-3 py-1.5 text-xs font-medium transition-colors sm:px-3.5 sm:text-sm lg:w-full lg:whitespace-normal lg:rounded-xl lg:border-0 lg:px-4 lg:py-3 ${
                      tab === tb.id
                        ? 'border-brand-200 bg-brand-50 text-brand-700 dark:border-brand-500/30 dark:bg-brand-500/20 dark:text-brand-400'
                        : 'border-ink-200 bg-white text-ink-600 hover:bg-ink-50 dark:border-white/10 dark:bg-white/5 dark:text-ink-400 dark:hover:bg-white/10'
                    }`}
                  >
                    <TabIcon className="h-4 w-4" />
                    <span className="truncate max-w-[180px] sm:max-w-none">{tb.label}</span>
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
            className="flex-1 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card"
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

            {tab === 'interview' && (
              <div className="space-y-6">
                <div>
                  <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
                    {t('interview.title')}
                  </h3>
                  <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">
                    {t('interview.description')}
                  </p>
                </div>

                <div>
                  <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">
                    {t('interview.address')}
                  </label>
                  <input
                    type="text"
                    value={values[SETTING_KEYS.interviewLocationAddress] || ''}
                    onChange={(e) =>
                      setValues({
                        ...values,
                        [SETTING_KEYS.interviewLocationAddress]: e.target.value,
                      })
                    }
                    placeholder={t('interview.addressPlaceholder')}
                    className={inputClass}
                  />
                  <p className="mt-1 text-xs text-ink-400">{t('interview.addressHint')}</p>
                </div>

                <div>
                  <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">
                    {t('interview.directions')}
                  </label>
                  <textarea
                    rows={3}
                    value={values[SETTING_KEYS.interviewLocationDirections] || ''}
                    onChange={(e) =>
                      setValues({
                        ...values,
                        [SETTING_KEYS.interviewLocationDirections]: e.target.value,
                      })
                    }
                    placeholder={t('interview.directionsPlaceholder')}
                    className={inputClass}
                  />
                  <p className="mt-1 text-xs text-ink-400">{t('interview.directionsHint')}</p>
                </div>

                <div>
                  <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">
                    {t('interview.mapUrl')}
                  </label>
                  <input
                    type="url"
                    value={values[SETTING_KEYS.interviewLocationMapUrl] || ''}
                    onChange={(e) =>
                      setValues({
                        ...values,
                        [SETTING_KEYS.interviewLocationMapUrl]: e.target.value,
                      })
                    }
                    placeholder="https://maps.app.goo.gl/..."
                    className={inputClass}
                  />
                  <p className="mt-1 text-xs text-ink-400">{t('interview.mapUrlHint')}</p>
                </div>

                <div className="flex items-start gap-2 rounded-xl border border-amber-200 dark:border-amber-500/30 bg-amber-50 dark:bg-amber-500/10 px-3.5 py-3 text-xs text-amber-800 dark:text-amber-300">
                  <Info className="mt-0.5 h-4 w-4 shrink-0" />
                  <span>{t('interview.notice')}</span>
                </div>
              </div>
            )}

          </motion.div>
        </div>
      )}
    </div>
  )
}
