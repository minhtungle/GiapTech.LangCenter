# Chuyển dữ liệu VIETGEN Academy (hệ cũ) → LangCenter

> Script và dữ liệu: [`scripts/chuyen-doi-vietgen/`](../../scripts/chuyen-doi-vietgen/chuyen-doi/HUONG-DAN.md).
> Ánh xạ chi tiết: `scripts/chuyen-doi-vietgen/tai-lieu/ANH_XA_SANG_LANGCENTER.md`.
>
> **Thư mục `du-lieu/` chứa dữ liệu cá nhân thật và bị `.gitignore` chặn.** Các file sinh ra từ
> nó (`01-chuyen-doi.sql`, `ky-vong.json`, hai file CSV) cũng bị chặn — xem
> `scripts/chuyen-doi-vietgen/.gitignore`.

## Việc này là gì

Chuyển **tài khoản · phân quyền · đơn hàng · doanh thu** của trung tâm VIETGEN Academy từ hệ cũ
(SQL Server, đã export JSON) sang một **trung tâm mới tạo** trong LangCenter.

Nạp bằng **SQL thẳng, không qua EF**: `AppDbContext.SaveChanges` luôn ghi `created_at = now` khi
thêm mới, nên nạp qua EF sẽ mất ngày tạo và ngày đăng ký gốc — tức mất luôn trục thời gian của
mọi báo cáo doanh thu.

## Ba điều phải nói trước với người dùng

1. **Doanh thu đổi định nghĩa.** LangCenter tính theo **cam kết** (`so_tien × ty_gia`), hệ cũ
   tính theo **tiền đã đóng**. Con số sẽ nhảy từ 7,80 tỷ lên **15,27 tỷ** — không phải lỗi.
   Tiền đã đóng vẫn xem được ở cột "đã thu". Doanh số quy cho **người tạo khách**
   (`KHACH_HANG.created_by_id`), không phải người tạo thanh toán.
2. **Username đổi đuôi** thành `@vietgeneducation.edu.vn` (hệ cũ dùng `@vietgenacademy.edu.vn`).
   Phần tên giữ nguyên. Phải nói rõ khi phát mật khẩu, nếu không ai cũng gõ đuôi cũ rồi nhận
   "sai mật khẩu" mà không hiểu vì sao.
3. **887 đơn hiện "Còn thiếu".** Đơn nào trung tâm không còn đòi thì người thu tự đánh dấu
   *"Đã nhận đủ tiền"* trên giao diện — hệ thống không tự đoán hộ.

Ngoài ra: sale thấy **toàn bộ** khách của trung tâm (LangCenter không có phạm vi dữ liệu CRM
theo người); chỉ tiêu doanh thu tháng, cấp bậc NS-1…NS-5 và giới tính **không chuyển** vì không
có chỗ chứa.

## Checklist chạy thật trên VPS

> Mọi lệnh chạy ở `/opt/langcenter` theo [trien-khai-pull-code.md](trien-khai-pull-code.md).
> **Nạp `.env` trước** — VPS dùng `POSTGRES_USER=langcenter_app`, khác dev (`langcenter`), và
> Postgres **không map port ra host** nên phải đi qua `docker compose exec`.

```bash
cd /opt/langcenter
set -a; . ./.env; set +a
echo "$POSTGRES_USER / $POSTGRES_DB"     # xác nhận đã nạp đúng
```

### 0. Mang dữ liệu lên và sinh SQL tại chỗ

`01-chuyen-doi.sql` và 3 file sinh kèm **bị `.gitignore` chặn** (chứa dữ liệu cá nhân), nên
`git pull` trên VPS **không** có chúng. Cách đã chốt: mang `du-lieu/` lên rồi chạy `sinh_sql.py`
tại chỗ.

Sinh tại chỗ thay vì `scp` file SQL có một cái lợi thật: nếu luật làm sạch đổi, chỉ cần sửa
`sinh_sql.py` và chạy lại — không có nguy cơ file SQL trên VPS là bản cũ mà không ai biết.

**Chỉ `du-lieu/` cần chép lên** — script đã có trong repo, `git pull` là có bản mới nhất.

```bash
# --- Trên MÁY LOCAL ---
cd /path/to/GiapTech.LangCenter/scripts/chuyen-doi-vietgen
scp -r du-lieu <user>@<vps>:/root/chuyen-doi-vietgen/

# --- Trên VPS ---
cd /opt/langcenter && git pull       # lấy script bản mới nhất
python3 --version                    # cần Python 3 (Ubuntu LTS có sẵn; script chỉ dùng
                                     # thư viện chuẩn, KHÔNG cần pip install)

mkdir -p /root/chuyen-doi-vietgen/chuyen-doi
python3 /opt/langcenter/scripts/chuyen-doi-vietgen/chuyen-doi/sinh_sql.py \
  /root/chuyen-doi-vietgen/du-lieu /root/chuyen-doi-vietgen/chuyen-doi

ls -l /root/chuyen-doi-vietgen/chuyen-doi/01-chuyen-doi.sql   # ~11 nghìn dòng
```

`sinh_sql.py` nhận `<thư mục JSON> <thư mục xuất>`. **Phải truyền cả hai**: không truyền thì nó
tìm `../du-lieu` cạnh chính nó — tức trong repo, nơi `.gitignore` đã chặn dữ liệu.

- [ ] Đối chiếu `chuyen-doi/ky-vong.json` vừa sinh với bảng ở [bước 6](#6-đối-soát) — phải khớp.
      Lệch nghĩa là dữ liệu nguồn khác bản đã kiểm thử, **dừng lại hỏi**.
- [ ] Đọc `chuyen-doi/bao-cao-lam-sach.csv`, nhất là các dòng **KIỂM TRA LẠI**.
- [ ] Xác nhận đang ở VPS production, không phải máy khác.

> **`du-lieu/` chứa họ tên · email · SĐT · số tài khoản ngân hàng thật.** Để ngoài thư mục
> `/opt/langcenter` (ví dụ `/root/chuyen-doi-vietgen`) cho khỏi lọt vào `git status`, và
> **xoá sau khi chuyển xong**:
> ```bash
> rm -rf /root/chuyen-doi-vietgen/du-lieu
> shred -u /root/chuyen-doi-vietgen/chuyen-doi/01-chuyen-doi.sql
> shred -u /root/chuyen-doi-vietgen/chuyen-doi/*.csv
> ```

### 1. Backup — bắt buộc, không bỏ qua

```bash
mkdir -p backup
docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \
  pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" > backup/truoc-chuyen-doi-$(date +%F-%H%M).sql
ls -lh backup/truoc-chuyen-doi-*.sql      # phải > 0 byte
```

Đây là **đường lui duy nhất**. Script chỉ `INSERT` vào một trung tâm mới, nhưng backup là thứ
cứu khi phát hiện sai ở bước đối soát.

### 2. Tạo trung tâm

Màn chủ hệ thống `/chu/trung-tam` (ADR-0009), tên **"VIETGEN Academy"**.

- [ ] Ghi lại **mã trung tâm** và **mật khẩu admin** — mật khẩu chỉ hiện **đúng một lần**.
- [ ] Đăng nhập admin một lần để đổi mật khẩu (hệ thống bắt buộc đổi lần đầu).
- [ ] **Không thêm dữ liệu gì khác.** Script tự huỷ nếu trung tâm không còn trắng.

### 3. Nạp dữ liệu

```bash
docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \
  psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 \
  -v ma_trung_tam=XXXXXXX < /root/chuyen-doi-vietgen/chuyen-doi/01-chuyen-doi.sql
```

- [ ] Kết thúc phải in `COMMIT` và `XONG.`
- [ ] Gặp `ERROR` → toàn bộ đã rollback, **không có gì bị ghi**. Đọc lỗi rồi hỏi trước khi thử lại.

### 4. Restart API

```bash
docker compose restart api
docker compose logs -f api | head -30     # chờ "Now listening on"
```

Bắt buộc: quyền được cache 5 phút, nạp bằng SQL không làm cache tự hết hạn. Bỏ bước này thì
người dùng đăng nhập được nhưng **mọi endpoint trả 403**.

### 5. Đặt mật khẩu tạm

```bash
MA_TRUNG_TAM=XXXXXXX ADMIN_PASS='<mật khẩu admin mới>' \
  API=https://<domain-quan-tri> \
  PG=langcenter-postgres-1 \
  OUT=/root/mat-khau-tam-XXXXXXX.csv \
  bash /opt/langcenter/scripts/chuyen-doi-vietgen/chuyen-doi/02-dat-mat-khau-tam.sh
```

Script tự lấy `POSTGRES_USER` / `POSTGRES_DB` từ `.env` đã nạp ở trên.

- [ ] Kết thúc phải in `Còn 0 tài khoản hoạt động chưa có mật khẩu.`
- [ ] 28 tài khoản bị khoá ở hệ cũ **cố ý không** được đặt mật khẩu. Mở lại tài khoản nào thì
      admin đặt mật khẩu cho tài khoản đó trên giao diện.
- [ ] File CSV **chứa mật khẩu thật** (quyền 600). Phát xong **xoá ngay**: `shred -u <file>`.

Script gọi API để băm, **không ghi hash tay** — cùng lý do với `scripts/dong-bo-mat-khau-dev.sh`:
ghi hash tay là tự cài một thuật toán băm thứ hai, lệch với `IPasswordHasher` là mọi nick hỏng
cùng lúc.

### 6. Đối soát

```bash
docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \
  psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ma_trung_tam=XXXXXXX \
  < /opt/langcenter/scripts/chuyen-doi-vietgen/chuyen-doi/03-doi-soat.sql
```

Phải khớp `ky-vong.json`:

| Chỉ số | Giá trị |
|---|---|
| Người dùng · Tài khoản | 173 · 173 (chưa kể admin) |
| Tài khoản hoạt động | 145 |
| Phòng ban · Khoá học · Khách hàng | 10 · 36 · 939 |
| Đơn · Lần thu | 1056 · 1115 |
| Doanh thu (cam kết) | 15.273.437.000 |
| Đã thu | 7.804.775.161 |

- [ ] Đăng nhập thử một tài khoản mỗi nhóm quyền.
- [ ] Mở màn Doanh thu và Thống kê CRM, lọc theo **đội nhóm** và theo **nhân viên**.
- [ ] Kiểm một tenant khác trên VPS **không đổi gì** (số khách, số đơn giữ nguyên).

### 7. Phát tài khoản

Gửi mỗi người: **mã trung tâm · username (`danh-sach-tai-khoan.csv`) · mật khẩu tạm**. Nhắc rõ
đuôi username đã đổi sang `@vietgeneducation.edu.vn`. Mọi người phải đổi mật khẩu ở lần đăng
nhập đầu.

- [ ] Xoá file mật khẩu tạm sau khi phát xong.

## Vì sao an toàn với các trung tâm đang chạy trên VPS

Đã kiểm trên chính file SQL, không phải tin vào mô tả:

- **Chỉ `INSERT`** vào trung tâm mới, cộng **đúng ba `UPDATE`** — và cả ba chỉ sửa hàng do chính
  script vừa chèn (gán `nguoi_quan_ly_id` và cột audit, những thứ cần `NGUOI_DUNG` có trước).
  Mỗi câu đều kết thúc bằng `AND t.tenant_id = (SELECT tenant_id FROM _ctx)`.
- **Không có `DELETE` nào.**
- **Không ghi vào `TENANT`**: bảng này chỉ được `SELECT` để lấy `tenant_id`. Đuôi tên đăng nhập
  của trung tâm là thiết lập admin tự khai trên giao diện, script không đụng.
- **Một giao dịch duy nhất**: lỗi ở bất kỳ đâu là `ROLLBACK` toàn bộ, không để lại dữ liệu nửa vời.
- **Năm chốt an toàn** chạy trước khi ghi dòng đầu tiên: đúng một trung tâm khớp mã · trung tâm
  đó chỉ có admin của seeder · chưa có khách hàng · chưa có khoá học · chưa có phòng ban · có đủ
  hai nhóm quyền seed.

## Quay lui

Trước khi đối soát xong, đường lui rẻ nhất là **xoá trung tâm vừa tạo** — mọi hàng đều mang
`tenant_id` của nó, và không hàng nào của trung tâm khác bị đụng.

Nếu đã phát mật khẩu và cần quay lui hẳn:

```bash
cd /opt/langcenter
set -a; . ./.env; set +a
docker compose stop api
docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \
  psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" < backup/truoc-chuyen-doi-<thời-điểm>.sql
docker compose start api
```

Bản dump này **ghi đè dữ liệu hiện có** — chỉ dùng khi đã chắc chắn, và chỉ với dump của chính
lần chạy này. Dừng API trước, như runbook quy định, để không ai ghi vào giữa lúc khôi phục.

## Chạy lại script có an toàn không

Có. `01-chuyen-doi.sql` kiểm 5 điều kiện trước khi ghi (đúng một trung tâm · trung tâm chỉ có
admin của seeder · chưa có khách · chưa có khoá học · chưa có phòng ban · đủ 2 nhóm quyền seed).
Chạy lần hai trên trung tâm đã nạp sẽ dừng với:

```
ERROR: Trung tâm đã có 174 người dùng (chỉ được có admin của seeder). Huỷ — quy tắc #1.
```

`02-dat-mat-khau-tam.sh` cũng chạy lại được: nó chỉ đụng tài khoản còn mang hash giữ chỗ.
