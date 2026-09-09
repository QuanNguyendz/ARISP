import type { HrApplicationItem } from '@ari/shared/types/application'

/**
 * Các BƯỚC trên thanh quy trình của màn chi tiết tin tuyển dụng.
 *
 * Đây là phễu THẬT của ARISP, không phải bảng bước của một ATS khác chép sang: mỗi bước ứng đúng
 * một trạng thái hồ sơ đã có (`ApplicationStatuses`), nên con số trên thanh luôn cộng lại bằng tổng
 * hồ sơ và không có ô nào "không biết xếp vào đâu".
 *
 * Vì sao các vòng phỏng vấn KHÔNG nằm cứng ở đây: số vòng do từng tin cấu hình (`roundConfigs`),
 * nên chúng được chèn vào giữa lúc chạy — xem `buildStages`.
 */

/** Một bước trên thanh quy trình. */
export interface PipelineStage {
  id: string
  label: string
  count: number

  /** Hồ sơ thuộc bước này. Tính sẵn để bảng bên dưới không phải lọc lại. */
  items: HrApplicationItem[]

  /**
   * Bước "kết thúc xấu" (bị loại, tự rút, từ chối offer). Tách ra vì nó nằm NGOÀI dòng chảy trái →
   * phải và được vẽ ở đầu thanh, giống chỗ "Không phù hợp" của các ATS quen dùng.
   */
  isRejectedBucket?: boolean

  /** Bước kết thúc thành công — tô màu khác để nhìn ra ngay ở cuối phễu. */
  isSuccess?: boolean
}

/** Trạng thái coi là đã rời phễu theo hướng xấu. */
const REJECTED = new Set([
  'cv_rejected',
  'not_pass',
  'offer_declined',
  'withdrawn',
  // Dữ liệu cũ trước khi bảng trạng thái được chuẩn hoá — vẫn phải xếp được vào đâu đó.
  'failed',
  'rejected',
])

export interface RoundConfigLike {
  roundNumber: number
  roundType?: string | null
}

/**
 * Dựng danh sách bước từ hồ sơ thật + cấu hình vòng của tin.
 *
 * `roundLabel` do nơi gọi truyền vào để chuỗi vẫn đi qua i18n — module này cố ý không tự dịch.
 */
export function buildStages(
  apps: HrApplicationItem[],
  rounds: RoundConfigLike[],
  labels: {
    rejected: string
    newApplicants: string
    hmReview: string
    scheduling: string
    passed: string
    offer: string
    hired: string
    roundLabel: (r: RoundConfigLike) => string
  }
): PipelineStage[] {
  const pick = (fn: (a: HrApplicationItem) => boolean) => apps.filter(fn)

  const stage = (id: string, label: string, items: HrApplicationItem[], extra?: Partial<PipelineStage>) =>
    ({ id, label, count: items.length, items, ...extra }) as PipelineStage

  // Đang phỏng vấn thì tách theo VÒNG: một tin có thể có 3 vòng, gộp chung lại thì Recruiter không
  // biết ai đang ở đâu — mà đó chính là câu hỏi họ mở màn này để trả lời.
  const inInterview = pick((a) => a.status === 'interview')
  const roundStages = [...rounds]
    .sort((a, b) => a.roundNumber - b.roundNumber)
    .map((r) =>
      stage(
        `round_${r.roundNumber}`,
        labels.roundLabel(r),
        inInterview.filter((a) => (a.currentRound ?? 1) === r.roundNumber)
      )
    )

  // Hồ sơ đang phỏng vấn nhưng số vòng không khớp cấu hình nào (tin sửa cấu hình sau khi ứng viên
  // đã vào vòng): dồn vào vòng cuối thay vì để biến mất khỏi thanh.
  const covered = new Set(roundStages.flatMap((s) => s.items.map((a) => a.id)))
  const orphanInterview = inInterview.filter((a) => !covered.has(a.id))
  if (orphanInterview.length > 0 && roundStages.length > 0) {
    const last = roundStages[roundStages.length - 1]
    last.items = [...last.items, ...orphanInterview]
    last.count = last.items.length
  }

  const newStage = stage(
    'new',
    labels.newApplicants,
    pick((a) => a.status === 'cv_submitted' || a.status === 'invited')
  )

  const stages = [
    stage('rejected', labels.rejected, pick((a) => REJECTED.has(a.status)), {
      isRejectedBucket: true,
    }),
    newStage,
    stage('hm_review', labels.hmReview, pick((a) => a.status === 'hm_review')),
    stage('scheduling', labels.scheduling, pick((a) => a.status === 'screening')),
    ...roundStages,
    stage('passed', labels.passed, pick((a) => a.status === 'pass')),
    stage('offer', labels.offer, pick((a) => a.status === 'offer')),
    stage('hired', labels.hired, pick((a) => a.status === 'hired'), { isSuccess: true }),
  ]

  /**
   * CHỐT CHẶN: mọi hồ sơ phải nằm ở ĐÚNG MỘT bước.
   *
   * Một trạng thái mới thêm vào backend, hay một giá trị cũ còn sót trong DB, sẽ không khớp bảng trên và
   * người đó **biến mất khỏi màn hình** — không báo lỗi, không dấu vết, Recruiter chỉ thấy thiếu người.
   * Dồn về "Mới ứng tuyển" để luôn còn đường nhìn thấy và xử lý, thay vì âm thầm rơi ra ngoài.
   */
  const claimed = new Set(stages.flatMap((s) => s.items.map((a) => a.id)))
  const unclaimed = apps.filter((a) => !claimed.has(a.id))
  if (unclaimed.length > 0) {
    newStage.items = [...newStage.items, ...unclaimed]
    newStage.count = newStage.items.length
  }

  return stages
}

/** Tuổi từ ngày sinh — bảng ứng viên hiện tuổi chứ không hiện ngày sinh. */
export function ageFrom(dateOfBirth?: string | null): number | null {
  if (!dateOfBirth) return null
  const dob = new Date(dateOfBirth)
  if (Number.isNaN(dob.getTime())) return null
  const now = new Date()
  let age = now.getFullYear() - dob.getFullYear()
  const monthDiff = now.getMonth() - dob.getMonth()
  if (monthDiff < 0 || (monthDiff === 0 && now.getDate() < dob.getDate())) age -= 1
  return age >= 0 && age < 120 ? age : null
}
