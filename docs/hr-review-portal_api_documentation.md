# Hướng Dẫn Tích Hợp API Cho Frontend (Cập nhật ngày 09/06/2026)

Tài liệu này tóm tắt các thay đổi, nâng cấp và bổ sung của hệ thống API thuộc dự án **ARISP** thực hiện trong hôm nay. Hướng dẫn này giúp đội ngũ Frontend nắm rõ cấu trúc Request/Response, cách truyền tham số phân trang, bộ lọc và luồng xác thực (Authentication).

---

## 🗄️ Cấu trúc các bảng dữ liệu liên quan (Database Schema)

Để phát triển giao diện chuẩn xác, Frontend cần hiểu mối quan hệ của các bảng dữ liệu (Table) sau ở Backend:

| Tên bảng (Table) | Đối tượng đại diện | Các trường quan trọng | Mối quan hệ & Vai trò |
| :--- | :--- | :--- | :--- |
| **`users`** | Nhân viên nội bộ doanh nghiệp | `id`, `email`, `full_name`, `role`, `is_active` | Chứa tài khoản của `Super_admin`, `Hr_admin`, và `Recruiter`. |
| **`candidate_accounts`** | Tài khoản của ứng viên | `id`, `email`, `full_name`, `is_active` | Tài khoản dùng để đăng nhập vào Candidate Portal. |
| **`applications`** | Hồ sơ ứng tuyển của ứng viên | `id`, `candidate_account_id`, `job_posting_id`, `status` | Liên kết giữa ứng viên (`candidate_accounts`) và tin tuyển dụng (`job_postings`). Có thể liên kết bằng `candidate_account_id` hoặc so khớp qua email ứng viên. |
| **`interview_sessions`** | Các vòng phỏng vấn | `id`, `application_id`, `round_number`, `round_type`, `status` | Một hồ sơ ứng tuyển (`applications`) có thể có nhiều vòng phỏng vấn (Screening, Technical,...). |
| **`evaluations`** | Kết quả đánh giá của AI | `id`, `session_id`, `application_id`, `ai_verdict`, `overall_score` | Được tạo ra tự động bởi AI sau khi vòng phỏng vấn kết thúc ở trạng thái `completed`. |
| **`hr_reviews`** | HR duyệt và ghi đè kết quả | `id`, `evaluation_id`, `final_verdict`, `is_override`, `candidate_feedback` | Chứa kết quả xác nhận cuối cùng của HR cho một Evaluation. Khi được tạo ra, hệ thống tự động gửi email cho ứng viên. |
| **`audit_logs`** | Nhật ký hoạt động hệ thống | `id`, `actor_user_id`, `action`, `metadata`, `ip_address` | Ghi lại lịch sử các thao tác quan trọng (như đổi quyền user, xác nhận kết quả phỏng vấn). |

---

## 👥 2. API Quản lý User (Đã nâng cấp)
**Endpoint:** `GET /api/admin/users`
**Phân quyền:** Chỉ `SuperAdmin` hoặc `HrAdmin` được phép gọi.

### 📥 Tham số đầu vào (Query Parameters):
| Tham số | Kiểu dữ liệu | Mặc định | Mô tả |
| :--- | :--- | :--- | :--- |
| `search` | String | *Null* | Tìm kiếm gần đúng (không phân biệt hoa thường) theo tên hoặc email. |
| `role` | String | *Null* | Lọc theo Role (`super_admin`, `hr_admin`, `recruiter`, `candidate`). |
| `isActive` | Boolean | *Null* | Lọc theo trạng thái tài khoản (`true`: đang hoạt động, `false`: đã khóa). |
| `page` | Integer | `1` | Số thứ tự trang cần lấy (bắt đầu từ 1). |
| `pageSize` | Integer | `10` | Số lượng bản ghi trên một trang (giới hạn tối đa: `100`). |

### 📤 Định dạng phản hồi (JSON Response):
```json
{
  "totalCount": 12,
  "page": 1,
  "pageSize": 10,
  "totalPages": 2,
  "items": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "email": "superadmin@arisp.com",
      "fullName": "Alex Super Admin",
      "role": "Super_admin",
      "isActive": true,
      "createdAt": "2026-06-08T15:53:32Z"
    }
  ]
}
```

---

## 📩 3. API Xác nhận kết quả HR Review (Đã nâng cấp)
**Endpoint:** `POST /api/interview/review/confirm`
**Phân quyền:** Chỉ `SuperAdmin` hoặc `HrAdmin` mới được phép ghi đè (Override) kết quả AI.

### 📥 Định dạng Request Body (JSON):
```json
{
  "evaluationId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  "finalVerdict": "pass",
  "overrideReason": "Ứng viên trả lời lưu loát phần Coding và System Design",
  "shareRecording": true,
  "shareTranscript": true,
  "shareEvaluation": true,
  "shareFeedback": true,
  "candidateFeedback": "Chúc mừng bạn đã vượt qua thử thách đầu tiên!"
}
```
### ⚙️ Các nghiệp vụ tự động ở Backend:
1.  **Kiểm tra quyền hạn:** Nếu `finalVerdict` khác với kết quả AI đánh giá ban đầu (`AiVerdict`) $\rightarrow$ Backend tự động xác thực vai trò của tài khoản thực hiện. Chỉ `HrAdmin` hoặc `SuperAdmin` mới được lưu trạng thái ghi đè kết quả (`IsOverride = true`).
2.  **Tự động gửi email:** Sau khi lưu thành công kết quả review, hệ thống sẽ tự động kích hoạt gửi Email HTML chuyên nghiệp tới ứng viên liên quan:
    *   `finalVerdict = "pass"` $\rightarrow$ Gửi email **Chúc mừng trúng tuyển (Congrats)**.
    *   `finalVerdict = "not_pass"` $\rightarrow$ Gửi email **Từ chối (Rejection)**.

---

## 💼 4. Nhóm API Candidate Portal (Dành cho Ứng viên)
Các API này hỗ trợ ứng viên theo dõi hồ sơ của mình trên cổng riêng.
**Phân quyền:** Chỉ tài khoản có Role `Candidate` truy cập. Hệ thống có cơ chế bảo vệ chống lỗi phân quyền ngang (IDOR Protection) bằng cách kiểm tra quyền sở hữu dựa trên Token.

### A. Lấy danh sách hồ sơ ứng tuyển của tôi
*   **Endpoint:** `GET /api/portal/applications`
*   **Mô tả:** Trả về toàn bộ các đơn tuyển dụng ứng viên này đã nộp.
*   **Response:**
    ```json
    [
      {
        "id": "55555555-5555-5555-5555-555555555555",
        "jobTitle": "Senior Backend Engineer (.NET & AI)",
        "status": "reviewing",
        "createdAt": "2026-06-08T15:53:32Z"
      }
    ]
    ```

### B. Chi tiết hồ sơ ứng tuyển
*   **Endpoint:** `GET /api/portal/applications/{id}`
*   **Mô tả:** Trả về thông tin chi tiết đơn tuyển dụng kèm theo danh sách các vòng phỏng vấn mà ứng viên đã hoàn thành hoặc chuẩn bị tham gia.
*   **Response:**
    ```json
    {
      "id": "55555555-5555-5555-5555-555555555555",
      "jobTitle": "Senior Backend Engineer (.NET & AI)",
      "status": "reviewing",
      "sessions": [
        {
          "sessionId": "66666666-6666-6666-6666-666666666666",
          "roundNumber": 1,
          "roundType": "screening",
          "status": "completed",
          "evaluationId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
        }
      ]
    }
    ```

### C. Xem kết quả đánh giá vòng thi (Được chia sẻ)
*   **Endpoint:** `GET /api/portal/evaluations/{sessionId}`
*   **Mô tả:** Ứng viên xem kết quả đánh giá AI và nhận xét của HR cho vòng thi của mình (Chỉ hiển thị các phần thông tin mà HR tích chọn đồng ý chia sẻ khi duyệt ở mục 3).
*   **Response:**
    ```json
    {
      "sessionId": "66666666-6666-6666-6666-666666666666",
      "roundNumber": 1,
      "aiVerdict": "pass",
      "overallScore": 85.0,
      "candidateFeedback": "Chúc mừng bạn đã vượt qua thử thách đầu tiên!",
      "shareEvaluation": true,
      "shareFeedback": true
    }
    ```

---

## 📌 5. Lưu ý quan trọng cho Frontend
1.  **Xử lý phân trang:** Ở trang quản trị danh sách User, hãy sử dụng các thuộc tính `totalCount` và `totalPages` để hiển thị phân trang ở giao diện người dùng (ví dụ: nút Next/Prev, số trang).
2.  **Header Authorization:** Đảm bảo tất cả các API trên đều đính kèm Header:
    `Authorization: Bearer <Token>`
3.  **Lỗi trùng khóa đánh giá (Conflict):** Hãy đảm bảo chỉ thực hiện Xác nhận HR Review (`/api/Interview/review/confirm`) **một lần duy nhất** cho mỗi `EvaluationId`. Gửi liên tiếp hoặc gửi lại nhiều lần cho cùng 1 ID đánh giá sẽ gây ra lỗi máy chủ (`ArgumentException` hoặc `Conflict`).
