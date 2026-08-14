/**
 * Template Playbook tối ưu cho pipeline RAG của ARISP.
 *
 * Mọi quy ước dưới đây bám theo `rag-service/app/rag/chunker.py` (mirror của bản .NET):
 *   - Văn bản bị cắt theo DÒNG TRỐNG (`\n\n`), KHÔNG phải theo tiêu đề markdown.
 *   - Khối < 1000 ký tự → giữ nguyên thành 1 chunk.
 *   - Khối >= 1000 ký tự → cắt cứng mỗi 500 ký tự, không nể câu chữ (đứt giữa câu, giữa từ).
 *   - Lúc phỏng vấn, retriever trả về từng chunk RỜI — không kèm đoạn trước/sau.
 *
 * Hai hệ quả quyết định hình dạng file này:
 *   1. Mỗi khối phải tự hiểu được khi đứng một mình → nhắc lại vị trí/vòng/tiêu chí trong khối.
 *   2. KHÔNG có tiêu đề markdown đứng riêng. `## Ngân hàng câu hỏi` cách nội dung một dòng
 *      trống sẽ thành một chunk ~25 ký tự, retrieve lên chỉ là nhiễu. Vai trò phân loại được
 *      giao cho tiền tố `[NHÃN]` — vừa là nhãn cho người đọc, vừa là nội dung thật cho AI.
 *
 * Phần hướng dẫn sử dụng nằm ở màn Trợ giúp chứ không nhét vào file: chú thích trong file
 * cũng bị chunk và nạp vào RAG y như nội dung thật.
 */
export const PLAYBOOK_TEMPLATE_FILENAME = 'ARISP-playbook-template.md'

export const PLAYBOOK_TEMPLATE = `[HƯỚNG DẪN — XOÁ TOÀN BỘ KHỐI NÀY TRƯỚC KHI UPLOAD] Thay mọi chỗ <...> bằng nội dung thật, xoá các khối không dùng. Giữ đúng một dòng trống giữa hai khối và giữ mỗi khối dưới 1000 ký tự. Không thêm tiêu đề đứng riêng một dòng.

[PHONG CÁCH] Vị trí <TÊN VỊ TRÍ>, vòng <N>: AI giữ giọng thân thiện - chuyên nghiệp, xưng "tôi", gọi ứng viên là "bạn". Mỗi lượt hỏi đúng MỘT câu, không gộp nhiều ý. Sau câu trả lời, phản hồi ngắn một câu rồi mới hỏi tiếp. Không khen quá đà, không kết luận đúng/sai ngay tại chỗ.

[PHONG CÁCH] Vị trí <TÊN VỊ TRÍ>: khi ứng viên trả lời lan man quá 2 phút, cắt lịch sự bằng "Cảm ơn bạn, mình muốn hỏi sâu hơn một chút" rồi hỏi tiếp. Khi ứng viên im lặng quá 15 giây, diễn đạt lại câu hỏi theo cách khác thay vì bỏ qua câu.

[BẮT BUỘC] Vị trí <TÊN VỊ TRÍ>, vòng <N>, câu 1: <nội dung câu hỏi bắt buộc>. Mục đích: đo <năng lực>. Ứng viên đạt khi nêu được: <ý 1>, <ý 2>, <ý 3>.

[BẮT BUỘC] Vị trí <TÊN VỊ TRÍ>, vòng <N>, câu 2: <nội dung câu hỏi bắt buộc>. Mục đích: đo <năng lực>. Ứng viên đạt khi nêu được: <ý 1>, <ý 2>.

[CÂU HỎI - CƠ BẢN] Vị trí <TÊN VỊ TRÍ>, chủ đề <chủ đề>: <câu hỏi>. Dùng khi ứng viên mới bắt đầu hoặc vừa trả lời chưa tốt câu trước. Đáp án mong đợi: <ý 1>, <ý 2>.

[CÂU HỎI - TRUNG BÌNH] Vị trí <TÊN VỊ TRÍ>, chủ đề <chủ đề>: <câu hỏi>. Dùng khi ứng viên đã trả lời tốt câu cơ bản. Đáp án mong đợi: <ý 1>, <ý 2>. Câu đào sâu nếu còn thời gian: <câu phụ>.

[CÂU HỎI - NÂNG CAO] Vị trí <TÊN VỊ TRÍ>, chủ đề <chủ đề>: <câu hỏi>. Chỉ dùng khi ứng viên đã trả lời tốt mức trung bình. Đáp án mong đợi: <ý 1>, <ý 2>. Dấu hiệu ứng viên thực sự giỏi: <mô tả>.

[TIÊU CHÍ] Vị trí <TÊN VỊ TRÍ>, tiêu chí "<tên tiêu chí — dùng ĐÚNG tên trong scoring rubric của tin tuyển dụng>", trọng số <X>%: đo <mô tả ngắn>. Mức 80-100: <hành vi quan sát được>. Mức 60-79: <mô tả>. Dưới 60: <mô tả>.

[TIÊU CHÍ] Vị trí <TÊN VỊ TRÍ>, tiêu chí "<tên tiêu chí 2>", trọng số <X>%: đo <mô tả ngắn>. Mức 80-100: <mô tả>. Mức 60-79: <mô tả>. Dưới 60: <mô tả>.

[CẢNH BÁO] Vị trí <TÊN VỊ TRÍ>: nếu ứng viên <hành vi hoặc câu trả lời đáng ngờ>, hỏi lại một câu kiểm chứng: "<câu hỏi>". Nếu vẫn không làm rõ được, ghi nhận vào phần nhận xét của báo cáo.

[CẢNH BÁO] Vị trí <TÊN VỊ TRÍ>: nếu ứng viên mô tả dự án nhưng không nói được vai trò cá nhân, hỏi "Phần nào trong đó do chính bạn làm, và bạn đã quyết định điều gì?". Trả lời chung chung lần thứ hai là dấu hiệu cần lưu ý trong báo cáo.

[CẤM] Trong mọi buổi phỏng vấn, AI không hỏi về: tình trạng hôn nhân, dự định sinh con, tôn giáo, quê quán, quan điểm chính trị, tình trạng sức khoẻ hoặc khuyết tật không liên quan trực tiếp tới công việc, và mức lương ở công ty cũ.

[CẤM] Nếu ứng viên tự nhắc tới thông tin cá nhân thuộc nhóm cấm, AI ghi nhận lịch sự rồi chuyển ngay về nội dung chuyên môn, không hỏi thêm và không đưa vào đánh giá.

[VĂN HOÁ] Công ty đề cao <giá trị 1>, biểu hiện là <hành vi cụ thể>. Câu hỏi thăm dò: "<câu hỏi>". Trả lời cho thấy phù hợp: <mô tả>. Trả lời cho thấy chưa phù hợp: <mô tả>.

[VĂN HOÁ] Công ty đề cao <giá trị 2>, biểu hiện là <hành vi cụ thể>. Câu hỏi thăm dò: "<câu hỏi>". Trả lời cho thấy phù hợp: <mô tả>.

[KỊCH BẢN] Vị trí <TÊN VỊ TRÍ>, vòng <N>: đưa tình huống "<mô tả ngắn, đủ dữ kiện để ứng viên trả lời được>". Yêu cầu ứng viên trình bày hướng xử lý. Cần nghe được: <ý 1>, <ý 2>, <ý 3>. Nếu ứng viên bí, gợi ý: "<gợi ý>".

[KẾT THÚC] Vị trí <TÊN VỊ TRÍ>: khi đã hỏi hết câu bắt buộc hoặc gần hết giờ, AI cảm ơn ứng viên, cho biết bộ phận nhân sự sẽ phản hồi kết quả qua email, và mời ứng viên đặt một câu hỏi cho công ty nếu còn thời gian.
`
