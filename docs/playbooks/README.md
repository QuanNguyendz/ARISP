# Bộ playbook mẫu — kích hoạt RAG cho phỏng vấn AI

Hai bộ tài liệu soạn sẵn theo đúng cách pipeline RAG hiện tại đọc chúng (ADR-025 · 039 · 060 · 070 · 071 · 073):

- **`cong-ty/`** — playbook cấp công ty, áp cho **mọi tin**. HR Admin / Super Admin nạp ở màn **Interview Playbook**.
- **`tin-senior-backend-dotnet/`** — playbook của **một tin** (Senior Backend .NET, 3 vòng: 1 sơ loại → 2 trắc nghiệm → 3 chuyên môn — khớp tin `seed-interview-job` của môi trường dev). Hiring Manager chính của tin (hoặc HR Admin) nạp ở màn tin → **Playbook của tin**.

> Playbook **chỉ dùng cho buổi phỏng vấn THẬT** (Kiosk). Buổi thử (Practice) chỉ đọc JD + CV.

## 1. Nạp file nào, ở đâu, với lựa chọn gì

### Playbook công ty (màn Interview Playbook → *Thêm playbook công ty*)

| File | Loại tài liệu | Hệ thống dùng ở đâu |
|---|---|---|
| `01_compliance.txt` | Quy định không được hỏi | **Chấm CV**: toàn bộ, vào mục "không được dùng để chấm điểm". **Phỏng vấn**: thành *ràng buộc cứng*, nhưng chỉ khi đoạn đó được truy hồi (xem mục 3) |
| `02_competency_framework.txt` | Khung năng lực | **Chấm CV** (toàn bộ, làm chuẩn đối chiếu) + ngữ cảnh khi sinh câu hỏi |
| `03_red_flag.txt` | Red Flag Guide | **Chấm CV** (toàn bộ) + "dấu hiệu cần đào sâu" khi sinh câu hỏi |
| `04_culture_guide.txt` | Văn hóa & giá trị | Ngữ cảnh khi sinh câu hỏi (khi hội thoại chạm tới làm việc nhóm, bất đồng, học hỏi…) |
| `05_style_guide.txt` | Hướng dẫn phong cách | Ngữ cảnh khi sinh câu hỏi — viết theo **tình huống** (ứng viên "không biết", trả lời chung chung, hỏi lương…) để được truy hồi đúng lúc |

> **Thay nội dung `04_culture_guide.txt` bằng giá trị văn hoá thật của công ty** trước khi nạp lên production. Các file còn lại dùng được ngay.

### Playbook của tin (màn tin → *Playbook của tin* → *Thêm*)

| File | Loại tài liệu | Áp cho | Hệ thống dùng ở đâu |
|---|---|---|---|
| `tin_01_competency_framework.txt` | Khung năng lực | **Cả tin** | Chấm CV + ngữ cảnh sinh câu hỏi |
| `tin_02_red_flag.txt` | Dấu hiệu cần đào sâu | **Cả tin** | Chấm CV + đào sâu khi phỏng vấn |
| `vong1_01_round_playbook.txt` | Playbook theo vòng | **Vòng 1** | Ngữ cảnh sinh câu hỏi vòng sơ loại |
| `vong1_02_must_ask.txt` | Câu hỏi bắt buộc | **Vòng 1** | 3 câu AI **hỏi đầu tiên**, phiên không kết thúc được khi còn câu chưa hỏi |
| `vong1_03_question_bank.txt` | Ngân hàng câu hỏi | **Vòng 1** | Ngữ cảnh sinh câu hỏi |
| `vong1_04_expected_answer.txt` | Đáp án mong đợi | **Vòng 1** | Đào sâu khi hỏi (không đọc cho ứng viên) **+ chuẩn chấm** khi AI viết báo cáo |
| `vong3_01_round_playbook.txt` | Playbook theo vòng | **Vòng 3** | Ngữ cảnh sinh câu hỏi vòng chuyên môn |
| `vong3_02_must_ask.txt` | Câu hỏi bắt buộc | **Vòng 3** | 3 câu hỏi đầu tiên, chặn kết thúc phiên |
| `vong3_03_question_bank.txt` | Ngân hàng câu hỏi | **Vòng 3** | Ngữ cảnh sinh câu hỏi |
| `vong3_04_technical_scenario.txt` | Kịch bản kỹ thuật | **Vòng 3** | Ngữ cảnh sinh câu hỏi |
| `vong3_05_expected_answer.txt` | Đáp án mong đợi | **Vòng 3** | Đào sâu khi hỏi + chuẩn chấm báo cáo |
| `vong3_06_red_flag.txt` | Dấu hiệu cần đào sâu | **Vòng 3** | Đào sâu khi phỏng vấn (không vào prompt chấm CV) |

"Vòng 3" là vòng chuyên môn của tin mẫu. Tin của bạn có vòng chuyên môn là vòng 2 thì chọn vòng 2 — hệ thống từ chối gắn playbook vào vòng trắc nghiệm.

### Bộ tiêu chí — BẮT BUỘC, nhập ở màn tin (không qua *Thêm playbook*)

| File | Nơi nhập | Thiếu thì sao |
|---|---|---|
| `tieu_chi_cham_cv.xlsx` | Màn tin → **Bộ tiêu chí chấm CV** → *Nhập Excel* → Lưu | Không chấm CV được, tin không sang `pending`/`active` (ADR-070) |
| `tieu_chi_phong_van_chung_chuyen_mon.xlsx` | Màn tin → **Bộ tiêu chí chấm phỏng vấn** → *bộ chung của tin* → *Nhập Excel* | Buổi phỏng vấn không ra báo cáo (`BlockedNoRubric`, ADR-073) |
| `tieu_chi_phong_van_vong1_so_loai.xlsx` | Cùng chỗ → *bộ riêng vòng 1* | Vòng 1 dùng bộ chung (chuyên môn) — chấm sơ loại theo tiêu chí kỹ thuật |

Cả ba file đã được kiểm bằng chính `RubricSheet.Parse` + `ScoringRubric.Validate` + `CvRubricEditing.Normalize`: tổng trọng số 100, đủ 4 mức neo, bộ CV có 2–5 ý kiểm/tiêu chí. Bộ phỏng vấn không có ý kiểm — hệ thống bỏ cột đó với phỏng vấn.

**Không cần** nạp mẫu tiêu chí ở cấp công ty: nó chỉ là mẫu để chép, không tham gia chấm, lại còn bị nạp vào kho RAG như một đoạn ngữ cảnh chung cho mọi tin.

## 2. Vì sao là `.txt`

| Định dạng | Chuyện xảy ra khi nạp |
|---|---|
| `.txt` | Giữ nguyên dòng trống → **mỗi đoạn là một chunk**, đúng ranh giới người viết đặt |
| `.docx` | Parser nối các đoạn bằng **một** dấu xuống dòng và bỏ đoạn trống → cả file thành một khối, bị cắt máy móc mỗi 500 ký tự, đứt giữa câu |
| `.pdf` | Text từng trang ra liền một mạch, mất xuống dòng → cùng hậu quả |
| `.md` | Màn upload cho chọn, nhưng parser hiện **từ chối** (`DocumentParserService` chỉ nhận `.txt/.pdf/.docx`) |

Lưu file **UTF-8**. Sửa bằng Notepad/VS Code đều được.

## 3. Luật viết (khi sửa file này hoặc soạn cho tin khác)

Pipeline truy hồi chỉ lấy **5 đoạn** mỗi lần sinh câu hỏi, cạnh tranh chung với các đoạn của CV và JD. Câu truy vấn là **câu bắt buộc đang hỏi**, hoặc **câu hỏi + câu trả lời gần nhất**. Từ đó:

1. **Một đoạn = một chủ đề, tự đứng được.** Đoạn được lấy ra một mình, nên không viết "như trên", "xem mục 2".
2. **Mỗi đoạn dưới 1000 ký tự** (nên 250–800). Từ 1000 ký tự trở lên, chunker cắt cứng mỗi 500 ký tự. Các đoạn cách nhau đúng **một dòng trống**; trong đoạn chỉ xuống dòng đơn.
3. **Dòng đầu mỗi đoạn là nhãn chủ đề** `[Vòng 3 – Đáp án mong đợi – …]` chứa từ khoá. Tìm kiếm từ khoá (full-text `simple`) không bỏ dấu, không tách gốc từ, nên phải viết **đúng chữ ứng viên sẽ nói**, kèm thuật ngữ tiếng Anh (N+1, AsNoTracking, race condition…).
4. **Không có đoạn chỉ là tiêu đề** — một đoạn "PLAYBOOK CÔNG TY" đứng riêng là một chunk rác chiếm chỗ trong top 5.
5. **Câu bắt buộc**: mỗi dòng là một câu AI *buộc phải hỏi*, trừ dòng bắt đầu bằng `#`. Không viết ghi chú không có `#` — nó sẽ thành một câu hỏi. Giữ **3–4 câu/vòng**: chúng được hỏi trước tiên và phiên bị giới hạn 12 câu. Câu bắt buộc cấp **công ty không được dùng**; câu bắt buộc "Cả tin" áp cho **mọi** vòng hội thoại, nên hãy gắn theo vòng.
6. **Mỗi câu bắt buộc có một đoạn đáp án mong đợi dùng cùng từ khoá.** Khi AI hỏi câu bắt buộc, chính câu đó là câu truy vấn — đoạn đáp án khớp từ khoá sẽ được kéo lên để AI biết cần đào sâu tới đâu. Lúc chấm, hệ thống truy hồi đáp án bằng ~2000 ký tự đầu của buổi phỏng vấn, tức là phần câu bắt buộc.
7. **Cấm hỏi phải chứa từ ứng viên có thể nói ra** (con nhỏ, có bầu, quê, tôn giáo…). Ở phía phỏng vấn, đoạn cấm chỉ thành ràng buộc khi được truy hồi.
8. **Ngân sách chấm CV:** Khung năng lực + Dấu hiệu cần đào sâu của **công ty + cả tin** bị gộp và cắt ở **6000 ký tự** (thứ tự gộp không cố định, phần vượt bị cắt ngẫu nhiên). Bộ mẫu hiện dùng ~4000. Dấu hiệu chỉ để phỏng vấn thì gắn **theo vòng** — không tốn ngân sách này.

## 4. Kiểm tra sau khi nạp

Mọi tài liệu phải có số chunk bằng số đoạn trong file và `chunk_dai_nhat` dưới 1000:

```sql
SELECT pd.file_name, pd.scope, pd.round_number, pd.document_type,
       count(dc.id) AS so_chunk, max(length(dc.chunk_text)) AS chunk_dai_nhat
FROM playbook_documents pd
LEFT JOIN document_chunks dc ON dc.source_type = 'playbook' AND dc.source_id = pd.id
WHERE pd.deleted_at IS NULL
GROUP BY pd.file_name, pd.scope, pd.round_number, pd.document_type
ORDER BY pd.scope, pd.round_number NULLS FIRST, pd.document_type;
```

Số chunk mong đợi: compliance 7 · competency (công ty) 3 · red_flag (công ty) 3 · culture 5 · style 6 · competency (tin) 5 · red_flag (tin) 1 · vòng 1: 3 / 1 / 4 / 4 · vòng 3: 3 / 1 / 10 / 3 / 9 / 5.

Sau một buổi phỏng vấn thật ở vòng có câu bắt buộc, 3 câu đầu phải mang `source = 'playbook_must_ask'`:

```sql
SELECT q.sequence_number, q.source, left(q.question_text, 80)
FROM questions q
WHERE q.session_id = '<id phiên>'
ORDER BY q.sequence_number;
```
