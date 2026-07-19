/**
 * Skeleton — khối "giả lập nội dung đang tải" với dải sáng shimmer **chuyển động**
 * quét ngang liên tục (kiểu Facebook/LinkedIn). Một dải màu nhạt hơn trượt qua nền
 * xám → thể hiện rõ trang đang tải, khiến người dùng kiên nhẫn chờ hơn.
 *
 * Cơ chế: gradient ngang có bề rộng 200% nền, animation đổi background-position để
 * dải sáng "chạy" qua. Tự đổi tông ở dark mode.
 */
export function Skeleton({ className = '' }: { className?: string }) {
  return (
    <div
      className={
        'animate-shimmer rounded-md bg-[length:200%_100%] ' +
        'bg-gradient-to-r from-ink-200 via-ink-50 to-ink-200 ' +
        'dark:from-ink-800 dark:via-ink-700 dark:to-ink-800 ' +
        className
      }
    />
  )
}

export default Skeleton
