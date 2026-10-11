# Triển khai 11/10/2026 — ghi chú đợt phát hành

> Đọc cùng [trien-khai-pull-code.md](trien-khai-pull-code.md) (quy trình chung, không đổi).
> File này chỉ nói **đợt này có gì khác** so với bản đang chạy trên VPS.

## Tóm tắt

| | |
|---|---|
| Commit cuối | `cd96a19` |
| Số commit | 13 |
| Migration mới | **1** — `ThemNgonNgu` |
| Biến môi trường mới | **1** — `APP_BASE_URL` (xem dưới) |
| Test backend | 776 / 778 xanh |

## ⚠️ Việc PHẢI làm trước khi deploy

### Thêm `APP_BASE_URL` vào `.env`

```bash
# /opt/langcenter/.env
APP_BASE_URL=https://<domain-that>
```

**Không có dấu `/` ở cuối.**

Thiếu biến này thì **thư chào mừng tài khoản có dòng link rỗng** — người nhận cầm tên đăng
nhập và mật khẩu tạm mà không biết gõ địa chỉ nào. Không có lỗi nào báo, nên phải đặt trước.

`EMAIL_KHOA_MA_HOA` thì **giữ nguyên giá trị đang có** — đổi là mọi mật khẩu SMTP đã lưu
thành không giải mã được.

## Migration `ThemNgonNgu`

Thuần bổ sung, **không đụng dữ liệu hiện có**:

- Thêm bảng `NGON_NGU`
- Thêm cột `ngon_ngu_id` (**nullable**) vào `KHOA_HOC` và `SAN_PHAM`

Mọi khoá học và sản phẩm đang có giữ nguyên, ở trạng thái "chưa gắn ngôn ngữ". Lọc "Tất cả"
vẫn thấy chúng.

API tự chạy migration lúc khởi động, và `scripts/trien-khai.sh` đã `pg_dump` trước đó — không
cần làm gì thêm.

## Có gì mới

### Danh mục ngôn ngữ (CRM → Ngôn ngữ)

Khai ngôn ngữ giảng dạy, gắn vào khoá học / sản phẩm, rồi **lọc khách hàng và doanh thu theo
ngôn ngữ**.

**Nhóm Quản trị viên tự có quyền `NgonNgu`** — `BoKhuyetQuyenQuanTri` chạy lúc khởi động và
bổ khuyết mọi chức năng mới cho admin của các trung tâm đã tồn tại. Không phải làm gì thêm.

Các nhóm khác (kinh doanh, giáo viên…) **không tự có** — đó là chủ ý, vì trung tâm tự quyết ai
được sửa danh mục nền. Muốn cấp thì vào **Quản trị → Phân quyền**, chọn nhóm, tick ô `Ngôn ngữ`.

> Người đang đăng nhập phải **đăng xuất rồi vào lại** mới thấy menu mới — quyền có cache theo
> phiên.

Dữ liệu cũ chưa gắn ngôn ngữ; gắn dần khi tiện.

### Doanh thu

- Cột **Đã thu** dưới số cam kết, và ô tổng **Đã thu** cạnh tổng doanh thu
- Cột **Phụ trách** (tên nhân viên + đội nhóm) ở cả Doanh thu và Khách hàng
- Ghi đơn ở màn Doanh thu nay có ô **"Đã nhận đủ tiền"** — tích thì hệ thống ghi luôn lần thu
  gốc. Trước đây đơn tạo ở màn này không bao giờ có lần thu nào.

### Giao diện

- **Vòng xoay** trên nút khi đang chạy, **thông báo** góc dưới phải khi xong
- Bảng rộng **cuộn ngang**, khoá cột đầu và cột thao tác
- Modal chuẩn **header · body · footer**, header/footer cố định
- Bộ lọc gom vào modal **Lọc thêm**

### Sửa lỗi đáng kể

- Xoá doanh thu nổ **500** khi đơn đã có lần thu → nay báo lỗi đọc được
- Modal sửa **dùng lại dữ liệu của lần mở trước** → nguy hiểm vì bấm Lưu là ghi đè nhầm

## Kiểm sau khi deploy

```bash
# 1. Migration đã chạy
docker compose exec -T postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  -c "SELECT migration_id FROM \"__EFMigrationsHistory\" ORDER BY migration_id DESC LIMIT 3;"
#    phải thấy 20261011120311_ThemNgonNgu

# 2. Dữ liệu còn nguyên — so với số trước khi deploy
docker compose exec -T postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  -c "SELECT (SELECT COUNT(*) FROM \"KHOA_HOC\") AS khoa,
             (SELECT COUNT(*) FROM \"KHACH_HANG\") AS khach,
             (SELECT COUNT(*) FROM \"DANG_KY_KHOA_HOC\") AS don;"

# 3. Endpoint mới sống
curl -s -o /dev/null -w '%{http_code}\n' https://<domain>/api/v1/ngon-ngu
#    401 là ĐÚNG (chưa đăng nhập); 404 nghĩa là code chưa lên
```

Trên giao diện:

1. **CRM → Ngôn ngữ** hiện ra (sau khi cấp quyền `NgonNgu`)
2. Mở một form bất kỳ, cuộn xuống — **tiêu đề và hàng nút đứng yên**
3. Lưu một thứ gì đó — có **thông báo "Đã lưu"** góc dưới phải

## Nếu phải quay lại

```bash
cd /opt/langcenter
git checkout 402e7f3      # commit trước đợt này
./scripts/trien-khai.sh
```

Migration **không tự lùi**. Nhưng cột `ngon_ngu_id` là nullable và bảng `NGON_NGU` không ai
đọc ở bản cũ, nên để nguyên cũng không sao — bản cũ chạy bình thường.
