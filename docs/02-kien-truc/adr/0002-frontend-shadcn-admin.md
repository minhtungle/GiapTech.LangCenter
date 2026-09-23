# ADR-0002: Frontend dùng React + shadcn-admin (thay vì Blazor)

## Bối cảnh
Backend đã chọn API-first (ASP.NET Core Web API, xem ADR-0001) để có thể tái sử dụng cho mobile app
sau này. Cần chọn công nghệ frontend web phù hợp, ưu tiên tốc độ dựng UI quản trị (dashboard nội bộ),
mật độ thông tin cao, và khả năng tái sử dụng kiến thức sang mobile app.

## Quyết định
Dùng **React + TypeScript**, dựng trên nền **shadcn-admin** (Vite + TailwindCSS + shadcn/ui + Radix UI)
làm bộ khung giao diện quản trị. Component & data layer: TanStack Table (bảng dữ liệu), TanStack Query
(gọi API/cache), React Hook Form + Zod (form/validate), Recharts (biểu đồ), react-i18next (đa ngôn ngữ).

## Phương án đã cân nhắc
- **Blazor WebAssembly** — cân nhắc ban đầu vì giữ nguyên toàn bộ stack C# cho team vốn quen .NET. Loại
  bỏ vì: (1) người dùng chỉ định rõ muốn dùng shadcn-admin (bộ admin template React có sẵn, tối giản,
  mật độ thông tin cao — đúng yêu cầu UI/UX đã đặt ra); (2) hệ sinh thái component/chart cho các nhu cầu
  đặc thù (bảng dữ liệu dày, lịch, biểu đồ) phong phú hơn ở React tại thời điểm quyết định.
- **Tự dựng design system từ đầu (không dùng admin template có sẵn)** — loại bỏ vì tốn thời gian không
  cần thiết cho khu vực công cụ nội bộ, nơi tốc độ thao tác quan trọng hơn bản sắc thương hiệu riêng
  (theo nguyên tắc design system ở `docs/frontend/`).

## Hệ quả
- (+) Tận dụng ngay bộ khung shadcn-admin: tối giản, mật độ thông tin cao, có sẵn nhiều pattern
  dashboard (bảng, form, filter) đúng yêu cầu UI/UX đã đặt ra.
- (+) Dễ tái sử dụng tư duy component sang React Native nếu làm mobile app native sau này.
- (−) Team .NET cần thời gian làm quen thêm hệ sinh thái JS/TypeScript (đánh đổi được chấp nhận theo
  yêu cầu tường minh của người phụ trách dự án).
- (−) Hai codebase (backend C#, frontend TypeScript) thay vì một stack duy nhất — cần kỷ luật đồng bộ
  API contract chặt chẽ hơn (xem ADR-0003 về versioning).
