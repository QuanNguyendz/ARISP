# ARISP - Tài liệu Tích hợp API Job Posting

Tài liệu này cung cấp chi tiết về 3 API chính liên quan đến tính năng Job Posting (Đăng tin tuyển dụng) dành cho Frontend Team.

---

## 1. GIẢI THÍCH CÁC THUỘC TÍNH (MODELS)

Dưới đây là giải nghĩa chi tiết cho các trường dữ liệu mà Frontend sẽ phải làm việc khi gửi hoặc nhận dữ liệu Job Posting.

### Bảng `JobPosting` (Tin tuyển dụng)
- `title` (string): Tiêu đề của tin tuyển dụng (VD: "Senior ReactJS"). Bắt buộc, max 200 ký tự.
- `department` (string): Phòng ban tuyển dụng (VD: "IT Engineering").
- `jobDescription` (string): Chi tiết mô tả công việc. Phải là mã HTML sinh ra từ công cụ text editor.
- `interviewMode` (string): Hình thức phỏng vấn. Chỉ nhận 3 giá trị: `"remote"`, `"onsite"`, hoặc `"both"`.
- `isPublicListing` (boolean): Có hiển thị công khai trên Job Board hay không.
- `location` (string): Địa điểm làm việc. Bắt buộc nếu `interviewMode` là `"onsite"` hoặc `"both"`.
- `workMode` (string): Hình thức làm việc (VD: `"hybrid"`, `"remote"`, `"office"`).
- `salaryMin` / `salaryMax` (number): Mức lương tối thiểu và tối đa. Phải `>= 0`.
- `salaryCurrency` (string): Đơn vị tiền tệ (VD: `"VND"`, `"USD"`).
- `salaryIsNegotiable` (boolean): Nếu là `true` (Lương thỏa thuận) thì `salaryMin` và `salaryMax` bắt buộc phải là `null`.
- `employmentType` (string): Loại hợp đồng (VD: `"full_time"`, `"part_time"`, `"contract"`).
- `experienceLevel` (string): Mức độ kinh nghiệm (VD: `"fresher"`, `"junior"`, `"senior"`).
- `skills` (array of string): Mảng các kỹ năng yêu cầu (VD: `[".NET", "React"]`).
- `jobCategory` (string): Lĩnh vực chuyên môn. Bắt buộc là 1 trong các giá trị: `"backend", "frontend", "devops", "qa", "data", "ai_ml", "mobile", "pm", "designer", "other"`.
- `applicationDeadline` (string ISO): Hạn chót nộp hồ sơ. Phải là thời điểm trong tương lai.
- `isUrgent` (boolean): Đánh dấu tin tuyển dụng gấp (hiển thị tag Gấp trên UI).
- `detectedLanguage` (string): Mã ngôn ngữ của bài Job Description do hệ thống tự nhận diện (VD: `"vi"`, `"en"`).
- `languageRequirement` (string): Yêu cầu ngoại ngữ dạng text do HR nhập (VD: "IELTS 6.0").
- `rescheduleDeadlineHours` (number): Số giờ tối đa trước phỏng vấn cho phép ứng viên đổi lịch (Mặc định: 24).
- `inviteTokenTtlHours` (number): Thời hạn sống của link mời phỏng vấn tính bằng giờ (Mặc định: 48).
- `scoringRubric` (array of object): Tiêu chí chấm điểm do AI thực hiện. Bắt buộc phải là mảng object thật, ví dụ `[{"criterion": "Tiếng Anh", "weight": 40}]`.
- `personaName` / `personaVoiceId` / `personaStyle` (string): Cấu hình giọng nói, phong cách cho AI Interviewer.

### Bảng `InterviewRoundConfig` (Cấu hình Vòng phỏng vấn)
Được truyền dưới dạng mảng nằm trong thuộc tính `roundConfigs` của `JobPosting`.
- `roundNumber` (number): Số thứ tự vòng phỏng vấn (Bắt buộc `> 0`).
- `roundType` (string): Loại vòng phỏng vấn (VD: `"screening"`, `"technical"`, `"hr"`).
- `interviewLanguage` (string): Ngôn ngữ dùng để phỏng vấn ở vòng này (VD: `"vi"`, `"en"`).
- `interviewCodeTtlHours` (number): Thời hạn tối đa để ứng viên hoàn thành bài test code (tính bằng giờ, bắt buộc `> 0`).
- `maxDurationMinutes` (number): Thời lượng phỏng vấn tối đa của vòng này (tính bằng phút, bắt buộc `> 0`).

---

## 2. TẠO MỚI TIN TUYỂN DỤNG (CREATE JOB)
API này cho phép HR tạo một Job Posting mới kèm theo cấu hình các vòng phỏng vấn AI.

- **Endpoint:** `POST /api/Jobs`
- **Auth Required:** `Bearer Token` (Role: `super_admin`, `hr_admin`, `recruiter`)
- **Content-Type:** `application/json`

### Các quy tắc Validation khắt khe Frontend cần lưu ý:
1. **Lương (Salary):**
   - Nếu check box `Thỏa thuận` (`salaryIsNegotiable`: true) thì **BẮT BUỘC** trường `salaryMin` và `salaryMax` phải gửi lên là `null` (hoặc loại bỏ khỏi JSON payload).
   - Nếu không thỏa thuận, giá trị nhập vào phải `>= 0` và `salaryMax` phải `>= salaryMin`.
2. **Vị trí chuyên môn (JobCategory):** 
   - Chỉ được phép gửi lên 1 trong các chuỗi sau (viết thường): `"backend", "frontend", "devops", "qa", "data", "ai_ml", "mobile", "pm", "designer", "other"`.
3. **Tiêu chí chấm điểm (ScoringRubric):**
   - **BẮT BUỘC** truyền mảng JSON Object thực sự (ví dụ: `[{"criterion": "...", "weight": 40}]`). Tuyệt đối không dùng `JSON.stringify()` để ép thành một chuỗi string duy nhất.
4. **Mô tả công việc (JobDescription):**
   - Gửi nguyên mã HTML sinh ra từ Rich Text Editor. **KHÔNG** được chứa các dấu xuống dòng gốc (`Enter`) làm gãy chuỗi JSON. Nếu có xuống dòng phải thay bằng ký tự `\n` hoặc nối thành 1 dòng liên tục trước khi nén JSON.
5. **Hình thức phỏng vấn (InterviewMode):**
   - Bắt buộc là: `"remote"`, `"onsite"`, hoặc `"both"`. Nếu khác `remote` thì bắt buộc phải có `location`.

### Payload Mẫu (JSON):
```json
{
  "title": "Middle/Senior Backend Developer",
  "department": "Engineering Team",
  "jobDescription": "<h2>Mô tả công việc</h2><p>Thiết kế, xây dựng hệ thống...</p>",
  "interviewMode": "onsite",
  "isPublicListing": true,
  "languageRequirement": "IELTS 6.0 hoặc tương đương",
  "rescheduleDeadlineHours": 24,
  "inviteTokenTtlHours": 48,
  "roundConfigs": [
    {
      "roundNumber": 1,
      "roundType": "screening",
      "interviewLanguage": "vi",
      "interviewCodeTtlHours": 2,
      "maxDurationMinutes": 30
    }
  ],
  "scoringRubric": [
    { "criterion": "C# & .NET Core", "weight": 50 },
    { "criterion": "Database Design", "weight": 50 }
  ],
  "personaName": "Mia (Tech Recruiter)",
  "personaVoiceId": "mia_en_us_1",
  "personaStyle": "professional",
  "location": "Tòa nhà Bitexco, Quận 1, TP.HCM",
  "workMode": "hybrid",
  "salaryMin": 1500,
  "salaryMax": 2500,
  "salaryCurrency": "USD",
  "salaryIsNegotiable": false,
  "employmentType": "full_time",
  "experienceLevel": "senior",
  "skills": ["C#", ".NET 8", "PostgreSQL"],
  "jobCategory": "backend",
  "applicationDeadline": "2026-10-15T23:59:59Z",
  "isUrgent": true
}
```

---

## 3. LẤY DANH SÁCH JOB CÔNG KHAI (JOB BOARD)
API này dùng để lấy danh sách các Job đang được Active để hiển thị ngoài trang chủ (Job Board) cho ứng viên xem.

- **Endpoint:** `GET /api/Jobs`
- **Auth Required:** Không cần token (Public API).
- **Mô tả:** Trả về danh sách rút gọn (`JobPostingListItemResponse`) được sắp xếp theo thời gian xuất bản (mới nhất lên đầu). Chỉ những Job có `isPublicListing = true` và `status = "active"` mới được hiển thị.

### Response Mẫu:
```json
[
  {
    "id": "33333333-3333-3333-3333-333333333333",
    "title": "Middle/Senior Backend Developer",
    "department": "Engineering Team",
    "interviewMode": "onsite",
    "status": "active",
    "detectedLanguage": "vi",
    "languageRequirement": "IELTS 6.0 hoặc tương đương",
    "createdAt": "2026-06-01T06:00:00Z",
    "publishedAt": "2026-06-01T06:00:00Z",
    "location": "Tòa nhà Bitexco, Quận 1, TP.HCM",
    "workMode": "hybrid",
    "employmentType": "full_time",
    "experienceLevel": "senior",
    "jobCategory": "backend",
    "isUrgent": true
  }
]
```

---

## 4. LẤY CHI TIẾT 1 JOB (JOB DETAILS)
API dùng để lấy chi tiết thông tin của 1 Job (để hiển thị trên trang Job Detail khi ứng viên click vào xem từ Job Board).

- **Endpoint:** `GET /api/Jobs/{id}`
- **Auth Required:** Không cần token (Public API).
- **Lưu ý:** `ScoringRubric` sẽ được trả về dưới dạng mảng/object JSON thật (Frontend có thể chấm (`.`) để truy cập dữ liệu bên trong ngay) chứ không phải dạng stringify.

### Response Mẫu:
```json
{
  "id": "33333333-3333-3333-3333-333333333333",
  "createdByUserId": "22222222-2222-2222-2222-222222222222",
  "title": "Middle/Senior Backend Developer",
  "department": "Engineering Team",
  "jobDescription": "<h2>Mô tả công việc</h2><p>Thiết kế...</p>",
  "interviewMode": "onsite",
  "status": "active",
  "isPublicListing": true,
  "detectedLanguage": "vi",
  "languageRequirement": "IELTS 6.0 hoặc tương đương",
  "languageConfirmed": true,
  "roundConfigs": [
    {
      "roundNumber": 1,
      "roundType": "screening",
      "interviewLanguage": "vi",
      "interviewCodeTtlHours": 2,
      "maxDurationMinutes": 30
    }
  ],
  "createdAt": "2026-06-01T06:00:00Z",
  "location": "Tòa nhà Bitexco, Quận 1, TP.HCM",
  "workMode": "hybrid",
  "salaryMin": 1500,
  "salaryMax": 2500,
  "salaryCurrency": "USD",
  "salaryIsNegotiable": false,
  "employmentType": "full_time",
  "experienceLevel": "senior",
  "skills": ["C#", ".NET 8", "PostgreSQL"],
  "jobCategory": "backend",
  "applicationDeadline": "2026-10-15T23:59:59Z",
  "isUrgent": true,
  "scoringRubric": [
    { "criterion": "C# & .NET Core", "weight": 50 },
    { "criterion": "Database Design", "weight": 50 }
  ]
}
```
