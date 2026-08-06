# Cấu hình CORS cho bucket R2 (bắt buộc — nếu không, xem CV DOCX sẽ hỏng)

> Áp dụng khi `Storage:Provider = S3` (production dùng Cloudflare R2 — ADR-036).
> Dev mặc định `Provider = Local` nên **không cần** bước này.

---

## Vì sao cần

ADR-036 chốt: file để **private** trên R2, trình duyệt đọc trực tiếp qua **presigned URL**.
Presigned URL giải quyết phần *xác thực*, nhưng **không** giải quyết phần *CORS* — trình
duyệt vẫn chặn JavaScript đọc response nếu R2 không trả `Access-Control-Allow-Origin`.
Bucket R2 mặc định **không có CORS rule nào**.

Hệ quả: chỉ những đường dẫn tải file bằng **JavaScript** mới hỏng, các đường dẫn còn lại
vẫn chạy nên rất dễ tưởng nhầm là mọi thứ đều ổn.

| Thứ | Cách tải trong `DocumentViewer` | Cần CORS? |
|---|---|---|
| PDF | `<iframe src>` | Không |
| Ảnh | `<img src>` | Không |
| Nút "Tải về" | `<a href>` | Không |
| **DOCX** | **`fetch()` → blob → `docx-preview`** | **Có** |

Triệu chứng khi thiếu CORS: modal xem tài liệu hiện *"Không thể hiển thị file DOCX trực
tiếp (có thể do CORS hoặc file lỗi). Hãy tải về để xem."* — trong khi PDF và nút Tải về
vẫn bình thường, và ở local (Provider=Local, cùng origin) thì không tái hiện được.

---

## Rule cần đặt

```json
[
  {
    "AllowedOrigins": [
      "https://arisp.io.vn",
      "https://www.arisp.io.vn",
      "https://staff.arisp.io.vn"
    ],
    "AllowedMethods": ["GET", "HEAD"],
    "AllowedHeaders": ["*"],
    "ExposeHeaders": ["Content-Length", "Content-Type"],
    "MaxAgeSeconds": 3600
  }
]
```

Ghi chú:

- **Chỉ `GET`/`HEAD`.** Upload đi qua backend (server-to-server, không dính CORS) nên
  không mở `PUT`/`POST` cho trình duyệt.
- **Liệt kê đủ 3 origin.** `arisp.io.vn` và `staff.arisp.io.vn` là 2 origin riêng biệt
  theo ADR-046; thiếu cái nào thì đúng site đó hỏng.
- **Không dùng `"*"`** cho `AllowedOrigins`: bucket chứa CV ứng viên, để `*` là cho bất
  kỳ trang web nào cũng đọc được file nếu lỡ lộ presigned URL.
- Nếu cần trỏ máy dev vào R2 để thử, thêm tạm `http://localhost:3000` và
  `http://localhost:3001` — **gỡ ra sau khi thử xong**.

---

## Cách đặt

### Cách 1 — Cloudflare Dashboard

R2 → chọn bucket → **Settings** → **CORS Policy** → **Edit** → dán JSON trên → Save.

### Cách 2 — AWS CLI (R2 tương thích S3)

```bash
cat > r2-cors.json <<'EOF'
{
  "CORSRules": [
    {
      "AllowedOrigins": ["https://arisp.io.vn", "https://www.arisp.io.vn", "https://staff.arisp.io.vn"],
      "AllowedMethods": ["GET", "HEAD"],
      "AllowedHeaders": ["*"],
      "ExposeHeaders": ["Content-Length", "Content-Type"],
      "MaxAgeSeconds": 3600
    }
  ]
}
EOF

aws s3api put-bucket-cors \
  --bucket "$R2_BUCKET" \
  --cors-configuration file://r2-cors.json \
  --endpoint-url "https://$R2_ACCOUNT_ID.r2.cloudflarestorage.com"
```

---

## Kiểm chứng

Lấy 1 presigned URL bất kỳ (mở DevTools → Network khi bấm xem CV, copy URL request đi
tới `*.r2.cloudflarestorage.com`), rồi:

```bash
curl -sI -H "Origin: https://staff.arisp.io.vn" "<presigned-url>" | grep -i access-control
```

Phải thấy:

```
access-control-allow-origin: https://staff.arisp.io.vn
```

Không thấy dòng này = rule chưa ăn. Sau đó mở lại modal xem CV DOCX trên
`staff.arisp.io.vn` để xác nhận nội dung dựng được.

---

## Nhớ làm lại khi

- Tạo bucket R2 mới (staging, migrate account…) — CORS **không** đi theo khi copy object.
- Thêm domain mới cho hệ thống → phải bổ sung vào `AllowedOrigins`.
