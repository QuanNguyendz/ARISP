# Glossary – AI-Powered Recruitment and Interview Support Platform for Enterprises (ARISP)

Thuật ngữ và định nghĩa domain dùng trong dự án.

---

## Domain Terms

| Thuật ngữ | Định nghĩa |
|---|---|
| **Company / Enterprise** | Doanh nghiệp sử dụng nội bộ nền tảng ARISP (Single-tenant) |
| **Super Admin** | Quản trị viên hệ thống – quản trị cấu hình toàn hệ thống,allowed email domains, audit log và tài khoản nhân viên HR |
| **HR Leader** | Trưởng nhóm HR – sở hữu **quy trình và tuân thủ**: cấu hình Job Posting, đăng tin **vượt cổng chữ ký HM** khi cần (lý do ≥10 — ADR-068), quản trị Playbook **cấp công ty**, xem mọi Evaluation. Là người **gán / chuyển Hiring Manager chính** của tin (ADR-068) và **chốt thư mời** (ADR-063); chốt thay HM khi cần (bắt buộc lý do + audit log) — ADR-061 |
| **Hiring Manager (HM)** | Trưởng bộ phận có nhu cầu tuyển — **người ra quyết định tuyển** (ADR-061). Ký duyệt JD (hoặc yêu cầu sửa), duyệt shortlist, **chốt Pass/Not Pass**, **soạn và gửi duyệt** thư mời nhận việc (HR Leader chốt — ADR-063). Phạm vi theo đội tuyển dụng của từng tin, không theo phòng ban. **Mọi tin luôn có đúng một HM chính**; thiếu HM hoặc HM bị khoá thì các cổng của tin **đóng** (ADR-068) |
| **Recruiter (HR Staff)** | Chuyên viên tuyển dụng – **vận hành phễu**: tạo Job Posting nháp, sàng lọc hồ sơ, xếp lịch, cấp mã Interview Code, soạn thư mời. Xem được báo cáo AI của tin mình nhưng **không chốt kết quả** |
| **Candidate** | Ứng viên tham gia phỏng vấn AI tự động |
| **Job Posting** | Tin tuyển dụng do HR tạo, gồm JD và cấu hình phỏng vấn |
| **Application** | Hồ sơ ứng tuyển của Candidate cho một Job Posting cụ thể (gồm CV + thông tin cá nhân) |
| **Interview Session** | Một phiên phỏng vấn tự động từ đầu đến cuối do AI dẫn dắt, gồm nhiều câu hỏi và câu trả lời |
| **Question** | Câu hỏi do AI tạo ra trong một Interview Session, bám sát JD + CV của ứng viên |
| **Answer** | Câu trả lời của Candidate cho một Question (audio → transcript qua STT) |
| **AI Evaluation** | Đánh giá tổng hợp của AI sau khi session kết thúc: Verdict + Score + Reasoning |
| **Verdict** | Kết quả đề xuất của AI: `Pass` hoặc `Not Pass` |
| **HR Review** | Bước xem Evaluation Report rồi Confirm hoặc Override quyết định của AI. **Người thực hiện là Hiring Manager của tin** (ADR-061); admin làm thay thì là HR Fallback (ADR-068 bỏ ngoại lệ "tin chưa gán HM"). Bảng `hr_reviews` giữ nguyên tên |
| **Override** | Thay đổi Verdict của AI, kèm `override_reason` bắt buộc. Quyền này thuộc **Hiring Manager** của tin và HR Admin/Super Admin — không thuộc Recruiter |
| **Recruitment Request (Phiếu yêu cầu tuyển dụng)** | Điểm bắt đầu bắt buộc của mọi tin (ADR-063). Hiring Manager lập, HR Leader duyệt **kèm phân công Recruiter trong cùng một thao tác** hoặc trả lại kèm lý do. `rejected` **không phải trạng thái kết thúc** — là một vòng của chu trình sửa–gửi lại. Không ai duyệt phiếu của chính mình |
| **Thu hồi phê duyệt** | Huỷ hiệu lực chữ ký của HR Leader trên một phiếu đã duyệt khi nhu cầu đổi (ADR-066). Hai đích: **Mở lại để sửa** (→ chờ duyệt, phân công Recruiter bị gỡ theo) và **Đóng phiếu** (→ đã huỷ, giữ nguyên dấu vết). Chỉ làm được khi phiếu **chưa dựng thành tin** — sau đó TIN là nguồn sự thật. Lý do bắt buộc ≥10 ký tự; chủ phiếu hoặc quản trị viên, **không phải Recruiter** |
| **Đội / Bộ phận (Department)** | Đơn vị tổ chức của công ty (bảng `departments`, ADR-065). Super Admin quản lý; mỗi tài khoản nhân sự thuộc **đúng một đội**. Là dữ liệu **tổ chức**, KHÔNG phải trục phân quyền — trả lời "phiếu này của đội nào", còn "ai được quyết định về tin này" vẫn do **Hiring Team** trả lời. Đội trên phiếu lấy cứng từ tài khoản người lập; đội giải thể thì **tắt, không xoá** |
| **Mức độ ưu tiên (Priority)** | `high` / `medium` / `low` trên phiếu — xếp thứ tự hàng chờ duyệt của HR Leader, sắp ở tầng SQL chứ không ở giao diện |
| **Thoả thuận (lương)** | Phiếu không điền con số lương nào. **Suy ra từ dữ liệu**, không có cột riêng; bắt buộc đánh dấu tường minh khi gửi để không lẫn với "quên điền" |
| **Hiring Team** | Đội tuyển dụng của MỘT tin (`job_hiring_team_members`) — nguồn phạm vi dữ liệu duy nhất của Hiring Manager. Thành viên **đọc** được hồ sơ/lịch/báo cáo của tin nhưng **không sửa** được tin |
| **Shortlist Approval** | Cổng duyệt hồ sơ của Hiring Manager trước khi ứng viên được xếp lịch. Trạng thái `hm_review` + cột `hm_decision` (`pending\|approved\|rejected\|bypassed`) |
| **JD Sign-off** | Chữ ký duyệt mô tả công việc của Hiring Manager trước khi tin được đăng (`hm_sign_off_status`). Là **cột trên tin**, không phải trạng thái mới trong vòng đời tin: `pending|approved|rejected|bypassed`. Ký duyệt = đăng tin (nguyên tử); **yêu cầu sửa** = tin về `rejected` để Recruiter sửa rồi gửi lại; admin đăng vượt cổng = `bypassed` (ADR-068) |
| **HR Bypass** | Quản trị viên vượt một cổng của Hiring Manager. **Lý do bắt buộc** (≥ 10 ký tự), ghi `audit_log` và **luôn báo cho chính HM bị vượt**. Hiện thành nhãn riêng, không gộp vào "đã duyệt" |
| **HR Fallback** | Quản trị viên **chốt thay** Hiring Manager ở bước kết quả phỏng vấn (`hr_reviews.is_hr_fallback` + `fallback_reason`). Khác Bypass ở chỗ đây là quyết định cuối, không phải mở cổng. **Luôn** cần `fallback_reason` ≥10 ký tự (ADR-068) |
| **Offer** | Thư mời nhận việc (`offers`) — đoạn kết của phễu. Nháp (HM chính hoặc chủ tin soạn) → gửi duyệt (báo HR Leader) → **HR Leader chốt** → chủ tin/admin gửi ứng viên → nhận/từ chối (ADR-063/068). **Một hồ sơ chỉ có một thư còn hiệu lực**, chặn ở tầng DB |
| **Chuyển Hiring Manager** | Thao tác của HR Leader / Super Admin gán hoặc đổi HM chính của tin (`PUT /jobs/{id}/hiring-manager`, lý do ≥10, audit `hiring_manager_transferred`, báo HM cũ + mới + chủ tin). Người cũ vẫn ở lại đội; lịch rảnh của họ hết hiệu lực. Chủ tin không gỡ được HM chính (ADR-068) |
| **Offer Expiry** | Hạn phản hồi của thư mời. Quá hạn → hosted service tự đóng `expired` và hồ sơ về `offer_declined` |
| **Hired** | Trạng thái cuối khi ứng viên **nhận** thư mời. Terminal — không chuyển đi đâu nữa |
| **Offer Declined** | Trạng thái cuối khi ứng viên **từ chối** thư mời hoặc thư hết hạn. Terminal |
| **Email Composer** | Trình soạn thư gửi ứng viên (ADR-061): mở ra đã điền đầy đủ theo mẫu, sửa được, rồi mới gửi. Việc gửi nằm trong chính lệnh nghiệp vụ — **bấm Huỷ = không có gì xảy ra cả** |
| **Email Log** | Bản ghi thư đã gửi cho ứng viên (`email_logs`) — lưu **đúng nội dung đã rời hệ thống** (bản đã lọc HTML), không phải bản dựng lại từ mẫu |
| **AI Interviewer** | AI agent đóng vai nhà phỏng vấn tự động trong Interview Session |
| **Evaluation Report** | Báo cáo đầy đủ AI xuất sau session: Verdict, Score, Reasoning, per-question analysis |
| **Scoring Rubric** | Bộ tiêu chí đánh giá tùy chỉnh theo vị trí (technical, communication, culture fit, v.v.). Hai loại: `interview_rubric` (chấm buổi phỏng vấn, ADR-060) và **Bộ tiêu chí chấm CV** (ADR-070) |
| **Bộ tiêu chí chấm CV (`cv_rubric`)** | Danh sách tiêu chí + trọng số (tổng 100) + chuẩn chấm + **mức neo** mà AI dùng để chấm CV của một tin (ADR-070). **Bắt buộc**: HM khai ngay trên phiếu yêu cầu, chép sang tin khi dựng tin; tin thiếu bộ tiêu chí không gửi duyệt/đăng được và **không có CV nào được chấm**. Mỗi tin đúng **một bộ sống**; mỗi lần lưu là một **phiên bản** mới. HM chính (hoặc quản trị viên) sửa được ở màn tin. `cv_rubric` cấp công ty chỉ là **mẫu** để chép |
| **Ý kiểm (Checklist)** | Các dấu hiệu CÓ/KHÔNG kiểm được từ CV trong một tiêu chí (tối đa 8, vd "Có ≥ 4 năm .NET production"). AI chọn dải điểm rồi trả lời từng ý kèm trích dẫn; **điểm trong dải do backend tính**: đáy dải + (ý đạt ÷ ý đã trả lời) × độ rộng dải — dải 90–100 đạt 2/4 → 95. Ý "đạt" không có trích dẫn không được tính (ADR-071) |
| **Mức neo (Levels)** | Lời mô tả cụ thể cho 4 dải điểm cố định của một tiêu chí: 90–100 xuất sắc · 70–89 tốt · 40–69 đạt một phần · 0–39 chưa đạt. Người khai chỉ viết lời, không tự đặt ngưỡng — để hai lần chấm cùng CV rơi vào cùng dải |
| **Chấm lại (Rescoring)** | Lưu phiên bản bộ tiêu chí mới → mọi hồ sơ của tin tự được chấm lại theo bộ mới (hàng đợi nền). Điểm cũ **giữ làm lịch sử**, không bị xoá; trong lúc chờ, hồ sơ hiện "Đang chấm lại" thay vì điểm cũ |
| **JD (Job Description)** | Mô tả công việc – AI dùng để định hướng câu hỏi sát yêu cầu doanh nghiệp |
| **CV (Resume)** | Hồ sơ ứng viên – AI dùng để cá nhân hóa câu hỏi theo kinh nghiệm thực tế |
| **Transcript** | Nội dung text được chuyển từ audio của ứng viên (qua Deepgram Nova-3 STT) |
| **Adaptive Difficulty** | Cơ chế AI tự điều chỉnh độ khó câu hỏi theo chất lượng câu trả lời |
| **Interview Playbook** | Tập hợp tài liệu phỏng vấn nội bộ của doanh nghiệp (style guide, question bank, rubric...) – đưa vào RAG để AI phỏng vấn đúng phong cách công ty. Playbook **công ty** do HR Leader quản lý ở màn Playbook; playbook **theo tin / theo vòng** do **Hiring Manager chính** quản lý ngay trong màn tin, Recruiter chỉ đọc (ADR-069) |
| **Company Knowledge Base** | Cơ sở kiến thức của Company trong pgvector – bao gồm Playbook documents được chunk và embed |
| **Question Bank** | Ngân hàng câu hỏi do HR chuẩn bị – AI ưu tiên hỏi từ đây trước khi tự sinh |
| **Must-ask Questions** | Câu hỏi bắt buộc phải hỏi trước khi session kết thúc, do Hiring Manager chính khai trong playbook của tin (ADR-069) |
| **Competency Framework** | Ma trận kỹ năng theo level (Junior/Mid/Senior) của từng vị trí – AI dùng để biết đánh giá năng lực nào |
| **Red Flag Guide** | Tài liệu mô tả dấu hiệu cần probe sâu hoặc loại bỏ ứng viên – AI nhận biết và xử lý khi gặp |
| **Technical Scenario** | Bài toán / case study cụ thể HR chuẩn bị – AI dẫn dắt ứng viên qua scenario trong session |
| **Kết quả phỏng vấn (theo vòng)** | Danh sách các buổi phỏng vấn thật của một hồ sơ theo vòng: ca đã gán · trạng thái (`scheduled` · `overdue` · `waiting` · `in_progress` · `evaluating` = AI đang chấm · `pending_review` · `reviewed` · `aborted`) · báo cáo AI · có video / transcript. Hiện ở màn tin và màn hồ sơ của cả ba vai; là đường vào màn đánh giá (ADR-069) |
| **Playbook Scope** | Phạm vi áp dụng của tài liệu Playbook: Company (toàn doanh nghiệp), Job Posting (vị trí cụ thể), Round (vòng cụ thể) |
| **Job Board** | Tính năng cho phép ứng viên tự tìm kiếm và ứng tuyển việc làm IT trên ARISP, không cần được HR mời trước |
| **Practice Interview (Phỏng vấn thử)** | Phiên phỏng vấn AI giới hạn **1 lượt / vòng** (mở sau khi pass CV + đặt lịch buổi thật của vòng đó, vào qua Portal không cần code), dùng JD + CV (không có Playbook), giúp ứng viên làm quen format. **Audio-only, không avatar, trần 20 phút, nhập kép giọng nói + bàn phím** (ADR-050). Transcript và nhận xét AI **lưu vĩnh viễn nhưng CHỈ ứng viên xem lại được** — **ẩn hoàn toàn khỏi HR Lead và Recruiter**, và **không hiện verdict Pass/Not Pass** (ADR-051). Không ảnh hưởng kết quả tuyển dụng |
| **Real Interview (Phỏng vấn thực)** | Phiên phỏng vấn AI chính thức, dùng JD + CV + Playbook (full RAG), chỉ mở đúng mã code thi thật, kết quả ảnh hưởng đến quyết định tuyển dụng |
| **Self-apply** | Hành động ứng viên chủ động ứng tuyển vào Job Posting qua Job Board, không cần invite từ HR |
| **Session Type** | Phân loại phiên phỏng vấn: `practice` (thử, JD+CV only) hoặc `real` (thực, full RAG), xác định nguồn RAG và mức độ ảnh hưởng đến kết quả tuyển dụng |
| **Interview Code** | Mã **6 ký tự alphanumeric** nhân sự cấp cho ứng viên để vào buổi phỏng vấn **thật** qua trang Kiosk. Dùng **một lần**, TTL mặc định **2 giờ**, gắn với `application_id` + vòng; vô hiệu ngay sau khi dùng thành công. **Một mã sống mỗi (hồ sơ, vòng)** — cấp mã mới là mã cũ hết hạn. Vòng **chuyên môn**: đưa tận tay tại văn phòng, không hiện trong Portal; vòng **sơ loại**: gửi link vào phòng kèm mã, ứng viên làm từ nhà. Không có mã cho buổi thử và vòng trắc nghiệm (ADR-015/016) |
| **Kiosk** | Tên gọi **nội bộ** cho màn hình phỏng vấn thật tại văn phòng. **Là gì:** trình duyệt chạy **toàn màn hình có ràng buộc** trên máy trạm của công ty, vào bằng Interview Code, không cần đăng nhập (JWT phạm vi một phiên — ADR-052). **KHÔNG phải** kiosk cấp hệ điều hành: không khoá được Alt+Tab, phím Windows, màn hình phụ hay thiết bị thứ hai. Khoá cứng thật cần `chrome --kiosk` + **Windows Assigned Access** — xem ADR-062 và [kiosk-workstation-setup.md](../docs/kiosk-workstation-setup.md). Giao diện hiển thị với ứng viên là *"Chế độ khoá · Văn phòng"*, **không dùng chữ "Kiosk"** |
| **Chế độ khoá màn hình** | Lớp ràng buộc của ADR-054: bật toàn màn hình, **lớp phủ chặn** phòng phỏng vấn khi ứng viên rời toàn màn hình, chặn menu chuột phải và các phím tắt bắt được, cảnh báo khi đóng trang. Mục đích là **răn đe + ghi bằng chứng**, **không phải ngăn chặn tuyệt đối** — trình duyệt không có quyền đó |
| **Cheat Signal** | Một lần rời khỏi màn phỏng vấn được ghi nhận: `fullscreen_exit`, `tab_hidden`, `window_blur`, `shortcut_blocked`, `page_unload`. Gửi về `POST /interview/session/{id}/signals`, lưu thành `CheatDetectionSignal` (ADR-054) |
| **CheatScore** | Điểm tổng hợp từ các Cheat Signal theo trọng số từng loại, gửi kèm kết quả cho nhân sự. **Là dữ kiện tham khảo cho người chấm, không tự đánh trượt ai** |
| **Magic Link** | Link xác thực email không cần mật khẩu, **TTL 15 phút, dùng một lần**. Dùng cho Candidate Portal, xác minh email và đặt lại mật khẩu |
| **Pre-provisioning** | Super Admin tạo sẵn tài khoản nhân sự trong DB — **không có self-register** cho vai trò nội bộ. Email chưa có trong DB thì bị chặn đăng nhập, không tạo tài khoản nháp |
| **CV-JD Match Analysis** | Gemini AI chấm CV (file) so với JD (file gốc + nội dung tin) **theo Bộ tiêu chí chấm CV của tin** — từng tiêu chí kèm bằng chứng trích từ CV (ADR-070). Một lần cho mỗi (tin, CV, phiên bản bộ tiêu chí); ứng viên xem trước khi ứng tuyển và nhân sự nhận đúng bản đó, không chấm lại. Tin chưa có bộ tiêu chí thì không chấm |
| **Match Score** | Điểm phù hợp (0–100) = **trung bình có trọng số** của điểm từng tiêu chí, **do backend tính** (AI không tự cho điểm tổng); tiêu chí AI không chấm được bị loại khỏi cả tử lẫn mẫu. HM/Recruiter/HR xem được **phép tính và bằng chứng** từng tiêu chí ở màn hồ sơ. Chỉ mang tính tham khảo – candidate luôn có thể ứng tuyển bất kể điểm |
| **Trạng thái điểm CV** | `scored` (có điểm) · `pending_rubric` (tin chưa có bộ tiêu chí) · `queued` (đang chờ chấm) · `rescoring` (đang chấm lại theo bộ mới) · `scoring_failed` (lượt chấm hỏng — kèm lý do + giờ hệ thống tự thử lại; đang chấm lại mà hỏng thì vẫn giữ điểm theo bộ cũ) · `invalid_cv` (file không phải CV — không bao giờ hiện thành 0 điểm) |
| **JD File** | File mô tả công việc gốc (PDF/DOCX) được HR upload kèm Job Posting, bên cạnh text JD. Gemini ưu tiên phân tích từ file gốc |
| **Skills Gap** | Danh sách kỹ năng mà JD yêu cầu nhưng CV chưa thể hiện – Gemini tự động detect và liệt kê |
| **Account Request (Yêu cầu tạo tài khoản)** | Đề xuất tạo tài khoản staff do HR Leader gửi (lẻ hoặc hàng loạt cùng `BatchId`), lưu ở bảng `account_requests` status `pending`/`approved`/`rejected`. Super Admin duyệt → tạo `User` active + email mật khẩu tạm, hoặc từ chối kèm lý do (ADR-041) |
| **Lock Reason (Lý do khóa)** | Lý do bắt buộc nhập khi Super Admin khóa một tài khoản (`User.LockReason`); hiển thị tại "Tất cả người dùng", xóa khi mở khóa |
| **Unlock Appeal (Kháng cáo mở khóa)** | Cơ chế cho người dùng bị khóa gửi lý do xin gỡ khóa – dự kiến triển khai phase sau |
| **Storage Key** | Khóa định danh đối tượng file lưu trong DB thay cho URL trực tiếp; dùng để sinh presigned URL qua `IFileStorageService` (ADR-036) |

---

## Technical Terms

| Thuật ngữ | Định nghĩa |
|---|---|
| **STT** | Speech-to-Text – chuyển giọng nói thành văn bản (**Deepgram Nova-3** streaming, gộp sẵn VAD/endpointing) |
| **TTS** | Text-to-Speech – chuyển văn bản thành giọng nói (ElevenLabs Flash v2.5 streaming, ~75ms) |
| **RAG** | Retrieval-Augmented Generation – retrieve chunks liên quan từ JD/CV/Playbook trước khi sinh câu hỏi. Do **RAG Service (Python)** sở hữu (ADR-039) |
| **RAG Service** | Microservice Python (`rag-service/`, FastAPI + LangChain + LangGraph) sở hữu toàn bộ chunk/embed/retrieve/sinh câu hỏi+đánh giá; .NET gọi qua HTTP/SSE nội bộ (ADR-039) |
| **Hybrid RAG** | Truy hồi kết hợp **dense** (pgvector cosine) + **sparse** (Postgres full-text) hợp nhất bằng **RRF** + weighting theo scope (ADR-025). Giai đoạn 1; sau là CRAG → Agentic |
| **RRF** | Reciprocal Rank Fusion – thuật toán hợp nhất 2 danh sách xếp hạng (dense + sparse) trong Hybrid RAG |
| **LangGraph** | Framework dựng pipeline RAG dạng StateGraph (retrieve→generate) trong RAG Service; điểm mở rộng cho CRAG/Agentic |
| **pgvector** | PostgreSQL extension lưu và tìm kiếm vector embeddings (bảng `document_chunks`, dùng cho RAG) |
| **VAD** | Voice Activity Detection – detect khi ứng viên bắt đầu/sắp dừng nói (barge-in + trigger RAG sớm). **Tích hợp sẵn trong Deepgram Nova-3** (`vad_events`/`endpointing`/`utterance_end`) — không cần thư viện riêng |
| **SignalR Hub** | Endpoint realtime ASP.NET Core, quản lý session lifecycle events |
| **ADR** | Architecture Decision Record – tài liệu ghi lại quyết định kiến trúc và lý do |
| **Clean Architecture** | Pattern tổ chức code: Domain → Application → Infrastructure → API |
| **EF Core Migration** | Script thay đổi database schema tạo bởi Entity Framework Core |
| **JWT** | JSON Web Token – cơ chế xác thực stateless |
| **OAuth2 Domain Validation** | Cơ chế bảo mật xác thực: Bắt buộc phân tích email đăng nhập OAuth2 nội bộ và so khớp domain công ty (`allowed_email_domains`) để chặn truy cập từ domain lạ |
| **Single-tenant** | Kiến trúc hệ thống dành riêng cho 1 doanh nghiệp duy nhất, không dùng `organization_id` phân tách |
| **Phòng chờ phỏng vấn** | Trạng thái `waiting` của phiên thật (ADR-067): ứng viên đã nhập mã nhưng AI chưa hỏi câu nào. Ra khỏi trạng thái này chỉ bằng thao tác **cho vào** của Hiring Manager — và họ phải **đã vào phòng** trước đó |
| **Available time của HM** | Khung giờ Hiring Manager có mặt được cho một vòng của một tin (`hiring_manager_availabilities`). HM khai ở mục **"Lịch tôi có mặt được"** trên màn tin — **độc lập với việc duyệt hồ sơ**; vòng trắc nghiệm không có. Recruiter chỉ xếp được ca nằm **trọn** trong một khung; việc chốt giờ với ứng viên (SMS/Zalo) diễn ra ngoài hệ thống |
| **Bridge file** | File chỉ chứa @import references, không có nội dung trực tiếp (AGENTS.md, CLAUDE.md) |
| **Source of truth** | `.ai/` folder – nơi duy nhất chứa thông tin chính thức, mọi tool đọc từ đây |
| **Gemini AI** | Google Gemini 2.5 Flash – dùng cho CV-JD Match Analysis (multimodal file input). Không dùng cho phỏng vấn AI (vẫn dùng GPT-4o) |
| **IGeminiProvider** | Interface abstract cho Google Gemini API trong backend. Method chính: `AnalyzeCvJdMatchAsync()`. Swap provider qua env var `GEMINI_PROVIDER` |
| **IFileStorageService** | Interface abstract cho lưu trữ file. 2 implementation: Local disk (dev) / Cloudflare R2 S3-compatible (prod) chọn qua `Storage:Provider`. DB chỉ lưu `storageKey`, sinh presigned URL khi cần (ADR-036) |
| **BatchId** | Mã nhóm gắn vào các `account_requests` được HR Leader gửi cùng một lần (bulk import) – để theo dõi/duyệt theo lô |

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
