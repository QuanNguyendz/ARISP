import { useEffect, useState } from 'react'

/**
 * Trả về `value` sau khi nó ngừng đổi `delay` ms — dùng cho ô tìm kiếm "gõ tới đâu lọc tới đó"
 * để không bắn request theo từng ký tự (ô nhập vẫn phản hồi tức thì).
 */
export function useDebouncedValue<T>(value: T, delay = 300): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(timer)
  }, [value, delay])

  return debounced
}

export default useDebouncedValue
