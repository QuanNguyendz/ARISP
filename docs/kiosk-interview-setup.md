# Test phỏng vấn thật (Kiosk) + phỏng vấn thử — hướng dẫn từng bước

> Áp dụng cho môi trường **Development**. Mọi endpoint `/api/dev/*` trả 404 ở production.
> Tham chiếu: ADR-050 (practice), ADR-051 (xem lại buổi thử), ADR-052 (Kiosk + ghi hình), ADR-053 (ràng buộc vòng).

## 0. Chuẩn bị

```bash
# API (tự chạy migration lúc khởi động)
dotnet run --project ari-service/src/ARI.API

# Web
cd ari-web
npm run dev:candidate   # http://localhost:3000  (ứng viên + Kiosk)
npm run dev:staff       # http://localhost:3001  (HR / Recruiter)
```

Muốn chạy lại buổi thử nhiều lần trên cùng hồ sơ: đặt `Interview:PracticeAttemptsPerRound = 0` trong `appsettings.Development.json`.

## 1. Dựng job test 3 vòng

```bash
curl -X POST "http://localhost:5000/api/dev/seed-interview-job?fresh=true"
```

Tạo job **`[DEV] Kiosk Sandbox`** với 3 vòng:

| Vòng | Loại | Cách thực hiện |
|---|---|---|
| 1 | `screening` | Phỏng vấn AI — thử qua Portal, **thật qua Kiosk** |
| 2 | `online_test` | Thi trắc nghiệm (10 câu mẫu đã seed sẵn, sàn 70 điểm, 15 phút) |
| 3 | `technical` | Phỏng vấn AI thật qua Kiosk |

Response trả về: tài khoản ứng viên, tài khoản HR (`hr.dev@arisp.local`), `applicationId`, `practiceUrl`, và **`kioskCode`** — mã 6 ký tự cho vòng 1, dùng một lần, hết hạn sau 2 giờ.

> Gọi lại endpoint bất cứ lúc nào để lấy mã mới (mã cũ còn hạn thì trả lại chính nó).

## 2. Phỏng vấn thử (Remote, không cần mã)

1. Đăng nhập ứng viên tại `http://localhost:3000/jobs/login`.
2. Vào `/candidate/applications` → thẻ job hiện ô **"Phỏng vấn thử (Remote) · Vòng 1"** → bấm bắt đầu.
3. Kiểm tra thiết bị → phỏng vấn (audio-only, trần 20 phút, có thể gõ tay sửa nội dung thu âm).
4. Kết thúc → **"Xem lại & nhận xét AI"** → `/candidate/practice/:sessionId`.

Buổi thử **không** ảnh hưởng gì tới hồ sơ: trạng thái giữ nguyên, HR không nhìn thấy.

## 3. Phỏng vấn THẬT tại Kiosk

**Thiết bị Kiosk** = một máy đặt ở lễ tân, mở sẵn `http://localhost:3000/kiosk` (thực tế nên bật full-screen). Không cần ai đăng nhập trên máy này.

1. Ứng viên nhập **mã 6 ký tự** (`kioskCode` ở bước 1) → bấm **Bắt đầu phỏng vấn**.
   - Mã sai / đã dùng / hết hạn đều báo lý do riêng để biết cần làm gì.
2. **Kiểm tra thiết bị** (bắt buộc): cho phép camera + micro, thấy hình mình và vạch âm nhảy → **Vào phòng phỏng vấn**.
3. Trong phòng:
   - Avatar AI hỏi bằng giọng nói; phụ đề hiện ở cột phải.
   - Trả lời bằng giọng nói — chữ tự điền vào ô trả lời, **sửa/gõ tay được** trước khi bấm **Gửi trả lời**.
   - Nút mic bật/tắt; góc trên hiện **"Đang ghi hình"** + đồng hồ đếm ngược (trần 45 phút, đổi màu khi còn ≤5 phút).
   - Bấm nút đỏ để kết thúc sớm; hết giờ thì AI tự nói câu kết rồi đóng phiên.
4. Kết thúc → màn cảm ơn hiện **"Đang lưu bản ghi hình…"** → **"Đã lưu bản ghi hình"** → tự quay về màn nhập mã sau 30 giây (sẵn sàng cho người kế tiếp).

**Lỡ reload / mất điện giữa buổi:** mở lại `/kiosk`, bấm **"Tiếp tục buổi phỏng vấn đang dở"** — không cần mã mới (mã dùng một lần không nhập lại được).

### Khoá màn hình khi phỏng vấn (ADR-054)

Nhập đúng mã → trình duyệt **tự vào toàn màn hình**. Trong lúc phỏng vấn:

- Rời toàn màn hình (Esc, F11…) → **lớp phủ che toàn bộ phòng phỏng vấn**, phải bấm "Quay lại toàn màn hình" mới tiếp tục được.
- Chặn menu chuột phải và các phím tắt bắt được: `F11`, `F5`, `Ctrl+P/S/U/F/T/N/W/R`.
- Đóng/tải lại trang → cảnh báo xác nhận.
- **Mọi lần rời đi đều được ghi lại** (thoát toàn màn hình, chuyển tab, click ra ngoài cửa sổ, bấm phím tắt bị chặn, đóng trang) và gửi về server. Màn kết thúc hiện tổng số lần; HR thấy điểm nghi vấn + chi tiết theo loại trong màn đánh giá.

> **Giới hạn thật sự:** trang web **không** chặn được `Alt+Tab`, phím Windows/Command hay `Ctrl+Alt+Del`. Muốn khoá cứng thì phải cấu hình ở tầng máy:
> ```bash
> # Chrome ở chế độ kiosk thật (Windows)
> chrome.exe --kiosk --disable-pinch --overscroll-history-navigation=0 http://localhost:3000/kiosk
> ```
> Kết hợp **Windows Assigned Access** (Kiosk Mode của Windows) cho máy đặt tại quầy lễ tân. Lớp khoá phía web ở trên là phòng tuyến thứ hai + nguồn ghi log.

**Video:** lưu vào storage, tự xoá sau `Interview:RecordingRetentionDays` (mặc định **7 ngày**). Transcript và bản đánh giá **không** bị xoá.

## 4. HR xác nhận kết quả → mở vòng sau

1. Đăng nhập HR tại `http://localhost:3001/login` (`hr.dev@arisp.local`).
2. `/hr/evaluations` → chọn đánh giá vòng 1 → xem điểm, phân tích từng câu, **video buổi phỏng vấn** (kèm dòng nhắc hạn xoá).
   - Buổi **thử** không xuất hiện ở đây — theo thiết kế.
3. Bấm **Xác nhận** (hoặc Override kèm lý do).

**Trạng thái hồ sơ mong đợi sau mỗi bước:**

| Thời điểm | Trạng thái ứng viên thấy |
|---|---|
| Sau buổi thử | Không đổi (`Đang phỏng vấn`) |
| Xong phỏng vấn thật vòng 1, HR chưa xác nhận | `Đang phỏng vấn` + "Kết quả đang chờ HR Leader xác nhận" |
| HR xác nhận **Đạt** vòng 1 (còn vòng 2, 3) | **`Qua vòng 1/3`** |
| HR xác nhận **Đạt** vòng 3 (vòng cuối) | **`Đạt`** |
| HR xác nhận **Không đạt** ở bất kỳ vòng nào | `Không phù hợp` |

> AI **không bao giờ** tự đổi trạng thái hồ sơ — chỉ HR xác nhận mới đổi (ADR-053).

## 5. Vòng 2 — thi trắc nghiệm

- Ứng viên: `/candidate/applications/{applicationId}` → card **Trắc nghiệm** → làm bài (tự nộp khi hết giờ).
- HR xem điểm: `/hr/jobs/{jobPostingId}/online-test/results` (có xuất Excel).
- Ngân hàng câu hỏi: `/hr/jobs/{jobPostingId}/online-test` (thêm tay hoặc nhập từ Excel).

Kết quả trắc nghiệm **không tự** đổi trạng thái hồ sơ — HR đọc điểm rồi quyết định cấp mã vòng kế.

## 6. Vòng 3 — cấp mã Kiosk mới

Trang chi tiết ứng viên phía HR (`/hr/candidates/{applicationId}`) → **Cấp mã phỏng vấn**. Điều kiện: vòng đó đã có lịch `scheduled` (seed đã tạo sẵn cho vòng 1 và 3).

Rồi lặp lại bước 3 với mã mới.

## 7. Chấm lại phiên cũ (khi sửa prompt)

```bash
curl -X POST "http://localhost:5000/api/dev/regrade-session/{sessionId}?lang=vi"
```

Xoá bản đánh giá cũ và chấm lại bằng prompt hiện tại, ép ngôn ngữ báo cáo (`vi` | `en`).
Từ chối nếu HR đã xác nhận đánh giá đó.

## Xử lý sự cố

| Hiện tượng | Nguyên nhân thường gặp |
|---|---|
| Kiosk báo "Mã không tồn tại" | Gõ nhầm — mã không dùng ký tự `I`, `O`, `0`, `1` |
| Cấp mã báo "chưa đặt lịch" | Vòng đó chưa có `InterviewBooking` trạng thái `scheduled` |
| Không có avatar, chỉ có giọng | Thiếu key HeyGen → tự động lùi về audio-only (vẫn phỏng vấn bình thường) |
| Không nhận giọng nói | Thiếu key Deepgram → gõ tay vào ô trả lời rồi bấm Gửi |
| "Không lưu được bản ghi hình" | Kiểm tra `Interview:MaxRecordingSizeMb` và cấu hình storage. Lỗi cũ đã sửa: MediaRecorder gửi `video/webm;codecs=vp9,opus` — dấu phẩy trong tham số MIME làm AWS SDK ném `FormatException`; nay content type được cắt bỏ tham số trước khi PUT |
| Màn Kiosk co về góc trái | Đã sửa: route `/kiosk` từng bị bọc trong `InterviewLayout` (flex container + thanh tiêu đề) — nay là route toàn màn hình độc lập |
