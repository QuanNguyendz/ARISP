import type { LoginRequest, AuthResponse, CandidateRegisterRequest, User } from '@ari/shared/types/auth'
import { API_BASE_URL, ROLES } from '@ari/shared/config/constants'

export interface RegisterRequest {
  email: string
  password: string
  name: string
  company?: string
  phone?: string
  role?: string
}

const USE_FAKE_AUTH = import.meta.env.VITE_ENABLE_FAKE_AUTH === 'true'

/**
 * Lỗi HTTP có mang theo MÃ và TRẠNG THÁI, để `resolveApiError` dịch được sang ngôn ngữ giao diện.
 *
 * `fetch` không dựng sẵn hình dạng lỗi như axios, nên trước đây cả thân lỗi bị nén xuống một chuỗi
 * `Error.message` — mã lỗi biến mất trên đường đi, và khi server không gửi `message` thì thứ hiện ra
 * màn hình là đúng hai chữ "HTTP 500". Giữ nguyên `message` cho code cũ đang đọc `err.message`, và
 * gắn thêm `code`/`response` cho đường mới.
 */
export interface ApiHttpError extends Error {
  code?: string
  response: { status: number; data: unknown }
}

function buildHttpError(status: number, data: unknown): ApiHttpError {
  const body = (typeof data === 'object' && data !== null ? data : {}) as {
    message?: string
    code?: string
  }
  const error = new Error(body.message || `HTTP ${status}`) as ApiHttpError
  if (body.code) error.code = body.code
  error.response = { status, data }
  return error
}

async function parseResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const data = await response.json().catch(() => ({}))
    throw buildHttpError(response.status, data)
  }
  return response.json()
}

export const authService = {
  async staffLogin(credentials: LoginRequest): Promise<AuthResponse> {
    if (USE_FAKE_AUTH) {
      await new Promise((resolve) => setTimeout(resolve, 800))
      return {
        accessToken: 'mock-staff-token-' + Date.now(),
        refreshToken: 'mock-refresh-token-' + Date.now(),
        fullName: 'HR Admin',
        role: ROLES.HRAdmin,
      }
    }

    const response = await fetch(`${API_BASE_URL}/auth/staff/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(credentials),
    })

    return parseResponse<AuthResponse>(response)
  },

  async employerLogin(_credentials: LoginRequest): Promise<AuthResponse> {
    if (USE_FAKE_AUTH) {
      await new Promise((resolve) => setTimeout(resolve, 800))
      return {
        accessToken: 'mock-employer-token-' + Date.now(),
        refreshToken: 'mock-refresh-token-' + Date.now(),
        fullName: 'HR Admin',
        role: ROLES.HRAdmin,
      }
    }

    window.location.href = authService.buildOAuthRedirectUrl(
      'Google',
      `${window.location.origin}/auth/login`
    )
    throw new Error('Redirecting to Google sign-in')
  },

  async candidateLogin(credentials: LoginRequest): Promise<AuthResponse> {
    if (USE_FAKE_AUTH) {
      await new Promise((resolve) => setTimeout(resolve, 800))
      return {
        accessToken: 'mock-candidate-token-' + Date.now(),
        refreshToken: 'mock-refresh-token-' + Date.now(),
        fullName: 'Nguyễn Văn An',
        role: ROLES.Candidate,
      }
    }

    const response = await fetch(`${API_BASE_URL}/auth/candidate/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(credentials),
    })

    if (!response.ok) {
      throw buildHttpError(response.status, await response.json().catch(() => ({})))
    }

    return response.json() as Promise<AuthResponse>
  },

  async candidateRegister(request: CandidateRegisterRequest): Promise<{ message: string }> {
    if (USE_FAKE_AUTH) {
      await new Promise((resolve) => setTimeout(resolve, 800))
      return { message: 'Candidate registered successfully.' }
    }

    const response = await fetch(`${API_BASE_URL}/auth/candidate/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    })

    return parseResponse<{ message: string }>(response)
  },

  async verifyEmail(email: string, token: string): Promise<{ message: string }> {
    if (USE_FAKE_AUTH) {
      await new Promise((resolve) => setTimeout(resolve, 500))
      return { message: 'Xác minh email thành công.' }
    }

    const response = await fetch(
      `${API_BASE_URL}/auth/candidate/verify-email?email=${encodeURIComponent(email)}&token=${encodeURIComponent(token)}`,
      { method: 'GET' }
    )

    return parseResponse<{ message: string }>(response)
  },

  async resendVerification(email: string): Promise<{ message: string }> {
    if (USE_FAKE_AUTH) {
      await new Promise((resolve) => setTimeout(resolve, 500))
      return { message: 'Email xác minh đã được gửi lại.' }
    }

    const response = await fetch(`${API_BASE_URL}/auth/candidate/resend-verification`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email }),
    })

    return parseResponse<{ message: string }>(response)
  },

  async resetPassword(request: {
    email: string
    token: string
    newPassword: string
  }): Promise<{ message: string }> {
    if (USE_FAKE_AUTH) {
      await new Promise((resolve) => setTimeout(resolve, 800))
      return { message: 'Password reset successfully.' }
    }

    const response = await fetch(`${API_BASE_URL}/auth/candidate/reset-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    })

    return parseResponse<{ message: string }>(response)
  },

  async forgotPassword(request: { email: string }): Promise<{ message: string }> {
    if (USE_FAKE_AUTH) {
      await new Promise((resolve) => setTimeout(resolve, 800))
      return { message: 'If the email exists, a reset link has been sent.' }
    }

    const response = await fetch(`${API_BASE_URL}/auth/candidate/forgot-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    })

    return parseResponse<{ message: string }>(response)
  },

  async staffForgotPassword(request: { email: string }): Promise<{ message: string }> {
    if (USE_FAKE_AUTH) {
      await new Promise((resolve) => setTimeout(resolve, 800))
      return { message: 'If the email exists, a reset link has been sent.' }
    }

    const response = await fetch(`${API_BASE_URL}/auth/staff/forgot-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    })

    return parseResponse<{ message: string }>(response)
  },

  async staffResetPassword(request: {
    email: string
    token: string
    newPassword: string
  }): Promise<{ message: string }> {
    if (USE_FAKE_AUTH) {
      await new Promise((resolve) => setTimeout(resolve, 800))
      return { message: 'Password reset successfully.' }
    }

    const response = await fetch(`${API_BASE_URL}/auth/staff/reset-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    })

    return parseResponse<{ message: string }>(response)
  },

  async verifyMagicLink(email: string, token: string): Promise<AuthResponse> {
    if (USE_FAKE_AUTH) {
      await new Promise((resolve) => setTimeout(resolve, 500))
      return {
        accessToken: 'mock-magic-link-token-' + Date.now(),
        refreshToken: 'mock-refresh-token-' + Date.now(),
        fullName: 'Nguyễn Văn An',
        role: ROLES.Candidate,
      }
    }

    const response = await fetch(
      `${API_BASE_URL}/auth/magic-link/verify?email=${encodeURIComponent(email)}&token=${encodeURIComponent(token)}`,
      { method: 'GET' }
    )

    const data = await parseResponse<{ message: string; token: string }>(response)
    return {
      accessToken: data.token,
      refreshToken: '',
      fullName: '',
      role: ROLES.Candidate,
    }
  },

  async logout(): Promise<void> {
    if (USE_FAKE_AUTH) return
    await fetch(`${API_BASE_URL}/auth/logout`, { method: 'POST' }).catch(() => {})
  },

  async refreshToken(refreshToken: string): Promise<{ accessToken: string }> {
    if (USE_FAKE_AUTH) {
      return { accessToken: 'mock-refreshed-token-' + Date.now() }
    }
    const response = await fetch(`${API_BASE_URL}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    })
    return parseResponse<{ accessToken: string }>(response)
  },

  async getCurrentUser(accessToken: string): Promise<User> {
    if (USE_FAKE_AUTH) {
      return {
        id: 'mock-user',
        email: 'mock@arisp.com',
        name: 'Mock User',
        role: ROLES.Candidate,
      }
    }
    const response = await fetch(`${API_BASE_URL}/auth/me`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    })
    return parseResponse<User>(response)
  },

  buildOAuthRedirectUrl(provider: string = 'Google', returnUrl: string = '/'): string {
    const encodedReturnUrl = encodeURIComponent(returnUrl)
    return `${API_BASE_URL}/auth/external/signin?provider=${encodeURIComponent(provider)}&returnUrl=${encodedReturnUrl}`
  },

  buildCandidateOAuthRedirectUrl(provider: string = 'Google', returnUrl: string = '/'): string {
    const encodedReturnUrl = encodeURIComponent(returnUrl)
    return `${API_BASE_URL}/auth/candidate/external/signin?provider=${encodeURIComponent(provider)}&returnUrl=${encodedReturnUrl}`
  },

  candidateLoginWithGoogle(): void {
    window.location.href = authService.buildCandidateOAuthRedirectUrl(
      'Google',
      `${window.location.origin}/auth/callback`
    )
  },

  parseOAuthCallback(url: string): {
    accessToken?: string
    role?: string
    status?: string
    message?: string
  } {
    const hash = url.split('#')[1] || ''
    const params = new URLSearchParams(hash)
    return {
      accessToken: params.get('access_token') || undefined,
      role: params.get('role') || undefined,
      status: params.get('status') || undefined,
      message: params.get('message') || undefined,
    }
  },
}
