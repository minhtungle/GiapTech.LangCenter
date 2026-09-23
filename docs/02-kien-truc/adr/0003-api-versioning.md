# ADR-0003: Thiết kế phiên bản API (URL segment versioning)

## Bối cảnh
Frontend (React) và backend (.NET) tách rời hoàn toàn (ADR-0002); API sẽ được tái sử dụng cho mobile
app sau này. Cần chiến lược versioning để tránh phá vỡ client đang chạy khi có thay đổi hợp đồng API.

## Quyết định
- Dùng thư viện **Asp.Versioning.Mvc**.
- Versioning theo **URL segment**: `/api/v1/matches`, `/api/v2/matches`.
- Mỗi version có Swagger/OpenAPI document riêng (`/swagger/v1/swagger.json`, ...).
- Controller tổ chức theo thư mục version: `Controllers/V1/`, `Controllers/V2/`.
- Tăng version khi có breaking change (đổi/xóa field, đổi kiểu dữ liệu, đổi hành vi mặc định); không
  tăng version khi chỉ thêm field/endpoint mới hoặc sửa lỗi không đổi hợp đồng.
- Version cũ khi deprecate: đánh dấu qua header `Sunset`/`Deprecation`, thông báo trước cho client một
  khoảng thời gian trước khi gỡ bỏ.

## Phương án đã cân nhắc
- **Versioning qua HTTP header** (vd `Api-Version: 1.0`) — loại bỏ vì khó debug bằng mắt thường hơn, khó
  cache ở tầng Caddy/CDN so với URL segment.
- **Versioning qua query string** (vd `?api-version=1.0`) — loại bỏ vì dễ bị quên khi client tự ý bỏ qua
  param, không rõ ràng bằng URL segment khi đọc log.
- **Không versioning, chỉ thêm field optional mãi mãi** — loại bỏ vì không bền vững về lâu dài khi cần
  đổi hành vi mặc định hoặc cấu trúc response.

## Hệ quả
- (+) Client cũ (kể cả mobile app sau này) không bị phá vỡ khi backend phát triển thêm tính năng.
- (+) Dễ đọc, dễ debug, dễ cache theo path.
- (−) Cần kỷ luật rà soát Application layer (CQRS) để tránh trùng lặp Command/Query giữa các version khi
  không thực sự cần thiết (xem `docs/backend/`).
