import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ClipboardCheck, ChevronDown } from 'lucide-react'
import type { HrApplicationItem } from '@ari/shared/types/application'

/**
 * Những gì ứng viên tự viết trên biểu mẫu ứng tuyển: thời gian báo trước khi nghỉ việc và thư giới
 * thiệu.
 *
 * <b>Vì sao có file này.</b> Hai ô này đi thẳng vào `applications` từ lúc nộp hồ sơ (`notice_period`
 * / `cover_letter`), trả cả trong `ApplicationResponse`, rồi nằm im ở đó — không màn nào của nhân sự
 * kê ra. Người sàng hồ sơ đọc CV mà không đọc được chính lời ứng viên viết cho vị trí này.
 *
 * <b>Không lặp lại thông tin liên hệ.</b> Họ tên · điện thoại · email đã nằm ngay phía trên ở mọi
 * chỗ dùng thẻ này (panel hồ sơ của màn tin, và khối "Liên hệ" của ba màn hồ sơ đầy đủ) — in lần
 * thứ hai chỉ làm màn hình dài ra mà không thêm thông tin nào.
 *
 * Một component cho cả bốn chỗ — chép thành bốn bản là mở đường cho lần sửa sau chỉ sửa một.
 */
export default function ApplicationFormCard({
  app,
  className = '',
  defaultOpen = true,
}: {
  app: Pick<HrApplicationItem, 'coverLetter' | 'noticePeriod'>
  className?: string
  /** Thu gọn sẵn ở những màn đã dài — người đọc tự mở khi cần. */
  defaultOpen?: boolean
}) {
  const { t } = useTranslation('modules/staff/candidatePipeline')
  const [open, setOpen] = useState(defaultOpen)
  const hasLetter = !!app.coverLetter?.trim()

  return (
    <section className={className}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        className="flex w-full items-center gap-2 text-left"
      >
        <ClipboardCheck className="h-4 w-4 shrink-0 text-brand-600 dark:text-brand-400" />
        <span className="text-sm font-semibold text-ink-900 dark:text-white">
          {t('applicationForm.title')}
        </span>

        {/* Thu gọn rồi thì dòng tóm tắt vẫn phải trả lời được câu hỏi thường gặp nhất — "bao giờ
            người này đi làm được" — chứ không chỉ là một cái tiêu đề câm. */}
        {!open && (
          <span className="min-w-0 flex-1 truncate text-xs text-ink-500 dark:text-ink-400">
            {app.noticePeriod?.trim() || '—'} ·{' '}
            {hasLetter
              ? t('applicationForm.hasCoverLetter')
              : t('applicationForm.noCoverLetterShort')}
          </span>
        )}

        <ChevronDown
          className={`ml-auto h-4 w-4 shrink-0 text-ink-400 transition-transform ${open ? 'rotate-180' : ''}`}
        />
      </button>

      {open && (
        <>
          <p className="mt-1 text-xs text-ink-500 dark:text-ink-400">
            {t('applicationForm.hint')}
          </p>

          <dl className="mt-3">
            <div className="flex items-baseline gap-2 border-b border-ink-100/70 py-1.5 dark:border-white/5">
              <dt className="w-36 shrink-0 text-xs text-ink-400">
                {t('applicationForm.noticePeriod')}
              </dt>
              <dd className="min-w-0 flex-1 break-words text-sm text-ink-800 dark:text-ink-100">
                {app.noticePeriod?.trim() || '—'}
              </dd>
            </div>
          </dl>

          {/* Thư giới thiệu là ô KHÔNG bắt buộc — người không viết vẫn phải thấy rõ là "không viết",
              chứ không phải một khoảng trống mà người đọc tưởng màn hình lỗi. */}
          <div className="mt-4">
            <p className="mb-1 text-xs text-ink-400">{t('applicationForm.coverLetter')}</p>
            {hasLetter ? (
              <p className="whitespace-pre-line rounded-xl bg-ink-50 p-3 text-sm leading-relaxed text-ink-800 dark:bg-white/5 dark:text-ink-100">
                {app.coverLetter}
              </p>
            ) : (
              <p className="text-sm italic text-ink-400">{t('applicationForm.noCoverLetter')}</p>
            )}
          </div>
        </>
      )}
    </section>
  )
}
