import { useState } from 'react'
import {
  KeyRound,
  Lock,
  X,
  Eye,
  EyeOff,
  Loader2,
  Save,
  CheckCircle2,
  AlertCircle,
  AlertTriangle,
} from 'lucide-react'
import { profileService } from '@services/profile/profileService'

const inputWrap =
  'flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2.5 focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100'

/**
 * Modal đổi (hoặc đặt lần đầu cho tài khoản Google) mật khẩu đăng nhập của ứng viên.
 * Gọi POST /api/portal/profile/change-password.
 */
export default function ChangePasswordModal({
  passwordSet,
  onClose,
  onSuccess,
}: {
  passwordSet: boolean
  onClose: () => void
  onSuccess: () => void
}) {
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [show, setShow] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  const rules = [
    { ok: next.length >= 8, label: 'Ít nhất 8 ký tự' },
    { ok: /[A-Z]/.test(next), label: 'Một chữ hoa' },
    { ok: /\d/.test(next), label: 'Một chữ số' },
    { ok: /[!@#$%^&*]/.test(next), label: 'Một ký tự đặc biệt (!@#$%^&*)' },
  ]
  const allValid = rules.every((r) => r.ok)
  const matched = next.length > 0 && next === confirm

  async function submit() {
    setError('')
    if (passwordSet && !current) {
      setError('Vui lòng nhập mật khẩu hiện tại.')
      return
    }
    if (!allValid) {
      setError('Mật khẩu mới chưa đáp ứng đủ điều kiện.')
      return
    }
    if (!matched) {
      setError('Mật khẩu xác nhận không khớp.')
      return
    }
    setSubmitting(true)
    try {
      await profileService.changePassword({
        currentPassword: passwordSet ? current : undefined,
        newPassword: next,
      })
      onSuccess()
    } catch (e) {
      const err = e as { response?: { data?: { message?: string } }; message?: string }
      setError(err?.response?.data?.message || err?.message || 'Không đổi được mật khẩu.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-ink-900/40 p-4 backdrop-blur-sm"
      onClick={onClose}
    >
      <div
        className="w-full max-w-md rounded-2xl border border-ink-200 bg-white p-6 shadow-card-hover"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h3 className="flex items-center gap-2 font-display text-lg font-bold text-ink-900">
            <KeyRound className="h-5 w-5 text-brand-600" />
            {passwordSet ? 'Đổi mật khẩu' : 'Đặt mật khẩu'}
          </h3>
          <button
            onClick={onClose}
            className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 hover:text-ink-700"
            aria-label="Đóng"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {!passwordSet && (
          <p className="mb-4 rounded-xl bg-brand-50 px-3 py-2 text-xs text-brand-700">
            Tài khoản Google của bạn chưa có mật khẩu. Đặt mật khẩu để có thể đăng nhập bằng email +
            mật khẩu.
          </p>
        )}

        <div className="space-y-3">
          {passwordSet && (
            <PwdInput
              icon={Lock}
              placeholder="Mật khẩu hiện tại"
              value={current}
              show={show}
              onChange={setCurrent}
            />
          )}
          <PwdInput
            icon={KeyRound}
            placeholder="Mật khẩu mới"
            value={next}
            show={show}
            onChange={setNext}
          />
          <PwdInput
            icon={KeyRound}
            placeholder="Xác nhận mật khẩu mới"
            value={confirm}
            show={show}
            onChange={setConfirm}
          />
          <button
            type="button"
            onClick={() => setShow((s) => !s)}
            className="inline-flex items-center gap-1.5 text-xs text-ink-500 hover:text-brand-700"
          >
            {show ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
            {show ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
          </button>
        </div>

        {/* Checklist điều kiện */}
        <ul className="mt-3 grid grid-cols-2 gap-1.5">
          {rules.map((r) => (
            <li
              key={r.label}
              className={`flex items-center gap-1.5 text-xs ${
                r.ok ? 'text-emerald-600' : 'text-ink-400'
              }`}
            >
              {r.ok ? (
                <CheckCircle2 className="h-3.5 w-3.5" />
              ) : (
                <AlertCircle className="h-3.5 w-3.5" />
              )}
              {r.label}
            </li>
          ))}
        </ul>
        {confirm.length > 0 && !matched && (
          <p className="mt-2 text-xs text-red-600">Mật khẩu xác nhận không khớp.</p>
        )}

        {error && (
          <div className="mt-3 flex items-start gap-2 rounded-xl bg-red-50 px-3 py-2 text-xs text-red-700">
            <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {error}
          </div>
        )}

        <div className="mt-5 flex justify-end gap-2">
          <button
            onClick={onClose}
            className="rounded-xl border border-ink-200 px-4 py-2 text-sm font-semibold text-ink-600 hover:bg-ink-50"
          >
            Huỷ
          </button>
          <button
            onClick={submit}
            disabled={submitting || !allValid || !matched || (passwordSet && !current)}
            className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-5 py-2 text-sm font-bold text-white hover:bg-brand-700 disabled:opacity-50"
          >
            {submitting ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Save className="h-4 w-4" />
            )}
            {passwordSet ? 'Đổi mật khẩu' : 'Đặt mật khẩu'}
          </button>
        </div>
      </div>
    </div>
  )
}

function PwdInput({
  icon: Icon,
  placeholder,
  value,
  show,
  onChange,
}: {
  icon: typeof Lock
  placeholder: string
  value: string
  show: boolean
  onChange: (v: string) => void
}) {
  return (
    <div className={inputWrap}>
      <Icon className="h-4 w-4 shrink-0 text-ink-400" />
      <input
        type={show ? 'text' : 'password'}
        className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
        placeholder={placeholder}
        value={value}
        autoComplete="new-password"
        onChange={(e) => onChange(e.target.value)}
      />
    </div>
  )
}
