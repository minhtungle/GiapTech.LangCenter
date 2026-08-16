# ADR-0001: Lựa chọn công nghệ nền (Backend + Database)

## Bối cảnh
Cần chọn công nghệ backend và cơ sở dữ liệu cho hệ thống quản lý CLB đá bóng multi-tenant, triển khai
ban đầu trên 1 VPS chi phí thấp, đội ngũ phát triển quen thuộc .NET.

## Quyết định
- Backend: **ASP.NET Core Web API (.NET 8 LTS)**, kiến trúc Clean Architecture 4 lớp
  (Domain/Application/Infrastructure/API), CQRS qua MediatR.
- Database: **PostgreSQL**, mô hình multi-tenant shared-schema (mọi bảng nghiệp vụ có cột `tenant_id`,
  áp dụng EF Core Global Query Filter).
- ORM: **Entity Framework Core** (Code-First).

## Phương án đã cân nhắc
- **SQL Server** — loại bỏ vì phát sinh chi phí license ngoài bản Express (giới hạn 10GB DB), trong khi
  PostgreSQL miễn phí hoàn toàn và được .NET hỗ trợ đầy đủ qua Npgsql.
- **Schema-per-tenant** (mỗi CLB 1 schema riêng trong cùng DB) — loại bỏ ở giai đoạn MVP vì phức tạp hoá
  migration không cần thiết khi số lượng tenant còn nhỏ; cân nhắc lại nếu số tenant tăng mạnh.
- **Node.js/NestJS** — loại bỏ vì đội ngũ không quen thuộc, chi phí học tập không cần thiết khi .NET đáp
  ứng đủ yêu cầu hiệu năng cho quy mô này.

## Hệ quả
- (+) Chi phí hạ tầng thấp (không license DB), team triển khai nhanh nhờ quen thuộc .NET.
- (+) Global Query Filter giảm rủi ro rò rỉ dữ liệu chéo tenant do lỗi lập trình.
- (−) Shared-schema multi-tenant giới hạn khả năng tuỳ biến cấu trúc dữ liệu riêng cho từng CLB (nếu về
  sau có nhu cầu này, cần ADR mới đánh giá lại).
- (−) Khi số tenant lớn, cần đánh giá lại chiến lược partition dữ liệu (xem lộ trình mở rộng ở
  `docs/kien-truc/TONG-QUAN-KIEN-TRUC.md`).
