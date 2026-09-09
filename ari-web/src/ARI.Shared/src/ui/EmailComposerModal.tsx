import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Bold, Italic, List, Link2, RotateCcw, Eye, Pencil, X } from 'lucide-react'
import { emailService, type EmailOverride, type RenderedEmail } from '@ari/shared/fservices/email'

interface EmailComposerModalProps {
  open: boolean
  templateKey: string
  /** Hồ sơ ứng tuyển làm ngữ cảnh dựng thư. */
  contextId: string
  /** Tham số phụ theo mẫu — thư mời phỏng vấn dùng để truyền slotId. */
  secondaryId?: string
  title?: string
  confirmLabel?: string
  onCancel: () => void
  /** Gửi: nhận nội dung đã sửa (hoặc undefined nếu người dùng không đổi gì). */
  onSend: (override?: EmailOverride) => Promise<void> | void
  sending?: boolean
}

/**
 * Soạn/sửa thư gửi ứng viên trước khi gửi (ADR-061, Phase 4).
 *
 * Modal này CHỈ soạn. Việc gửi do chính hành động nghiệp vụ thực hiện (duyệt CV + xếp lịch, loại
 * hồ sơ…) qua tham số `emailOverride` — bấm Huỷ ở đây nghĩa là KHÔNG có gì xảy ra cả, không chốt
 * chỗ, không đổi trạng thái, không thư nào gửi đi. Đó là lý do không tách thành "lưu nháp rồi gửi".
 *
 * Soạn thảo dùng thanh công cụ tối giản trên contentEditable thay vì kéo một WYSIWYG đầy đủ: thư
 * mẫu đã điền sẵn gần hết nội dung, việc của người dùng là sửa câu chữ và thêm một đoạn ghi chú.
 * Nội dung vẫn được LỌC LẠI Ở SERVER trước khi gửi — thanh công cụ này không phải cổng bảo mật.
 */
export default function EmailComposerModal({
  open,
  templateKey,
  contextId,
  secondaryId,
  title,
  confirmLabel,
  onCancel,
  onSend,
  sending = false,
}: EmailComposerModalProps) {
  const { t } = useTranslation('modules/shared/emailComposer')
  const bodyRef = useRef<HTMLDivElement>(null)

  const [template, setTemplate] = useState<RenderedEmail | null>(null)
  const [subject, setSubject] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [previewing, setPreviewing] = useState(false)
  const [edited, setEdited] = useState(false)

  useEffect(() => {
    if (!open) return
    let cancelled = false

    setLoading(true)
    setError(null)
    setEdited(false)
    setPreviewing(false)

    emailService
      .preview({ templateKey, contextId, secondaryId })
      .then((data) => {
        if (cancelled) return
        setTemplate(data)
        setSubject(data.subject)
        // KHÔNG ghi vào bodyRef ở đây — xem useEffect bên dưới.
      })
      .catch((e: unknown) => {
        if (cancelled) return
        const message =
          (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('loadError')
        setError(message)
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [open, templateKey, contextId, secondaryId, t])

  /**
   * Đổ nội dung mẫu vào vùng soạn thảo — chạy SAU khi vùng đó đã được gắn vào cây DOM.
   *
   * Không gộp được vào `.then()` của lượt tải: lúc promise trả về thì `loading` vẫn là `true`, mà
   * vùng soạn thảo nằm trong nhánh `loading === false` nên chưa mount — `bodyRef.current` là `null`
   * và nội dung mẫu rơi vào hư không. Triệu chứng: **mọi thư mở ra với thân rỗng**, người dùng gõ
   * gì thì ứng viên nhận đúng chừng ấy — mất hết giờ hẹn, địa điểm và nút xác nhận của mẫu.
   *
   * `template` và `loading` chỉ đổi một lần cho mỗi lượt mở, nên effect này không ghi đè bản sửa
   * tay của người dùng.
   */
  useEffect(() => {
    if (loading || !template || !bodyRef.current) return
    bodyRef.current.innerHTML = template.html
  }, [loading, template])

  if (!open) return null

  const exec = (command: string, value?: string) => {
    document.execCommand(command, false, value)
    bodyRef.current?.focus()
    setEdited(true)
  }

  const restoreTemplate = () => {
    if (!template) return
    setSubject(template.subject)
    if (bodyRef.current) bodyRef.current.innerHTML = template.html
    setEdited(false)
  }

  const handleSend = async () => {
    const currentBody = bodyRef.current?.innerHTML ?? ''
    const changed = edited || (template != null && (subject !== template.subject || currentBody !== template.html))

    // Không đổi gì → gửi undefined để server dùng đúng mẫu, và EmailLog ghi wasEdited = false.
    await onSend(changed ? { subject, bodyHtml: currentBody } : undefined)
  }

  const toolbarButton =
    'inline-flex items-center justify-center w-8 h-8 rounded-lg text-ink-600 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10 transition-colors'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-3xl max-h-[90vh] overflow-hidden flex flex-col rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-950 shadow-card">
        <header className="flex items-center justify-between gap-4 border-b border-ink-100 dark:border-white/10 px-6 py-4">
          <div>
            <h2 className="font-semibold text-ink-900 dark:text-white">{title ?? t('title')}</h2>
            {template && (
              <p className="mt-0.5 text-sm text-ink-500 dark:text-ink-400">
                {t('recipient', { name: template.toName || template.toEmail, email: template.toEmail })}
              </p>
            )}
          </div>
          <button
            type="button"
            onClick={onCancel}
            aria-label={t('cancel')}
            className="rounded-lg p-1.5 text-ink-500 hover:bg-ink-100 dark:hover:bg-white/10 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </header>

        <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
          {error && (
            <div className="rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 px-4 py-3 text-sm text-red-700 dark:text-red-400">
              {error}
            </div>
          )}

          {loading ? (
            <p className="py-8 text-center text-sm text-ink-500 dark:text-ink-400">{t('loading')}</p>
          ) : (
            <>
              <div>
                <label
                  htmlFor="email-subject"
                  className="block text-sm font-medium text-ink-900 dark:text-white mb-1.5"
                >
                  {t('subject')}
                </label>
                <input
                  id="email-subject"
                  type="text"
                  value={subject}
                  onChange={(e) => {
                    setSubject(e.target.value)
                    setEdited(true)
                  }}
                  className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-brand-500"
                />
              </div>

              <div>
                <div className="flex items-center justify-between gap-2 mb-1.5">
                  <span className="text-sm font-medium text-ink-900 dark:text-white">{t('body')}</span>
                  <div className="flex items-center gap-1">
                    {!previewing && (
                      <>
                        <button type="button" className={toolbarButton} onClick={() => exec('bold')} title={t('bold')}>
                          <Bold className="w-4 h-4" />
                        </button>
                        <button type="button" className={toolbarButton} onClick={() => exec('italic')} title={t('italic')}>
                          <Italic className="w-4 h-4" />
                        </button>
                        <button
                          type="button"
                          className={toolbarButton}
                          onClick={() => exec('insertUnorderedList')}
                          title={t('bulletList')}
                        >
                          <List className="w-4 h-4" />
                        </button>
                        <button
                          type="button"
                          className={toolbarButton}
                          onClick={() => {
                            const url = window.prompt(t('linkPrompt'))
                            if (url) exec('createLink', url)
                          }}
                          title={t('link')}
                        >
                          <Link2 className="w-4 h-4" />
                        </button>
                      </>
                    )}
                    <button
                      type="button"
                      className={toolbarButton}
                      onClick={() => setPreviewing((p) => !p)}
                      title={previewing ? t('edit') : t('preview')}
                    >
                      {previewing ? <Pencil className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                    <button
                      type="button"
                      className={toolbarButton}
                      onClick={restoreTemplate}
                      title={t('restoreTemplate')}
                    >
                      <RotateCcw className="w-4 h-4" />
                    </button>
                  </div>
                </div>

                <div
                  ref={bodyRef}
                  contentEditable={!previewing}
                  suppressContentEditableWarning
                  onInput={() => setEdited(true)}
                  className={`min-h-[240px] max-h-[45vh] overflow-y-auto rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-4 py-3 text-sm text-ink-900 dark:text-ink-100 focus:outline-none focus:ring-2 focus:ring-brand-500 ${
                    previewing ? 'cursor-default' : ''
                  }`}
                />
                <p className="mt-1.5 text-xs text-ink-500 dark:text-ink-400">{t('hint')}</p>
              </div>
            </>
          )}
        </div>

        <footer className="flex items-center justify-end gap-2 border-t border-ink-100 dark:border-white/10 px-6 py-4">
          <button
            type="button"
            onClick={onCancel}
            disabled={sending}
            className="rounded-xl border border-ink-200 dark:border-white/10 px-4 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/5 disabled:opacity-50 transition-colors"
          >
            {t('cancel')}
          </button>
          <button
            type="button"
            onClick={handleSend}
            disabled={loading || sending || !!error}
            className="rounded-xl bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {sending ? t('sending') : (confirmLabel ?? t('send'))}
          </button>
        </footer>
      </div>
    </div>
  )
}
