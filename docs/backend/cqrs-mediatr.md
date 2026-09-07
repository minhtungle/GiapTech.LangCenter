# CQRS + MediatR

## Nguyên tắc

- **Mỗi FR-xx = một hoặc vài Command/Query riêng.** Không gom nhiều chức năng vào một handler "đa năng".
- **Command** = thay đổi trạng thái (thêm/sửa/xóa). **Query** = chỉ đọc.
- Handler nằm ở lớp `Application`, **không** biết gì về HTTP hay EF Core cụ thể — chỉ dùng interface
  (`IAppDbContext`, `IEmailSender`...). Xem [clean-architecture.md](./clean-architecture.md).

## Tổ chức thư mục

Nhóm theo **module nghiệp vụ**, mỗi use-case một thư mục chứa Command/Query + Handler + Validator + DTO
đặt cạnh nhau:

```
Application/
├── LichThiDau/
│   ├── Commands/
│   │   ├── TaoTranDau/          # FR-10
│   │   ├── CapNhatSoDoChienThuat/  # FR-10 tab (b)
│   │   ├── DanhGiaSauTran/      # FR-10 tab (c)
│   │   ├── VoteMvp/             # FR-10 tab (c)
│   │   ├── XoaTranDau/          # FR-11
│   │   └── ChapNhanLoiMoi/      # FR-09
│   └── Queries/
│       ├── LayDanhSachTranDau/  # FR-07, FR-08
│       └── LayChiTietTranDau/   # FR-10
├── ThongKe/Queries/             # FR-12 → FR-14
├── TaiChinh/                    # FR-15, FR-16
├── QuanTri/                     # FR-03 → FR-06
├── DangNhap/                    # FR-01, FR-02
└── Common/
    ├── Behaviors/               # Validation, Logging, Transaction
    ├── Interfaces/              # IAppDbContext, ICurrentTenant, IEmailSender...
    └── Exceptions/
```

Mỗi handler **ghi rõ mã FR** trong XML doc comment để tra ngược về
[tài liệu nghiệp vụ](../nghiep-vu/README.md).

## Pipeline behavior

Chạy theo thứ tự cho mọi request:

| Behavior | Trách nhiệm |
|---|---|
| `LoggingBehavior` | Ghi log request/response (che dữ liệu nhạy cảm: mật khẩu, token) |
| `ValidationBehavior` | Chạy FluentValidation, ném `ValidationException` → API map sang **mã lỗi** |
| `TransactionBehavior` | Bọc **Command** trong transaction; Query không cần |

## Validation

FluentValidation, một `AbstractValidator<T>` cho mỗi Command/Query có input cần kiểm tra.

Validator **chỉ kiểm tra dữ liệu đầu vào** (bắt buộc, độ dài, khoảng giá trị). Quy tắc nghiệp vụ cần
truy cập DB (ví dụ "học viên này đã ghi danh lớp này chưa") kiểm tra trong **handler**, và vẫn phải có ràng
buộc tương ứng ở **tầng DB** — xem [ràng buộc ERD](../database/erd.md#ràng-buộc-nghiệp-vụ-quan-trọng).

## Trả lỗi

Handler ném exception mang **mã lỗi**, không mang chuỗi text tiếng Việt. Exception middleware ở lớp API
map sang response chuẩn:

```json
{ "errorCode": "MVP_VOTE_DA_TON_TAI", "details": { "tranDauId": "..." } }
```

Frontend dịch mã lỗi qua `react-i18next` — xem [ADR-0002](../kien-truc/adr/0002-frontend-shadcn-admin.md).
Đây là **quy tắc bất di bất dịch #3**: API không hard-code message một ngôn ngữ.

## Quan hệ với API versioning

Controller `V1` và `V2` có thể **dùng chung** Command/Query nếu hợp đồng nghiệp vụ không đổi — chỉ khác
lớp DTO mapping ở tầng API. Chỉ tách handler riêng cho từng version khi **hành vi nghiệp vụ** thực sự
khác nhau, tránh nhân đôi Application layer không cần thiết
(xem hệ quả (−) ở [ADR-0003](../kien-truc/adr/0003-api-versioning.md)).

## Test

- **Unit test** cho handler: mock `IAppDbContext` (hoặc dùng SQLite in-memory), kiểm tra logic nghiệp vụ
  và validation.
- **Integration test** cho endpoint API: bao gồm test cách ly tenant (bắt buộc, xem
  [multi-tenant.md](./multi-tenant.md)) và test phân quyền (tài khoản thiếu quyền → 403).
