# CLAUDE.md — Quy tắc làm việc & bản đồ tài liệu

> **Đọc file này trước khi viết bất kỳ dòng code nào.**
>
> File này chứa **quy tắc bắt buộc** + **bản đồ điều hướng**. Nội dung chi tiết đã tách vào `docs/` —
> khi mâu thuẫn, **tài liệu trong `docs/` thắng** (file này chỉ dẫn đường, không phải nguồn chân lý về
> chi tiết). Cập nhật ngay khi có thay đổi.

---

## 1. Dự án này là gì

**Hệ thống quản lý trung tâm ngoại ngữ**, mô hình **multi-tenant**, gồm **ba hệ thống con**
chia theo nhóm quyền — **HRM** (nhân sự) · **CRM** (khách hàng) · **LMS** (đào tạo). Đây là cách
nhóm chức năng phân quyền để lọc sidebar, **không phải ba ứng dụng**: một API, một database,
một lần đăng nhập. → [phan-quyen-dong.md](./docs/backend/phan-quyen-dong.md#ba-hệ-thống-con-hrm--crm--lms)

Mỗi trung tâm đăng ký là một tenant độc lập, dữ liệu cách ly hoàn toàn theo `tenant_id`.
Đăng nhập bằng bộ ba **{mã trung tâm, tên đăng nhập, mật khẩu}**.

**Tên chuẩn:** `GiapTech.LangCenter` (namespace, solution, image `ghcr.io/giaptech/langcenter-api`).

### Trạng thái

Tách ra từ một ứng dụng quản lý CLB đá bóng (04/09/2026), giữ toàn bộ tầng hệ thống. Nghiệp vụ
LMS dựng từ 05/09/2026 theo đặc tả Vietgenedu.

**20/24 mã FR chạy đầu-cuối** trên PostgreSQL + MinIO thật. 387 test backend xanh.

| Đã chạy đầu-cuối | Chưa có |
|---|---|
| Đăng nhập · quên mật khẩu · buộc đổi mật khẩu lần đầu | **FR-15 Thống kê / Dashboard** |
| Người dùng (hồ sơ 3 vai trò) tách khỏi tài khoản · nhóm quyền · thiết lập | **Bài kiểm tra** — có schema, chưa có API/UI |
| **Ba hệ thống con** HRM/CRM/LMS: bộ chuyển, sidebar lọc, tab phân quyền | **Nghiệp vụ HRM** — mới có khung trống |
| **CRM** (FR-17 → FR-20): khách hàng + view 3 tab · doanh thu đa tiền tệ · **khoá học + sản phẩm** | Số **đã thu** ở CRM chưa chảy sang sổ học phí LMS |
| **FR-21 xếp lớp**: bán khoá → gửi yêu cầu → duyệt vào lớp (2 cách), học phí lấy từ đơn CRM | `LOP_HOC` chưa nối `KHOA_HOC` (chưa ưu tiên lớp cùng khoá) |
| Hồ sơ con người tách theo hệ thống: Nhân sự (HRM) · Học viên (LMS) | |
| Lớp học: vòng đời, phân công, ghi danh, học phí riêng từng người | Đăng ký trung tâm an toàn production (nợ N3) |
| Buổi học: sinh lịch tự động; điểm danh hai nguồn | Nhắc nợ / thông báo qua email |
| Bài tập, bài nộp nhiều lần, tài liệu, tệp đính kèm | Import Excel học viên |
| Học phí: sổ thu + công nợ tính động | Danh mục ngày nghỉ khi sinh lịch |
| **Nhật ký thao tác** mọi module (FR-16) | Dọn nhật ký cũ theo chính sách lưu giữ |

Chi tiết và nợ kỹ thuật: [`docs/ke-hoach.md`](./docs/ke-hoach.md).

---

## 2. Mười một quy tắc bất di bất dịch

1. **Cập nhật hệ thống KHÔNG được ảnh hưởng dữ liệu hiện có.** Nếu một thay đổi bắt buộc phải
   động tới dữ liệu đang có → **dừng lại hỏi người dùng trước khi làm**.
   - Lệnh cập nhật ghi đè trường nào thì trường đó **phải** có trong DTO trả về **và** trong form.
     Không gửi giá trị cứng (`null`, `''`) cho trường không hiển thị trên UI.
   - Test phải kiểm "sửa một trường không làm mất trường khác", không chỉ kiểm trường vừa đổi.
   - Migration làm hẹp cột / đổi kiểu / xóa cột, reset DB, `docker compose down -v`, xóa hàng loạt
     → **hỏi trước**, kèm phương án an toàn hơn.
   - Thêm claim vào JWT là **thay đổi phá vỡ tương thích** với token đang lưu hành — phải có đường
     lui cho phiên đang mở.

   → Đã xảy ra 16/08/2026: form sửa tài khoản thiếu ô địa chỉ nên âm thầm xóa địa chỉ mỗi lần lưu.
   Canh bởi `CapNhatKhongMatDuLieuTests`.

2. **Mọi bảng nghiệp vụ có `tenant_id` + EF Core Global Query Filter** — không được quên ở entity mới.
   Rò rỉ dữ liệu chéo trung tâm là lỗi nghiêm trọng nhất hệ thống này có thể mắc.
   → [multi-tenant.md](./docs/backend/multi-tenant.md)
3. **API không hard-code message lỗi một ngôn ngữ** — trả **mã lỗi**, frontend dịch qua `react-i18next`.
   → [cqrs-mediatr.md](./docs/backend/cqrs-mediatr.md#trả-lỗi)
4. **Đổi schema/API → cập nhật tài liệu trong cùng PR**, không tách "làm sau".
5. **Không push thẳng `main`**, không force-push, không amend commit đã publish, không `--no-verify`
   (trừ yêu cầu tường minh). → [CONTRIBUTING.md](./CONTRIBUTING.md)
6. **Mọi service ngoài reverse proxy không expose port ra Internet.**
   → [ADR-0004](./docs/kien-truc/adr/0004-ha-tang-tu-host-vps.md)
7. **Quyết định kiến trúc lớn/khó đảo ngược → viết ADR mới**, không sửa đè ADR cũ.
8. **Ràng buộc "chỉ một" phải là UNIQUE INDEX ở tầng DB**, không chỉ `if` trong handler.
   Kiểm bằng `AnyAsync` rồi `Add` là bẫy kinh điển: hai request song song đều thấy "chưa có" và
   đều ghi. Thêm ràng buộc kiểu này → thêm một dòng `InlineData` vào `DongThoiTests`.
   Lưu ý riêng của multi-tenant: username duy nhất **trong tenant**, tức
   `UNIQUE(tenant_id, username)` — làm `UNIQUE(username)` toàn cục sẽ chặn hai trung tâm cùng có
   tài khoản `admin`.
9. **Phân quyền đọc động từ bảng `QUYEN_CHUC_NANG`** — không hard-code `[Authorize(Roles=...)]`.
   → [phan-quyen-dong.md](./docs/backend/phan-quyen-dong.md)
10. **`Domain` không phụ thuộc EF Core / ASP.NET Core** — cấu hình EF đặt ở `Infrastructure`.
   → [clean-architecture.md](./docs/backend/clean-architecture.md)
11. **Tăng version API chỉ khi breaking change** — thêm field/endpoint mới hoặc sửa bug thì không.
    → [ADR-0003](./docs/kien-truc/adr/0003-api-versioning.md)

---

## 3. Bản đồ tài liệu

| Cần biết gì | Đọc ở đâu |
|---|---|
| **Tiến độ, lộ trình, nợ kỹ thuật** | [`docs/ke-hoach.md`](./docs/ke-hoach.md) |
| Tổng quan nghiệp vụ, đọc 1 mạch | [`docs/tong-thuat.md`](./docs/tong-thuat.md) |
| **24 mã FR** theo module | [`docs/nghiep-vu/`](./docs/nghiep-vu/README.md) |
| **ERD 34 bảng** + ràng buộc + hành vi xoá | [`docs/database/erd.md`](./docs/database/erd.md) |
| **Nhật ký theo ngày** (bối cảnh git log không có) | [`docs/nhat-ky/`](./docs/nhat-ky/README.md) |
| Clean Architecture, luật phụ thuộc | [`docs/backend/clean-architecture.md`](./docs/backend/clean-architecture.md) |
| Quy ước đặt tên, migration EF Core | [`docs/database/quy-uoc-migration.md`](./docs/database/quy-uoc-migration.md) |
| CQRS/MediatR, tổ chức handler theo FR | [`docs/backend/cqrs-mediatr.md`](./docs/backend/cqrs-mediatr.md) |
| Multi-tenant, chỗ Query Filter **không** bảo vệ | [`docs/backend/multi-tenant.md`](./docs/backend/multi-tenant.md) |
| Phân quyền động | [`docs/backend/phan-quyen-dong.md`](./docs/backend/phan-quyen-dong.md) |
| Nguyên tắc UI/UX bắt buộc | [`docs/frontend/ui-ux-nguyen-tac.md`](./docs/frontend/ui-ux-nguyen-tac.md) |
| Design token | [`docs/frontend/design-tokens.md`](./docs/frontend/design-tokens.md) |
| Hạ tầng, VPS, runbook sự cố | [`docs/ha-tang/`](./docs/ha-tang/README.md) |
| **Triển khai lên VPS** (`git pull` + build tại chỗ) | [`docs/ha-tang/trien-khai-pull-code.md`](./docs/ha-tang/trien-khai-pull-code.md) |
| Kiến trúc tổng quan + trạng thái quyết định | [`docs/kien-truc/TONG-QUAN-KIEN-TRUC.md`](./docs/kien-truc/TONG-QUAN-KIEN-TRUC.md) |
| 5 ADR đã chốt | [`docs/kien-truc/adr/`](./docs/kien-truc/adr/) |
| Git flow, commit convention, PR checklist | [`CONTRIBUTING.md`](./CONTRIBUTING.md) |
| Chính sách bảo mật | [`SECURITY.md`](./SECURITY.md) |
| Thuật ngữ dễ nhầm | [`docs/kien-truc/THUAT-NGU.md`](./docs/kien-truc/THUAT-NGU.md) |

---

## 4. Tech stack (đã chốt)

| Thành phần | Lựa chọn | ADR |
|---|---|---|
| Backend | ASP.NET Core Web API (.NET 8 LTS), Clean Architecture 4 lớp, CQRS + MediatR | [0001](./docs/kien-truc/adr/0001-lua-chon-cong-nghe.md) |
| ORM / DB | EF Core (Code-First) + **PostgreSQL** (không SQL Server — tránh license) | [0001](./docs/kien-truc/adr/0001-lua-chon-cong-nghe.md) |
| Auth | ASP.NET Core Identity + JWT Bearer (access + refresh token) | — |
| Frontend | **React + TypeScript** trên nền **shadcn-admin** (Vite + Tailwind + shadcn/ui + Radix) — **không Blazor** | [0002](./docs/kien-truc/adr/0002-frontend-shadcn-admin.md) |
| FE data/form | TanStack Query · React Hook Form + Zod | [0002](./docs/kien-truc/adr/0002-frontend-shadcn-admin.md) |
| FE lịch | **FullCalendar 6** (MIT) — tháng/tuần/danh sách, có `timeZone`; tải theo yêu cầu | — |
| Đa ngôn ngữ | BE `.resx` theo culture · FE `react-i18next` · API trả **mã lỗi** | — |
| API versioning | URL segment `/api/v1/...`, `Asp.Versioning.Mvc` | [0003](./docs/kien-truc/adr/0003-api-versioning.md) |
| Hạ tầng | 1 VPS · Docker Compose · **Nginx + certbot** (có sẵn trên VPS) · **MinIO** · Redis (tuỳ chọn) | [0004](./docs/kien-truc/adr/0004-ha-tang-tu-host-vps.md) |
| CI/CD | GitHub Actions → build & test → image → **ghcr.io** → SSH `docker compose pull && up -d` | [0004](./docs/kien-truc/adr/0004-ha-tang-tu-host-vps.md) |
| Thông báo | SMTP (SendGrid/Gmail API) + SMS Gateway nội địa (eSMS/Speedsms) | — |
| Quan sát | Loki+Promtail (log) · Prometheus+Grafana (metrics) · Uptime Kuma (alert) · Sentry (error) | [0004](./docs/kien-truc/adr/0004-ha-tang-tu-host-vps.md) |

---

## 5. Cấu trúc mã nguồn

```
src/
├── GiapTech.LangCenter.Domain          # Entity, Enum, quy tắc nghiệp vụ thuần — KHÔNG phụ thuộc EF Core/ASP.NET
├── GiapTech.LangCenter.Application     # CQRS: mỗi FR-xx = Command/Query riêng, DTO, interface, FluentValidation
├── GiapTech.LangCenter.Infrastructure  # EF Core DbContext, Repository, gửi SMS/Email, MinIO client
└── GiapTech.LangCenter.API             # Controller theo version (Controllers/V1/...), Middleware, JWT, Swagger
frontend/                                    # React (Vite + Tailwind + TanStack Query)
docs/                                        # Tài liệu (xem mục 3)
```

Tầng hệ thống hiện có, đặt ở đâu:

| Việc | Nơi đặt |
|---|---|
| Cách ly tenant (Query Filter, tự gán `tenant_id`) | `Infrastructure/Persistence/AppDbContext.cs` |
| Danh mục chức năng phân quyền | `Domain/Common/ChucNang.cs` |
| Kiểm quyền ở endpoint | `API/Authorization/RequirePermission.cs` |
| Mã lỗi trả về client | `Application/Common/Exceptions/MaLoi.cs` + `frontend/src/lib/i18n.ts` |
| Khởi tạo tenant mới (admin + nhóm quyền) | `Infrastructure/Persistence/Seed/TenantSeeder.cs` |
| Phân trang | `Application/Common/Models/Trang.cs` |
| Tải ảnh | `Application/Common/Anh/AnhDtos.cs` |

### Thứ tự làm việc khi thêm tính năng mới

1. Viết mô tả **FR-xx** vào [`docs/nghiep-vu/`](./docs/nghiep-vu/README.md) trước khi code.
2. Cập nhật [ERD](./docs/database/erd.md) + migration nếu đổi dữ liệu.
3. Viết code: **Domain → Application → Infrastructure → API**. Entity mới **bắt buộc** kế thừa
   `TenantEntity` (quy tắc #2) và thêm hằng vào `ChucNang.TatCa` nếu là module mới (quy tắc #9).
4. Cập nhật Swagger/OpenAPI + tài liệu API.
5. Viết test — unit cho `Application`, integration cho endpoint (**bắt buộc có test cách ly tenant**).
   Ràng buộc "chỉ một" → thêm `InlineData` vào `DongThoiTests` (quy tắc #8).
6. Thêm mã lỗi mới vào **cả** `MaLoi.cs` **và** `i18n.ts` — thiếu bản dịch thì người dùng thấy
   chuỗi mã lỗi trên màn hình (quy tắc #3).
7. Cập nhật [`CHANGELOG.md`](./CHANGELOG.md) + ghi [nhật ký ngày](./docs/nhat-ky/README.md) —
   làm ngay sau khi commit, không dồn lại.

---

## 6. Nền tảng đã có sẵn

Đã kiểm chứng và đang chạy — **không phải làm lại**:

- **Multi-tenant**: [Global Query Filter](./docs/backend/multi-tenant.md) tự áp cho mọi
  `ITenantEntity`, tự gán `tenant_id` khi ghi. Canh bởi `CachLyTenantTests` — trong đó có test
  hỏi chiều ngược: *"entity KHÔNG bị lọc có phải ngoại lệ có chủ ý không"*, buộc người thêm
  entity mới phải dừng lại khai lý do.
- **Phân quyền động**: `[RequirePermission]` + policy sinh động + `IAuthorizationHandler` đọc
  `QUYEN_CHUC_NANG` có cache. → [phan-quyen-dong.md](./docs/backend/phan-quyen-dong.md)
- **Xác thực**: JWT Bearer + refresh token **có xoay vòng và phát hiện tái sử dụng**; quên mật
  khẩu (token hash, hạn 30 phút, dùng một lần); middleware buộc đổi mật khẩu lần đầu chặn ở
  **tầng API**, không phó mặc frontend.
  Dùng riêng `PasswordHasher` của Identity, **không** kéo cả Identity stack — nó giả định
  username duy nhất toàn cục, trái với multi-tenant.
- **Quản trị**: CRUD tài khoản · nhóm quyền · thiết lập chung. Có chốt
  `ChotConNguoiQuanTri` không cho trung tâm mất người quản trị cuối cùng.
- **Ảnh**: tải/đọc/xoá qua MinIO, API làm proxy (MinIO không expose ra Internet — quy tắc #6),
  khoá mang `tenantId` ở đầu để cách ly.
- **Frontend**: đăng nhập, đổi mật khẩu, quên mật khẩu, đăng ký trung tâm, 3 màn quản trị.
  Interceptor tự làm mới token, **có khử đua** để không kích hoạt cơ chế chống đánh cắp.
- **Hạ tầng**: Docker Compose, CI GitHub Actions, script triển khai VPS.

### Nghiệp vụ LMS đã dựng

- **Lớp học** (FR-07, FR-08): vòng đời nháp → sắp khai giảng → đang học → kết thúc; giáo viên
  chính + trợ giảng; ghi danh học viên với **học phí riêng từng người**.
- **Buổi học & điểm danh** (FR-09, FR-10): sinh lịch tự động theo thứ trong tuần; điểm danh
  **hai nguồn** — học viên tự khai (giới hạn khung giờ) và giáo viên chốt.
- **Học liệu** (FR-11 → FR-13): bài tập, bài nộp nhiều lần giữ lịch sử, tài liệu, tệp đính kèm.
- **Học phí** (FR-14): sổ thu + công nợ **tính động, không lưu cột**.
- **Ba tầng bảo vệ riêng biệt** — đừng gộp:

  | Tầng | Lo việc gì |
  |---|---|
  | `[RequirePermission]` | Có gọi được endpoint không |
  | Global Query Filter | Cách ly **tenant** |
  | `IPhamViLopHoc` / `IPhamViHocPhi` | Phạm vi **bên trong** tenant: "lớp mình dạy", "sổ của mình" |

  Ba tầng này lọc **hàng nào** được thấy, **không** lọc **cột nào**. Nên **trường nhạy cảm
  không được đi nhờ DTO của module khác**: `LopHocDto.HocPhi` gác bằng `LopHoc.Xem` — quyền mà
  giáo viên và học viên đều có — nên tiền lọt ra dù `IPhamViHocPhi` hoàn toàn đúng (07/09/2026).
  Nếu buộc phải để trường tiền trong DTO module khác, gọi `DuocXemTienCuaLop()` trả null; canh
  bởi `RoRiHocPhiTests`.

- **Người ≠ tài khoản** (tách 07/09/2026). `NGUOI_DUNG` là con người và sống lâu hơn
  `TAI_KHOAN`; mỗi vai trò có bảng hồ sơ riêng (`HO_SO_GIAO_VIEN/HOC_VIEN/NHAN_VIEN`).
  Hai cột trạng thái **đừng nhầm**: `trang_thai_nhan_su` (còn làm không — chặn phân công lớp
  mới) và `TAI_KHOAN.trang_thai` (còn đăng nhập không — không đụng dữ liệu).
  Trong code: **tra quyền dùng `ICurrentUser.TaiKhoanId`**, **khoá ngoại nghiệp vụ dùng
  `ICurrentUser.UserId`** — lẫn hai thứ này trả rỗng một cách im lặng, không có lỗi biên dịch.

Nợ kỹ thuật: [`docs/ke-hoach.md`](./docs/ke-hoach.md).

---

## 7. Lệnh build/test/dev

Yêu cầu: .NET SDK 8.0+ · Node 20+ · Docker (chạy PostgreSQL local).

```bash
# --- Backend ---
dotnet build          # 0 warning — TreatWarningsAsErrors đang bật
dotnet test           # 387 test: luật phụ thuộc, cách ly tenant, phân quyền, xác thực,
                      #           quản trị, lớp học, điểm danh, học liệu, học phí

# Chạy API cần 2 biến bắt buộc (thiếu là 500 lúc đăng nhập / tải ảnh, không phải lúc khởi động):
export JWT_SECRET="chuoi-bi-mat-dev-dai-hon-32-ky-tu-cho-du-an-toan"
export Minio__Endpoint="localhost:59000" Minio__AccessKey="devminio" \
       Minio__SecretKey="devminio123" Minio__UseSsl="false"
dotnet run --project src/GiapTech.LangCenter.API   # Swagger tại /swagger, cổng 5229

# --- Kiểm tra tài liệu ---
python3 scripts/check-doc-links.py

# --- Kiểm khoá i18n (thiếu bản dịch KHÔNG làm build đỏ, người dùng thấy chuỗi khoá) ---
python3 scripts/check-i18n-keys.py

# --- Frontend ---
cd frontend && npm install
npm run dev     # http://localhost:5173, proxy /api -> :5229
npm run build   # tsc -b && vite build
npx oxlint src e2e

# --- E2E (Playwright) ---
# Mỗi test tự tạo một trung tâm qua /dang-ky-trung-tam, mà endpoint đó có hạn mức 10 req/phút
# mỗi IP (thêm 08/09/2026) → chạy cả bộ sẽ 429. PHẢI tắt hạn mức khi chạy E2E:
GIOI_HAN_TAN_SUAT=false dotnet run --project src/GiapTech.LangCenter.API   # ở terminal khác
E2E_BASE_URL=http://localhost:5173 npx playwright test

# --- PostgreSQL + MinIO cho dev ---
docker run -d --name lms-minio -p 59000:9000 \
  -e MINIO_ROOT_USER=devminio -e MINIO_ROOT_PASSWORD=devminio123 \
  minio/minio:latest server /data
docker run -d --name lms-pg -e POSTGRES_PASSWORD=devpass -e POSTGRES_USER=langcenter \
  -e POSTGRES_DB=langcenter -p 55432:5432 postgres:16-alpine
export ConnectionStrings__Default="Host=localhost;Port=55432;Database=langcenter;Username=langcenter;Password=devpass"
dotnet ef database update --project src/GiapTech.LangCenter.Infrastructure \
  --startup-project src/GiapTech.LangCenter.API
# → 34 bảng (13 hệ thống + 21 nghiệp vụ) — xem docs/database/erd.md

# Tạo trung tâm thử — endpoint ẩn danh, mã 7 ký tự do hệ thống sinh:
curl -X POST localhost:5229/api/v1/dang-ky-trung-tam \
  -H 'Content-Type: application/json' -d '{"tenTrungTam":"Trung tâm Ngoại ngữ Dev"}'
# → { maTrungTam: "A3K9M2P", username: "admin", matKhau: "123456" }
#   Bắt buộc đổi mật khẩu ở lần đăng nhập đầu.
```

### Thêm migration

```bash
dotnet ef migrations add TenMigration \
  --project src/GiapTech.LangCenter.Infrastructure \
  --startup-project src/GiapTech.LangCenter.API \
  --output-dir Persistence/Migrations
```

`--output-dir` là bắt buộc: không có nó EF đặt migration vào `Infrastructure/Migrations/`, lệch
khỏi chỗ các migration hiện tại đang nằm.

> `TreatWarningsAsErrors=true` trong `Directory.Build.props` — cảnh báo làm build đỏ. Sửa cảnh báo,
> đừng tắt cờ.

### Sáu test canh kiến trúc, đáng biết trước khi sửa code

| Test | Canh gì |
|---|---|
| `KienTruc/LuatPhuThuocTests.cs` | Quy tắc #10: `Domain` lỡ tham chiếu EF Core / ASP.NET Core / MediatR, hoặc `Application` tham chiếu ngược lên `Infrastructure`/`API` → đỏ ngay kèm hướng dẫn sửa. |
| `MultiTenancy/CachLyTenantTests.cs` | Quy tắc #2, **cả hai chiều**: mọi `ITenantEntity` có Query Filter, VÀ mọi entity không bị lọc phải nằm trong danh sách ngoại lệ có khai lý do. |
| `DongThoiTests.cs` | Quy tắc #8: ràng buộc "chỉ một" là UNIQUE ở tầng DB. Có cả test chiều ngược: `UNIQUE(username)` toàn cục sẽ chặn hai trung tâm cùng có tài khoản `admin`. |
| `KienTruc/MoiEndpointPhaiDuocGacTests.cs` | Quy tắc #9: mọi endpoint phải có `[RequirePermission]` **hoặc** `[AllowAnonymous]` **hoặc** khai lý do. Chốt luôn số endpoint ẩn danh (6) để thêm cái mới phải có ý thức. |
| `KienTruc/RanhGioiHeThongConTests.cs` | ADR-0005: HRM · CRM · LMS không gọi chéo nhau ngoài cầu nối đã khai (nay đúng một: FR-21). Giữ đường lui rẻ nếu sau này cần tách. |
| `KienTruc/MoiEntityPhaiCoConfigTests.cs` | Quy tắc #10: mọi entity có `IEntityTypeConfiguration` (kiểm qua tên bảng `SNAKE_CASE`), và mọi cột chuỗi có `HasMaxLength` — thiếu thì EF âm thầm cho `text` vô hạn và Cascade mặc định. |

Cả sáu đều theo cùng khuôn: **danh sách ngoại lệ có khai lý do** + **test chiều ngược** để danh
sách không lạc hậu. Ai vi phạm sẽ phải dừng lại viết ra lý do, hoặc nhận ra mình quên.
