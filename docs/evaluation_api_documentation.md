# Tài Liệu Tích Hợp API - Phân Hệ Đánh Giá (Evaluation API)

Tài liệu này hướng dẫn chi tiết về các bảng dữ liệu liên quan và **4 API** trong `EvaluationsController` để phục vụ việc tích hợp giao diện Admin Portal cho Frontend.

---

## 💾 Các Bảng Làm Việc Liên Quan (Database Tables)
Hệ thống sử dụng và kết hợp thông tin từ 4 bảng chính để xây dựng báo cáo đánh giá hoàn chỉnh:



1. **`Evaluations` (Bảng Đánh giá AI)**: Lưu kết quả phân tích tự động từ AI sau buổi phỏng vấn.
   * *Trường quan trọng*: `Id`, `SessionId`, `ApplicationId`, `AiVerdict` (`pass` | `not_pass`), `OverallScore`, `CriterionScores` (JSON điểm thành phần), `CheatScore`, `CheatSignals` (JSON tín hiệu gian lận), `LanguageAssessment` (JSON đánh giá ngôn ngữ).
2. **`HrReviews` (Bảng HR Phê duyệt)**: Lưu trữ quyết định phê duyệt cuối cùng của HR.
   * *Trường quan trọng*: `Id`, `EvaluationId` (Khóa ngoại liên kết với Evaluations), `FinalVerdict` (`pass` | `not_pass`), `IsOverride` (Có ghi đè AI không), `CandidateFeedback` (Nhận xét gửi ứng viên).
3. **`Applications` (Hồ sơ ứng tuyển)**: Liên kết qua `ApplicationId` để lấy thông tin ứng viên (`CandidateName`, `CandidateEmail`).
4. **`JobPostings` (Tin tuyển dụng)**: Liên kết qua `JobPostingId` của Application để lấy tên vị trí tuyển dụng (`JobTitle`).

## 📌 Danh Sách 4 API Trong EvaluationsController

### 1. Lấy danh sách đánh giá phân trang
* **Endpoint:** `GET /api/evaluations`
* **Query Parameters:**
  * `jobPostingId` *(Guid, Optional)*: Lọc theo ID vị trí tuyển dụng. (Không được truyền Guid rỗng).
  * `status` *(string, Optional)*: Lọc trạng thái. Chấp nhận các giá trị:
    * `pending`: Danh sách **chờ duyệt** (chưa có bản ghi `HrReview`).
    * `completed`: Danh sách **đã duyệt** (đã có bản ghi `HrReview`).
    * `pass`: Danh sách đạt (dựa trên kết quả duyệt của HR, hoặc AI nếu chưa duyệt).
    * `not_pass`: Danh sách không đạt.
  * `page` *(int, Optional, Default = 1)*: Trang hiện tại ($\ge 1$).
  * `pageSize` *(int, Optional, Default = 10)*: Kích thước trang ($1 \to 10$).

* **Response (200 OK):**
  ```json
  {
    "items": [
      {
        "id": "77777777-7777-7777-7777-777777777777",
        "sessionId": "66666666-6666-6666-6666-666666666666",
        "applicationId": "55555555-5555-5555-5555-555555555555",
        "roundNumber": 1,
        "sessionType": "real",
        "aiVerdict": "pass",
        "overallScore": 85.0,
        "cheatScore": 5.0,
        "createdAt": "2026-06-04T15:01:52Z",
        "candidateName": "John Doe Candidate",
        "candidateEmail": "candidate@example.com",
        "jobTitle": "Senior Backend Engineer (.NET & AI)",
        "status": "completed",
        "finalVerdict": "pass"
      }
    ],
    "total": 1,
    "page": 1,
    "pageSize": 10,
    "totalPages": 1
  }
  ```

---

### 2. Xem chi tiết báo cáo đánh giá theo ID
* **Endpoint:** `GET /api/evaluations/{id}`
* **Path Parameter:**
  * `id` *(Guid, Required)*: ID của bản đánh giá `Evaluation` (hoặc `SessionId` của phiên phỏng vấn).

* **Response (200 OK):**
  ```json
  {
    "id": "77777777-7777-7777-7777-777777777777",
    "sessionId": "66666666-6666-6666-6666-666666666666",
    "applicationId": "55555555-5555-5555-5555-555555555555",
    "roundNumber": 1,
    "sessionType": "real",
    "aiVerdict": "pass",
    "overallScore": 85.0,
    "criterionScores": {
      "technical": 88.0,
      "communication": 80.0,
      "culture_fit": 85.0
    },
    "reasoning": "Candidate shows strong backend fundamentals in .NET Core, good understanding of query performance tuning, and clear communication.",
    "recommendedNextStep": "Mời ứng viên tham gia vòng phỏng vấn chuyên sâu Technical Deep-dive.",
    "questionAnalyses": [
      {
        "Question": "Tại sao bạn chọn .NET?",
        "Answer": "Vì nó mạnh mẽ và bảo mật tốt.",
        "Score": 90.0,
        "Analysis": "Trả lời tự tin, đúng trọng tâm.",
        "Feedback": "Tốt"
      }
    ],
    "cheatScore": 5.0,
    "cheatSignals": [
      {
        "type": "tab_switch",
        "severity": "low",
        "description": "Ứng viên chuyển tab 1 lần",
        "timestamp": "2026-06-04T14:15:00Z"
      }
    ],
    "languageAssessment": {
      "language": "en",
      "fluency": 8.0,
      "grammar": 7.5,
      "vocabulary": 8.0,
      "comprehension": 8.5,
      "overall_score": 8.0
    },
    "createdAt": "2026-06-04T15:01:52Z",
    "updatedAt": "2026-06-04T15:01:52Z",
    "candidateName": "John Doe Candidate",
    "candidateEmail": "candidate@example.com",
    "jobTitle": "Senior Backend Engineer (.NET & AI)",
    "hrReview": {
      "id": "88888888-8888-8888-8888-888888888888",
      "evaluationId": "77777777-7777-7777-7777-777777777777",
      "reviewedByUserId": "22222222-2222-2222-2222-222222222222",
      "finalVerdict": "pass",
      "isOverride": false,
      "overrideReason": null,
      "shareRecording": true,
      "shareTranscript": true,
      "shareEvaluation": true,
      "shareFeedback": true,
      "candidateFeedback": "Chúng tôi đánh giá cao kinh nghiệm thực chiến của bạn. Hẹn gặp bạn ở vòng sau.",
      "createdAt": "2026-06-05T15:01:52Z",
      "updatedAt": "2026-06-05T15:01:52Z"
    }
  }
  ```
  *(Trường `hrReview` sẽ trả về `null` nếu trạng thái là `pending`).*

---

### 3. Xem chi tiết báo cáo đánh giá theo Session ID
* **Endpoint:** `GET /api/evaluations/session/{sessionId}`
* **Mục đích sử dụng:** 
  * Dùng khi ứng viên hoàn thành ca thi (tại trang `/interview/room/:sessionId`), Frontend chuyển hướng ứng viên sang `/candidate/results/:sessionId` và dùng `sessionId` để gọi trực tiếp kết quả.
  * Dùng tại màn hình danh sách ca phỏng vấn của HR (`/admin/interviews`), khi click nút "Xem" chi tiết một ca thi cụ thể.
  * Giúp lấy báo cáo đánh giá thông qua `sessionId` mà không cần biết trước ID của bảng đánh giá (`evaluationId`).
* **Path Parameter:**
  * `sessionId` *(Guid, Required)*: ID của phiên phỏng vấn.
* **Response (200 OK):** Trả về cấu trúc giống hệt API số 2.

---

### 4. Lấy danh sách đánh giá theo ID Hồ sơ ứng tuyển (Application ID)
* **Endpoint:** `GET /api/evaluations/application/{applicationId}`
* **Path Parameter:**
  * `applicationId` *(Guid, Required)*: ID của hồ sơ ứng tuyển.
* **Response (200 OK):** Trả về danh sách (`List<EvaluationListItemResponse>`) lịch sử tất cả các vòng phỏng vấn mà ứng viên này đã tham gia.
  ```json
  [
    {
      "id": "77777777-7777-7777-7777-777777777777",
      "sessionId": "66666666-6666-6666-6666-666666666666",
      "applicationId": "55555555-5555-5555-5555-555555555555",
      "roundNumber": 1,
      "sessionType": "real",
      "aiVerdict": "pass",
      "overallScore": 85.0,
      "cheatScore": 5.0,
      "createdAt": "2026-06-04T15:01:52Z",
      "candidateName": "John Doe Candidate",
      "candidateEmail": "candidate@example.com",
      "jobTitle": "Senior Backend Engineer (.NET & AI)",
      "status": "completed",
      "finalVerdict": "pass"
    }
  ]
  ```

---

## ⚠️ Thông Tin Mã Lỗi Cần Xử Lý
* **`400 Bad Request`**: Xảy ra khi dữ liệu đầu vào không hợp lệ (Ví dụ: `page < 1`, `pageSize` > 10, `jobPostingId` rỗng, hoặc lọc `status` sai giá trị quy định).
* **`401 Unauthorized`**: Token xác thực không hợp lệ hoặc đã hết hạn.
* **`403 Forbidden`**: Tài khoản đăng nhập không thuộc quyền `InternalStaff` (như tài khoản Candidate).
* **`404 Not Found`**: ID Đánh giá, Session hoặc Application không tồn tại trong hệ thống.
