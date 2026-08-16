# Module Thống kê (FR-12 → FR-14)

## FR-12 — Lọc thông tin

**Dùng chung bộ lọc với module [Lịch thi đấu](./lich-thi-dau.md)** (FR-07) — cùng component, cùng tập
tham số (thời gian, kết quả, số bàn thắng, số bàn thua). Không dựng hai bộ lọc song song.

## FR-13 — Biểu đồ diễn biến

Xu hướng **bàn thắng / bàn thua** qua các trận theo thời gian.

- Thư viện: Recharts.
- **Click vào một điểm dữ liệu → điều hướng sang chi tiết trận đấu đó** (view FR-10).
- Dữ liệu nguồn: `TRAN_DAU.ty_so_nha`, `TRAN_DAU.ty_so_khach`, `TRAN_DAU.thoi_gian`.

## FR-14 — Bảng xếp hạng MVP

Top cầu thủ theo **4 tiêu chí** (chuyển đổi tiêu chí xếp hạng được):

| # | Tiêu chí | Nguồn dữ liệu |
|---|---|---|
| 1 | Lượt vote tim | Đếm `VOTE_MVP` theo `nguoi_duoc_vote_id` |
| 2 | Chỉ số kỹ năng tổng hợp | Tổng hợp `DANHGIA_CAUTHU.chi_so_ky_nang` (JSON) |
| 3 | Số bàn thắng | Tổng `DANHGIA_CAUTHU.so_ban_ghi_duoc` |
| 4 | Số bàn cứu thua | Tổng `DANHGIA_CAUTHU.so_ban_cuu_thua` |

Bảng xếp hạng chịu ảnh hưởng của bộ lọc FR-12 (chỉ tính các trận nằm trong khoảng lọc).

## Bố cục trang

Theo [nguyên tắc UI/UX](../frontend/ui-ux-nguyen-tac.md): **KPI card gọn ở đầu**, biểu đồ chi tiết bên
dưới. Tùy chọn nâng cao ẩn dưới "Xem thêm".

## Lưu ý triển khai

- Truy vấn thống kê là nơi **dễ quên `tenant_id` nhất** vì thường viết dạng aggregate/raw SQL. Global
  Query Filter chỉ áp dụng cho LINQ qua DbSet — xem [multi-tenant](../backend/multi-tenant.md).
- Cân nhắc cache ngắn hạn (Redis) cho bảng xếp hạng nếu số trận lớn.

## Tham chiếu

- Bảng `TRAN_DAU`, `DANHGIA_CAUTHU`, `VOTE_MVP`, `CAU_THU` — xem [ERD](../database/erd.md).
