import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Loader2,
  Check,
  UploadCloud,
  ArrowUp,
  ArrowDown,
  Image as ImageIcon,
} from 'lucide-react'
import { PageHeader, ErrorAlert } from '@ari/shared/ui'
import {
  jdTemplateService,
  type JdTemplate,
  type JdTemplateSection,
} from '@ari/shared/fservices/jdTemplate'
import JdPaperPreview from '@/components/jdPreview/JdPaperPreview'
import { useContainerWidth } from '@/components/jdPreview/useContainerWidth'

/**
 * Cấu hình mẫu bản mô tả công việc của công ty (ADR-064) — màn riêng của HR Leader.
 *
 * Hai cột: trái là cấu hình, phải là **xem trước dựng đúng bố cục của renderer** (đầu trang có logo
 * + thông tin công ty → tiêu đề vị trí → bảng thông tin nhanh → từng mục → chân trang). Không có
 * xem trước thì HR Leader phải xuất một file JD thật mới biết mình vừa đổi cái gì.
 */

const inputCls =
  'w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400'
const labelCls = 'mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300'
const cardCls =
  'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card sm:p-6'

export default function JdTemplatePage() {
  const { t } = useTranslation('modules/hr/jdTemplate')
  const fileRef = useRef<HTMLInputElement>(null)

  const [template, setTemplate] = useState<JdTemplate | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState('')
  const [saved, setSaved] = useState(false)

  // Tờ giấy A4 rộng cố định 794px rồi thu nhỏ cho vừa cột — phải biết bề ngang thật của cột.
  const { ref: previewBoxRef, width: previewWidth } = useContainerWidth()

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setTemplate(await jdTemplateService.get())
    } catch (e: any) {
      setError(e?.response?.data?.message || t('errors.loadFailed'))
    } finally {
      setLoading(false)
    }
  }, [t])

  useEffect(() => {
    void load()
  }, [load])

  const patch = (changes: Partial<JdTemplate>) =>
    setTemplate((prev) => (prev ? { ...prev, ...changes } : prev))

  const patchSection = (index: number, changes: Partial<JdTemplateSection>) =>
    setTemplate((prev) =>
      prev
        ? { ...prev, sections: prev.sections.map((s, i) => (i === index ? { ...s, ...changes } : s)) }
        : prev
    )

  /** Đổi thứ tự mục — thứ tự này là thứ tự in ra file, không phải chỉ để nhìn. */
  const move = (index: number, delta: number) =>
    setTemplate((prev) => {
      if (!prev) return prev
      const next = [...prev.sections]
      const target = index + delta
      if (target < 0 || target >= next.length) return prev
      ;[next[index], next[target]] = [next[target], next[index]]
      return { ...prev, sections: next }
    })

  const save = async () => {
    if (!template) return
    setSaving(true)
    setError('')
    setSaved(false)
    try {
      await jdTemplateService.update({
        companyName: template.companyName,
        companyAddress: template.companyAddress,
        companyWebsite: template.companyWebsite,
        companyEmail: template.companyEmail,
        documentTitle: template.documentTitle,
        accentColor: template.accentColor,
        fontFamily: template.fontFamily,
        footerNote: template.footerNote,
        sections: template.sections,
      })
      setSaved(true)
      await load()
    } catch (e: any) {
      setError(e?.response?.data?.message || t('errors.saveFailed'))
    } finally {
      setSaving(false)
    }
  }

  const pickLogo = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    setUploading(true)
    setError('')
    try {
      const url = await jdTemplateService.uploadLogo(file)
      patch({ logoUrl: url })
    } catch (err: any) {
      setError(err?.response?.data?.message || t('errors.logoFailed'))
    } finally {
      setUploading(false)
      if (fileRef.current) fileRef.current.value = ''
    }
  }

  if (loading || !template) {
    return (
      <div className="p-6 lg:p-8">
        <div className="flex items-center gap-2 py-16 text-sm text-ink-500">
          <Loader2 className="h-4 w-4 animate-spin" /> {t('loading')}
        </div>
      </div>
    )
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <PageHeader title={t('title')} description={t('description')} />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      <div className="grid grid-cols-1 items-start gap-5 xl:grid-cols-[minmax(0,1fr)_minmax(0,32rem)]">
        {/* ------------------------------- Cấu hình ------------------------------- */}
        <div className="space-y-5">
          <section className={cardCls}>
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">
              {t('company.title')}
            </h2>

            <div className="mb-4 flex flex-wrap items-center gap-4">
              <div className="grid h-20 w-40 shrink-0 place-items-center overflow-hidden rounded-xl border border-dashed border-ink-200 bg-ink-50 dark:border-white/10 dark:bg-white/5">
                {template.logoUrl ? (
                  <img src={template.logoUrl} alt={t('company.logo')} className="max-h-full max-w-full object-contain" />
                ) : (
                  <ImageIcon className="h-6 w-6 text-ink-300" />
                )}
              </div>
              <div>
                <input
                  ref={fileRef}
                  type="file"
                  accept="image/png,image/jpeg"
                  onChange={pickLogo}
                  className="hidden"
                />
                <button
                  type="button"
                  onClick={() => fileRef.current?.click()}
                  disabled={uploading}
                  className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-medium text-white disabled:opacity-60"
                >
                  {uploading ? <Loader2 className="h-4 w-4 animate-spin" /> : <UploadCloud className="h-4 w-4" />}
                  {t('company.uploadLogo')}
                </button>
                <p className="mt-2 text-xs text-ink-400">{t('company.logoHint')}</p>
              </div>
            </div>

            <div className="space-y-3">
              <div>
                <label className={labelCls}>{t('company.name')} *</label>
                <input
                  value={template.companyName}
                  onChange={(e) => patch({ companyName: e.target.value })}
                  className={inputCls}
                />
              </div>
              <div>
                <label className={labelCls}>{t('company.address')}</label>
                <input
                  value={template.companyAddress ?? ''}
                  onChange={(e) => patch({ companyAddress: e.target.value })}
                  className={inputCls}
                />
              </div>
              <div>
                <label className={labelCls}>{t('company.documentTitle')}</label>
                <input
                  value={template.documentTitle ?? ''}
                  onChange={(e) => patch({ documentTitle: e.target.value })}
                  className={inputCls}
                />
                <p className="mt-1 text-xs text-ink-400">{t('company.documentTitleHint')}</p>
              </div>
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <div>
                  <label className={labelCls}>{t('company.website')}</label>
                  <input
                    value={template.companyWebsite ?? ''}
                    onChange={(e) => patch({ companyWebsite: e.target.value })}
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className={labelCls}>{t('company.email')}</label>
                  <input
                    value={template.companyEmail ?? ''}
                    onChange={(e) => patch({ companyEmail: e.target.value })}
                    className={inputCls}
                  />
                </div>
              </div>
            </div>
          </section>

          <section className={cardCls}>
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">
              {t('style.title')}
            </h2>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className={labelCls}>{t('style.accent')}</label>
                <div className="flex items-center gap-2">
                  <input
                    type="color"
                    value={template.accentColor}
                    onChange={(e) => patch({ accentColor: e.target.value.toUpperCase() })}
                    className="h-10 w-14 shrink-0 cursor-pointer rounded-lg border border-ink-200 dark:border-white/10"
                  />
                  <input
                    value={template.accentColor}
                    onChange={(e) => patch({ accentColor: e.target.value })}
                    className={inputCls}
                  />
                </div>
              </div>
              <div>
                <label className={labelCls}>{t('style.font')}</label>
                <input
                  value={template.fontFamily}
                  onChange={(e) => patch({ fontFamily: e.target.value })}
                  className={inputCls}
                />
                <p className="mt-1 text-xs text-ink-400">{t('style.fontHint')}</p>
              </div>
            </div>
            <div className="mt-3">
              <label className={labelCls}>{t('style.footer')}</label>
              <input
                value={template.footerNote ?? ''}
                onChange={(e) => patch({ footerNote: e.target.value })}
                className={inputCls}
              />
            </div>
          </section>

          <section className={cardCls}>
            <h2 className="text-sm font-semibold text-ink-900 dark:text-white">{t('sections.title')}</h2>
            <p className="mb-4 mt-1 text-xs text-ink-500 dark:text-ink-400">{t('sections.description')}</p>

            <div className="space-y-2">
              {template.sections.map((section, index) => (
                <div
                  key={section.key}
                  className="flex flex-wrap items-center gap-2 rounded-xl border border-ink-200 p-3 dark:border-white/10"
                >
                  <input
                    type="checkbox"
                    checked={section.enabled}
                    onChange={(e) => patchSection(index, { enabled: e.target.checked })}
                    className="h-4 w-4 shrink-0 accent-brand-600"
                    aria-label={t('sections.enabled')}
                  />
                  <input
                    value={section.title}
                    onChange={(e) => patchSection(index, { title: e.target.value })}
                    className={`${inputCls} min-w-[10rem] flex-1`}
                  />
                  {/* Khoá hiện ra để HR Leader hiểu vì sao không sửa được nó. */}
                  <code className="shrink-0 rounded bg-ink-100 px-2 py-1 text-xs text-ink-500 dark:bg-white/10 dark:text-ink-400">
                    {section.key}
                  </code>
                  <div className="flex shrink-0 gap-1">
                    <button
                      type="button"
                      onClick={() => move(index, -1)}
                      disabled={index === 0}
                      className="rounded-lg p-2 text-ink-400 hover:bg-ink-100 disabled:opacity-30 dark:hover:bg-white/10"
                      aria-label={t('sections.moveUp')}
                    >
                      <ArrowUp className="h-4 w-4" />
                    </button>
                    <button
                      type="button"
                      onClick={() => move(index, 1)}
                      disabled={index === template.sections.length - 1}
                      className="rounded-lg p-2 text-ink-400 hover:bg-ink-100 disabled:opacity-30 dark:hover:bg-white/10"
                      aria-label={t('sections.moveDown')}
                    >
                      <ArrowDown className="h-4 w-4" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </section>

          <div className="flex items-center gap-3">
            <button
              onClick={save}
              disabled={saving}
              className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-5 py-2.5 text-sm font-medium text-white disabled:opacity-60"
            >
              {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
              {t('save')}
            </button>
            {saved && <span className="text-sm text-emerald-600">{t('savedOk')}</span>}
          </div>
        </div>

        {/* ------------------------------- Xem trước ------------------------------- */}
        <div className="xl:sticky xl:top-[var(--sticky-top,1.5rem)]" ref={previewBoxRef}>
          <p className="mb-2 text-xs font-medium uppercase tracking-wide text-ink-400">
            {t('preview.label')}
          </p>
          <JdPaperPreview template={template} containerWidth={previewWidth} />
          <p className="mt-2 text-center text-xs text-ink-400">{t('preview.note')}</p>
        </div>
      </div>
    </div>
  )
}
