import { create } from 'zustand'

/**
 * Từ khoá tìm việc dùng chung cho ô tìm ở header (CandidateHeader) và ô tìm ở hero trang việc làm
 * (FindJobPage) — gõ ở đâu cũng lọc ngay danh sách, hai ô luôn hiển thị cùng một từ khoá.
 */
interface JobSearchState {
  query: string
  /**
   * Vừa gõ ở màn khác nên phải điều hướng sang trang việc làm. Header được render lại theo từng
   * trang → input cũ bị unmount; cờ này để header mới mount lấy lại con trỏ, gõ không bị đứt.
   */
  refocus: boolean
  setQuery: (query: string) => void
  requestRefocus: () => void
  clearRefocus: () => void
  reset: () => void
}

export const useJobSearchStore = create<JobSearchState>((set) => ({
  query: '',
  refocus: false,
  setQuery: (query) => set({ query }),
  requestRefocus: () => set({ refocus: true }),
  clearRefocus: () => set({ refocus: false }),
  reset: () => set({ query: '', refocus: false }),
}))

export default useJobSearchStore
