import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'

/**
 * Khung trượt ngang có **thanh kéo ở TRÊN** nội dung.
 *
 * <b>Vì sao không dùng thẳng `overflow-x-auto`.</b> Thanh kéo của trình duyệt luôn nằm ở đáy khung
 * cuộn. Khung ở đây cao vài màn hình (danh sách hồ sơ + khung đọc CV), nên muốn kéo ngang thì phải
 * cuộn dọc xuống tận đáy khối trước — đúng thứ tự ngược đời mà người dùng gặp phải.
 *
 * Cách làm: một thanh kéo THỨ HAI đặt phía trên, rỗng ruột, chỉ mang đúng bề rộng của nội dung; hai
 * khung đồng bộ `scrollLeft` cho nhau. Thanh trên `sticky` nên còn nhìn thấy nội dung là còn kéo
 * được, không phải đi tìm.
 *
 * Không overflow ngang thì thanh trên tự biến mất — một thanh kéo không kéo được gì chỉ gây nhiễu.
 */
export default function HorizontalScrollArea({
  children,
  className = '',
  innerClassName = '',
  stickyTop = 'var(--sticky-top, 1.5rem)',
}: {
  children: ReactNode
  className?: string
  /** Lớp cho phần tử bọc nội dung — nơi khai bề rộng tối thiểu (`lg:min-w-[64rem]`…). */
  innerClassName?: string
  /** Khoảng chừa cho thanh kéo trên khi dính mép — mặc định lấy biến của layout. */
  stickyTop?: string
}) {
  const barRef = useRef<HTMLDivElement>(null)
  const bodyRef = useRef<HTMLDivElement>(null)
  const innerRef = useRef<HTMLDivElement>(null)
  const [contentWidth, setContentWidth] = useState(0)
  const [overflowing, setOverflowing] = useState(false)

  /** Đang chép `scrollLeft` sang khung kia — cờ chặn vòng lặp sự kiện `scroll` dội qua dội lại. */
  const syncing = useRef(false)

  const measure = useCallback(() => {
    const inner = innerRef.current
    const body = bodyRef.current
    if (!inner || !body) return
    setContentWidth(inner.scrollWidth)
    setOverflowing(inner.scrollWidth - body.clientWidth > 1)
  }, [])

  useEffect(() => {
    measure()
    const inner = innerRef.current
    const body = bodyRef.current
    if (!inner || !body || typeof ResizeObserver === 'undefined') return

    // Theo dõi CẢ HAI: nội dung đổi bề rộng (chọn/bỏ chọn hồ sơ) và khung đổi bề rộng (thu nhỏ cửa sổ,
    // mở/đóng thanh điều hướng). Thiếu một trong hai thì thanh trên lệch với nội dung thật.
    const ro = new ResizeObserver(measure)
    ro.observe(inner)
    ro.observe(body)
    return () => ro.disconnect()
  }, [measure])

  const mirror = (from: HTMLDivElement | null, to: HTMLDivElement | null) => {
    if (syncing.current || !from || !to) return
    syncing.current = true
    to.scrollLeft = from.scrollLeft
    // Nhả cờ ở khung hình kế: sự kiện `scroll` do lệnh gán trên sinh ra được phát BẤT ĐỒNG BỘ.
    requestAnimationFrame(() => {
      syncing.current = false
    })
  }

  return (
    <div className={className}>
      {overflowing && (
        <div
          ref={barRef}
          onScroll={() => mirror(barRef.current, bodyRef.current)}
          style={{ top: stickyTop }}
          className="sticky z-10 overflow-x-auto overflow-y-hidden rounded-t-lg bg-ink-50/80 backdrop-blur dark:bg-ink-900/80"
        >
          <div style={{ width: contentWidth, height: 1 }} />
        </div>
      )}

      {/* Khung thật GIẤU thanh kéo của nó khi thanh trên đang hiện — hai thanh cho cùng một trục chỉ
          làm người dùng phân vân cái nào là cái thật. Không có thanh trên (chưa đo xong, hoặc không
          tràn) thì trả lại thanh mặc định, để không bao giờ có khung trượt mà không có cách nào kéo. */}
      <div
        ref={bodyRef}
        onScroll={() => mirror(bodyRef.current, barRef.current)}
        className={`overflow-x-auto ${overflowing ? '[scrollbar-width:none] [&::-webkit-scrollbar]:hidden' : ''}`}
      >
        <div ref={innerRef} className={innerClassName}>
          {children}
        </div>
      </div>
    </div>
  )
}
