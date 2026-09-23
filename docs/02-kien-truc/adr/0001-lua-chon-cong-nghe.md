# ADR-0001: Lựa chọn công nghệ nền (Backend + Database)

## Bối cảnh
Cần chọn công nghệ backend và cơ sở dữ liệu cho **hệ thống quản lý trung tâm ngoại ngữ**
multi-tenant (mỗi trung tâm là một tenant độc lập), triển khai ban đầu trên 1 VPS chi phí thấp,
đội ngũ phát triển quen thuộc .NET.

> Quyết định này được chốt 16/08/2026 cho một dự án khác trên cùng nền tảng, và **giữ nguyên hiệu
> lực** khi repo chuyển thành LMS (05/09/2026) — toàn bộ tầng hệ thống dùng lại được. Bối cảnh
> trên đã viết lại theo dự án hiện tại; lý do kỹ thuật không đổi.

## Quyết định
- Backend: **ASP.NET Core Web API (.NET 8 LTS)**, kiến trúc Clean Architecture 4 lớp
  (Domain/Application/Infrastructure/API), CQRS qua MediatR.
- Database: **PostgreSQL**, mô hình multi-tenant shared-schema (mọi bảng nghiệp vụ có cột `tenant_id`,
  áp dụng EF Core Global Query Filter).
- ORM: **Entity Framework Core** (Code-First).

## Phương án đã cân nhắc
- **SQL Server** — loại bỏ vì phát sinh chi phí license ngoài bản Express (giới hạn 10GB DB), trong khi
  PostgreSQL miễn phí hoàn toàn và được .NET hỗ trợ đầy đủ qua Npgsql.
- **Schema-per-tenant** (mỗi trung tâm 1 schema riêng trong cùng DB) — loại bỏ ở giai đoạn MVP vì phức tạp hoá
  migration không cần thiết khi số lượng tenant còn nhỏ; cân nhắc lại nếu số tenant tăng mạnh.
- **Node.js/NestJS** — loại bỏ vì đội ngũ không quen thuộc, chi phí học tập không cần thiết khi .NET đáp
  ứng đủ yêu cầu hiệu năng cho quy mô này.

## Hệ quả
- (+) Chi phí hạ tầng thấp (không license DB), team triển khai nhanh nhờ quen thuộc .NET.
- (+) Global Query Filter giảm rủi ro rò rỉ dữ liệu chéo tenant do lỗi lập trình.
- (−) Shared-schema multi-tenant giới hạn khả năng tuỳ biến cấu trúc dữ liệu riêng cho từng trung tâm (nếu về
  sau có nhu cầu này, cần ADR mới đánh giá lại).
- (−) Khi số tenant lớn, cần đánh giá lại chiến lược partition dữ liệu (xem lộ trình mở rộng ở
  `docs/02-kien-truc/tong-quan-kien-truc.md`).
