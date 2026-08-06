import { useCallback, useEffect, useRef, useState } from 'react'
import { interviewService } from '@ari/shared/fservices/interview'

/**
 * Khoá màn hình cho buổi phỏng vấn THẬT tại Kiosk (ADR-054).
 *
 * GIỚI HẠN THẬT SỰ CỦA TRÌNH DUYỆT — đọc kỹ trước khi tin tưởng:
 * trang web KHÔNG chặn được Alt+Tab, phím Windows/Command, Ctrl+Alt+Del hay việc tắt máy.
 * Chỉ hệ điều hành/trình duyệt ở chế độ kiosk thật (Chrome `--kiosk`, Windows Assigned Access,
 * MDM) mới khoá cứng được. Ở đây ta làm hai lớp:
 *   1) NGĂN các lối thoát dễ: bật toàn màn hình, chặn menu chuột phải + phím tắt bắt được,
 *      cảnh báo khi đóng/tải lại trang, và **chặn giao diện phỏng vấn bằng lớp phủ** khi
 *      ứng viên rời toàn màn hình → muốn tiếp tục phải bấm quay lại.
 *   2) GHI LẠI mọi lần rời đi (số lần theo từng loại) gửi về server để tổng hợp vào kết quả —
 *      nếu vẫn có cách lách thì ít nhất nó để lại dấu vết trong báo cáo cho HR.
 */
export type KioskSignalType =
  | 'fullscreen_exit'
  | 'tab_hidden'
  | 'window_blur'
  | 'shortcut_blocked'
  | 'page_unload'

export interface KioskLockdownState {
  /** Đang ở chế độ toàn màn hình hay không. */
  isFullscreen: boolean
  /** Tổng số lần rời khỏi màn phỏng vấn (mọi loại). */
  exitCount: number
  /** Chi tiết theo từng loại — hiện ở màn kết thúc + gửi kèm payload. */
  counters: Record<KioskSignalType, number>
  /** Bật toàn màn hình — PHẢI gọi trong một thao tác người dùng (click/keydown). */
  requestFullscreen: () => Promise<void>
}

const EMPTY_COUNTERS: Record<KioskSignalType, number> = {
  fullscreen_exit: 0,
  tab_hidden: 0,
  window_blur: 0,
  shortcut_blocked: 0,
  page_unload: 0,
}

/** Phím tắt chặn được bằng preventDefault (Alt+Tab / phím Windows thì KHÔNG). */
function isBlockedShortcut(e: KeyboardEvent): boolean {
  if (e.key === 'F11' || e.key === 'F5') return true
  if (e.ctrlKey || e.metaKey) {
    const k = e.key.toLowerCase()
    // in/lưu/xem nguồn/tìm/mở tab mới/đóng tab/tải lại
    if (['p', 's', 'u', 'f', 't', 'n', 'w', 'r'].includes(k)) return true
  }
  return false
}

export function useKioskLockdown(sessionId: string | null, enabled: boolean): KioskLockdownState {
  const [isFullscreen, setIsFullscreen] = useState(() => !!document.fullscreenElement)
  const [counters, setCounters] = useState<Record<KioskSignalType, number>>({ ...EMPTY_COUNTERS })
  const sessionIdRef = useRef(sessionId)
  sessionIdRef.current = sessionId

  /** Ghi nhận 1 lần rời đi: đếm cục bộ + báo server (best-effort, không chặn UI). */
  const report = useCallback((type: KioskSignalType) => {
    setCounters((prev) => {
      const next = { ...prev, [type]: prev[type] + 1 }
      const id = sessionIdRef.current
      if (id) {
        interviewService
          .reportSessionSignal(id, type, { at: new Date().toISOString(), occurrence: next[type] })
          .catch(() => {
            /* mất mạng thì bỏ qua — không được làm gián đoạn buổi phỏng vấn */
          })
      }
      return next
    })
  }, [])

  const requestFullscreen = useCallback(async () => {
    try {
      if (!document.fullscreenElement) await document.documentElement.requestFullscreen()
    } catch {
      // Trình duyệt từ chối (thiếu thao tác người dùng / policy) — lớp phủ vẫn giữ ứng viên lại.
    }
  }, [])

  useEffect(() => {
    if (!enabled) return

    const onFullscreenChange = () => {
      const active = !!document.fullscreenElement
      setIsFullscreen(active)
      if (!active) report('fullscreen_exit')
    }

    const onVisibility = () => {
      if (document.visibilityState === 'hidden') report('tab_hidden')
    }

    const onBlur = () => report('window_blur')

    const onContextMenu = (e: MouseEvent) => e.preventDefault()

    const onKeyDown = (e: KeyboardEvent) => {
      if (!isBlockedShortcut(e)) return
      e.preventDefault()
      e.stopPropagation()
      report('shortcut_blocked')
    }

    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      // Gửi bằng sendBeacon: request thường bị huỷ khi trang đang đóng.
      const id = sessionIdRef.current
      if (id) interviewService.reportSignalBeacon(id, 'page_unload')
      e.preventDefault()
      e.returnValue = ''
    }

    document.addEventListener('fullscreenchange', onFullscreenChange)
    document.addEventListener('visibilitychange', onVisibility)
    window.addEventListener('blur', onBlur)
    document.addEventListener('contextmenu', onContextMenu)
    document.addEventListener('keydown', onKeyDown, true)
    window.addEventListener('beforeunload', onBeforeUnload)

    return () => {
      document.removeEventListener('fullscreenchange', onFullscreenChange)
      document.removeEventListener('visibilitychange', onVisibility)
      window.removeEventListener('blur', onBlur)
      document.removeEventListener('contextmenu', onContextMenu)
      document.removeEventListener('keydown', onKeyDown, true)
      window.removeEventListener('beforeunload', onBeforeUnload)
    }
  }, [enabled, report])

  const exitCount = Object.values(counters).reduce((sum, n) => sum + n, 0)
  return { isFullscreen, exitCount, counters, requestFullscreen }
}

/** Thoát toàn màn hình khi rời phòng phỏng vấn (màn kết thúc / quay về nhập mã). */
export async function exitFullscreen(): Promise<void> {
  try {
    if (document.fullscreenElement) await document.exitFullscreen()
  } catch {
    /* noop */
  }
}
