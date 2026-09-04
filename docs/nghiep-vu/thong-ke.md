# Module Thống kê (FR-12 → FR-14)

> ⚠️ **Tài liệu của DỰ ÁN CŨ** (quản lý CLB đá bóng phong trào). Từ 05/09/2026 repo này là
> base cho hệ thống quản lý trung tâm ngoại ngữ — **phần nghiệp vụ dưới đây không còn trong
> code**. Giữ lại để tham khảo cách viết đặc tả. Xem [`CLAUDE.md`](../../CLAUDE.md) mục 1.

## FR-12 — Lọc thông tin

**Dùng chung bộ lọc với module [Lịch thi đấu](./lich-thi-dau.md)** (FR-07) — cùng component, cùng tập
tham số (thời gian, kết quả, số bàn thắng, số bàn thua). Không dựng hai bộ lọc song song.

## FR-13 — Biểu đồ diễn biến

Xu hướng **bàn thắng / bàn thua** qua các trận theo thời gian.

- Thư viện: Recharts.
- **Click vào một điểm dữ liệu → điều hướng sang chi tiết trận đấu đó** (view FR-10).
- Dữ liệu nguồn: `TRAN_DAU.ty_so_nha`, `TRAN_DAU.ty_so_khach`, `TRAN_DAU.thoi_gian`.

## FR-14 — Bảng xếp hạng MVP

Top cầu thủ theo **4 tiêu chí** (chuyển đổi tiêu chí xếp hạng được).

DTO mang **đủ bốn tiêu chí** trong một lần gọi nên đổi cột xếp hạng không cần gọi lại API —
frontend sắp tại chỗ, số cầu thủ của một CLB chỉ vài chục dòng.

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

## Quyết định triển khai

**Một endpoint trả cả ba phần** (`POST /thong-ke`) thay vì ba endpoint riêng: cả ba đọc cùng
một tập trận đã lọc, gọi ba lần là ba lần quét lại cùng dữ liệu — và ba lần đó có thể rơi vào
hai trạng thái DB khác nhau nếu ai đó vừa nhập kết quả.

Dùng **POST cho một endpoint đọc**: bộ lọc là object lồng (danh sách kết quả, danh sách trạng
thái) mà querystring diễn đạt vụng — cùng lý do và cùng kiểu body với `POST /tran-dau/tim-kiem`.

### Cách ly tenant

Mọi truy vấn đi qua `db.TranDaus` / `db.DanhGiaCauThus` / `db.VoteMvps` nên Global Query Filter
tự lọc. **Không dùng raw SQL** — đó là chỗ dễ quên `tenant_id` nhất (xem
[multi-tenant](../backend/multi-tenant.md)) và rò rỉ chéo CLB là lỗi nghiêm trọng nhất hệ thống
có thể mắc (quy tắc #2).

Canh bởi `ThongKe_cach_ly_theo_tenant`, kiểm cả ba phần. Kiểm chứng bằng phản chứng: thêm
`IgnoreQueryFilters()` vào truy vấn trận thì test đỏ ngay.

### Bốn quy tắc tính toán

- **Tỷ lệ thắng chia cho số trận ĐÃ ĐÁ**, không phải tổng trận: lên lịch 10 trận mới đá 2 mà
  thắng cả 2 thì tỷ lệ là 100%, không phải 20%. Canh bởi `Ty_le_thang_tinh_tren_tran_da_da`.
- **Biểu đồ bỏ qua trận chưa đá**: vẽ thành điểm 0-0 sẽ kéo đường xu hướng xuống, trông như
  đội vừa thua liên tiếp.
- **Điểm kỹ năng là trung bình trên các trận CÓ CHẤM**, không tính trận chưa chấm là 0 điểm:
  chấm 8 điểm một trận rồi bỏ chấm trận sau không có nghĩa là 4 điểm. Chưa chấm lần nào thì
  trả `null`, không phải 0 — hai thứ khác nhau.
- **Cầu thủ có mặt trong đội hình nhưng chưa được đánh giá vẫn lên bảng**: không thì người đá
  đủ mười trận mà chưa ai chấm bị coi như không tồn tại.

JSON chỉ số hỏng chỉ bỏ qua bản ghi đó, không làm sập cả bảng xếp hạng.

### Đọc chỉ số kỹ năng ở tầng ứng dụng

`DANHGIA_CAUTHU.chi_so_ky_nang` là JSON; truy vấn nó trong LINQ cần hàm riêng của Npgsql mà
`Application` không được phụ thuộc provider (xem `LuatPhuThuocTests`). Số bản ghi đánh giá của
một CLB phong trào nhỏ nên tải về xử lý là chấp nhận được.

### Chưa làm

Cache ngắn hạn (Redis) cho bảng xếp hạng — chỉ cần khi số trận lớn, hiện chưa tới ngưỡng đó.

## Tham chiếu

- Bảng `TRAN_DAU`, `DANHGIA_CAUTHU`, `VOTE_MVP`, `CAU_THU` — xem [ERD](../database/erd.md).
