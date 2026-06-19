export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api'
export const WS_BASE_URL = import.meta.env.VITE_WS_BASE_URL || 'ws://localhost:5000'

// Gốc backend phục vụ file tĩnh (vd /uploads/...), bỏ hậu tố "/api" của API_BASE_URL.
export const ASSET_BASE_URL = API_BASE_URL.replace(/\/api\/?$/, '')

/** Ghép đường dẫn file tương đối từ backend (vd "/uploads/x.pdf") thành URL tuyệt đối. */
export function resolveAssetUrl(path?: string | null): string {
  if (!path) return ''
  if (/^https?:\/\//i.test(path)) return path
  return `${ASSET_BASE_URL}${path.startsWith('/') ? '' : '/'}${path}`
}

export const ROLES = {
  SuperAdmin: 'Super_admin',
  HRAdmin: 'Hr_admin',
  Recruiter: 'Recruiter',
  Candidate: 'Candidate',
} as const

export const INTERVIEW_MODES = {
  Remote: 'Remote',
  OnSite: 'OnSite',
  Both: 'Both',
} as const

export const SESSION_TYPES = {
  Real: 'real',
  Practice: 'practice',
} as const

export const ROUND_TYPES = {
  Screening: 'Screening',
  Technical: 'Technical',
} as const

export const VERDICT = {
  Pass: 'Pass',
  NotPass: 'NotPass',
  Pending: 'Pending',
} as const

export const TIMEOUTS = {
  interviewCodeTTL: 2 * 60 * 60 * 1000,
  magicLinkTTL: 15 * 60 * 1000,
  inviteLinkTTL: 72 * 60 * 60 * 1000,
} as const
