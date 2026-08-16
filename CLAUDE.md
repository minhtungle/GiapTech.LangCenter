# CLAUDE.md — Quy tắc làm việc & bản đồ tài liệu

> **Đọc file này trước khi viết bất kỳ dòng code nào.**
>
> File này chứa **quy tắc bắt buộc** + **bản đồ điều hướng**. Nội dung chi tiết đã tách vào `docs/` —
> khi mâu thuẫn, **tài liệu trong `docs/` thắng** (file này chỉ dẫn đường, không phải nguồn chân lý về
> chi tiết). Cập nhật ngay khi có thay đổi.

---

## 1. Dự án này là gì

Ứng dụng web **quản lý câu lạc bộ đá bóng phong trào**, mô hình **multi-tenant** — mỗi CLB đăng ký là
một tenant độc lập, dữ liệu cách ly hoàn toàn theo `tenant_id`. Đăng nhập bằng bộ ba
**{ID đội, tên đăng nhập, mật khẩu}**.

**Tên chuẩn:** `GiapTech.SoccerRoom` (namespace, solution, image `ghcr.io/giaptech/soccerroom-api`).

5 module · 16 mã FR · 3 actor (Admin / Manager / Player) — đọc 1 mạch ở
[`docs/tong-thuat.md`](./docs/tong-thuat.md).

**Trạng thái:** 🚧 **chạy được đầu-cuối trên PostgreSQL thật**: frontend đăng nhập → cụm quản trị (FR-01→FR-06). 44 test xanh. Bước kế tiếp ở [mục 6](#6-bootstrap-checklist).

---

## 2. Mười quy tắc bất di bất dịch

1. **Mọi bảng nghiệp vụ có `tenant_id` + EF Core Global Query Filter** — không được quên ở entity mới.
   Rò rỉ dữ liệu chéo CLB là lỗi nghiêm trọng nhất hệ thống này có thể mắc.
   → [multi-tenant.md](./docs/backend/multi-tenant.md)
2. **API không hard-code message lỗi một ngôn ngữ** — trả **mã lỗi**, frontend dịch qua `react-i18next`.
   → [cqrs-mediatr.md](./docs/backend/cqrs-mediatr.md#trả-lỗi)
3. **Đổi schema/API → cập nhật tài liệu trong cùng PR**, không tách "làm sau".
4. **Không push thẳng `main`**, không force-push, không amend commit đã publish, không `--no-verify`
   (trừ yêu cầu tường minh). → [CONTRIBUTING.md](./CONTRIBUTING.md)
5. **Mọi service ngoài Caddy không expose port ra Internet.**
   → [ADR-0004](./docs/kien-truc/adr/0004-ha-tang-tu-host-vps.md)
6. **Quyết định kiến trúc lớn/khó đảo ngược → viết ADR mới**, không sửa đè ADR cũ.
7. **Mỗi cầu thủ chỉ vote MVP 1 lần/trận** — `UNIQUE(tran_dau_id, nguoi_vote_id)` ở **tầng DB**, không
   chỉ chặn ở UI. → [ERD](./docs/database/erd.md#ràng-buộc-nghiệp-vụ-quan-trọng)
8. **Phân quyền đọc động từ bảng `QUYEN_CHUC_NANG`** — không hard-code `[Authorize(Roles=...)]`.
   → [phan-quyen-dong.md](./docs/backend/phan-quyen-dong.md)
9. **`Domain` không phụ thuộc EF Core / ASP.NET Core** — cấu hình EF đặt ở `Infrastructure`.
   → [clean-architecture.md](./docs/backend/clean-architecture.md)
10. **Tăng version API chỉ khi breaking change** — thêm field/endpoint mới hoặc sửa bug thì không.
    → [ADR-0003](./docs/kien-truc/adr/0003-api-versioning.md)

---

## 3. Bản đồ tài liệu

| Cần biết gì | Đọc ở đâu |
|---|---|
| **Tiến độ, lộ trình, nợ kỹ thuật** | [`docs/ke-hoach.md`](./docs/ke-hoach.md) |
| **Nhật ký theo ngày** (bối cảnh git log không có) | [`docs/nhat-ky/`](./docs/nhat-ky/README.md) |
| Tổng quan nghiệp vụ, đọc 1 mạch | [`docs/tong-thuat.md`](./docs/tong-thuat.md) |
| **16 mã FR** theo module | [`docs/nghiep-vu/`](./docs/nghiep-vu/README.md) |
| **ERD 16 bảng** + ràng buộc | [`docs/database/erd.md`](./docs/database/erd.md) |
| Quy ước đặt tên, migration EF Core | [`docs/database/quy-uoc-migration.md`](./docs/database/quy-uoc-migration.md) |
| Clean Architecture, luật phụ thuộc | [`docs/backend/clean-architecture.md`](./docs/backend/clean-architecture.md) |
| CQRS/MediatR, tổ chức handler theo FR | [`docs/backend/cqrs-mediatr.md`](./docs/backend/cqrs-mediatr.md) |
| Multi-tenant, chỗ Query Filter **không** bảo vệ | [`docs/backend/multi-tenant.md`](./docs/backend/multi-tenant.md) |
| Phân quyền động | [`docs/backend/phan-quyen-dong.md`](./docs/backend/phan-quyen-dong.md) |
| Nguyên tắc UI/UX bắt buộc | [`docs/frontend/ui-ux-nguyen-tac.md`](./docs/frontend/ui-ux-nguyen-tac.md) |
| Design token | [`docs/frontend/design-tokens.md`](./docs/frontend/design-tokens.md) |
| Hạ tầng, VPS, runbook sự cố | [`docs/ha-tang/`](./docs/ha-tang/README.md) |
| Kiến trúc tổng quan + trạng thái quyết định | [`docs/kien-truc/TONG-QUAN-KIEN-TRUC.md`](./docs/kien-truc/TONG-QUAN-KIEN-TRUC.md) |
| 4 ADR đã chốt | [`docs/kien-truc/adr/`](./docs/kien-truc/adr/) |
| Thuật ngữ dễ nhầm (MVP ≠ Minimum Viable Product) | [`docs/kien-truc/THUAT-NGU.md`](./docs/kien-truc/THUAT-NGU.md) |
| Git flow, commit convention, PR checklist | [`CONTRIBUTING.md`](./CONTRIBUTING.md) |
| Chính sách bảo mật | [`SECURITY.md`](./SECURITY.md) |

---

## 4. Tech stack (đã chốt)

| Thành phần | Lựa chọn | ADR |
|---|---|---|
| Backend | ASP.NET Core Web API (.NET 8 LTS), Clean Architecture 4 lớp, CQRS + MediatR | [0001](./docs/kien-truc/adr/0001-lua-chon-cong-nghe.md) |
| ORM / DB | EF Core (Code-First) + **PostgreSQL** (không SQL Server — tránh license) | [0001](./docs/kien-truc/adr/0001-lua-chon-cong-nghe.md) |
| Auth | ASP.NET Core Identity + JWT Bearer (access + refresh token) | — |
| Frontend | **React + TypeScript** trên nền **shadcn-admin** (Vite + Tailwind + shadcn/ui + Radix) — **không Blazor** | [0002](./docs/kien-truc/adr/0002-frontend-shadcn-admin.md) |
| FE data/form | TanStack Table · TanStack Query · React Hook Form + Zod · Recharts | [0002](./docs/kien-truc/adr/0002-frontend-shadcn-admin.md) |
| Đa ngôn ngữ | BE `.resx` theo culture · FE `react-i18next` · API trả **mã lỗi** | — |
| API versioning | URL segment `/api/v1/...`, `Asp.Versioning.Mvc` | [0003](./docs/kien-truc/adr/0003-api-versioning.md) |
| Hạ tầng | 1 VPS · Docker Compose · **Caddy** (auto HTTPS) · **MinIO** · Redis (tuỳ chọn) | [0004](./docs/kien-truc/adr/0004-ha-tang-tu-host-vps.md) |
| CI/CD | GitHub Actions → build & test → image → **ghcr.io** → SSH `docker compose pull && up -d` | [0004](./docs/kien-truc/adr/0004-ha-tang-tu-host-vps.md) |
| Video sau trận | Chỉ lưu **link** (Youtube/Drive), không lưu file video | — |
| Thông báo | SMTP (SendGrid/Gmail API) + SMS Gateway nội địa (eSMS/Speedsms) | — |
| Quan sát | Loki+Promtail (log) · Prometheus+Grafana (metrics) · Uptime Kuma (alert) · Sentry (error) | [0004](./docs/kien-truc/adr/0004-ha-tang-tu-host-vps.md) |

---

## 5. Cấu trúc mã nguồn

```
src/
├── GiapTech.SoccerRoom.Domain          # Entity, Enum, quy tắc nghiệp vụ thuần — KHÔNG phụ thuộc EF Core/ASP.NET
├── GiapTech.SoccerRoom.Application     # CQRS: mỗi FR-xx = Command/Query riêng, DTO, interface, FluentValidation
├── GiapTech.SoccerRoom.Infrastructure  # EF Core DbContext, Repository, gửi SMS/Email, MinIO client
└── GiapTech.SoccerRoom.API             # Controller theo version (Controllers/V1/...), Middleware, JWT, Swagger
frontend/                               # React + shadcn-admin (Vite)
docs/                                   # Tài liệu (xem mục 3)
```

### Thứ tự làm việc khi thêm tính năng mới

1. Đọc mô tả **FR-xx** ở [`docs/nghiep-vu/`](./docs/nghiep-vu/README.md).
2. Cập nhật [ERD](./docs/database/erd.md) + migration nếu đổi dữ liệu.
3. Viết code: **Domain → Application → Infrastructure → API**.
4. Cập nhật Swagger/OpenAPI + tài liệu API.
5. Viết test — unit cho `Application`, integration cho endpoint (**bắt buộc có test cách ly tenant**).
6. Cập nhật [`CHANGELOG.md`](./CHANGELOG.md).
7. Ghi [nhật ký ngày](./docs/nhat-ky/README.md) + cập nhật trạng thái ở
   [`docs/ke-hoach.md`](./docs/ke-hoach.md) — làm ngay sau khi commit, không dồn lại.

---

## 6. Bootstrap checklist

- [x] `git init` + `.gitignore` (.NET + Node + **`.env`**), commit đầu tiên.
- [x] Khởi tạo solution .NET theo [mục 5](#5-cấu-trúc-mã-nguồn): 4 project + 2 test project,
      `Directory.Build.props` (net8.0, nullable, warnings-as-errors), test canh luật phụ thuộc.
- [x] Cài EF Core + Npgsql, tạo `DbContext` với 16 entity, migration `InitialCreate` (15 bảng).
      Đã chốt: [denormalize `tenant_id` xuống cả 7 bảng con](./docs/database/erd.md#denormalize-tenant_id-xuống-bảng-con).
- [x] [Global Query Filter](./docs/backend/multi-tenant.md) tự động + tự gán `tenant_id` khi ghi,
      có `CachLyTenantTests` canh. **Còn thiếu:** middleware đọc claim ở tầng API.
- [x] JWT Bearer + `Asp.Versioning.Mvc` (`/api/v1/`) + Swagger có ô nhập token.
      Dùng riêng `PasswordHasher` của Identity, **không** kéo cả Identity stack (nó giả định
      username duy nhất toàn cục — trái với multi-tenant).
- [x] [Phân quyền động](./docs/backend/phan-quyen-dong.md): `[RequirePermission]` + policy sinh
      động + `IAuthorizationHandler` đọc `QUYEN_CHUC_NANG` có cache. Đã kiểm chứng bằng phản chứng.
- [x] Middleware tenant đọc claim → `ICurrentTenant`; exception middleware trả **mã lỗi**.
- [x] FR-01 đăng nhập (CQRS + FluentValidation), 10 integration test.
- [x] **Cụm quản trị hệ thống (FR-03 → FR-06)**: CRUD tài khoản · hồ sơ cầu thủ · nhóm quyền ·
      thiết lập chung. Seeder tạo CLB mới (admin/123456 + nhóm "Quản trị viên" đầy đủ).
      Middleware buộc đổi mật khẩu lần đầu — chặn ở tầng API, không phó mặc frontend.
- [x] **FR-02 quên mật khẩu** (token hash, hạn 30 phút, dùng một lần) + **refresh token** có xoay
      vòng và phát hiện tái sử dụng. Migration `ThemBangToken`.
- [x] **Frontend** (Vite + React + TS + Tailwind + TanStack Query): đăng nhập, đổi mật khẩu,
      quên mật khẩu, và 4 màn quản trị (tài khoản/cầu thủ/phân quyền/thiết lập).
      Interceptor tự làm mới token, có khử đua để không kích hoạt cơ chế chống đánh cắp.
- [x] Chốt [design token](./docs/frontend/design-tokens.md): xanh sân cỏ + cam nhấn + 3 màu trạng thái.
- [x] **Kiểm chứng trên PostgreSQL thật**: migration áp sạch, 17 bảng, UNIQUE vote MVP chặn đúng
      khi thử vi phạm trực tiếp bằng SQL.
- [ ] Cập nhật [mục 7](#7-lệnh-buildtestdev) bằng lệnh thật chạy được.

---

## 7. Lệnh build/test/dev

Yêu cầu: .NET SDK 8.0+ · Node 20+ · Docker (chạy PostgreSQL local).

```bash
# --- Backend (đã hoạt động) ---
dotnet build                                        # 0 warning — TreatWarningsAsErrors đang bật
dotnet test                                         # 44 test: luật phụ thuộc, cách ly tenant, phân quyền, xác thực, CRUD quản trị
dotnet run --project src/GiapTech.SoccerRoom.API    # Swagger tại /swagger

# --- Kiểm tra tài liệu (đã hoạt động) ---
python3 scripts/check-doc-links.py

# --- Frontend (đã hoạt động) ---
cd frontend && npm install && npm run dev   # http://localhost:5173, proxy /api -> :5229

# --- PostgreSQL cho dev ---
docker run -d --name sr-pg -e POSTGRES_PASSWORD=devpass -e POSTGRES_USER=soccerroom \
  -e POSTGRES_DB=soccerroom -p 55432:5432 postgres:16-alpine
export ConnectionStrings__Default="Host=localhost;Port=55432;Database=soccerroom;Username=soccerroom;Password=devpass"
dotnet ef database update --project src/GiapTech.SoccerRoom.Infrastructure \
  --startup-project src/GiapTech.SoccerRoom.API

# Tạo CLB thử (chỉ chạy ở Development): POST /api/v1/dang-ky-clb {"maDoi":"FCDEV","tenDoi":"..."}
# → admin/123456, bắt buộc đổi mật khẩu lần đầu
```

> `TreatWarningsAsErrors=true` trong `Directory.Build.props` — cảnh báo làm build đỏ. Sửa cảnh báo,
> đừng tắt cờ.

### Test luật phụ thuộc

`tests/GiapTech.SoccerRoom.Application.UnitTests/KienTruc/LuatPhuThuocTests.cs` biến quy tắc #9 thành
thứ CI bắt được: nếu `Domain` lỡ tham chiếu EF Core / ASP.NET Core / MediatR, hoặc `Application` tham
chiếu ngược lên `Infrastructure`/`API`, test đỏ ngay kèm hướng dẫn sửa.
