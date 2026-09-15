/**
 * Phân loại VÒNG phỏng vấn — một chỗ duy nhất trả lời "vòng này thuộc loại nào".
 *
 * VÌ SAO GOM VỀ ĐÂY: phép phân loại này bị chép ở nhiều màn, và mỗi bản chép lại quên một giá trị
 * khác nhau. Màn cấu hình lịch viết `type === 'technical' ? 'Chuyên môn' : 'Sơ loại'` — một phép
 * chọn HAI nhánh cho một tập BA giá trị, nên vòng **trắc nghiệm** hiện ra thành "Sơ loại" và người
 * vận hành đọc tab `Vòng 1 · Sơ loại` trong khi tin khai `Vòng 1: Trắc nghiệm`. Không lỗi, không
 * cảnh báo — chỉ là một cái nhãn nói sai sự thật ở đúng màn dùng để xếp lịch.
 *
 * Đây là lần thứ hai cùng một phép phân loại này trôi (lần trước: thanh bước quy trình in ra
 * `detail.round 1` vì ba màn tự ghép nhãn theo ba cách).
 *
 * **Module này KHÔNG dịch.** Nó chỉ trả về một khoá ổn định; mỗi màn tra khoá đó trong namespace
 * i18n của mình. Trộn dịch vào đây sẽ buộc mọi màn phải dùng chung một namespace.
 */

/** Khoá loại vòng, ổn định để tra i18n. `other` = giá trị lạ (dữ liệu cũ, cấu hình tay). */
export type RoundTypeKey = 'screening' | 'technical' | 'onlineTest' | 'other'

/** Giá trị backend lưu ở `InterviewRoundConfig.RoundType`. */
export function roundTypeKey(roundType?: string | null): RoundTypeKey {
  switch ((roundType || '').trim().toLowerCase()) {
    case 'screening':
      return 'screening'
    case 'technical':
      return 'technical'
    case 'online_test':
      return 'onlineTest'
    default:
      return 'other'
  }
}

/** Vòng thi trắc nghiệm — ứng viên làm bài trực tuyến, không có Hiring Manager ngồi cùng. */
export const isOnlineTestRound = (roundType?: string | null): boolean =>
  roundTypeKey(roundType) === 'onlineTest'
