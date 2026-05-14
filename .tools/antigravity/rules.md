# Antigravity Rules – ARISP

- Khi bắt đầu một task, luôn đọc các file dưới đây:

@../../../.ai/context.md
@../../../.ai/coding-rules.md
@../../../.ai/tasks.md
@../../../.ai/glossary.md
@../../../.ai/architecture.md

- Các file trong thư mục `.tools/` chứa các quy tắc đặc thù cho từng tool.
- Trước khi báo hoàn thành một task, hãy chạy kiểm thử và đảm bảo không có lỗi.
- Đừng bao giờ chỉnh sửa trực tiếp `AGENTS.md`, `CLAUDE.md`, hoặc `README.md`. Những file này chỉ dành cho con người và công cụ AI khác.

## ARISP-Specific Rules

- **Tên dự án:** ARISP (AI-Powered Recruitment and Interview Support Platform for Enterprises). Không dùng tên cũ `IAMP`.
- **Namespace:** `ARISP.<Layer>.<Module>` – không dùng `IAMP.*`.
- **B2B platform:** Luôn nhớ đối tượng chính là doanh nghiệp (HR Admin), không phải ứng viên cá nhân.
- **Multi-tenant:** Mọi entity thuộc Enterprise phải có `organization_id`. Khi viết query hoặc repository, bắt buộc filter theo `organization_id` của user đang đăng nhập.
- **AI Decision Flow:** AI chỉ ra quyết định sơ bộ (Verdict). HR phải Confirm/Override trước khi kết quả được gửi cho Candidate. Không bỏ qua bước HR Review.
- **Candidate Invite Flow:** Candidate không tự đăng ký. HR tạo Job Posting → hệ thống sinh invite link → Candidate nhận link và submit CV.
- **Streaming-First:** Không đề xuất bất kỳ giải pháp batch nào cho STT, LLM, TTS, Avatar nếu có alternative streaming khả thi.
