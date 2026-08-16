# Kế hoạch & tiến độ

> Cập nhật cuối: **2026-08-16**. Nhật ký chi tiết theo ngày: [nhat-ky/](./nhat-ky/README.md).

## Tiến độ tổng

```
Nghiệp vụ  ███████████░░░░░  11/16 FR (FR-10 thiếu bản vẽ sơ đồ kéo-thả)
Hạ tầng    ████████░░░░░░░░  chạy được ở máy dev, chưa triển khai
```

## Trạng thái 16 mã FR

| Mã | Chức năng | Backend | Frontend | Ghi chú |
|---|---|:---:|:---:|---|
| FR-01 | Đăng nhập | ✅ | ✅ | Kèm đổi mật khẩu, refresh token xoay vòng |
| FR-02 | Quên mật khẩu | ✅ | ✅ | Chưa cấu hình SMTP thật — email ghi log |
| FR-03 | Tài khoản người dùng | ✅ | ✅ | CRUD đủ, kể cả vô hiệu hóa |
| FR-04 | Hồ sơ cầu thủ | ✅ | 🟡 | Chưa upload được ảnh đại diện |
| FR-05 | Phân quyền truy cập | ✅ | ✅ | Ma trận chức năng × thao tác |
| FR-06 | Thiết lập chung | ✅ | 🟡 | Chưa upload được logo / ảnh bìa |
| FR-07 | Lọc thông tin (trận đấu) | ✅ | ✅ | `BoLocTranDau` dùng chung với FR-12 |
| FR-08 | Danh sách trận đấu | ✅ | ✅ | Datatable (phân trang) + Calendar (Calendar.js), chuyển đổi qua lại |
| FR-09 | Chấp nhận lời mời đối thủ | ✅ | ✅ | Chấp nhận → tự sinh trận, điều hướng sang chi tiết |
| FR-10 | Thêm/Cập nhật trận đấu | ✅ | 🟡 | 3 tab đủ; sơ đồ chưa có bản vẽ kéo-thả |
| FR-11 | Xóa trận đấu | ✅ | ✅ | Chỉ xóa cứng trận chưa diễn ra; có Lưu trữ thay thế |
| FR-12 | Lọc thông tin (thống kê) | ⬜ | ⬜ | Tái dùng FR-07 |
| FR-13 | Biểu đồ diễn biến | ⬜ | ⬜ | |
| FR-14 | Bảng xếp hạng MVP | ⬜ | ⬜ | 4 tiêu chí |
| FR-15 | Danh sách quỹ | ⬜ | ⬜ | |
| FR-16 | Thêm/Cập nhật quỹ | ⬜ | ⬜ | Cần cài đặt `ISmsSender` |

✅ xong · 🟡 dùng được nhưng thiếu phần · ⬜ chưa làm

## Hạng mục nền tảng

| Hạng mục | Trạng thái |
|---|---|
| Solution 4 lớp + luật phụ thuộc có test canh | ✅ |
| 17 bảng, migration chạy trên PostgreSQL thật | ✅ |
| Cách ly tenant (2 tầng phòng vệ tự động + test) | ✅ |
| Phân quyền động đọc từ DB + cache | ✅ |
| Frontend: layout, i18n, design token, auto-refresh token | ✅ |
| 109 test (34 unit + 75 integration) | ✅ |
| **Dockerfile cho API** | ✅ 2 giai đoạn, chạy user thường |
| **`docker compose up` chạy được** | ✅ 5 container, API healthy, migration tự áp |
| Test E2E frontend | ⬜ |
| Triển khai VPS (domain, HTTPS, backup) | ⬜ |

---

## Lộ trình

### Chen trước — nợ mức Cao

| # | Việc | Ước tính | Vì sao gấp |
|---|---|---|---|
| ~~N1~~ | ~~Dockerfile cho API~~ | — | ✅ Xong 16/08 |
| N2 | Test E2E frontend | ~0.5 phiên | 8 màn không có gì canh; lỗi enum ngày 16/08 lọt qua cả 44 test backend |

### Giai đoạn 1 — Lịch thi đấu (FR-07 → FR-11)

Module lớn nhất, tách 3 đợt để mỗi đợt đều có thứ dùng được.

**~~Đợt 1a — Nền tảng~~** ✅ xong 16/08
- ~~CRUD đối thủ · FR-07 bộ lọc · FR-08 Datatable · FR-11 xóa/lưu trữ~~

**~~Đợt 1b — Trận đấu đầy đủ~~** ✅ xong 16/08 (trừ Calendar)
- ~~FR-10a đội hình · FR-10c đánh giá + vote MVP · FR-09 lời mời~~
- ~~FR-08 chế độ Calendar~~ ✅ dùng Calendar.js

**Đợt 1c — Sơ đồ chiến thuật kéo-thả** (~1 phiên, còn lại)
- Backend FR-10b đã xong (lưu `jsonb` + validate JSON); UI hiện là ô nhập JSON thô.
- Cần: bản vẽ kéo-thả trên nền sân bóng. Nặng nhất cả dự án, độc lập.

### Giai đoạn 2 — Thống kê (FR-12 → FR-14) · ~1 phiên

Nhẹ nếu đợt 1a xong. ⚠️ Nơi **dễ quên `tenant_id` nhất** — aggregate query hay viết raw SQL.

### Giai đoạn 3 — Tài chính (FR-15, FR-16) · ~1 phiên

Cần cài đặt `ISmsSender`. Bắt buộc security review (dữ liệu tài chính).

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
