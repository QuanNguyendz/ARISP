import { setInterviewSessionToken } from '@ari/shared/api/apiClient'
import type { KioskSessionInfo } from '@ari/shared/fservices/interview'

/**
 * Phiên Kiosk đang chạy trên MÁY này (ADR-052).
 *
 * Lưu ở `sessionStorage` (không phải localStorage): máy Kiosk đặt nơi công cộng, đóng tab là
 * mất sạch — nhưng lỡ reload/mất điện giữa buổi thì vào lại được phòng cũ mà không cần mã mới
 * (mã 6 ký tự là one-time-use, cấp lại phải nhờ nhân sự). Xoá ngay khi buổi kết thúc.
 */
const STORAGE_KEY = 'arisp-kiosk-session'

export interface KioskSession {
  sessionId: string
  token: string
  tokenExpiresAt?: string | null
  candidateName?: string | null
  jobTitle?: string | null
  roundNumber: number
  roundType?: string | null
  language: string
  /** Đã vào phòng và bắt đầu phỏng vấn chưa (để reload biết là đang dở buổi). */
  started?: boolean
}

export function saveKioskSession(info: KioskSessionInfo): KioskSession | null {
  if (!info.valid || !info.sessionId || !info.token) return null
  const session: KioskSession = {
    sessionId: info.sessionId,
    token: info.token,
    tokenExpiresAt: info.tokenExpiresAt,
    candidateName: info.candidateName,
    jobTitle: info.jobTitle,
    roundNumber: info.roundNumber,
    roundType: info.roundType,
    language: info.language,
  }
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session))
  } catch {
    /* sessionStorage bị chặn — vẫn chạy được trong tab hiện tại nhờ token đã nạp vào apiClient */
  }
  setInterviewSessionToken(session.token)
  return session
}

export function loadKioskSession(): KioskSession | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as KioskSession
    if (!session?.sessionId || !session?.token) return null
    // Token hết hạn → coi như không có phiên (buổi phỏng vấn đã quá dài / máy để quên).
    if (session.tokenExpiresAt && new Date(session.tokenExpiresAt).getTime() <= Date.now()) {
      clearKioskSession()
      return null
    }
    setInterviewSessionToken(session.token)
    return session
  } catch {
    return null
  }
}

export function markKioskSessionStarted(): void {
  const session = loadKioskSession()
  if (!session) return
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ ...session, started: true }))
  } catch {
    /* noop */
  }
}

export function clearKioskSession(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    /* noop */
  }
  setInterviewSessionToken(null)
}
