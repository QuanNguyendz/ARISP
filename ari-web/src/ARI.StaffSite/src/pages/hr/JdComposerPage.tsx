import JdComposerView from '@/components/jdComposer/JdComposerView'

/**
 * Soạn bản mô tả công việc theo mẫu công ty (ADR-064). Server đã lọc phạm vi theo phiếu nên hai
 * khu vực dùng chung một view — xem chú thích ở component.
 */
export default function JdComposerPage() {
  return <JdComposerView />
}
