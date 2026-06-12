import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, CheckCircle2, Loader2 } from 'lucide-react';
import { authService } from '@services/auth/authService';
import { useAuthStore } from '@store/auth/authStore';

const pendingMessages: Record<string, string> = {
  pending_approval: 'Tài khoản của bạn đang chờ được phê duyệt.',
  created_pending: 'Tài khoản của bạn đã được ghi nhận và đang chờ quản trị viên phê duyệt.',
};

type CallbackState =
  | { kind: 'loading'; message: string }
  | { kind: 'pending'; message: string }
  | { kind: 'error'; message: string };

// Get dashboard path based on role
function getRoleDashboard(role: string): string {
  const r = role.toLowerCase().replace(/\s+/g, '_');
  switch (r) {
    case 'super_admin':
      return '/super-admin/dashboard';
    case 'hr_admin':
      return '/hr/dashboard';
    case 'recruiter':
      return '/recruiter/dashboard';
    case 'candidate':
      return '/candidate/portal';
    default:
      return '/hr/dashboard';
  }
}

export default function OAuthCallbackPage() {
  const navigate = useNavigate();
  const setAuthFromResponse = useAuthStore((state) => state.setAuthFromResponse);
  const [callbackState, setCallbackState] = useState<CallbackState>({
    kind: 'loading',
    message: 'Đang xác thực đăng nhập với Google...',
  });

  const parsedCallback = useMemo(() => authService.parseOAuthCallback(window.location.href), []);

  useEffect(() => {
    const status = new URLSearchParams(window.location.search).get('status') ?? parsedCallback.status;
    const message = new URLSearchParams(window.location.search).get('message') ?? parsedCallback.message;

    if (parsedCallback.accessToken) {
      const role = parsedCallback.role ?? 'Hr_admin';
      setAuthFromResponse({
        accessToken: parsedCallback.accessToken,
        refreshToken: new URLSearchParams(window.location.hash.slice(1)).get('refresh_token') ?? '',
        fullName: '',
        role: role,
      });
      window.history.replaceState({}, '', '/auth/callback');
      navigate(getRoleDashboard(role), { replace: true });
      return;
    }

    if (status === 'pending') {
      setCallbackState({
        kind: 'pending',
        message: pendingMessages[message ?? ''] ?? 'Tài khoản của bạn đang chờ được duyệt.',
      });
      return;
    }

    if (status === 'error') {
      setCallbackState({
        kind: 'error',
        message: message || 'Đăng nhập OAuth thất bại. Vui lòng thử lại.',
      });
      return;
    }

    setCallbackState({
      kind: 'error',
      message: 'Không tìm thấy thông tin đăng nhập hợp lệ từ OAuth callback.',
    });
  }, [navigate, parsedCallback, setAuthFromResponse]);

  const isPending = callbackState.kind === 'pending';
  const isError = callbackState.kind === 'error';

  return (
    <div className="min-h-screen bg-bg-primary flex items-center justify-center px-6">
      <div className="w-full max-w-md rounded-3xl border border-white/10 bg-white/5 p-8 shadow-2xl backdrop-blur-xl">
        <div className="flex flex-col items-center text-center">
          <div
            className={`mb-5 flex h-16 w-16 items-center justify-center rounded-full ${
              isPending
                ? 'bg-amber-500/15 text-amber-300'
                : isError
                  ? 'bg-red-500/15 text-red-300'
                  : 'bg-accent-primary/15 text-accent-primary'
            }`}
          >
            {callbackState.kind === 'loading' ? (
              <Loader2 className="h-8 w-8 animate-spin" />
            ) : isPending ? (
              <CheckCircle2 className="h-8 w-8" />
            ) : (
              <AlertCircle className="h-8 w-8" />
            )}
          </div>

          <h1 className="mb-3 text-2xl font-semibold text-white">
            {callbackState.kind === 'loading'
              ? 'Đang xử lý đăng nhập'
              : isPending
                ? 'Tài khoản chờ duyệt'
                : 'Không thể đăng nhập'}
          </h1>

          <p className="mb-8 text-sm leading-6 text-text-secondary">{callbackState.message}</p>

          {(isPending || isError) && (
            <button
              type="button"
              onClick={() => navigate('/auth/login', { replace: true })}
              className="w-full rounded-xl bg-gradient-to-r from-accent-primary to-violet px-4 py-3 text-sm font-semibold text-white transition-opacity hover:opacity-90"
            >
              Quay lại trang đăng nhập
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
