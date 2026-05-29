# Glossary – AI-Powered Recruitment and Interview Support Platform for Enterprises (ARISP)

Thuật ngữ và định nghĩa domain dùng trong dự án.

---

## Domain Terms

| Thuật ngữ | Định nghĩa |
|---|---|
| **Company / Enterprise** | Doanh nghiệp sử dụng nội bộ nền tảng ARISP (Single-tenant) |
| **Super Admin** | Quản trị viên hệ thống – quản trị cấu hình toàn hệ thống,allowed email domains, audit log và tài khoản nhân viên HR |
| **HR Leader** | Trưởng bộ phận tuyển dụng – người có quyền cấu hình Job Posting, duyệt Playbook, review Evaluation và quyết định Confirm hoặc Override verdict |
| **Recruiter (HR Staff)** | Chuyên viên tuyển dụng – người tạo Job Posting nháp, quản lý hồ sơ ứng tuyển, cấp mã Interview Code cho thi thật On-site tại văn phòng |
| **Candidate** | Ứng viên tham gia phỏng vấn AI tự động |
| **Job Posting** | Tin tuyển dụng do HR tạo, gồm JD và cấu hình phỏng vấn |
| **Application** | Hồ sơ ứng tuyển của Candidate cho một Job Posting cụ thể (gồm CV + thông tin cá nhân) |
| **Interview Session** | Một phiên phỏng vấn tự động từ đầu đến cuối do AI dẫn dắt, gồm nhiều câu hỏi và câu trả lời |
| **Question** | Câu hỏi do AI tạo ra trong một Interview Session, bám sát JD + CV của ứng viên |
| **Answer** | Câu trả lời của Candidate cho một Question (audio → transcript qua STT) |
| **AI Evaluation** | Đánh giá tổng hợp của AI sau khi session kết thúc: Verdict + Score + Reasoning |
| **Verdict** | Kết quả đề xuất của AI: `Pass` hoặc `Not Pass` |
| **HR Review** | Bước HR Leader xem Evaluation Report và Confirm hoặc Override quyết định của AI |
| **Override** | Quyết định của HR Leader nhằm thay đổi đề xuất Verdict của AI kèm ghi chú lý do bắt buộc |
| **AI Interviewer** | AI agent đóng vai nhà phỏng vấn tự động trong Interview Session |
| **Evaluation Report** | Báo cáo đầy đủ AI xuất sau session: Verdict, Score, Reasoning, per-question analysis |
| **Scoring Rubric** | Bộ tiêu chí đánh giá tùy chỉnh theo vị trí (technical, communication, culture fit, v.v.) |
| **JD (Job Description)** | Mô tả công việc – AI dùng để định hướng câu hỏi sát yêu cầu doanh nghiệp |
| **CV (Resume)** | Hồ sơ ứng viên – AI dùng để cá nhân hóa câu hỏi theo kinh nghiệm thực tế |
| **Transcript** | Nội dung text được chuyển từ audio của ứng viên (qua Google Speech-to-Text STT) |
| **Adaptive Difficulty** | Cơ chế AI tự điều chỉnh độ khó câu hỏi theo chất lượng câu trả lời |
| **Interview Playbook** | Tập hợp tài liệu phỏng vấn nội bộ của doanh nghiệp (style guide, question bank, rubric...) – đưa vào RAG để AI phỏng vấn đúng phong cách công ty |
| **Company Knowledge Base** | Cơ sở kiến thức của Company trong pgvector – bao gồm Playbook documents được chunk và embed |
| **Question Bank** | Ngân hàng câu hỏi do HR chuẩn bị – AI ưu tiên hỏi từ đây trước khi tự sinh |
| **Must-ask Questions** | Câu hỏi bắt buộc phải hỏi trước khi session kết thúc, do HR định nghĩa trong Playbook |
| **Competency Framework** | Ma trận kỹ năng theo level (Junior/Mid/Senior) của từng vị trí – AI dùng để biết đánh giá năng lực nào |
| **Red Flag Guide** | Tài liệu mô tả dấu hiệu cần probe sâu hoặc loại bỏ ứng viên – AI nhận biết và xử lý khi gặp |
| **Technical Scenario** | Bài toán / case study cụ thể HR chuẩn bị – AI dẫn dắt ứng viên qua scenario trong session |
| **Playbook Scope** | Phạm vi áp dụng của tài liệu Playbook: Company (toàn doanh nghiệp), Job Posting (vị trí cụ thể), Round (vòng cụ thể) |
| **Job Board** | Tính năng cho phép ứng viên tự tìm kiếm và ứng tuyển việc làm IT trên ARISP, không cần được HR mời trước |
| **Practice Interview (Phỏng vấn thử)** | Phiên phỏng vấn AI giới hạn 1 lần per Application, dùng JD + CV (không có Playbook), giúp ứng viên làm quen format. HR có thể xem kết quả nhưng không ảnh hưởng verdict |
| **Real Interview (Phỏng vấn thực)** | Phiên phỏng vấn AI chính thức, dùng JD + CV + Playbook (full RAG), chỉ mở đúng mã code thi thật, kết quả ảnh hưởng đến quyết định tuyển dụng |
| **Self-apply** | Hành động ứng viên chủ động ứng tuyển vào Job Posting qua Job Board, không cần invite từ HR |
| **Session Type** | Phân loại phiên phỏng vấn: `practice` (thử, JD+CV only) hoặc `real` (thực, full RAG), xác định nguồn RAG và mức độ ảnh hưởng đến kết quả tuyển dụng |

---

## Technical Terms

| Thuật ngữ | Định nghĩa |
|---|---|
| **STT** | Speech-to-Text – chuyển giọng nói thành văn bản (Google Speech-to-Text streaming) |
| **TTS** | Text-to-Speech – chuyển văn bản thành giọng nói (ElevenLabs Flash v2.5 streaming) |
| **RAG** | Retrieval-Augmented Generation – AI retrieve relevant chunks từ JD/CV vector store trước khi generate câu hỏi |
| **pgvector** | PostgreSQL extension lưu và tìm kiếm vector embeddings (dùng cho RAG) |
| **VAD** | Voice Activity Detection – detect khi ứng viên sắp dừng nói để trigger RAG retrieval sớm |
| **SignalR Hub** | Endpoint realtime ASP.NET Core, quản lý session lifecycle events |
| **ADR** | Architecture Decision Record – tài liệu ghi lại quyết định kiến trúc và lý do |
| **Clean Architecture** | Pattern tổ chức code: Domain → Application → Infrastructure → API |
| **EF Core Migration** | Script thay đổi database schema tạo bởi Entity Framework Core |
| **JWT** | JSON Web Token – cơ chế xác thực stateless |
| **OAuth2 Domain Validation** | Cơ chế bảo mật xác thực: Bắt buộc phân tích email đăng nhập OAuth2 nội bộ và so khớp domain công ty (`allowed_email_domains`) để chặn truy cập từ domain lạ |
| **Single-tenant** | Kiến trúc hệ thống dành riêng cho 1 doanh nghiệp duy nhất, không dùng `organization_id` phân tách |
| **Hybrid Idle Strategy** | Chiến lược HeyGen: chỉ bật Streaming Avatar khi AI nói, phát idle video loop khi AI im để tiết kiệm cost |
| **Bridge file** | File chỉ chứa @import references, không có nội dung trực tiếp (AGENTS.md, CLAUDE.md) |
| **Source of truth** | `.ai/` folder – nơi duy nhất chứa thông tin chính thức, mọi tool đọc từ đây |

---

## Abbreviations

| Viết tắt | Đầy đủ |
|---|---|
| **ARISP** | AI-Powered Recruitment and Interview Support Platform for Enterprises |
| AI | Artificial Intelligence |
| VPS | Virtual Private Server |
| CDN | Content Delivery Network |
| SSL | Secure Sockets Layer |
| ORM | Object-Relational Mapping |
| EF | Entity Framework |
| MVP | Minimum Viable Product |
| JD | Job Description |
| CV | Curriculum Vitae (Resume) |
| HR | Human Resources |
| OIDC | OpenID Connect |
| SSO | Single Sign-On |
