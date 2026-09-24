import { Link, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ShieldAlert } from 'lucide-react'
import { useAuthStore } from '@ari/shared/store/auth'
import { homePathForRole, roleLabel } from '@ari/shared/utils/roles'

/**
 * Màn "không đủ quyền" cho route `/403`.
 *
 * VÌ SAO CÓ FILE NÀY: `/403` trước đây render thẳng `NotFoundPage`, nên mọi lần
 * `ProtectedRoute` chặn vì sai vai trò, người dùng đọc được đúng một câu "Trang không tìm
 * thấy" — mô tả sai hẳn tình huống (trang có thật, chỉ là họ không vào được) và giấu luôn
 * manh mối rằng ai đó vừa bị điều hướng tới dashboard của vai trò khác.
 *
 * `redirectTo` do `ProtectedRoute` gửi kèm là trang chủ ĐÚNG của vai trò hiện tại; thiếu nó
 * thì tự suy lại từ store, cuối cùng mới về `/`.
 */
export default function ForbiddenPage() {
  const { t } = useTranslation('common')
  const location = useLocation()
  const user = useAuthStore((state) => state.user)

  const state = location.state as { redirectTo?: string; from?: { pathname?: string } } | null
  const home = state?.redirectTo || homePathForRole(user?.role) || '/'

  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-ink-50 p-6 text-center dark:bg-ink-950">
      <div className="mb-6 flex h-16 w-16 items-center justify-center rounded-full bg-amber-100 text-amber-600 dark:bg-amber-500/15 dark:text-amber-400">
        <ShieldAlert className="h-8 w-8" />
      </div>

      <p className="text-6xl font-bold text-ink-300 dark:text-ink-700 sm:text-7xl">403</p>
      <h1 className="mt-4 text-2xl font-semibold text-ink-800 dark:text-ink-100">
        {t('forbiddenPage.title')}
      </h1>
      <p className="mt-3 max-w-md text-sm leading-6 text-ink-500 dark:text-ink-400">
        {user?.role
          ? t('forbiddenPage.descriptionWithRole', { role: roleLabel(user.role) })
          : t('forbiddenPage.description')}
      </p>

      <Link
        to={home}
        replace
        className="mt-8 inline-flex rounded-xl bg-brand-600 px-5 py-3 text-sm font-semibold text-white transition hover:bg-brand-700"
      >
        {t('forbiddenPage.backToWorkspace')}
      </Link>
    </div>
  )
}
