# Cấu hình máy trạm phỏng vấn tại văn phòng (Kiosk)

> **Dành cho bộ phận IT của doanh nghiệp**, làm một lần cho mỗi máy trạm.
>
> Đây **không phải** hướng dẫn test. Muốn chạy thử luồng phỏng vấn ở môi trường dev, xem
> [kiosk-interview-setup.md](kiosk-interview-setup.md).
>
> Tham chiếu: **ADR-062** (mô hình đe doạ, vì sao chọn nền web), ADR-052 (JWT phạm vi một phiên +
> ghi hình), ADR-054 (chế độ khoá màn hình + nhật ký rời phòng).

---

## Đọc trước: bước này có bắt buộc không?

**Có, nếu doanh nghiệp muốn "chế độ khoá" là lời hứa thật.**

Phần mềm ARISP chạy trong trình duyệt, và **trình duyệt không có quyền khoá hệ điều hành**. Cụ thể,
trang web **không** chặn được:

- `Alt+Tab` chuyển cửa sổ
- Phím `Windows` / `Cmd` mở menu Start
- `Ctrl+Alt+Del`
- Màn hình thứ hai
- Điện thoại đặt cạnh bàn phím

Lớp bảo vệ mà ứng dụng tự làm được (ADR-054) là **răn đe + ghi bằng chứng**: bật toàn màn hình, phủ
lớp chặn khi ứng viên rời toàn màn hình, chặn menu chuột phải và các phím tắt bắt được, rồi **ghi lại
mọi lần rời đi** gửi kèm kết quả cho nhân sự.

**Bỏ qua tài liệu này thì hệ thống vẫn chạy bình thường — chỉ là chỉ còn lớp răn đe đó.** Làm theo
tài liệu này thì thêm được lớp khoá cứng ở tầng hệ điều hành.

---

## 1. Lối tắt Chrome ở chế độ kiosk (tối thiểu — 5 phút)

Tạo shortcut trên desktop của máy trạm:

```
"C:\Program Files\Google\Chrome\Application\chrome.exe" ^
  --kiosk ^
  --disable-pinch ^
  --overscroll-history-navigation=0 ^
  --disable-features=TranslateUI ^
  --incognito ^
  "https://<tên-miền-ứng-viên>/kiosk"
```

| Cờ | Tác dụng |
|---|---|
| `--kiosk` | Toàn màn hình thật, **ẩn thanh địa chỉ và nút điều hướng** — ứng viên không gõ URL khác được |
| `--incognito` | Không lưu phiên giữa các ứng viên; đóng trình duyệt là sạch |
| `--disable-pinch` | Chặn phóng to bằng cảm ứng/touchpad |
| `--overscroll-history-navigation=0` | Chặn vuốt ngang để lùi trang |

> Thay `<tên-miền-ứng-viên>` bằng domain thật của site ứng viên (mặc định là site chạy cổng 3000 —
> xem `Frontend:CandidateBaseUrl`).

**Giới hạn còn lại sau bước này:** `Alt+Tab` và phím `Windows` **vẫn thoát ra được**. Muốn chặn nốt,
làm tiếp mục 2.

---

## 2. Windows Assigned Access (khoá cứng — khuyến nghị)

Assigned Access khoá một tài khoản Windows vào **đúng một ứng dụng**. Đăng nhập tài khoản đó thì
máy chỉ mở được Chrome ở chế độ kiosk, không có desktop, không Start menu, `Alt+Tab` vô hiệu.

1. Tạo một **tài khoản cục bộ** riêng, không mật khẩu, ví dụ `phongvan`. Không dùng tài khoản có
   quyền quản trị.
2. `Settings → Accounts → Other users → Set up a kiosk` (Windows 11) — hoặc
   `Assigned access` ở bản cũ hơn.
3. Chọn tài khoản `phongvan` → chọn **Microsoft Edge** hoặc **Chrome** ở chế độ kiosk → nhập URL
   `https://<tên-miền-ứng-viên>/kiosk`.
4. Đặt kiểu kiosk là **"As a digital sign or interactive display"** nếu chỉ cần một URL cố định,
   hoặc **"As a public browser"** nếu muốn có nút quay lại.

Thoát chế độ kiosk: `Ctrl+Alt+Del` → đăng xuất, hoặc tổ hợp thoát do IT đặt. **Không đưa tổ hợp này
cho ứng viên.**

> Máy Mac dùng **Guided Access** hoặc một hồ sơ MDM tương đương. Máy Linux dùng phiên
> `chrome --kiosk` trên một window manager tối giản.

---

## 3. Phần cứng và mạng

| Hạng mục | Yêu cầu | Vì sao |
|---|---|---|
| **Màn hình** | Chỉ **một** màn hình. Rút cáp màn hình phụ | Màn hình phụ nằm ngoài tầm ghi hình |
| **Webcam + mic** | Kiểm tra hoạt động trước buổi | Ứng dụng có bước kiểm tra thiết bị, nhưng phát hiện muộn thì mất thời gian của ứng viên |
| **Quyền camera/mic** | Cấp sẵn cho domain trong Chrome (`Site settings → Camera/Microphone → Allow`) | Tránh hộp thoại xin quyền chen giữa buổi |
| **Mạng** | Có dây nếu được | Mất kết nối giữa buổi tuy khôi phục được (ADR-052) nhưng vẫn gián đoạn |
| **Cổng USB** | Khoá qua Group Policy nếu chính sách công ty yêu cầu | Chặn mang dữ liệu ra/vào |
| **Vị trí đặt máy** | Nơi nhân viên quan sát được màn hình | **Đây mới là lớp giám sát chính** — xem ADR-062 |

---

## 4. Checklist trước mỗi buổi phỏng vấn

Dành cho nhân viên lễ tân / tuyển dụng:

- [ ] Máy đã đăng nhập tài khoản kiosk, Chrome đang ở màn **"Nhập mã phỏng vấn"**
- [ ] Màn hình chỉ có một, không có màn hình phụ đang cắm
- [ ] Webcam không bị che, mic hoạt động
- [ ] Bàn làm việc trống — **không điện thoại, không tài liệu, không thiết bị thứ hai**
- [ ] Đối chiếu **giấy tờ tuỳ thân** của ứng viên với hồ sơ trước khi đọc mã
- [ ] Cấp mã 6 ký tự (hiệu lực 2 giờ, dùng một lần)
- [ ] Nhắc ứng viên: buổi phỏng vấn **được ghi hình**, và **mọi lần rời khỏi màn hình đều được ghi nhận**

> Bước đối chiếu giấy tờ là thứ **duy nhất** chặn được việc nhờ người khác thi hộ và việc dùng camera
> ảo — phần mềm không làm thay được (ADR-062, giới hạn (d)).

---

## 5. Sau buổi phỏng vấn

- Màn hình **tự quay về trang nhập mã sau 30 giây** — máy sẵn sàng cho ứng viên kế tiếp, không cần
  thao tác gì.
- Mã đã dùng **tự vô hiệu**, không dùng lại được.
- Video lưu **7 ngày** rồi tự xoá (`Interview:RecordingRetentionDays`); transcript và bản đánh giá
  giữ vĩnh viễn (ADR-052).
- Số lần rời khỏi màn hình đi kèm kết quả gửi cho nhân sự — **là dữ kiện tham khảo cho người chấm,
  không tự đánh trượt ai**.

---

## Những gì cấu hình này KHÔNG giải quyết

Ghi ra để không ai tưởng đã kín:

| Không chặn được | Cách xử lý duy nhất |
|---|---|
| **Điện thoại có AI đặt ngoài khung hình** | Dọn bàn + nhân viên quan sát. Không phần mềm nào trên máy phỏng vấn thấy được |
| **Người khác thi hộ** | Đối chiếu giấy tờ tuỳ thân lúc cấp mã |
| **Camera ảo phát video quay sẵn** | Đối chiếu giấy tờ + quan sát tại chỗ |
| **Ứng viên đọc từ giấy ngoài khung hình** | Dọn bàn + quan sát |

Cả bốn đều là **quy trình vận hành**, không phải khiếm khuyết phần mềm. Lập luận đầy đủ ở **ADR-062**.
