import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { CheckCircle2, Loader2 } from 'lucide-react'
import { authService } from '@ari/shared/fservices/auth'
import { useAuthStore } from '@ari/shared/store/auth'
import { homePathForRole } from '@ari/shared/utils/roles'

/**
 * Chỉ còn 2 trạng thái vẽ ra màn hình. Mọi thất bại kiểu "thử lại bằng tài khoản khác"
 * (sai tên miền, chưa được cấp tài khoản, lỗi kỹ thuật) được đẩy thẳng về màn đăng nhập
 * kèm banner đỏ: đó chính là nơi người dùng phải thao tác tiếp, nên dựng thêm một màn
 * trung gian chỉ tốn một cú bấm mà không thêm thông tin gì.
 *
 * `pending` thì ngược lại — không có gì để thử lại, đây là TRẠNG THÁI (tài khoản đã ghi
 * nhận, đang chờ quản trị viên duyệt) chứ không phải lỗi; nhét vào banner đỏ trên form
 * đăng nhập sẽ mô tả sai tình huống, nên giữ màn riêng.
 */
type CallbackState =
  | { kind: 'loading'; message: string }
  | { kind: 'pending'; message: string }

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
      // `role` lấy từ store chứ không từ hash: `setAuthFromResponse` đã hợp nhất role của
      // response với claim trong JWT, nên hash thiếu `role` vẫn ra đúng vai trò thật.
      const user = setAuthFromResponse({
        accessToken: parsedCallback.accessToken,
        refreshToken: new URLSearchParams(window.location.hash.slice(1)).get('refresh_token') ?? '',
        fullName: '',
        role: parsedCallback.role ?? '',
      })
      window.history.replaceState({}, '', '/auth/callback')
      // Trang chủ đọc từ BẢNG DÙNG CHUNG `ROLE_HOME`. Trước đây đây là một `switch` chép tay
      // thiếu `hiring_manager`, nên HM đăng nhập Google xong bị ném vào `/hr/dashboard` →
      // `ProtectedRoute` chặn → `/403`, dù token đã hợp lệ. Vai trò lạ rơi về `/`, nơi
      // `StaffHomeRedirect` / trang chủ ứng viên tự định tuyến tiếp — không đoán bừa dashboard.
      navigate(homePathForRole(user.role) ?? '/', { replace: true })
      return
    }

    if (status === 'pending') {
      setCallbackState({ kind: 'pending', message: describe('oauthCallback.createdPending') })
      return
    }

    // `rejected` = backend từ chối có lý do rõ ràng (sai tên miền, chưa được cấp tài khoản);
    // `error` = sự cố kỹ thuật giữa chừng; không status nào khớp = vào thẳng URL này không
    // qua luồng OAuth. Cả ba đều trả về màn đăng nhập kèm lý do.
    const failure =
      status === 'rejected' || status === 'error'
        ? describe('oauthCallback.errorDefault')
        : t('oauthCallback.noValidInfo')
    navigate(loginPath, { replace: true, state: { authError: failure } })
  }, [loginPath, navigate, parsedCallback, setAuthFromResponse, t])

  const isLoading = callbackState.kind === 'loading'
  const isPending = callbackState.kind === 'pending'

  return (
    <div className="min-h-screen bg-ink-50 text-ink-900 antialiased flex items-center justify-center p-6">
      <div className="absolute inset-0 bg-gradient-to-br from-brand-50 via-ink-100 to-ai-50" />

      <div className="relative w-full max-w-md rounded-2xl border border-ink-200 bg-white p-7 shadow-xl">
        <div className="flex flex-col items-center text-center">
          <div
            className={`mb-5 flex h-16 w-16 items-center justify-center rounded-full ${
              isPending ? 'bg-amber-100 text-amber-600' : 'bg-brand-50 text-brand-600'
            }`}
          >
            {isPending ? (
              <CheckCircle2 className="h-8 w-8" />
            ) : (
              <Loader2 className="h-8 w-8 animate-spin" />
            )}
          </div>

          <h1 className="mb-3 font-display text-2xl font-extrabold text-ink-900">
            {isPending ? t('oauthCallback.accountPending') : t('oauthCallback.processing')}
          </h1>

          <p className="mb-8 text-sm leading-6 text-ink-500">{callbackState.message}</p>

          {/* Chỉ `pending` mới dừng lại ở đây; `loading` sẽ tự chuyển tiếp nên không có nút. */}
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
