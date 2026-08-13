import { create } from 'zustand'

/**
 * Nguồn chân lý của theme là **một** khoá localStorage `theme` với 3 giá trị:
 * `'light'` | `'dark'` | `'system'`. Không có khoá = chưa chọn = **sáng**.
 *
 * Script inline trong `index.html` của mỗi site đọc đúng khoá này và gắn class
 * `dark` lên <html> TRƯỚC khi React render (tránh nháy màu). Store dưới đây chỉ
 * đọc lại trạng thái đã được script áp và ghi lại khi người dùng đổi — nên KHÔNG
 * dùng `persist` của zustand (trước đây persist ghi khoá riêng `arisp-theme` mà
 * script boot không hề đọc, khiến nút đổi theme mất tác dụng sau khi tải lại).
 */
export const THEME_STORAGE_KEY = 'theme'

export type ThemeMode = 'light' | 'dark' | 'system'

function prefersDark(): boolean {
  return typeof window !== 'undefined' && window.matchMedia('(prefers-color-scheme: dark)').matches
}

/** Đọc lựa chọn đã lưu; giá trị lạ/không có → `'light'` (mặc định của hệ thống). */
export function readThemeMode(): ThemeMode {
  try {
    const saved = localStorage.getItem(THEME_STORAGE_KEY)
    if (saved === 'dark' || saved === 'light' || saved === 'system') return saved
  } catch {
    /* localStorage bị chặn (private mode, iframe...) → dùng mặc định */
  }
  return 'light'
}

/** Gắn/gỡ class `dark` trên <html> và lưu lựa chọn. Trả về theme đang hiển thị. */
export function applyThemeMode(mode: ThemeMode): boolean {
  const isDark = mode === 'system' ? prefersDark() : mode === 'dark'
  document.documentElement.classList.toggle('dark', isDark)
  try {
    localStorage.setItem(THEME_STORAGE_KEY, mode)
  } catch {
    /* không lưu được thì vẫn áp cho phiên hiện tại */
  }
  return isDark
}

interface ThemeState {
  isDark: boolean
  toggleTheme: () => void
  setTheme: (isDark: boolean) => void
}

/** Store cho nút bật/tắt sáng–tối (chỉ 2 trạng thái). Màn Cài đặt của ứng viên
 *  có thêm lựa chọn "theo hệ thống" nên dùng thẳng `applyThemeMode`. */
export const useThemeStore = create<ThemeState>()((set) => ({
  // Lấy từ DOM: script inline đã quyết định xong trước khi React chạy.
  isDark:
    typeof document !== 'undefined' && document.documentElement.classList.contains('dark'),

  toggleTheme: () =>
    set((state) => ({ isDark: applyThemeMode(state.isDark ? 'light' : 'dark') })),

  setTheme: (isDark: boolean) => set({ isDark: applyThemeMode(isDark ? 'dark' : 'light') }),
}))
