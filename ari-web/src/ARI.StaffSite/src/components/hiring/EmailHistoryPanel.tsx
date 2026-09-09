import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Mail, Pencil, AlertTriangle, ChevronDown, ChevronRight } from 'lucide-react'
import { emailService, type EmailLogItem } from '@ari/shared/fservices/email'
import { HIRING_NS } from './hiringConfig'

interface EmailHistoryPanelProps {
  applicationId: string
}

const CARD =
  'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card'

/**
 * Lịch sử thư đã gửi cho ứng viên (ADR-061, phase 4.4).
 *
 * Nội dung hiển thị là bản ĐÃ LỌC lưu trong `email_logs` — chính xác thứ rời khỏi hệ thống, không
 * phải bản người dùng gõ vào. Với thư mời nhận việc thì đây là bằng chứng nội dung đã cam kết, nên
 * nó phải là bản đã gửi chứ không phải bản dựng lại từ mẫu.
 *
 * Thư hệ thống tự gửi (xác nhận nhận hồ sơ, nhắc lịch) cũng nằm ở đây, phân biệt bằng cột người
 * gửi rỗng — cần thấy đủ mọi thứ ứng viên đã nhận thì mới trả lời được câu "sao họ không biết?".
 */
export default function EmailHistoryPanel({ applicationId }: EmailHistoryPanelProps) {
  const { t } = useTranslation(HIRING_NS)
  const [expanded, setExpanded] = useState<string | null>(null)

  const { data: emails = [], isLoading, error } = useQuery({
    queryKey: ['application-emails', applicationId],
    queryFn: () => emailService.getApplicationEmails(applicationId),
    enabled: !!applicationId,
  })

  const label = (item: EmailLogItem) =>
    t(`emails.templates.${item.templateKey}`, { defaultValue: item.templateKey })

  return (
    <div className={CARD}>
      <h2 className="mb-1 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
        <Mail className="h-4 w-4 text-brand-600 dark:text-brand-400" /> {t('emails.title')}
      </h2>
      <p className="mb-4 text-xs text-ink-500 dark:text-ink-400">{t('emails.description')}</p>

      {error ? (
        <p className="text-sm text-red-600 dark:text-red-400">{t('emails.loadError')}</p>
      ) : isLoading ? (
        <p className="text-sm text-ink-500 dark:text-ink-400">{t('common.loading')}</p>
      ) : emails.length === 0 ? (
        <p className="text-sm text-ink-500 dark:text-ink-400">{t('emails.empty')}</p>
      ) : (
        <ul className="divide-y divide-ink-100 dark:divide-white/10">
          {emails.map((item) => {
            const open = expanded === item.id
            return (
              <li key={item.id} className="py-3">
                <button
                  type="button"
                  className="flex w-full items-start gap-2 text-left"
                  onClick={() => setExpanded(open ? null : item.id)}
                  aria-expanded={open}
                >
                  {open ? (
                    <ChevronDown className="mt-0.5 h-4 w-4 shrink-0 text-ink-400" />
                  ) : (
                    <ChevronRight className="mt-0.5 h-4 w-4 shrink-0 text-ink-400" />
                  )}
                  <span className="min-w-0 flex-1">
                    <span className="flex flex-wrap items-center gap-1.5">
                      <span className="truncate text-sm font-medium text-ink-900 dark:text-white">
                        {item.subject}
                      </span>
                      {item.wasEdited && (
                        <span
                          title={t('emails.editedHint')}
                          className="inline-flex items-center gap-1 rounded-full bg-violet-100 dark:bg-violet-500/20 px-2 py-0.5 text-xs font-semibold text-violet-700 dark:text-violet-400"
                        >
                          <Pencil className="h-3 w-3" /> {t('emails.edited')}
                        </span>
                      )}
                      {item.status === 'failed' && (
                        <span className="inline-flex items-center gap-1 rounded-full bg-red-100 dark:bg-red-500/20 px-2 py-0.5 text-xs font-semibold text-red-700 dark:text-red-400">
                          <AlertTriangle className="h-3 w-3" /> {t('emails.failed')}
                        </span>
                      )}
                    </span>
                    <span className="mt-0.5 block truncate text-xs text-ink-500 dark:text-ink-400">
                      {label(item)} ·{' '}
                      {item.sentByName || t('emails.sentBySystem')} ·{' '}
                      {new Date(item.createdAt).toLocaleString('vi-VN')}
                    </span>
                  </span>
                </button>

                {open && (
                  <div className="mt-3 space-y-2 pl-6">
                    {item.errorMessage && (
                      <p className="rounded-lg bg-red-50 dark:bg-red-500/10 px-3 py-2 text-xs text-red-700 dark:text-red-400">
                        {item.errorMessage}
                      </p>
                    )}
                    <div
                      className="max-h-80 overflow-y-auto rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/60 dark:bg-white/5 px-4 py-3 text-sm text-ink-800 dark:text-ink-200"
                      // Nội dung đã được server lọc trước khi lưu (Ganss.Xss) — đây là chính bản
                      // đã gửi đi, không phải chuỗi thô người dùng nhập.
                      dangerouslySetInnerHTML={{ __html: item.bodyHtml }}
                    />
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
