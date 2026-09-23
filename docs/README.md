# Tài liệu GiapTech.LangCenter

Hệ thống quản lý trung tâm ngoại ngữ, **multi-tenant**, ba hệ thống con HRM · CRM · LMS
(sắp có LDP — landing page). ASP.NET Core 8 + PostgreSQL + React.

## Đọc theo mục đích

| Anh đang cần gì | Đọc theo thứ tự này |
|---|---|
| **Hiểu hệ thống này làm gì** | [tong-thuat](./01-tong-quan/tong-thuat.md) → [nghiep-vu/](./06-nghiep-vu/README.md) |
| **Bắt đầu viết code** | [CLAUDE.md](../CLAUDE.md) → [quy-uoc-code](./08-quy-uoc/quy-uoc-code.md) → [clean-architecture](./03-backend/clean-architecture.md) |
| **Thêm một chức năng mới** | [cqrs-mediatr](./03-backend/cqrs-mediatr.md) → [phan-quyen-dong](./03-backend/phan-quyen-dong.md) → [quy-uoc-migration](./05-database/quy-uoc-migration.md) |
| **Hiểu vì sao làm thế này** | [tong-quan-kien-truc](./02-kien-truc/tong-quan-kien-truc.md) → [adr/](./02-kien-truc/adr/) |
| **Triển khai lên VPS** | [07-ha-tang/](./07-ha-tang/README.md) → [trien-khai-pull-code](./07-ha-tang/trien-khai-pull-code.md) |
| **Xây một hệ thống tương tự** | [09-cam-nang/](./09-cam-nang/README.md) ← viết riêng cho việc này |
| **Biết hôm đó đã xảy ra gì** | [nhat-ky/](./nhat-ky/README.md) |

## Cấu trúc

```
docs/
├── 01-tong-quan/   tong-thuat · ke-hoach (tiến độ, nợ kỹ thuật) · thuat-ngu
├── 02-kien-truc/   tong-quan-kien-truc · adr/ (9 quyết định đã chốt)
├── 03-backend/     clean-architecture · cqrs-mediatr · multi-tenant · phan-quyen-dong
├── 04-frontend/    ui-ux-nguyen-tac · design-tokens · da-ngon-ngu
├── 05-database/    erd (45 bảng) · quy-uoc-migration
├── 06-nghiep-vu/   FR-01 → FR-30 theo module
├── 07-ha-tang/     cai-dat-vps · trien-khai · runbook · bien-moi-truong
├── 08-quy-uoc/     quy-uoc-code · huong-dan-su-dung
├── 09-cam-nang/    rút tỉa cho dự án khác — đọc được độc lập
└── nhat-ky/        theo ngày, giữ bối cảnh mà git log không có
```

Số thứ tự là **thứ tự đọc gợi ý**, không phải mức độ quan trọng. `nhat-ky/` không đánh số vì
tên theo ngày đã tự sắp xếp.

## Ba quy tắc về chính tài liệu

1. **Đổi schema hoặc API → cập nhật tài liệu trong cùng PR.** Không có "làm sau" (quy tắc #4
   trong [CLAUDE.md](../CLAUDE.md)).
2. **Quyết định kiến trúc lớn → ADR mới**, không sửa đè ADR cũ. ADR sai thì viết ADR mới thay
   thế và ghi rõ nó thay cái nào — lịch sử quyết định có giá trị riêng.
3. **Chạy `python3 scripts/check-doc-links.py`** sau mỗi đợt sửa nhiều file. Nó **không** quét
   liên kết trong mã nguồn (`.cs`/`.ts`) — chỗ đó phải tự kiểm.

## Tài liệu nào là nguồn chân lý

Khi mâu thuẫn: **tài liệu trong `docs/` thắng `CLAUDE.md`**. `CLAUDE.md` là bản đồ dẫn đường và
quy tắc bắt buộc, không phải nguồn chân lý về chi tiết.

Khi tài liệu mâu thuẫn với mã nguồn: **mã nguồn thắng, và tài liệu phải sửa ngay**. Nếu anh gặp
chỗ lệch, đó là lỗi cần vá chứ không phải điều bình thường.
