# Kế hoạch & tiến độ

> Cập nhật cuối: **2026-08-18**. Nhật ký chi tiết theo ngày: [nhat-ky/](./nhat-ky/README.md).

## Tiến độ tổng

```
Nghiệp vụ  ████████████████  18/18 FR chạy đầu-cuối
Hạ tầng    ████████████░░░░  CI/CD sẵn sàng, chờ VPS thật
Còn lại    ██████░░░░░░░░░░  4 việc cần tài khoản/hạ tầng ngoài + 3 nợ kỹ thuật
```

## Trạng thái 20 mã FR

| Mã | Chức năng | Backend | Frontend | Ghi chú |
|---|---|:---:|:---:|---|
| FR-01 | Đăng nhập | ✅ | ✅ | Kèm đổi mật khẩu, refresh token xoay vòng |
| FR-02 | Quên mật khẩu | ✅ | ✅ | Chưa cấu hình SMTP thật — email ghi log |
| FR-03 | Tài khoản người dùng | ✅ | ✅ | CRUD đủ, kể cả vô hiệu hóa |
| FR-04 | Hồ sơ cầu thủ | ✅ | ✅ | Kèm số áo, vị trí sở trường, ảnh đại diện (MinIO) |
| FR-05 | Phân quyền truy cập | ✅ | ✅ | Ma trận chức năng × thao tác |
| FR-06 | Thiết lập chung | ✅ | ✅ | Kèm bộ áo đấu, logo và ảnh bìa (MinIO) |
| FR-07 | Lọc thông tin (trận đấu) | ✅ | ✅ | `BoLocTranDau` dùng chung với FR-12 |
| FR-08 | Danh sách trận đấu | ✅ | ✅ | Datatable (phân trang) + Calendar (Calendar.js), chuyển đổi qua lại |
| FR-09 | Chấp nhận lời mời đối thủ | ✅ | ✅ | Chấp nhận → tự sinh trận, điều hướng sang chi tiết |
| FR-10 | Thêm/Cập nhật trận đấu | ✅ | ✅ | 4 tab; bảng chiến thuật kéo-thả, mẫu đội hình, nhiều video |
| FR-11 | Xóa trận đấu | ✅ | ✅ | Chỉ xóa cứng trận chưa diễn ra; có Lưu trữ thay thế |
| FR-12 | Lọc thông tin (thống kê) | ✅ | ✅ | Component `BoLocTranDau` dùng chung với FR-07 |
| FR-13 | Biểu đồ diễn biến | ✅ | ✅ | Recharts, bấm điểm → chi tiết trận |
| FR-14 | Bảng xếp hạng MVP | ✅ | ✅ | 4 tiêu chí, đổi cột không gọi lại API |
| FR-15 | Danh sách quỹ | ✅ | ✅ | Tiến độ thu, màu theo trạng thái |
| FR-16 | Thêm/Cập nhật quỹ | ✅ | ✅ | Kèm thông tin chuyển khoản (QR + số TK) theo từng đợt. Nhắc nợ = sao chép danh sách; SMS/Email chưa làm |
| FR-17 | Cộng đồng (mới 18/08) | ✅ | ✅ | Danh sách CLB, chi tiết công khai, lời mời thách đấu |
| FR-18 | Lời mời qua link/QR (mới 20/08) | ✅ | ✅ | 13 trường hợp; nâng cấp đối thủ tên gõ tay thành CLB có ID |
| FR-20 | Màn Tổng quan (mới 21/08) | ✅ | ✅ | Việc cần làm + trận sắp tới, phân vai theo trưởng nhóm/cầu thủ |
| FR-19 | Đăng ký đá trận qua link/QR (mới 21/08) | ✅ | ✅ | Không cần đăng nhập. Tab Đăng ký trong chi tiết trận, chọn/bỏ ai được mời, 4 mốc hạn link. **Không** làm cho vote MVP — xem [tài liệu](./nghiep-vu/dang-ky-nhanh-qua-link.md) |

✅ xong · 🟡 dùng được nhưng thiếu phần · ⬜ chưa làm

## Hạng mục nền tảng

| Hạng mục | Trạng thái |
|---|---|
| Solution 4 lớp + luật phụ thuộc có test canh | ✅ |
| 17 bảng, migration chạy trên PostgreSQL thật | ✅ |
| Cách ly tenant (2 tầng phòng vệ tự động + test) | ✅ |
| Phân quyền động đọc từ DB + cache | ✅ |
| Frontend: layout, i18n, design token, auto-refresh token | ✅ |
| 399 test (57 unit + 342 integration) | ✅ |
| Upload ảnh qua MinIO (avatar, logo, ảnh bìa) | ✅ |
| **Dockerfile cho API** | ✅ 2 giai đoạn, chạy user thường |
| **`docker compose up` chạy được** | ✅ 5 container, API healthy, migration tự áp |
| Test E2E frontend (41 test Playwright) | ✅ |
| CI/CD 4 job: test → E2E → đẩy image → deploy | ✅ |
| Triển khai VPS (domain, HTTPS, backup) | 🟡 cần server thật |

---

## Lộ trình

### Chen trước — nợ mức Cao

| # | Việc | Ước tính | Vì sao gấp |
|---|---|---|---|
| ~~N1~~ | ~~Dockerfile cho API~~ | — | ✅ Xong 16/08 |
| ~~N2~~ | ~~Test E2E frontend~~ | — | ✅ Xong 17/08 — nay 35 test Playwright |
| ~~N3~~ | ~~Rate limit cho endpoint ẩn danh~~ | — | ✅ **Xong 21/08.** Làm ở **tầng API** chứ không ở Caddy như kế hoạch ban đầu: `caddy:2-alpine` không có module rate limit (phải tự build bằng `xcaddy`), và Caddy **không đọc được body** nên không phân biệt "một người dò 500 token" với "500 người mở link của mình". .NET 8 có `RateLimiting` sẵn. Cửa sổ trượt theo IP: tra cứu 30/phút · token 10/phút · xác thực 10/phút; trả 429 `QUA_NHIEU_YEU_CAU` kèm `Retry-After`. 7 phản chứng, 2 lọt lần đầu (nới hạn mức mà test chỉ so tên policy; đổi mặc định cờ thành tắt) — đã siết. Không chống DDoS phân tán, việc đó cần Cloudflare |
| ~~N4~~ | ~~Không có đường tạo CLB ở production~~ | — | `/dang-ky-clb` chỉ bật ở Development (mở ẩn danh ở production là cho phép sinh CLB rác không giới hạn). ✅ **Xong 20/08** — mở tự do, vì luồng lời mời qua link (FR-18) cần nó |
| ~~N5~~ | ~~E2E để lại tenant rác trong DB dev~~ | — | ✅ **Xong 20/08.** Mỗi test tự tạo CLB riêng nên mỗi lần chạy để lại ~44 CLB, và từ 18/08 chúng hiện lên trang Cộng đồng của mọi người — đo thật: **242 rác / 249 tổng**, toàn bộ trang đầu là rác. Dọn tay một lần rồi tích lại ngay sau lần chạy kế tiếp, nên phải tự động: `globalTeardown` gọi `POST /du-lieu-mau/don-tenant-test`. Kiểm chứng 2 lần chạy liên tiếp: xoá 290 rồi 48 CLB, mỗi lần về đúng 7 CLB mẫu |
| ~~N9~~ | ~~Tự xoá hết quyền của chính mình được~~ | — | ✅ **Xong 21/08.** Chốt ở mức **CLB** chứ không mức cá nhân (quyết định chủ sản phẩm): phải luôn còn ít nhất một người **đang hoạt động** có quyền `PhanQuyen`. Admin A vẫn tự bỏ quyền được nếu admin B còn giữ. Gắn ở **ba** đường: cập nhật tài khoản (gỡ quyền / vô hiệu hoá), xoá tài khoản, và cho nghỉ kèm khoá tài khoản. `PhanQuyen` là chức năng chốt vì nó là cửa duy nhất cấp lại mọi quyền khác |
| N8 | **Test flake không tái hiện** | ~1h | **E2E ~1/50 mỗi lần chạy** (quan sát 20/08 qua 4 lần: 43/44 · 43/44 · 43/44 · 44/44, đỏ một test KHÁC mỗi lần, chạy riêng thì luôn xanh). **Backend cũng gặp 21/08**: 1/342 đỏ một lần rồi xanh 3 lần liên tiếp sau đó — không kịp bắt tên test. Khác test mỗi lần ⇒ hạ tầng, không phải lỗi tính năng; nghi đua giữa `invalidateQueries` và `waitForTimeout` ở E2E, và thứ tự chạy song song ở backend. Đừng dùng "chạy lại thấy xanh" làm kết luận |
| N10 | **Chưa deploy lên VPS** | ~1h | Kế hoạch + script đã xong 22/08: [`trien-khai-pull-code.md`](./ha-tang/trien-khai-pull-code.md) và `scripts/trien-khai.sh` (đã chạy thật trên cụm local, 3/3 endpoint trả 200). Còn lại là việc cần **chủ sản phẩm**: mua/cấu hình VPS ≥2GB, trỏ DNS, đặt `.env` trên VPS. Không cần secret nào trong GitHub vì luồng này VPS tự build |
| ~~N7~~ | ~~Thiếu `EnableRetryOnFailure` cho Npgsql~~ | — | ✅ **Xong 20/08.** Kiểm chứng bằng phản chứng: restart postgres rồi gọi ngay — bản **không** retry trả `#1 -> 500`, bản **có** retry trả `#1 -> 200`. Đã rà `src/`: không chỗ nào tự mở `BeginTransaction` nên retry an toàn |
| ~~N6~~ | ~~Màn Tổng quan trống~~ | — | ✅ **Xong 21/08.** Nội dung do chủ sản phẩm chọn: **việc cần làm + trận sắp tới**, mỗi dòng bấm được để tới đúng chỗ xử lý. Không làm dải 4 số thống kê — đã có ở màn Thống kê/Tài chính. Phân vai: trưởng nhóm thấy việc của đội, cầu thủ thường chỉ thấy việc của mình. **Không** bắt `[RequirePermission]` vì đây là màn đầu tiên sau đăng nhập |

### Giai đoạn 1 — Lịch thi đấu (FR-07 → FR-11)

Module lớn nhất, tách 3 đợt để mỗi đợt đều có thứ dùng được.

**~~Đợt 1a — Nền tảng~~** ✅ xong 16/08
- ~~CRUD đối thủ · FR-07 bộ lọc · FR-08 Datatable · FR-11 xóa/lưu trữ~~

**~~Đợt 1b — Trận đấu đầy đủ~~** ✅ xong 16/08 (trừ Calendar)
- ~~FR-10a đội hình · FR-10c đánh giá + vote MVP · FR-09 lời mời~~
- ~~FR-08 chế độ Calendar~~ ✅ dùng Calendar.js

**~~Đợt 1c — Sơ đồ chiến thuật kéo-thả~~** ✅ xong 17/08
- ~~Bảng chiến thuật kéo-thả 4 loại sân, 2 hiệp, cả hai đội, mẫu đội hình dùng lại~~

### Giai đoạn 2 — Thống kê (FR-12 → FR-14) · ✅ xong

KPI + biểu đồ Recharts + bảng xếp hạng 4 tiêu chí, tất cả chịu ảnh hưởng của bộ lọc chung.

⚠️ Nơi **dễ quên `tenant_id` nhất** — đã tránh bằng cách không dùng raw SQL, mọi truy vấn đi
qua DbSet để Global Query Filter tự lọc. Canh bởi `ThongKe_cach_ly_theo_tenant`, kiểm chứng
bằng phản chứng (thêm `IgnoreQueryFilters()` thì test đỏ).

### Giai đoạn 3 — Tài chính (FR-15, FR-16) · ~1 phiên

**Đã xong:** CRUD đợt quỹ, thu tiền từng phần, khoản chi + số dư quỹ, sao chép danh sách nợ.

**Còn lại:** gửi nhắc nợ tự động qua SMS/Email — cần `ISmsSender` và tài khoản SendGrid/eSMS.
Bắt buộc security review (dữ liệu tài chính).

### Giai đoạn 4 — Triển khai

Dockerfile → thử `docker compose up` → VPS → domain + HTTPS → backup.

---

## Nợ kỹ thuật

| Việc | Mức |
|---|---|
| `/dang-ky-clb` chưa an toàn production (đang chặn bằng `IsDevelopment()`) | Cao |
| Upload ảnh MinIO — avatar, logo, ảnh bìa | Trung bình |
| ADR-0005: không dùng Identity đầy đủ (đang lệch `CLAUDE.md` mục 4) | Trung bình |
| Đăng xuất chưa gọi API thu hồi refresh token | Thấp |
| Trang style-guide · pre-commit hook · code-split · `.resx` backend | Thấp |


---

## Việc quản trị

- **PR `feature/khoi-tao-solution` → `main`** đang treo 8 commit
- `CHANGELOG.md` chưa cập nhật từ commit `6ba49ed` trở đi
