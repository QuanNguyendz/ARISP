import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { AlertCircle, CheckCircle2, Loader2 } from 'lucide-react'
import { authService } from '@ari/shared/fservices/auth'
import { useAuthStore } from '@ari/shared/store/auth'

type CallbackState =
  | { kind: 'loading'; message: string }
  | { kind: 'pending'; message: string }
  | { kind: 'error'; message: string }

/**
 * Mã lỗi backend (AuthErrorCodes) → khoá i18n. Trước đây trang này in thẳng giá trị
 * `message` ra màn hình nên người dùng đọc được nguyên chuỗi kỹ thuật kiểu
 * "external_authentication_failed"; mã nào không có trong bảng vẫn rơi về câu mặc định.
 */
const MESSAGE_KEYS: Record<string, string> = {
  domain_not_allowed: 'oauthCallback.domainNotAllowed',
  account_not_provisioned: 'oauthCallback.notProvisioned',
  pending_approval: 'oauthCallback.pendingApproval',
  account_disabled: 'oauthCallback.accountDisabled',
  external_authentication_failed: 'oauthCallback.externalFailed',
  no_email_from_provider: 'oauthCallback.noEmail',
}

// Get dashboard path based on role
function getRoleDashboard(role: string): string {
  const r = role.toLowerCase().replace(/\s+/g, '_')
  switch (r) {
    case 'super_admin':
      return '/super-admin/dashboard'
    case 'hr_admin':
      return '/hr/dashboard'
    case 'recruiter':
      return '/recruiter/dashboard'
    case 'candidate':
      return '/'
    default:
      return '/hr/dashboard'
  }
}

interface OAuthCallbackPageProps {
  /**
   * Màn đăng nhập để quay về. Trang này dùng chung cho cả 2 site nhưng đường dẫn đăng nhập
   * KHÁC nhau (staff `/auth/login`, ứng viên `/auth/candidate-login`) — trước đây ghi cứng
   * `/auth/login` nên nút quay lại ở site ứng viên rơi vào catch-all → 404.
   */
  loginPath?: string
}

export default function OAuthCallbackPage({ loginPath = '/auth/login' }: OAuthCallbackPageProps) {
  const { t } = useTranslation('auth')
  const navigate = useNavigate()
  const setAuthFromResponse = useAuthStore((state) => state.setAuthFromResponse)
  const [callbackState, setCallbackState] = useState<CallbackState>({
    kind: 'loading',
    message: t('oauthCallback.loading'),
  })

  const parsedCallback = useMemo(() => authService.parseOAuthCallback(window.location.href), [])

  useEffect(() => {
    const search = new URLSearchParams(window.location.search)
    const status = search.get('status') ?? parsedCallback.status
    const code = search.get('message') ?? parsedCallback.message
    // Mã lạ (backend thêm code mới mà FE chưa kịp dịch) → câu mặc định, không in mã ra màn hình.
    const describe = (fallback: string) => (code && MESSAGE_KEYS[code] ? t(MESSAGE_KEYS[code]) : t(fallback))

    if (parsedCallback.accessToken) {
      const role = parsedCallback.role ?? 'Hr_admin'
      setAuthFromResponse({
        accessToken: parsedCallback.accessToken,
        refreshToken: new URLSearchParams(window.location.hash.slice(1)).get('refresh_token') ?? '',
        fullName: '',
        role: role,
      })
      window.history.replaceState({}, '', '/auth/callback')
      navigate(getRoleDashboard(role), { replace: true })
      return
    }

    if (status === 'pending') {
      setCallbackState({ kind: 'pending', message: describe('oauthCallback.createdPending') })
      return
    }

    // `rejected` = backend từ chối có lý do rõ ràng (sai domain, chưa được cấp tài khoản);
    // `error` = sự cố kỹ thuật giữa chừng. Cả hai đều hiện thẻ lỗi kèm nút quay lại đăng nhập.
    if (status === 'rejected' || status === 'error') {
      setCallbackState({ kind: 'error', message: describe('oauthCallback.errorDefault') })
      return
    }

    setCallbackState({ kind: 'error', message: t('oauthCallback.noValidInfo') })
  }, [navigate, parsedCallback, setAuthFromResponse, t])

  const isLoading = callbackState.kind === 'loading'
  const isPending = callbackState.kind === 'pending'

  return (
    <div className="min-h-screen bg-ink-50 text-ink-900 antialiased flex items-center justify-center p-6">
      <div className="absolute inset-0 bg-gradient-to-br from-brand-50 via-ink-100 to-ai-50" />

      <div className="relative w-full max-w-md rounded-2xl border border-ink-200 bg-white p-7 shadow-xl">
        <div className="flex flex-col items-center text-center">
          <div
            className={`mb-5 flex h-16 w-16 items-center justify-center rounded-full ${
              isPending
                ? 'bg-amber-100 text-amber-600'
                : isLoading
                  ? 'bg-brand-50 text-brand-600'
                  : 'bg-red-100 text-red-600'
            }`}
          >
            {isLoading ? (
              <Loader2 className="h-8 w-8 animate-spin" />
            ) : isPending ? (
              <CheckCircle2 className="h-8 w-8" />
            ) : (
              <AlertCircle className="h-8 w-8" />
            )}
          </div>

          <h1 className="mb-3 font-display text-2xl font-extrabold text-ink-900">
            {isLoading
              ? t('oauthCallback.processing')
              : isPending
                ? t('oauthCallback.accountPending')
                : t('oauthCallback.cannotLogin')}
          </h1>

          <p className="mb-8 text-sm leading-6 text-ink-500">{callbackState.message}</p>

          {!isLoading && (
            <button
              type="button"
              onClick={() => navigate(loginPath, { replace: true })}
              className="w-full rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-3 text-sm font-semibold text-white transition-opacity hover:opacity-90"
            >
              {t('oauthCallback.backToLogin')}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}
