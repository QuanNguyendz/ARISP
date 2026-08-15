/** Định dạng dùng chung cho màn Phỏng vấn (trước đây chép đôi ở hai trang HR/Recruiter). */

export const fmtDate = (iso: string) =>
  new Date(iso).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })

export const fmtTime = (iso: string) =>
  new Date(iso).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })

export const fmtDur = (s?: number | null) => {
  if (!s || s <= 0) return null
  const m = Math.floor(s / 60)
  const sec = s % 60
  return sec === 0 ? `${m}m` : `${m}m ${sec}s`
}

export const initials = (name: string) =>
  name
    .trim()
    .split(/\s+/)
    .slice(-2)
    .map((n) => n[0])
    .join('')
    .toUpperCase()

/** So sánh một mốc ISO với ngày `yyyy-mm-dd` theo giờ ĐỊA PHƯƠNG (không dùng toISOString — lệch múi giờ). */
export const isSameDay = (iso: string, targetDateStr: string) => {
  if (!targetDateStr) return true
  const d = new Date(iso)
  const year = d.getFullYear()
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}` === targetDateStr
}
