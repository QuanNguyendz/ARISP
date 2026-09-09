import { useCallback, useRef, useState } from 'react'

/**
 * Bề ngang thật của một phần tử, cập nhật khi khung đổi kích thước.
 *
 * Cần cho bản xem trước A4: tờ giấy dựng ở kích thước thật 794px rồi mới thu nhỏ bằng CSS transform
 * (đo trước khi thu thì số đo mới đúng), nên phải biết cột chứa nó rộng bao nhiêu.
 *
 * **Dùng ref DẠNG HÀM, không phải `useRef` + `useEffect`.** Bản đầu dùng `useEffect` và không bao
 * giờ đo được: trang cấu hình mẫu return sớm khi đang tải, nên lúc effect chạy thì phần tử chưa tồn
 * tại; khi nó mount thật thì effect không chạy lại (deps không đổi) và bề ngang đứng ở 0 vĩnh viễn.
 * Ref dạng hàm được React gọi ĐÚNG lúc node gắn vào và gỡ ra, nên không phụ thuộc thứ tự render.
 *
 * `ResizeObserver` chứ không nghe `window.resize`: cột co lại khi đóng/mở thanh bên mà cửa sổ không
 * hề đổi kích thước.
 */
export function useContainerWidth<T extends HTMLElement = HTMLDivElement>() {
  const [width, setWidth] = useState(0)
  const observerRef = useRef<ResizeObserver | null>(null)

  const ref = useCallback((node: T | null) => {
    observerRef.current?.disconnect()
    observerRef.current = null

    if (!node) return

    setWidth(node.clientWidth)

    if (typeof ResizeObserver === 'undefined') return
    const observer = new ResizeObserver(() => setWidth(node.clientWidth))
    observer.observe(node)
    observerRef.current = observer
  }, [])

  return { ref, width }
}
