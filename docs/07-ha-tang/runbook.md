# Runbook — Xử lý sự cố

> ⚠️ Quy trình tham chiếu, **chưa diễn tập thực tế**. Cập nhật sau mỗi sự cố thật với những gì đã thực
> sự làm.

Mọi lệnh chạy từ `/opt/langcenter` trên VPS.

## Lệnh chẩn đoán nhanh

```bash
docker compose ps                  # service nào đang chạy/chết
docker compose logs -f --tail=100 api
docker compose logs -f --tail=100 caddy
df -h                              # dung lượng ổ đĩa
free -h                            # RAM
docker stats --no-stream           # tài nguyên từng container
```

## Sự cố 1 — Website không truy cập được

1. **VPS còn sống?** `ping <IP>`, thử SSH. Không vào được → kiểm tra bảng điều khiển nhà cung cấp VPS.
2. **Container chạy không?** `docker compose ps`. Có container `Exited` → `docker compose logs <tên>`.
3. **Caddy lỗi?** `docker compose logs caddy` — thường là lỗi cấp chứng chỉ (DNS chưa trỏ đúng, hoặc
   chạm rate limit Let's Encrypt).
4. **Ổ đĩa đầy?** `df -h` — đầy ổ khiến container chết hàng loạt. Xem sự cố 2.
5. Khởi động lại có kiểm soát: `docker compose restart api` trước, `docker compose down && up -d` sau.

## Sự cố 2 — Ổ đĩa đầy

Nguyên nhân thường gặp: image cũ tích tụ, log container không giới hạn, backup không dọn.

```bash
docker system df                       # xem cái gì chiếm chỗ
docker image prune -a                  # xóa image không dùng
docker builder prune                   # xóa cache build
du -sh /var/lib/docker/containers/*    # tìm log container phình to
du -sh /opt/backup/*                   # backup cũ chưa dọn
```

**Phòng ngừa:** đặt `logging` driver với `max-size`/`max-file` trong `docker-compose.yml`; cron dọn
backup cũ hơn N ngày.

> Không xóa volume (`docker volume prune`) khi chưa chắc chắn — `postgres-data` và `minio-data` nằm ở
> đó. Xóa nhầm là mất toàn bộ dữ liệu.

## Sự cố 3 — Database không kết nối được

```bash
docker compose logs postgres
docker compose exec postgres pg_isready -U "$POSTGRES_USER"
```

Kiểm tra: `ConnectionStrings__Default` có `Host=postgres` (tên service) chứ không phải `localhost`;
container `postgres` và `api` cùng network `backend`; `POSTGRES_PASSWORD` trong `.env` khớp với mật khẩu
đã khởi tạo volume.

> Đổi `POSTGRES_PASSWORD` trong `.env` **không** đổi mật khẩu trong volume đã tồn tại — phải đổi trong
> DB bằng `ALTER USER`.

## Sự cố 4 — Restore database từ backup

```bash
# 1. Dừng API để không có ghi mới trong lúc restore
docker compose stop api

# 2. Xác minh file backup trước khi dùng
gunzip -t /opt/backup/db-YYYY-MM-DD.sql.gz && echo "file nguyên vẹn"

# 3. Sao lưu trạng thái hiện tại TRƯỚC khi ghi đè (đường lui nếu restore sai bản)
docker compose exec -T postgres pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" \
  | gzip > /opt/backup/truoc-restore-$(date +%F-%H%M).sql.gz

# 4. Restore
gunzip -c /opt/backup/db-YYYY-MM-DD.sql.gz \
  | docker compose exec -T postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"

# 5. Khởi động lại và kiểm tra
docker compose start api
docker compose logs -f api
```

**Bước 3 không được bỏ qua** — restore nhầm bản backup mà không có ảnh chụp trạng thái hiện tại là mất
dữ liệu không thể phục hồi.

Sau khi restore: đăng nhập kiểm tra dữ liệu **của ít nhất 2 tenant** để xác nhận không lẫn dữ liệu.

## Sự cố 5 — Deploy hỏng, cần quay lui

Image được tag theo `github.sha`, nên quay lui = trỏ về SHA trước đó:

```bash
# Sửa tag image trong docker-compose.yml về SHA của bản chạy tốt gần nhất
docker compose pull && docker compose up -d
```

Nếu bản lỗi đã chạy migration đổi schema → quay lui image **không đủ**, phải xử lý cả migration. Đây là
lý do
[quy ước migration](../05-database/quy-uoc-migration.md#nguyên-tắc) yêu cầu review script SQL trước khi
apply lên production.

## Sự cố 6 — Chứng chỉ HTTPS không gia hạn

Caddy tự gia hạn. Nếu thất bại:

```bash
docker compose logs caddy | grep -i "certificate\|acme\|error"
```

Kiểm tra: DNS còn trỏ đúng IP không; cổng 80 còn mở không (Let's Encrypt cần cổng 80 để xác thực, kể cả
khi site chạy HTTPS); có chạm rate limit Let's Encrypt do restart Caddy quá nhiều lần không.

## Sự cố 7 — Nghi ngờ rò rỉ dữ liệu chéo tenant

**Mức độ nghiêm trọng cao nhất** của hệ thống này.

1. Xác định endpoint liên quan, khoanh vùng qua log.
2. Kiểm tra truy vấn có bị `IgnoreQueryFilters()`, raw SQL thiếu `tenant_id`, hoặc truy vấn trực tiếp
   bảng con không mang `tenant_id` — xem
   [những chỗ Global Query Filter không bảo vệ](../03-backend/multi-tenant.md#những-chỗ-global-query-filter-không-bảo-vệ).
3. Vá lỗi + **bổ sung integration test cách ly tenant** cho endpoint đó (thiếu test là lý do lỗi lọt
   lưới ngay từ đầu).
4. Đánh giá phạm vi ảnh hưởng và thông báo cho các trung tâm liên quan.

## Liên hệ & báo cáo bảo mật

Xem [SECURITY.md](../../SECURITY.md) — không tạo public issue mô tả chi tiết cách khai thác.
