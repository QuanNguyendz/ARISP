import { useTranslation } from 'react-i18next'
import { Eye, EyeOff } from 'lucide-react'

interface PasswordToggleProps {
  /** Mật khẩu đang hiện dạng chữ hay không. */
  visible: boolean
  onToggle: () => void
  className?: string
}

/**
 * Nút con mắt hiện/ẩn mật khẩu, đặt bên trong ô nhập.
 *
 * Trước đây mỗi màn tự dựng một `<button>` bọc thẳng icon 16px — vùng bấm đúng bằng
 * cái icon, rất dễ trượt tay (nhất là trên màn cảm ứng). Ở đây `p-2` nới vùng bấm ra
 * ~32px mà **không vẽ viền** (viền lộ ra thành cái ô vuông giữa ô nhập, nhìn rối);
 * nền chỉ hiện mờ lúc rê chuột để biết đang trỏ trúng. `-my-1.5` bù lại `py-2.5` của
 * ô bọc ngoài nên ô mật khẩu KHÔNG cao hơn ô email bên trên.
 *
 * `tabIndex={-1}`: người dùng gõ xong mật khẩu thường nhấn Tab để tới nút Đăng nhập —
 * chen nút này vào giữa làm gãy nhịp. Vẫn bấm được bằng chuột và vẫn đọc được bằng
 * trình đọc màn hình nhờ `aria-label` + `aria-pressed`.
 */
export default function PasswordToggle({ visible, onToggle, className = '' }: PasswordToggleProps) {
  const { t } = useTranslation('common')

  return (
    <button
      type="button"
      onClick={onToggle}
      tabIndex={-1}
      aria-label={visible ? t('button.hidePassword') : t('button.showPassword')}
      aria-pressed={visible}
      className={`-my-1.5 -mr-1.5 shrink-0 rounded-lg p-2 text-ink-400 transition hover:bg-ink-100/70 hover:text-ink-600 dark:hover:bg-white/10 dark:hover:text-ink-200 ${className}`}
    >
      {visible ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
    </button>
  )
}
