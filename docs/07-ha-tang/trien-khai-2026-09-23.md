# Checklist triển khai — đợt 23/09/2026

> Đợt này **khác thường ở hai điểm**: có migration, và **mọi người đang đăng nhập sẽ bị đá ra
> một lần**. Đọc hết trước khi bắt đầu.

Quy trình chung: [`trien-khai-pull-code.md`](trien-khai-pull-code.md). Tệp này chỉ ghi
những gì **riêng của đợt này**.

## Có gì trong đợt này

| Nhóm | Nội dung |
|---|---|
| **Bảo mật** | Vá 8/8 mục rà soát: đăng xuất thật · mật khẩu admin ngẫu nhiên · bịt kênh thời gian · khoá tạm sau 10 lần sai · header bảo mật · mật khẩu tối thiểu 12 ký tự · **refresh token vào cookie `httpOnly`** |
| **Nhật ký** | Ghi cả lần đăng nhập **thất bại** (trước đây mất trắng) |
| **Đa ngôn ngữ** | 5 thứ tiếng: vi · en · zh · ko · ja |
| **Giao diện** | Linh vật · thanh tiến trình · favicon + tiêu đề tab theo trung tâm |

## ⚠️ Hai điều PHẢI biết trước

### 1. Mọi người đang đăng nhập sẽ bị đá ra — một lần

ADR-0007 chuyển refresh token từ `localStorage` sang cookie `httpOnly`. Token cũ nằm ở chỗ bản
mới không đọc nữa, nên **không có đường lui êm**.

Đã cân nhắc bản trung gian đọc cả hai nguồn và **bỏ**: nó giữ nguyên lỗ hổng đang vá thêm một
thời gian, chỉ để người dùng khỏi đăng nhập lại một lần.

**Việc cần làm:** chọn **giờ thấp điểm** (tối muộn hoặc sáng sớm) và **báo trước cho trung
tâm** — nếu không, tổng đài sẽ nhận hàng loạt cuộc gọi "hệ thống bắt đăng nhập lại".

### 2. Chính sách mật khẩu siết từ 6 lên 12 ký tự

Người đang dùng **không bị ảnh hưởng** — mật khẩu cũ vẫn đăng nhập được. Chỉ khi **đổi mật
khẩu** hoặc **tạo tài khoản mới** mới phải đủ 12 ký tự.

Đáng báo trước cho người quản trị trung tâm để họ không bối rối khi tạo tài khoản mới.

## Trung tâm đầu tiên trên VPS mới

Tự đăng ký trung tâm **đóng mặc định**, nên VPS mới dựng có DB rỗng và **không có đường nào
vào hệ thống**. Từ 23/09/2026, hệ thống tự tạo trung tâm đầu tiên lúc khởi động — nhưng **chỉ
khi bạn tự đặt mật khẩu**:

```bash
# Thêm vào .env TRƯỚC khi khởi động lần đầu
TRUNG_TAM_DAU_TIEN_MAT_KHAU=<mật khẩu bạn chọn, ≥12 ký tự>
TRUNG_TAM_DAU_TIEN_TEN=Trung tâm Ngoại ngữ ABC      # tuỳ chọn
```

Khởi động xong, đọc log lấy **mã trung tâm** (mã không phải bí mật):

```bash
docker compose logs api | grep "trung tâm đầu tiên"
```

Rồi đăng nhập `admin` / mật khẩu vừa đặt → hệ thống **bắt đổi mật khẩu ngay**.

**Sau đó GỠ `TRUNG_TAM_DAU_TIEN_MAT_KHAU` khỏi `.env`** — giữ lại là để mật khẩu nằm trong
tệp cấu hình mà không còn tác dụng gì (seed chỉ chạy khi DB chưa có trung tâm nào).

### Vì sao hệ thống KHÔNG tự sinh mật khẩu ở đây

Endpoint đăng ký sinh mật khẩu ngẫu nhiên rồi **trả trong response** — an toàn vì chỉ người
gọi thấy. Seed thì chạy lúc khởi động, trong container, **không có ai để trả về**. Đường duy
nhất là ghi log — mà ai đọc được log server cũng thấy, và log thường gom về nơi lưu trữ tập
trung.

Nên mật khẩu do bạn đặt. Không đặt ⇒ **không tạo gì cả**, và log nói rõ vì sao.

## Migration

Đợt này có **một** migration: `LyDoThuHoiRefreshToken` — thêm cột `ly_do` (nullable) vào
`REFRESH_TOKEN`.

**Chạy tự động lúc API khởi động** (`Program.cs` gọi `MigrateAsync`), không cần lệnh riêng.

Đã kiểm trên DB dev: chỉ `ADD COLUMN ... NULL`, không đổi kiểu, không xoá gì; đếm 1329 hàng
trước/sau đều khớp.

## Các bước

```bash
# 1. SAO LƯU DB — luôn làm trước, kể cả khi migration trông vô hại
docker compose exec -T db pg_dump -U langcenter langcenter \
  > ~/backup-langcenter-$(date +%F-%H%M).sql
ls -lh ~/backup-langcenter-*.sql | tail -1     # xác nhận tệp có dung lượng thật

# 2. Lấy code mới
cd /srv/langcenter
git checkout main
git pull
git log --oneline -1                            # XEM mình đang deploy commit nào

# 3. Build + khởi động lại (migration tự chạy)
./scripts/trien-khai.sh
```

## Kiểm sau khi triển khai

> **Ba luồng dưới đây PHẢI thử tay, không tin test.** Phiên 23/09/2026 có ba lỗi chỉ lộ khi
> chạy trên PostgreSQL thật trong khi **673 test in-memory đều xanh**: khoá ngoại
> `created_by_id`, Cascade của EF không giải được RESTRICT, và tham số không thay được vào
> khối `DO $$`. Provider in-memory không ép khoá ngoại — chỉ `curl` thật mới bắt được.
>
> 1. Tạo trung tâm từ site chủ (`POST /chu-he-thong/trung-tam`)
> 2. **Đăng nhập bằng đúng mật khẩu nó trả về** — đây là bước bắt được lệch giữa nơi băm và
>    nơi báo lại, đúng lỗi từng khiến mọi trung tâm mới có `admin`/`123456`
> 3. Gắn domain rồi truy cập thử bằng domain đó

```bash
# a. API sống
curl -s -o /dev/null -w '%{http_code}\n' https://<domain>/health          # 200

# b. Tự đăng ký PHẢI đóng ở production
curl -s -o /dev/null -w '%{http_code}\n' -X POST \
  https://<domain>/api/v1/dang-ky-trung-tam \
  -H 'Content-Type: application/json' -d '{"tenTrungTam":"thu"}'          # 404

# c. Header bảo mật có mặt
curl -sI https://<domain>/api/v1/tinh-nang | grep -iE 'x-frame|nosniff|strict-transport'

# d. Site chủ: đăng nhập → tạo trung tâm → đăng nhập bằng mật khẩu vừa nhận
TOKEN=$(curl -s -X POST https://<domain-site-chu>/api/v1/chu-he-thong/dang-nhap \
  -H 'Content-Type: application/json' \
  -d '{"username":"chu","matKhau":"<mật khẩu đã đặt>"}' | jq -r .accessToken)

curl -s -X POST https://<domain-site-chu>/api/v1/chu-he-thong/trung-tam \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"tenTrungTam":"Kiểm thử triển khai"}'
# → { maTrungTam, username, matKhauAdmin } — CHÉP LẠI, chỉ trả một lần

curl -s -X POST https://<domain>/api/v1/auth/dang-nhap \
  -H 'Content-Type: application/json' \
  -d '{"maTrungTam":"<mã vừa nhận>","username":"admin","matKhau":"<mật khẩu vừa nhận>"}'
# → 200 kèm phaiDoiMatKhau: true. KHÔNG 200 nghĩa là nơi băm và nơi báo lại đã lệch.

# e. Token chủ KHÔNG gọi được API nghiệp vụ (hàng rào chính của ADR-0009)
curl -s -o /dev/null -w '%{http_code}\n' https://<domain>/api/v1/lop-hoc \
  -H "Authorization: Bearer $TOKEN"                                       # 401

# f. Dọn tenant E2E PHẢI tắt trên production
curl -s -o /dev/null -w '%{http_code}\n' -X POST \
  https://<domain-site-chu>/api/v1/chu-he-thong/don-tenant-e2e \
  -H "Authorization: Bearer $TOKEN"                                       # 404

# g. Cột mới đã có
docker compose exec -T db psql -U langcenter -d langcenter \
  -c '\d "REFRESH_TOKEN"' | grep ly_do                                     # có dòng ly_do

# e. Cookie phiên đúng thuộc tính — quan trọng nhất của đợt này
curl -sI -X POST https://<domain>/api/v1/auth/dang-nhap \
  -H 'Content-Type: application/json' \
  -d '{"maTrungTam":"<MÃ>","username":"<user>","matKhau":"<mk>"}' \
  | grep -i set-cookie
# PHẢI thấy: lms_rt=...; ...; secure; httponly
#   - thiếu `secure`  ⇒ token đi qua HTTP thuần
#   - thiếu `httponly` ⇒ JavaScript đọc được, tức ADR-0007 vô nghĩa
```

Và **mở trình duyệt thật** kiểm ba việc không script nào thay được:

- [ ] Đăng nhập → F5 → **vẫn ở trong hệ thống** (không văng về màn đăng nhập)
- [ ] Đổi mật khẩu → F5 → **vẫn ở trong hệ thống**
- [ ] Đổi ngôn ngữ sang tiếng Anh → F5 → **vẫn tiếng Anh**

Ba ca này đúng là ba lỗi đã gặp khi làm ADR-0007; script không bắt được vì chúng chỉ hỏng
trong trình duyệt thật.

## Nếu hỏng thì quay lại thế nào

```bash
cd /srv/langcenter
git log --oneline -5              # tìm commit trước đợt này
git checkout <sha-cũ>
./scripts/trien-khai.sh
```

⚠️ **Cột `ly_do` vẫn còn** sau khi quay lại code cũ — và **không sao**: nó nullable, code cũ
không đọc tới. **Đừng chạy migration ngược** để xoá nó; xoá cột là thao tác mất dữ liệu, mà
lợi ích bằng không.

Nếu phải khôi phục DB từ bản sao lưu:

```bash
docker compose stop api
docker compose exec -T db psql -U langcenter -d langcenter < ~/backup-langcenter-<...>.sql
docker compose start api
```
