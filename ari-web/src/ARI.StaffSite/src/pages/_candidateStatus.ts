/**
 * Trạng thái hồ sơ ứng tuyển → nhãn, nhóm lọc và màu chip, dùng cho CẢ HAI màn Ứng viên
 * (HR Lead và Recruiter).
 *
 * VÌ SAO TÁCH RA: hai trang từng chứa hàm này giống nhau từng byte. Vá một bên khi thêm trạng
 * thái mới là bên kia lặng lẽ rơi vào nhóm `other` — tức hồ sơ biến mất khỏi mọi bộ lọc và chip
 * hiện chuỗi thô (`hm_review`, `offer_declined`) ra cho người dùng đọc. Đúng bài học ADR-058/059.
 *
 * Nhãn lấy qua `t` của namespace `modules/{hr|recruiter}/candidates` — hai namespace khác nhau
 * nhưng CÙNG bộ khóa `status.*`, nên hàm nhận `t` thay vì tự gọi hook.
 */
export type Group = 'pending' | 'interviewing' | 'passed' | 'rejected' | 'other'

export interface StatusMeta {
  label: string
  group: Group
  badge: string
}

export function statusMeta(status: string, t: (key: string) => string): StatusMeta {
  const s = (status || '').toLowerCase()
  const map: Record<string, StatusMeta> = {
    invited: {
      label: t('status.invited'),
      group: 'pending',
      badge: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300',
    },
    cv_submitted: {
      label: t('status.cvSubmitted'),
      group: 'pending',
      badge: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    },
    // Cổng duyệt của Hiring Manager (ADR-061): vẫn thuộc nhóm "chờ" vì hồ sơ chưa vào vòng nào.
    hm_review: {
      label: t('status.hmReview'),
      group: 'pending',
      badge: 'bg-sky-100 dark:bg-sky-500/20 text-sky-700 dark:text-sky-400',
    },
    pending: {
      label: t('status.pending'),
      group: 'pending',
      badge: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    },
    pending_review: {
      label: t('status.pending'),
      group: 'pending',
      badge: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    },
    screening: {
      label: t('status.screening'),
      group: 'interviewing',
      badge: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
    },
    interview: {
      label: t('status.screening'),
      group: 'interviewing',
      badge: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
    },
    interview_code_generated: {
      label: t('status.codeGenerated'),
      group: 'interviewing',
      badge: 'bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-400',
    },
    interview_code_used: {
      label: t('status.screening'),
      group: 'interviewing',
      badge: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
    },
    practice: {
      label: t('status.practice'),
      group: 'interviewing',
      badge: 'bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-400',
    },
    pass: {
      label: t('status.pass'),
      group: 'passed',
      badge: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    },
    approved: {
      label: t('status.pass'),
      group: 'passed',
      badge: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    },
    completed: {
      label: t('status.completed'),
      group: 'passed',
      badge: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    },
    // Đoạn kết của phễu (ADR-061). Thiếu bốn khoá này thì bảng ứng viên hiện thẳng chuỗi thô
    // `offer_declined` cho người dùng đọc — nhóm mặc định là 'other', tức lọt khỏi mọi bộ lọc.
    offer: {
      label: t('status.offerSent'),
      group: 'passed',
      badge: 'bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-400',
    },
    hired: {
      label: t('status.hired'),
      group: 'passed',
      badge: 'bg-teal-100 dark:bg-teal-500/20 text-teal-700 dark:text-teal-400',
    },
    offer_declined: {
      label: t('status.offerDeclined'),
      group: 'rejected',
      badge: 'bg-orange-100 dark:bg-orange-500/20 text-orange-700 dark:text-orange-400',
    },
    cv_rejected: {
      label: t('status.cvRejected'),
      group: 'rejected',
      badge: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    },
    not_pass: {
      label: t('status.notPass'),
      group: 'rejected',
      badge: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    },
    rejected: {
      label: t('status.notPass'),
      group: 'rejected',
      badge: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    },
    failed: {
      label: t('status.notPass'),
      group: 'rejected',
      badge: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    },
    withdrawn: {
      label: t('status.withdrawn'),
      group: 'rejected',
      badge: 'bg-ink-100 dark:bg-white/10 text-ink-500 dark:text-ink-400',
    },
  }
  return (
    map[s] ?? {
      label: status || '—',
      group: 'other',
      badge: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300',
    }
  )
}
