# Thuật ngữ & quy ước tên gọi dễ nhầm lẫn

| Thuật ngữ | Ý nghĩa trong dự án này | Lưu ý tránh nhầm lẫn |
|---|---|---|
| Tenant | Một CLB độc lập, dữ liệu cách ly qua `tenant_id` | Không nhầm với "user" hay "organization" trong tài liệu bên thứ ba |
| Quyền (Role) | Nhóm quyền tự định nghĩa theo chức năng + thao tác (bảng `QUYEN`, `QUYEN_CHUC_NANG`) | Khác với `[Authorize(Roles=...)]` cố định kiểu mẫu thông thường của ASP.NET Core — đây là hệ phân quyền động đọc từ DB |
| Hồ sơ cầu thủ (CAU_THU) | Thông tin cầu thủ, độc lập với tài khoản đăng nhập | Một cầu thủ có thể **chưa có** tài khoản; một tài khoản có tối đa 1 hồ sơ cầu thủ liên kết |
| MVP | Most Valuable Player (cầu thủ xuất sắc nhất), **không phải** "Minimum Viable Product" | Ngữ cảnh luôn là bình chọn/thống kê trận đấu |
| FR-xx | Mã chức năng (Functional Requirement) trong tài liệu SRS | Dùng để tham chiếu chéo giữa SRS, ADR, code review |
| v1/v2 (API) | Phiên bản hợp đồng API theo URL segment | Không nhầm với version của ứng dụng (semver trong `CHANGELOG.md`) |
