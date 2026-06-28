# Context – ARISP (AI-Powered Recruitment and Interview Support Platform for Enterprises)

## Tổng quan dự án

ARISP là nền tảng tuyển dụng nội bộ doanh nghiệp tích hợp **Job Board IT** và **AI Interview Automation**. Ứng viên tự tìm và ứng tuyển việc làm IT trực tiếp trên nền tảng. AI tự động phỏng vấn ứng viên qua nhiều vòng, đánh giá Pass/Not Pass, HR xác nhận. Không cần nhân sự nội bộ tham gia trực tiếp vào buổi phỏng vấn.

**Mô hình kinh doanh:** Single-tenant – Dành riêng cho 1 doanh nghiệp sử dụng nội bộ (gộp Job Board + AI Interview Platform). Không hỗ trợ đa doanh nghiệp (multi-tenant) và không sử dụng cột `organization_id` trong cơ sở dữ liệu.

---

## User Roles (4 Vai trò người dùng)

| Role | Mô tả & Phân quyền |
|---|---|
| **Super Admin** | **Quản trị viên hệ thống** – Cấu hình hệ thống toàn cục (Allowed domains cho OAuth2, webhook endpoint), quản lý tài khoản HR, theo dõi toàn bộ `audit_log` hệ thống. |
| **HR Leader** | **Trưởng nhóm HR / HR Admin** – Quản lý Job Posting, cấu hình câu hỏi phỏng vấn, upload và quản trị Playbook phỏng vấn nội bộ, xem chi tiết Evaluation Report, quyết định phê duyệt (`Confirm`) hoặc thay đổi kết quả AI đề xuất (`Override` - bắt buộc nhập lý do). |
| **Recruiter (HR Staff)** | **Chuyên viên tuyển dụng** – Tạo Job Posting nháp, quản lý thông tin ứng viên, tạo và cấp mã **Interview Code** cho ứng viên thi thật On-site tại văn phòng, xem Evaluation Report của ứng viên (không có quyền override kết quả hoặc thay đổi cấu hình toàn cục). |
| **Candidate** | **Ứng viên** – Tạo tài khoản cá nhân, tìm kiếm và tự ứng tuyển việc IT qua Job Board; nhận Magic Link vào Candidate Portal để đặt lịch và làm **Phỏng vấn thử (Practice Remote)** tại nhà; đến văn phòng công ty và nhập mã Interview Code để làm **Phỏng vấn thật (Real On-site)**; xem lại video recording, transcript, feedback sau khi HR Leader duyệt. |

---

## Luồng Đăng nhập & Đăng ký (Auth Flow Separation)

Hệ thống ARISP áp dụng các cổng đăng nhập và quy trình đăng ký hoàn toàn biệt lập cho từng nhóm đối tượng để tối ưu bảo mật thông tin nội bộ:

### 1. Phân tách Cổng đăng nhập (Login Portals)
* **Cổng dành cho HR / Super Admin (Nội bộ)**: 
  * Truy cập tại đường dẫn quản trị (ví dụ: `/admin/login`).
  * Xác thực qua form **Email + Mật khẩu**. Hỗ trợ thêm **Google OAuth2** (Google Sign-In) cho người dùng nội bộ công ty.
* **Cổng dành cho ứng viên Candidate (Job Board)**: 
  * Truy cập tại `/jobs/login`.
  * Xác thực truyền thống qua form điền **Email + Mật khẩu cá nhân** (tự đăng ký trước đó).
* **Cổng dành cho ứng viên Candidate Portal (Xem kết quả/Lên lịch)**:
  * Xác thực **không mật khẩu (Passwordless)**. Ứng viên nhập Email -> Hệ thống gửi **Magic Link** về email -> Click link đăng nhập trực tiếp.
* **Cổng Kiosk phỏng vấn On-site (Tại văn phòng)**:
  * Giao diện khóa (Kiosk Mode) tại văn phòng. Chỉ hiển thị form nhập mã **Interview Code** gồm 6 ký tự.

### 2. Phân tách Quy trình Đăng ký (Registration Flow)
* **Đối với HR Leader / Recruiter**: **Không có tính năng Đăng ký (Sign up) công khai**.
  * **Cơ chế Pre-provisioning (Cấp tài khoản trước)**: Tài khoản của HR và HR Leader/Recruiter chỉ có thể được tạo (cấp phát) bởi Super Admin (hoặc Admin) trong cơ sở dữ liệu với địa chỉ email công ty (Gmail công ty).
  * **Vòng đời tài khoản staff (ADR-041)**: việc cấp phát đi qua luồng **yêu cầu → duyệt** thay vì Super Admin tự gõ tay:
    * **HR Leader gửi yêu cầu tạo tài khoản** (lẻ hoặc hàng loạt cùng `BatchId`) → lưu vào bảng `account_requests` (status `pending`).
    * **Super Admin duyệt** → hệ thống tạo `User` active + sinh mật khẩu tạm + gửi email; hoặc **từ chối** (bắt buộc nhập lý do). Trang "Duyệt tài khoản mới" của Super Admin **chỉ** hiển thị `account_requests` đang `pending` (không lẫn tài khoản bị khóa).
    * **Khóa / mở khóa** tách riêng (quản lý ở "Tất cả người dùng"): khóa bắt buộc nhập `lock_reason`; mở khóa xóa lý do. (Cơ chế kháng cáo mở khóa cho người bị khóa — phase sau.)
  * **Đăng nhập & Xác thực**: Khi người dùng nội bộ đăng nhập bằng Google Sign-In:
    * Hệ thống kiểm tra xem địa chỉ email này đã có sẵn dữ liệu trong database hay chưa. Nếu đã có dữ liệu, việc đăng nhập qua Google Sign-In sẽ diễn ra suôn sẻ.
    * Nếu chưa được cấp tài khoản trước đó (email chưa tồn tại trong database), hệ thống **chặn đăng nhập và tuyệt đối không cho phép tự động đăng ký** (không tạo tài khoản mới hay tài khoản nháp).
    * Email đăng nhập bắt buộc phải đúng theo cấu hình email công ty (thuộc danh sách domain được cho phép).
* **Đối với Candidate**: Đăng ký tài khoản tự do trên trang Job Board bằng email cá nhân bất kỳ (không giới hạn domain).

---

## Interview Mode – Remote vs On-site

### Remote Interview (Phỏng vấn thử - Practice Session)
- **CHỈ DÀNH CHO PHỎNG VẤN THỬ (PRACTICE).**
- Mở **tự động cho từng vòng** sau khi ứng viên **đã pass CV + đặt lịch buổi phỏng vấn thật của vòng đó** (qua Portal). Vào thẳng bằng route Portal (`/practice/:applicationId`) — **không cần Interview Code, không cần magic link riêng**.
- Giới hạn **1 lượt / VÒNG** (theo `(application_id, round_number)`). Cửa sổ dùng: từ lúc đặt lịch đến giờ phỏng vấn thật của vòng.
- **Giống hệt buổi thật sắp tới của vòng:** cùng `round_type` (technical → technical, sơ loại/ngôn ngữ → sơ loại/ngôn ngữ) + cùng ngôn ngữ phỏng vấn (chung `InterviewRoundConfig` theo `RoundNumber`). Khác biệt duy nhất so với real: nguồn RAG (JD+CV, không Playbook) + không quay video.
- RAG pipeline lúc phỏng vấn thử chỉ sử dụng dữ liệu JD và CV ứng viên (không nạp Playbook nội bộ bảo mật của doanh nghiệp).

### On-site Interview (Phỏng vấn thật tại công ty)
- **BẮT BUỘC CHO MỌI VÒNG PHỎNG VẤN THẬT.**
- Candidate đến văn phòng công ty **đúng khung giờ đã đặt lịch** (Availability Slot của vòng). Recruiter/HR cấp **Interview Code** (one-time-use, có TTL – mặc định 2 giờ; chỉ dùng cho real, không có `code_type`).
- Candidate nhập code tại thiết bị Kiosk của công ty → vào phỏng vấn ngay. Code vô hiệu hóa ngay sau khi dùng thành công.
- RAG pipeline sử dụng toàn bộ dữ liệu: JD + CV + Playbook nội bộ công ty (style guide, question bank, technical scenarios, v.v.).

---

## Business Logic – Recruitment Flow

### Phase 1: Enterprise Setup (HR Admin/Super Admin)

| Trường | Bắt buộc | Mô tả |
|---|---|---|
| Tên vị trí / Lĩnh vực | ✅ | Ví dụ: Backend Developer, IT |
| JD (Job Description) | ✅ | AI phân tích JD để tạo câu hỏi & detect yêu cầu ngôn ngữ |
| Cấu hình vòng phỏng vấn | ✅ | Số vòng, loại vòng (`screening` - Lọc, `technical` - Chuyên môn, `online_test` - Trắc nghiệm Online), ngôn ngữ |
| Phỏng vấn thật | ✅ | Mặc định On-site (Tại công ty) |
| Availability Slots (per vòng) | ⬜ | Khung giờ cho ứng viên đặt lịch **buổi phỏng vấn thật** của từng vòng; đặt lịch xong mở 1 lượt phỏng vấn thử cho vòng đó |
| Scoring Rubric | ⬜ | Custom tiêu chí đánh giá per Job Posting |
| Interview Persona | ⬜ | Tên, giọng, phong cách avatar AI |
| **Interview Playbook** | ⬜ | Upload tài liệu phỏng vấn nội bộ (câu hỏi, kịch bản, rubric chi tiết, ...) |

### Phase 2: Candidate Application
- Candidate tự ứng tuyển qua **Job Board** (chủ động) hoặc được HR mời trực tiếp (passive).

#### CV-JD Match Analysis (Gemini AI – Dành cho Candidate xem trước)
- Trên trang **Job Detail**, candidate có thể **upload CV** (PDF/DOCX) để kiểm tra mức độ phù hợp với JD **trước khi ứng tuyển**.
- Hệ thống sử dụng **Google Gemini AI** phân tích CV (file) so với JD (file/text) và trả về:
  - `matchScore` (0–100): Điểm phù hợp tổng thể.
  - `summary`: Tóm tắt đánh giá (điểm mạnh, điểm yếu, kỹ năng khớp, kỹ năng thiếu).
  - `skillsMatched`: Danh sách kỹ năng trong CV khớp với JD.
  - `skillsGaps`: Danh sách kỹ năng JD yêu cầu nhưng CV chưa thể hiện.
  - `experienceRelevance`: Đánh giá kinh nghiệm liên quan.
  - `overallRecommendation`: Nhận xét tổng quan (ví dụ: "Hồ sơ phù hợp tốt", "Cần bổ sung kỹ năng X").
- **Kết quả chỉ mang tính tham khảo cho candidate.** Dù điểm cao hay thấp, candidate vẫn **luôn có thể ứng tuyển** vào vị trí đó.
- Khi candidate bấm **"Ứng tuyển"**, toàn bộ kết quả phân tích (summary, matchScore, ...) được **đính kèm vào Application gửi cho HR y hệt**, hệ thống **không phân tích lại** (tiết kiệm chi phí API + đảm bảo nhất quán).
- Nếu candidate ứng tuyển mà **chưa từng chạy phân tích CV-JD**, hệ thống tự động chạy phân tích 1 lần khi submit application.

#### Flow ứng tuyển
1. Candidate xem Job Detail trên Job Board.
2. *(Tùy chọn)* Candidate upload CV → Gemini phân tích → hiển thị Match Score + Summary.
3. Candidate bấm "Ứng tuyển" → điền thông tin (hoặc dùng lại CV đã upload) → Submit.
4. Application được tạo kèm kết quả CV-JD Analysis (nếu có) → HR thấy ngay trên dashboard.
5. Dựa trên bảng xếp hạng `matchScore` + review CV thủ công, HR chọn ứng viên và gửi **magic link**.
- Candidate nhận magic link → truy cập **Candidate Portal** để:
  - Xem thông tin vị trí ứng tuyển.
  - **Chọn khung giờ (Availability Slot) cho buổi phỏng vấn thật của vòng đó.**
  - Đặt lịch xong → mở **1 lượt Phỏng vấn thử (Practice Remote)** cho vòng đó (1 lượt / vòng), dùng được đến giờ phỏng vấn thật.

### Phase 3: Access Real Interview (On-site)
- Candidate đến văn phòng công ty **đúng khung giờ đã đặt lịch** (Availability Slot của vòng).
- Recruiter/HR cấp **Interview Code** → Candidate nhập code tại Kiosk của công ty → vào phỏng vấn thật.

### Phase 4: Multi-round AI Interview

#### Round 1 – Screening (Language-aware)
- **Language-aware:** AI phân tích JD, detect yêu cầu ngôn ngữ (ví dụ: "Giao tiếp tiếng Anh tốt, TOEIC > 700" hoặc "IELTS > 6.5").
  - Nếu có yêu cầu → **phỏng vấn bằng ngôn ngữ đó** (English, Japanese, Korean, v.v.).
  - AI đánh giá cả **nội dung** lẫn **năng lực ngôn ngữ** (fluency, grammar, vocabulary, comprehension).
  - Không có yêu cầu → phỏng vấn tiếng Việt (mặc định).
- Nguồn câu hỏi: JD + CV + Language assessment (nếu có).
- Adaptive difficulty.

#### Round 2 – Technical Deep-dive
- Chỉ kích hoạt khi Candidate **Pass Round 1**.
- Đánh giá chuyên sâu kỹ năng kỹ thuật/chuyên môn.
- Ngôn ngữ: theo cấu hình Job Posting (mặc định tiếng Việt hoặc tiếp tục ngôn ngữ Round 1).

#### Auto-progression
```
Round N kết thúc → AI Evaluation → HR Leader Review
  → Pass: hệ thống tự động invite Round N+1 (HR cấp Interview Code mới khi ứng viên đến văn phòng)
  → Not Pass: gửi thông báo từ chối cho Candidate
```

### Phase 5: AI Evaluation (mỗi Round)
- **Verdict:** Pass / Not Pass
- **Overall Score** (0–100)
- **Per-criterion scores:** technical, communication, language proficiency (nếu applicable), culture fit
- **Language Assessment** (Round 1): fluency, grammar, vocabulary, comprehension score
- **Per-question analysis**
- **Recommended next step**

### Phase 6: HR Leader Review & Confirm
- HR Leader xem Evaluation Report + recording.
- **Confirm** hoặc **Override** (kèm `override_reason` bắt buộc).
- Sau confirm → hệ thống tự động lưu kết quả, cho phép Recruiter cấp mã Interview Code mới nếu có vòng sau (Round N+1) hoặc gửi email thông báo từ chối.

### Phase 7: Candidate Portal
- Đăng nhập bằng email + magic link (không cần password).
- Xem: recording, transcript, Evaluation Report (phần HR cho phép hiển thị), feedback.

---

## Feature Modules

### Interview Playbook (Org Knowledge Base)

Doanh nghiệp upload tài liệu phỏng vấn nội bộ để AI phỏng vấn đúng phong cách và đúng nội dung mong muốn.

**Phạm vi upload:**

| Cấp | Loại tài liệu | Ví dụ |
|---|---|---|
| **Company** | Interview Style Guide | "Chúng tôi phỏng vấn thân thiện, tập trung vào tư duy" |
| **Company** | Competency Framework | Ma trận kỹ năng theo level (Junior/Mid/Senior) |
| **Company** | Culture & Values Guide | Mission, core values, culture fit indicators |
| **Company** | Compliance / Must-not-ask | Câu hỏi không được hỏi (tuổi, tôn giáo...) |
| **Company** | Red Flag Guide | Dấu hiệu cần probe sâu hoặc loại bỏ ứng viên |
| **Job Posting** | Question Bank | Danh sách câu hỏi bắt buộc/gợi ý per vị trí |
| **Job Posting** | Technical Scenarios | Bài toán / case study cụ thể cho vị trí |
| **Job Posting** | Expected Answer Guide | Câu trả lời tốt cần đề cập gì |
| **Job Posting** | Must-ask Questions | Câu hỏi không thể bỏ qua |
| **Round** | Round-specific Playbook | Screening guide (Round 1), Technical deep-dive (Round 2) |

**Format hỗ trợ:** PDF, DOCX, TXT, Markdown, JSON (question bank format), Interview transcript ẩn danh.

**Cách RAG sử dụng:**
```
Khi AI sinh câu hỏi → retrieve từ:
  ① JD chunks              (weight cao – yêu cầu vị trí)
  ② CV chunks              (weight cao – kinh nghiệm ứng viên)
  ③ Org Playbook chunks    (weight trung bình – style + compliance)
  ④ Job Posting Playbook   (weight cao – câu hỏi bắt buộc, scenario)
  ⑤ Round Playbook         (weight cao – phù hợp vòng hiện tại)
```

### System Admin (Super Admin)
- Quản lý tài khoản HR: phân quyền theo bộ phận; **duyệt/từ chối yêu cầu tạo tài khoản** do HR Leader gửi (`account_requests`); **khóa/mở khóa** tài khoản (khóa bắt buộc nhập lý do `lock_reason`).
- **System Config:** Cấu hình tên miền công ty được phép đăng nhập qua OAuth2, các webhook URL.
- **Audit log:** ghi nhận chi tiết mọi hành động quan trọng toàn hệ thống để giám sát bảo mật.

### AI Enhancement
- **Custom Interview Persona:** tên, giọng, phong cách phỏng vấn theo văn hóa công ty.
- **Cheat Detection:** phát hiện đọc script, dùng AI trợ lý (speech pattern anomaly, eye tracking, response timing).
- **Bias Detection & Fairness Report:** phân tích kết quả theo nhân khẩu học.

### Integrations
- **ATS Webhook/API:** Workday, SAP SuccessFactors, Greenhouse – push kết quả tự động qua webhook cấu hình toàn cục.
- **OAuth2 / OIDC Integration:** Hỗ trợ đăng nhập qua **Google OAuth2** (Google Sign-In) cho người dùng nội bộ công ty (`users`), kiểm tra nghiêm ngặt email thuộc tên miền công ty (`allowed_email_domains` cấu hình toàn cục). SAML 2.0 hoàn toàn bị loại bỏ.
- **Slack/Teams Notifications:** Nhận thông báo thời gian thực khi có Evaluation mới cần Review (cấu hình Webhook toàn hệ thống).

### Job Board (IT-focused)

Ứng viên tạo tài khoản trên ARISP và tìm kiếm việc làm IT trực tiếp trên nền tảng.

- **Thị trường:** Chỉ tập trung IT (Dev, DevOps, QA, Data, Design, PM, ...)
- **Tên công ty:** Hiển thị công khai trên tin tuyển dụng
- **Self-apply:** Ứng viên chủ động ứng tuyển → HR xem CV → HR chủ động gửi magic link
- **Nguồn dữ liệu:** Job Posting nội bộ là nguồn chung cho cả Job Board lẫn AI Interview Platform

### Practice Interview (Phỏng vấn thử)

Tính năng giúp ứng viên làm quen với format phỏng vấn AI trước khi bước vào phỏng vấn thực.

- **Truy cập:** Qua **Portal** (route `/practice/:applicationId`) sau khi pass CV + đặt lịch buổi phỏng vấn thật của vòng — **không cần Interview Code**, không xuất hiện trên Job Board
- **Lượt dùng:** **1 lượt / VÒNG** (mở lại mỗi khi pass vòng + đặt lịch vòng kế); cửa sổ dùng = từ lúc đặt lịch đến giờ phỏng vấn thật của vòng
- **Nguồn câu hỏi:** JD + CV (không dùng Playbook – Playbook chỉ áp dụng cho phỏng vấn thực)
- **Recording:** Không quay video — chỉ lưu transcript + Evaluation Report
- **Kết quả:** HR **có thể xem** score, transcript của Practice Session
- **Chi phí:** Doanh nghiệp chịu (không thu từ ứng viên)
- **Phân biệt:** Practice Session không ảnh hưởng đến verdict/kết quả tuyển dụng

---

## Tech Stack

| Layer | Technology | Ghi chú |
|---|---|---|
| **Frontend** | React, TypeScript, TailwindCSS | |
| **Backend** | C#, ASP.NET Core (.NET 8) | Không dùng Node.js |
| **API Style** | REST API + SignalR | SignalR cho session events |
| **Realtime Media** | WebRTC | Audio stream + avatar video |
| **Database** | PostgreSQL on Supabase | Chỉ host DB – không dùng Supabase SDK |
| **ORM** | Entity Framework Core | |
| **Auth** | JWT + Role-based Authorization | BCrypt hash cho tài khoản Candidate và mật khẩu phụ; **OAuth2 / OpenID Connect + Domain validation** cho nội bộ công ty |
| **Cache** | Redis | |
| **AI/LLM (phỏng vấn)** | OpenAI GPT-4o | "Bộ não" sinh câu hỏi + đánh giá; Claude là option dành sau (ADR-043) |
| **RAG Service** | **Python + LangChain + LangGraph + FastAPI** | Microservice riêng `rag-service/`: chunk + embed (text-embedding-3-small) + **Hybrid RAG** (dense pgvector + sparse Postgres FTS + RRF + scope weighting) + sinh câu hỏi/đánh giá. .NET gọi qua HTTP/SSE nội bộ (ADR-039) |
| **Vector store** | pgvector (PostgreSQL) | `document_chunks` vector(1536), schema do EF Core sở hữu |
| **CV-JD Analysis** | Google Gemini 2.5 Flash | Phân tích CV vs JD, chấm điểm match, tóm tắt – dành cho candidate xem trước + HR review |
| **STT + VAD** | **Deepgram Nova-3** | Streaming real-time; **gộp sẵn VAD + endpointing** (`vad_events`/`endpointing`/`utterance_end`) — không cần thư viện VAD riêng |
| **TTS** | ElevenLabs Flash v2.5 | Streaming (~75ms) |
| **Avatar** | HeyGen Streaming Avatar | Hybrid Idle Strategy |
| **Email** | SendGrid / AWS SES | Invite, magic link, kết quả |
| **File Storage** | `IFileStorageService` – Local disk (dev) / Cloudflare R2 (prod) | S3-compatible qua `AWSSDK.S3`, presigned URL; DB lưu `storageKey` (ADR-036) |
| **Containers** | Docker + Docker Compose | |
| **Servers** | Ubuntu/Linux VPS | |
| **Reverse Proxy** | Nginx | SSL + routing |
| **CI/CD** | GitHub Actions | |
| **Monitoring** | Serilog + Grafana + Health Checks | |
| **Backup** | pg_dump schedule + VPS snapshot | |
| ** CDN** | Cloudflare CDN | Optional – sau MVP |
| **Version Control** | GitHub | |

> **Streaming-First:** Deepgram STT stream (+VAD) → Hybrid RAG parallel → GPT-4o stream → ElevenLabs Flash v2.5 stream → HeyGen Avatar stream. Mục tiêu: **~0.8–1.2 giây** latency sau khi ứng viên dừng nói (cascaded tối ưu — ADR-006/043).

---

## Quy tắc bắt buộc

1. **Không tự ý thay đổi tech stack** khi chưa được user xác nhận.
2. **Không hardcode secrets** – luôn dùng environment variables.
3. **Không dùng Supabase SDK** – kết nối PostgreSQL trực tiếp qua connection string.
4. **Không đề xuất Node.js** cho backend.
5. **Mọi thay đổi kiến trúc** cập nhật vào `.ai/architecture.md`.
6. **Trước khi bắt đầu task mới** – kiểm tra `.ai/tasks.md`.
7. **Khi có quyết định mới** – ghi lại ngay vào file `.ai/` tương ứng.
8. **AI/LLM:** Business logic không gọi trực tiếp OpenAI SDK – qua `IAIProvider` + `IEmbeddingProvider`. Swap qua cờ `AI:Provider` = `rag` (RAG microservice Python — mặc định khuyến nghị, ADR-039) | `openai` | `local`. Khi `rag`, ingestion qua `IRagIngestionService`.
9. **WebRTC** chỉ dùng cho media stream. Session events dùng SignalR.
10. **Streaming-First:** Không chấp nhận batch nếu có alternative streaming khả thi.
11. **Single-tenant:** Hệ thống phục vụ cho 1 công ty duy nhất. Không sử dụng `organization_id` và không thiết kế cấu trúc multi-tenant.
12. **Interview Code:** One-time-use, TTL ngắn (mặc định 2 giờ, cấu hình được), vô hiệu hóa ngay sau khi dùng.
13. **Language detection:** AI detect từ JD – không hardcode mapping ngôn ngữ.
14. **Connection Drop Recovery:** Nếu ứng viên mất kết nối, session duy trì trạng thái active. Khi nhập lại code, AI resume lại luồng câu hỏi chưa hỏi (track qua bảng `must_ask_tracking`).
15. **OAuth2 Email Domain validation:** Đăng nhập nội bộ bắt buộc phải xác thực domain thuộc danh sách `allowed_email_domains` được quy định trong cấu hình toàn cục.
16. **CV-JD Analysis (Gemini):** Kết quả phân tích CV-JD chỉ chạy **1 lần** per CV + Job Posting. Khi candidate ứng tuyển, kết quả được đính kèm vào Application – HR nhận y hệt, không phân tích lại.
17. **JD File Upload:** Job Posting hỗ trợ upload file JD gốc (PDF/DOCX) bên cạnh text JD. Gemini phân tích từ file gốc nếu có, fallback sang text JD.
18. **Gemini AI:** Dùng cho CV-JD Match Analysis **và** trích xuất JD để auto-fill form tạo tin (ADR-042). Phỏng vấn AI và RAG pipeline vẫn dùng OpenAI GPT-4o.
