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
một lần đăng nhập. → [phan-quyen-dong.md](docs/03-backend/phan-quyen-dong.md#ba-hệ-thống-con-hrm--crm--lms)

Mỗi trung tâm đăng ký là một tenant độc lập, dữ liệu cách ly hoàn toàn theo `tenant_id`.
Đăng nhập bằng bộ ba **{mã trung tâm, tên đăng nhập, mật khẩu}**.

**Đường dẫn frontend theo hệ thống con**: `/hrm/...` · `/crm/...` · `/lms/...` (thống nhất
10/09/2026), quản trị dùng chung ở `/quan-tri/...`. `Layout.tsx` suy ra hệ thống con **từ tiền
tố**, nên route mới **phải** có tiền tố đúng. **Đừng lẫn route với endpoint API**: `/lms/hoc-vien`
là đường frontend, API vẫn là `/api/v1/hoc-vien`.

**Tên chuẩn:** `GiapTech.LangCenter` (namespace, solution, image `ghcr.io/giaptech/langcenter-api`).

### Trạng thái

Tách ra từ một ứng dụng quản lý CLB đá bóng (04/09/2026), giữ toàn bộ tầng hệ thống. Nghiệp vụ
LMS dựng từ 05/09/2026 theo đặc tả Vietgenedu.

**29 mã FR chạy đầu-cuối** trên PostgreSQL + MinIO thật. 619 test backend · 40 frontend · 48 E2E xanh.

| Đã chạy đầu-cuối | Chưa có |
|---|---|
| Đăng nhập · quên mật khẩu · buộc đổi mật khẩu lần đầu | **Bài kiểm tra** — có schema, chưa có API/UI |
| Người dùng (hồ sơ 3 vai trò) tách khỏi tài khoản · nhóm quyền · thiết lập | **Kết thúc lớp** — enum có `DaKetThuc` nhưng chưa endpoint nào set (nợ N26) |
| **Ba hệ thống con** HRM/CRM/LMS: bộ chuyển, sidebar lọc, tab phân quyền · URL `/hrm` `/crm` `/lms` | Số **đã thu** ở CRM chưa chảy sang sổ học phí LMS |
| **CRM** (FR-17 → FR-20): khách hàng + view 3 tab · doanh thu đa tiền tệ · **khoá học + sản phẩm** | Hộp thoại chọn lớp chưa **sắp** lớp cùng khoá lên đầu (nợ N19, còn phần nhỏ) |
| **HRM** đủ nghiệp vụ (FR-22 → FR-24): cơ cấu tổ chức · chức vụ · hồ sơ mở rộng (CCCD, số TK, MXH, tệp) | Import Excel học viên |
| Hồ sơ con người: Nhân sự (HRM) · **Học viên quản lý tập trung ở CRM** (13/09) | **Bài tập cho khoá online** — FR-27, bước 4/4 chưa làm |
| Lớp học: vòng đời, phân công, ghi danh, học phí riêng từng người, **gán tối đa 3 khoá** | |
| Buổi học: sinh lịch tự động; điểm danh hai nguồn; **trạng thái suy theo giờ + màu riêng** (18/09) | Dọn nhật ký cũ theo chính sách lưu giữ |
| Bài tập, bài nộp nhiều lần, tài liệu, tệp đính kèm | Danh mục ngày nghỉ khi sinh lịch |
| Học phí: sổ thu + công nợ tính động — **ẩn khỏi LMS 12/09, chỉ CRM nắm tiền** |  |
| **Nhật ký thao tác** mọi module (FR-16) | |
| **FR-21 xếp lớp** CRM → LMS: duyệt/từ chối, trạng thái tham gia lớp **suy động**, **cảnh báo lệch khoá** | |
| **FR-15 Tổng quan**: một màn cho mọi vai trò, chỉ hiện việc tồn đọng, không có số tiền | |
| **FR-25 → FR-27 Học trực tuyến**: soạn khoá · cấp quyền học · tiến độ (bài tập chấm điểm chưa) | |
| **FR-28 Thống kê CRM**: doanh thu theo khoá/sản phẩm/đội, phễu, công nợ, biểu đồ tăng trưởng | |
| **FR-29 Thống kê nhân sự**: xếp hạng kinh doanh/giáo viên/trợ giảng + module tiêu chí chấm thang 5 | **LDP — landing page công khai** (FR-30), chưa bắt đầu |
| **Nhận diện tenant qua domain** (ADR-0008): hai đường vào — domain riêng ẩn ô mã, hoặc mã trung tâm | |
| **Site chủ hệ thống** (ADR-0009) đủ backend + UI: `/chu` — tạo trung tâm · gắn domain · đóng nợ N3 | |

Chi tiết và nợ kỹ thuật: [`docs/01-tong-quan/ke-hoach.md`](docs/01-tong-quan/ke-hoach.md).

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
   → [multi-tenant.md](docs/03-backend/multi-tenant.md)
3. **API không hard-code message lỗi một ngôn ngữ** — trả **mã lỗi**, frontend dịch qua `react-i18next`.
   → [cqrs-mediatr.md](docs/03-backend/cqrs-mediatr.md#trả-lỗi)
4. **Đổi schema/API → cập nhật tài liệu trong cùng PR**, không tách "làm sau".
5. **Không push thẳng `main`**, không force-push, không amend commit đã publish, không `--no-verify`
   (trừ yêu cầu tường minh). → [CONTRIBUTING.md](./CONTRIBUTING.md)
6. **Mọi service ngoài reverse proxy không expose port ra Internet.**
   → [ADR-0004](docs/02-kien-truc/adr/0004-ha-tang-tu-host-vps.md)
7. **Quyết định kiến trúc lớn/khó đảo ngược → viết ADR mới**, không sửa đè ADR cũ.
8. **Ràng buộc "chỉ một" phải là UNIQUE INDEX ở tầng DB**, không chỉ `if` trong handler.
   Kiểm bằng `AnyAsync` rồi `Add` là bẫy kinh điển: hai request song song đều thấy "chưa có" và
   đều ghi. Thêm ràng buộc kiểu này → thêm một dòng `InlineData` vào `DongThoiTests`.
   Lưu ý riêng của multi-tenant: username duy nhất **trong tenant**, tức
   `UNIQUE(tenant_id, username)` — làm `UNIQUE(username)` toàn cục sẽ chặn hai trung tâm cùng có
   tài khoản `admin`.
9. **Phân quyền đọc động từ bảng `QUYEN_CHUC_NANG`** — không hard-code `[Authorize(Roles=...)]`.
   Chức năng mới **phải khai thao tác** vào `ChucNang.ThaoTacTheoChucNang` trong cùng PR: thao
   tác THẬT của nghiệp vụ (`Chot`, `Duyet`, `ThuTien`…), không mặc định bốn ô CRUD. Khai thiếu
   thì ô không hiện trên màn phân quyền ⇒ không ai cấp được ⇒ endpoint 403 với **cả quản trị**.
   Kèm theo: nhãn tiếng Việt ở `i18n.ts` và giá trị ở `export type HanhDong` trong `quyen.ts`.
   → [phan-quyen-dong.md](docs/03-backend/phan-quyen-dong.md#-thêm-chức-năng-mới-bắt-buộc-khai-quyền-trong-cùng-pr)
10. **`Domain` không phụ thuộc EF Core / ASP.NET Core** — cấu hình EF đặt ở `Infrastructure`.
   → [clean-architecture.md](docs/03-backend/clean-architecture.md)
11. **Tăng version API chỉ khi breaking change** — thêm field/endpoint mới hoặc sửa bug thì không.
    → [ADR-0003](docs/02-kien-truc/adr/0003-api-versioning.md)

---

## 3. Bản đồ tài liệu

> **Điểm vào: [`docs/README.md`](docs/README.md)** — chỉ đường theo mục đích (hiểu nghiệp vụ /
> bắt đầu code / thêm chức năng / triển khai). Bảng dưới là tra cứu nhanh.

| Cần biết gì | Đọc ở đâu |
|---|---|
| **Tiến độ, lộ trình, nợ kỹ thuật** | [`docs/01-tong-quan/ke-hoach.md`](docs/01-tong-quan/ke-hoach.md) |
| Tổng quan nghiệp vụ, đọc 1 mạch | [`docs/01-tong-quan/tong-thuat.md`](docs/01-tong-quan/tong-thuat.md) |
| **29 mã FR** theo module | [`docs/06-nghiep-vu/`](docs/06-nghiep-vu/README.md) |
| **ERD 45 bảng** + ràng buộc + hành vi xoá | [`docs/05-database/erd.md`](docs/05-database/erd.md) |
| **Nhật ký theo ngày** (bối cảnh git log không có) | [`docs/nhat-ky/`](./docs/nhat-ky/README.md) |
| Clean Architecture, luật phụ thuộc | [`docs/03-backend/clean-architecture.md`](docs/03-backend/clean-architecture.md) |
| **Quy ước viết mã** (đặt tên, null, chú thích, test) | [`docs/08-quy-uoc/quy-uoc-code.md`](docs/08-quy-uoc/quy-uoc-code.md) |
| Quy ước đặt tên DB, migration EF Core | [`docs/05-database/quy-uoc-migration.md`](docs/05-database/quy-uoc-migration.md) |
| CQRS/MediatR, tổ chức handler theo FR | [`docs/03-backend/cqrs-mediatr.md`](docs/03-backend/cqrs-mediatr.md) |
| Multi-tenant, chỗ Query Filter **không** bảo vệ | [`docs/03-backend/multi-tenant.md`](docs/03-backend/multi-tenant.md) |
| Phân quyền động | [`docs/03-backend/phan-quyen-dong.md`](docs/03-backend/phan-quyen-dong.md) |
| Nguyên tắc UI/UX bắt buộc | [`docs/04-frontend/ui-ux-nguyen-tac.md`](docs/04-frontend/ui-ux-nguyen-tac.md) |
| Design token | [`docs/04-frontend/design-tokens.md`](docs/04-frontend/design-tokens.md) |
| **Đa ngôn ngữ** (5 thứ tiếng, cách thêm mới) | [`docs/04-frontend/da-ngon-ngu.md`](docs/04-frontend/da-ngon-ngu.md) |
| Hạ tầng, VPS, runbook sự cố | [`docs/07-ha-tang/`](docs/07-ha-tang/README.md) |
| **Triển khai lên VPS** (`git pull` + build tại chỗ) | [`docs/07-ha-tang/trien-khai-pull-code.md`](docs/07-ha-tang/trien-khai-pull-code.md) |
| Kiến trúc tổng quan + trạng thái quyết định | [`docs/02-kien-truc/tong-quan-kien-truc.md`](docs/02-kien-truc/tong-quan-kien-truc.md) |
| 9 ADR đã chốt | [`docs/02-kien-truc/adr/`](docs/02-kien-truc/adr) |
| **Nhận diện tenant qua domain** (23/09/2026) | [ADR-0008](docs/02-kien-truc/adr/0008-nhan-dien-tenant-qua-domain.md) |
| **Tài khoản cấp hệ thống** (site chủ) | [ADR-0009](docs/02-kien-truc/adr/0009-tai-khoan-cap-he-thong.md) |
| **Cẩm nang cho dự án khác** (đọc độc lập) | [`docs/09-cam-nang/`](docs/09-cam-nang/README.md) |
| Git flow, commit convention, PR checklist | [`CONTRIBUTING.md`](./CONTRIBUTING.md) |
| Chính sách bảo mật | [`SECURITY.md`](./SECURITY.md) |
| **Rà soát bảo mật luồng đăng nhập** (22/09/2026) | [`docs/02-kien-truc/ra-soat-bao-mat-dang-nhap.md`](docs/02-kien-truc/ra-soat-bao-mat-dang-nhap.md) |
| Thuật ngữ dễ nhầm | [`docs/01-tong-quan/thuat-ngu.md`](docs/01-tong-quan/thuat-ngu.md) |

---

## 4. Tech stack (đã chốt)

| Thành phần | Lựa chọn | ADR |
|---|---|---|
| Backend | ASP.NET Core Web API (.NET 8 LTS), Clean Architecture 4 lớp, CQRS + MediatR | [0001](docs/02-kien-truc/adr/0001-lua-chon-cong-nghe.md) |
| ORM / DB | EF Core (Code-First) + **PostgreSQL** (không SQL Server — tránh license) | [0001](docs/02-kien-truc/adr/0001-lua-chon-cong-nghe.md) |
| Auth | ASP.NET Core Identity + JWT Bearer (access + refresh token) | — |
| Frontend | **React + TypeScript** trên nền **shadcn-admin** (Vite + Tailwind + shadcn/ui + Radix) — **không Blazor** | [0002](docs/02-kien-truc/adr/0002-frontend-shadcn-admin.md) |
| FE data/form | TanStack Query · React Hook Form + Zod | [0002](docs/02-kien-truc/adr/0002-frontend-shadcn-admin.md) |
| FE lịch | **FullCalendar 6** (MIT) — tháng/tuần/danh sách, có `timeZone`; tải theo yêu cầu | — |
| Đa ngôn ngữ | **5 thứ tiếng** (vi·en·zh·ko·ja) · FE `react-i18next` · API trả **mã lỗi** | — |
| API versioning | URL segment `/api/v1/...`, `Asp.Versioning.Mvc` | [0003](docs/02-kien-truc/adr/0003-api-versioning.md) |
| Hạ tầng | 1 VPS · Docker Compose · **Nginx + certbot** (có sẵn trên VPS) · **MinIO** · Redis (tuỳ chọn) | [0004](docs/02-kien-truc/adr/0004-ha-tang-tu-host-vps.md) |
| CI/CD | GitHub Actions → build & test → image → **ghcr.io** → SSH `docker compose pull && up -d` | [0004](docs/02-kien-truc/adr/0004-ha-tang-tu-host-vps.md) |
| Thông báo | SMTP (SendGrid/Gmail API) + SMS Gateway nội địa (eSMS/Speedsms) | — |
| Quan sát | Loki+Promtail (log) · Prometheus+Grafana (metrics) · Uptime Kuma (alert) · Sentry (error) | [0004](docs/02-kien-truc/adr/0004-ha-tang-tu-host-vps.md) |

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

1. Viết mô tả **FR-xx** vào [`docs/06-nghiep-vu/`](docs/06-nghiep-vu/README.md) trước khi code.
2. Cập nhật [ERD](docs/05-database/erd.md) + migration nếu đổi dữ liệu.
3. Viết code: **Domain → Application → Infrastructure → API**. Entity mới **bắt buộc** kế thừa
   `TenantEntity` (quy tắc #2). Module mới: thêm hằng vào `ChucNang.TatCa`, phân loại trong
   `HeThongCua`, **và khai thao tác vào `ChucNang.ThaoTacTheoChucNang`** (quy tắc #9).
4. Cập nhật Swagger/OpenAPI + tài liệu API.
5. Viết test — unit cho `Application`, integration cho endpoint (**bắt buộc có test cách ly tenant**).
   Ràng buộc "chỉ một" → thêm `InlineData` vào `DongThoiTests` (quy tắc #8).
6. Thêm mã lỗi mới vào **cả** `MaLoi.cs` **và** `i18n.ts` — thiếu bản dịch thì người dùng thấy
   chuỗi mã lỗi trên màn hình (quy tắc #3). Chức năng/thao tác quyền mới: thêm nhãn vào khối
   `chucNang` + `hanhDong` của `i18n.ts` và giá trị vào `export type HanhDong` ở `quyen.ts` —
   kiểm bằng `python3 scripts/check-nhan-phan-quyen.py`.
7. Cập nhật [`CHANGELOG.md`](./CHANGELOG.md) + ghi [nhật ký ngày](./docs/nhat-ky/README.md) —
   làm ngay sau khi commit, không dồn lại.

---

## 6. Nền tảng đã có sẵn

Đã kiểm chứng và đang chạy — **không phải làm lại**:

- **Multi-tenant**: [Global Query Filter](docs/03-backend/multi-tenant.md) tự áp cho mọi
  `ITenantEntity`, tự gán `tenant_id` khi ghi. Canh bởi `CachLyTenantTests` — trong đó có test
  hỏi chiều ngược: *"entity KHÔNG bị lọc có phải ngoại lệ có chủ ý không"*, buộc người thêm
  entity mới phải dừng lại khai lý do.
- **Phân quyền động**: `[RequirePermission]` + policy sinh động + `IAuthorizationHandler` đọc
  `QUYEN_CHUC_NANG` có cache. → [phan-quyen-dong.md](docs/03-backend/phan-quyen-dong.md)
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

- **Lớp học** (FR-07, FR-08): vòng đời nháp → sắp khai giảng → đang học → kết thúc (bước cuối
  **chưa có endpoint** — nợ N26); giáo viên chính + trợ giảng; **gán tối đa 3 khoá học**
  (`LOP_HOC_KHOA_HOC`); ghi danh học viên với **học phí riêng từng người**.
- **Xếp lớp từ CRM** (FR-21): duyệt / từ chối kèm lý do; trạng thái tham gia lớp **suy động từ
  bảng ghi danh** (gỡ khỏi lớp hay đóng lớp là CRM tự đúng, không cần đồng bộ); **cảnh báo khi
  khoá của đơn không khớp khoá lớp dạy** — cảnh báo, không chặn.
- **Buổi học & điểm danh** (FR-09, FR-10): sinh lịch tự động theo thứ trong tuần; điểm danh
  **hai nguồn** — học viên tự khai (giới hạn khung giờ) và giáo viên chốt.
- **Học liệu** (FR-11 → FR-13): bài tập, bài nộp nhiều lần giữ lịch sử, tài liệu, tệp đính kèm.
- **Học phí** (FR-14): sổ thu + công nợ **tính động, không lưu cột**.
  **Từ 12/09/2026 ẩn khỏi giao diện và API của LMS** — chỉ CRM nắm số tiền. Dữ liệu, bảng và
  endpoint giữ nguyên; `LopHocDto.HocPhi` và `HocVienTrongLopDto.HocPhiApDung` luôn trả `null`.
- **Bốn tầng bảo vệ riêng biệt** — đừng gộp:

  | Tầng | Lo việc gì |
  |---|---|
  | `[RequirePermission]` | Có gọi được endpoint không |
  | Global Query Filter | Cách ly **tenant** |
  | `IPhamViLopHoc` / `IPhamViHocPhi` | Phạm vi **bên trong** tenant: "lớp mình dạy", "sổ của mình", **"học viên lớp mình"** (14/09) |
  | `IPhamViKhoaOnline` (13/09) | Khoá trực tuyến — cần riêng vì nó **không có lớp** để `IPhamViLopHoc` bám vào |

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

Nợ kỹ thuật: [`docs/01-tong-quan/ke-hoach.md`](docs/01-tong-quan/ke-hoach.md).

---

## 7. Lệnh build/test/dev

Yêu cầu: .NET SDK 8.0+ · Node 20+ · Docker (chạy PostgreSQL local).

```bash
# --- Backend ---
dotnet build          # 0 warning — TreatWarningsAsErrors đang bật
dotnet test           # 619 test: luật phụ thuộc, cách ly tenant, phân quyền, xác thực,
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

# --- Kiểm nhãn phân quyền (khoá động, check-i18n-keys.py không quét tới) ---
python3 scripts/check-nhan-phan-quyen.py

# --- Kiểm token màu (Tailwind IM LẶNG bỏ qua class sai ⇒ mất màu, không lỗi nào) ---
python3 scripts/check-token-mau.py

# --- Frontend ---
cd frontend && npm install
npm run dev     # http://localhost:5173, proxy /api -> :5229
npm run build   # tsc -b && vite build
npm test        # vitest run — 40 test cho PHÉP TÍNH (biểu đồ, ma trận quyền, tình trạng buổi)
npx oxlint src e2e

# --- E2E (Playwright) ---
# Mỗi test tự tạo một trung tâm qua /dang-ky-trung-tam, mà endpoint đó có hạn mức 10 req/phút
# mỗi IP (thêm 08/09/2026) → chạy cả bộ sẽ 429. PHẢI tắt hạn mức khi chạy E2E:
# Và từ 22/09/2026, tự đăng ký trung tâm TẮT mặc định (chủ sản phẩm chốt đóng hẳn) — mà mỗi
# test E2E lại tự tạo một trung tâm, nên phải bật lại khi chạy test:
# Từ 23/09/2026 thêm CHO_DON_E2E + CHU_HE_THONG_MAT_KHAU để globalTeardown dọn tenant rác
# sau mỗi lượt chạy (nợ N11). Thiếu hai biến này thì test vẫn chạy bình thường, chỉ là rác
# không được dọn và teardown in một dòng cảnh báo.
GIOI_HAN_TAN_SUAT=false CHO_TU_DANG_KY=true CHO_DON_E2E=true \
  CHU_HE_THONG_MAT_KHAU='mat-khau-chu-e2e-123456' \
  dotnet run --project src/GiapTech.LangCenter.API   # ở terminal khác

# CHẠY TỪ THƯ MỤC frontend/ — playwright.config.ts nằm ở đó, chạy từ gốc repo thì Playwright
# quét nhầm cả file vitest trong src/ và báo "No tests found".
cd frontend
E2E_BASE_URL=http://localhost:5173 CHU_HE_THONG_MAT_KHAU='mat-khau-chu-e2e-123456' \
  npx playwright test

# --- PostgreSQL + MinIO cho dev ---
docker run -d --name lms-minio -p 59000:9000 \
  -e MINIO_ROOT_USER=devminio -e MINIO_ROOT_PASSWORD=devminio123 \
  minio/minio:latest server /data
docker run -d --name lms-pg -e POSTGRES_PASSWORD=devpass -e POSTGRES_USER=langcenter \
  -e POSTGRES_DB=langcenter -p 55432:5432 postgres:16-alpine
export ConnectionStrings__Default="Host=localhost;Port=55432;Database=langcenter;Username=langcenter;Password=devpass"
dotnet ef database update --project src/GiapTech.LangCenter.Infrastructure \
  --startup-project src/GiapTech.LangCenter.API
# → 45 bảng (16 hệ thống + 29 nghiệp vụ; QUAN_TRI_HE_THONG thêm 23/09) — xem docs/05-database/erd.md

# Tạo trung tâm thử — endpoint ẩn danh, mã 7 ký tự do hệ thống sinh.
# CẦN `CHO_TU_DANG_KY=true` lúc chạy API, nếu không endpoint trả 404 (mặc định TẮT từ
# 22/09/2026 — xem API/TinhNang.cs):
curl -X POST localhost:5229/api/v1/dang-ky-trung-tam \
  -H 'Content-Type: application/json' -d '{"tenTrungTam":"Trung tâm Ngoại ngữ Dev"}'
# → { maTrungTam: "A3K9M2P", username: "admin", matKhau: "<ngẫu nhiên 16 ký tự>" }
#   Bắt buộc đổi mật khẩu ở lần đăng nhập đầu.
#
# VPS mới (DB rỗng): đặt TRUNG_TAM_DAU_TIEN_MAT_KHAU trong .env thì hệ thống tự tạo trung tâm
# đầu tiên lúc khởi động — xem docs/07-ha-tang/trien-khai-2026-09-23.md

# --- Đồng bộ mật khẩu mọi nick của MỘT trung tâm (CHỈ DEV) ---
# Khi phải đăng nhập lần lượt nhiều vai trò để xem mỗi người thấy gì.
MA_TRUNG_TAM=W686AE9 MAT_KHAU_MOI=123456 ADMIN_PASS='...' \
  ./scripts/dong-bo-mat-khau-dev.sh
# Script gọi API để BĂM (không ghi hash tay), rồi tắt cờ `phai_doi_mat_khau`.
# Nó đổi tài khoản đang gọi API SAU CÙNG — đổi trước thì token hết hiệu lực
# và mọi nick còn lại nhận 403 trong khi script vẫn chạy tới cuối.
```

### Tài khoản dev của tenant W686AE9

Mọi nick dùng chung mật khẩu **`123456`** (đồng bộ 16/09/2026, xem script ở trên).

| Nick | Vai trò | Người |
|---|---|---|
| `admin` | Nhân viên khác | Quản trị viên |
| `ns.mai` | Nhân viên khác | Chị Mai (nhân sự) |
| `nv1` … `nv4` | NV kinh doanh | Sale Hà Nội · Sài Gòn · Online A · Online B |
| `co.lan`, `nv6` | Giáo viên | Cô Lan · Thầy Hoà |
| `tg.hoa` | Trợ giảng | Thầy Hoà (trợ giảng) — **trợ giảng lớp *IELTS 6.5 — K1*** (gán 18/09 để thử chấm riêng GV/trợ giảng) |
| `hv1`, `hv2` | Học viên | `hv1` đang học lớp *IELTS 6.5 cấp tốc — K1* |

`hv1` là học viên **duy nhất** có tài khoản **và** đang trong lớp — dùng nó để thử luồng học viên
(tự điểm danh, nhận xét buổi học, chấm tiêu chí giảng dạy). 49 học viên còn lại là dữ liệu demo,
chưa có tài khoản.

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

### Bảy test canh kiến trúc, đáng biết trước khi sửa code

| Test | Canh gì |
|---|---|
| `KienTruc/LuatPhuThuocTests.cs` | Quy tắc #10: `Domain` lỡ tham chiếu EF Core / ASP.NET Core / MediatR, hoặc `Application` tham chiếu ngược lên `Infrastructure`/`API` → đỏ ngay kèm hướng dẫn sửa. |
| `MultiTenancy/CachLyTenantTests.cs` | Quy tắc #2, **cả hai chiều**: mọi `ITenantEntity` có Query Filter, VÀ mọi entity không bị lọc phải nằm trong danh sách ngoại lệ có khai lý do. |
| `DongThoiTests.cs` | Quy tắc #8: ràng buộc "chỉ một" là UNIQUE ở tầng DB. Có cả test chiều ngược: `UNIQUE(username)` toàn cục sẽ chặn hai trung tâm cùng có tài khoản `admin`. |
| `KienTruc/MoiEndpointPhaiDuocGacTests.cs` | Quy tắc #9: mọi endpoint phải có `[RequirePermission]` **hoặc** `[AllowAnonymous]` **hoặc** khai lý do. Chốt luôn số endpoint ẩn danh (6) để thêm cái mới phải có ý thức. |
| `KienTruc/RanhGioiHeThongConTests.cs` | ADR-0005: HRM · CRM · LMS không gọi chéo nhau ngoài cầu nối đã khai (nay đúng một: FR-21). Giữ đường lui rẻ nếu sau này cần tách. |
| `KienTruc/MoiEntityPhaiCoConfigTests.cs` | Quy tắc #10: mọi entity có `IEntityTypeConfiguration` (kiểm qua tên bảng `SNAKE_CASE`), và mọi cột chuỗi có `HasMaxLength` — thiếu thì EF âm thầm cho `text` vô hạn và Cascade mặc định. |
| `KienTruc/MaTranQuyenPhaiKhopThucTeTests.cs` | Quy tắc #9, **cả hai chiều**: endpoint không được gác bằng quyền chưa khai (403 cho cả quản trị), và quyền đã khai phải có endpoint dùng hoặc khai lý do (ô chết). Chốt luôn giá trị số của `HanhDong` 0–3 vì DB lưu số nguyên. |

Cả bảy đều theo cùng khuôn: **danh sách ngoại lệ có khai lý do** + **test chiều ngược** để danh
sách không lạc hậu. Ai vi phạm sẽ phải dừng lại viết ra lý do, hoặc nhận ra mình quên.
