#!/usr/bin/env bash
# ============================================================================
# ARISP — Sao lưu Postgres production (ADR-055)
#
# Từ khi bỏ Supabase, KHÔNG còn ai lo backup hộ nữa. Script này là lớp bảo vệ
# duy nhất cho dữ liệu: volume Postgres là thứ duy nhất trên VPS không dựng lại
# được từ git + GHCR.
#
# Cài cron (chạy 03:00 hằng ngày):
#   chmod +x /var/www/ARISP/scripts/backup-db.sh
#   crontab -e
#   0 3 * * * /var/www/ARISP/scripts/backup-db.sh >> /var/log/arisp-backup.log 2>&1
#
# Khôi phục:
#   docker exec -i arisp-postgres pg_restore -U postgres -d arisp_db --clean \
#     --if-exists < /var/backups/arisp/arisp_db_YYYYmmdd-HHMMSS.dump
#
# Biến môi trường ghi đè được: POSTGRES_CONTAINER, POSTGRES_DB, POSTGRES_USER,
# BACKUP_DIR, BACKUP_KEEP.
# ============================================================================
set -euo pipefail

CONTAINER="${POSTGRES_CONTAINER:-arisp-postgres}"
DB_NAME="${POSTGRES_DB:-arisp_db}"
DB_USER="${POSTGRES_USER:-postgres}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/arisp}"
KEEP="${BACKUP_KEEP:-14}"

log() { echo "[$(date '+%F %T')] $*"; }

if ! docker inspect -f '{{.State.Running}}' "$CONTAINER" 2>/dev/null | grep -q true; then
    log "LỖI: container '$CONTAINER' không chạy. Bỏ qua lần backup này."
    exit 1
fi

mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
OUT="$BACKUP_DIR/arisp_db_$STAMP.dump"

# -Fc (custom format): nén sẵn, và pg_restore chọn được từng bảng khi cần.
# Ghi ra .tmp rồi mới đổi tên — nếu pg_dump chết giữa chừng thì trong thư mục
# backup không bao giờ tồn tại file .dump dở dang trông như bản hợp lệ.
log "Bắt đầu dump $DB_NAME từ container $CONTAINER"
docker exec "$CONTAINER" pg_dump -U "$DB_USER" -d "$DB_NAME" -Fc > "$OUT.tmp"
mv "$OUT.tmp" "$OUT"

# Đọc thử mục lục bản dump. Một file dump tồn tại KHÔNG có nghĩa là nó khôi phục
# được — bước này bắt lỗi hỏng ngay hôm nay thay vì lúc đang cần restore.
if ! docker exec -i "$CONTAINER" pg_restore --list > /dev/null < "$OUT"; then
    log "LỖI: bản dump vừa tạo không đọc được — giữ lại để điều tra: $OUT"
    exit 1
fi

SIZE="$(du -h "$OUT" | cut -f1)"
log "Xong: $OUT ($SIZE)"

# Dọn bản cũ, giữ $KEEP bản gần nhất.
DELETED="$(ls -1t "$BACKUP_DIR"/arisp_db_*.dump 2>/dev/null | tail -n +"$((KEEP + 1))" || true)"
if [ -n "$DELETED" ]; then
    echo "$DELETED" | xargs -r rm --
    log "Đã xoá $(echo "$DELETED" | wc -l) bản cũ (giữ $KEEP bản gần nhất)"
fi

# NHẮC: backup nằm cùng máy với DB không cứu được khi mất/hỏng VPS.
# Cần copy định kỳ ra ngoài — R2, máy cá nhân, hoặc snapshot của nhà cung cấp.
log "Hoàn tất"
