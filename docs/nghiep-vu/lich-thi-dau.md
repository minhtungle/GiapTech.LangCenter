# Module Lịch thi đấu (FR-07 → FR-11)

## FR-07 — Lọc thông tin

Bộ lọc dùng chung với module [Thống kê](./thong-ke.md) (FR-12) — cùng một component, cùng một tập tham số:

| Tiêu chí | Kiểu |
|---|---|
| Thời gian | Khoảng từ ngày → đến ngày |
| Kết quả | Thắng / Hòa / Thua (chọn nhiều) |
| Số bàn thắng | Khoảng giá trị |
| Số bàn thua | Khoảng giá trị |

## FR-08 — Danh sách trận đấu

Hai chế độ hiển thị, **chuyển đổi qua lại** được, giữ nguyên bộ lọc đang áp dụng:

- **Calendar** — trận đấu theo lịch tháng/tuần, màu theo kết quả (xanh=thắng, vàng=hòa, đỏ=thua).
- **Datatable** — bảng mật độ dòng gọn, lọc/sort ngay trên tiêu đề cột (TanStack Table).

## FR-09 — Chấp nhận lời mời từ đối thủ

1. Xem danh sách lời mời giao hữu từ đối thủ (kèm thời gian đề xuất).
2. **Chấp nhận** → tự sinh trận đấu mới ở trạng thái *"đã lên lịch"*, lời mời chuyển sang *đã chấp nhận*.
3. **Từ chối** → đóng lời mời, không sinh trận.

Bảng liên quan: `LOI_MOI_DOI_THU`, `DOI_THU`, `TRAN_DAU`.

## FR-10 — Thêm / Cập nhật trận đấu

Mở **view riêng theo ID trận**, tổ chức thành **wizard 3 bước = 3 tab**, cho phép **lưu nháp giữa chừng**.

### Tab (a) — Thông tin chung

Thời gian, đối thủ (ID đội), danh sách thành viên tham gia, sắp xếp đội hình, ghi chú.

### Tab (b) — Sơ đồ đội hình / Chiến thuật

- Chọn cầu thủ từ danh sách thành viên đã khai ở tab (a).
- **Vẽ sơ đồ kéo-thả** (tham khảo `athletepath.com/soccer-formation-creator`).
- Ghi chú chiến thuật.
- Lưu dưới dạng JSON vào `SODO_CHIENTHUAT.so_do_json` (quan hệ **1—1** với trận đấu).
- Thao tác phức tạp → **ưu tiên desktop**.

### Tab (c) — Đánh giá sau trận

- Gắn **link video** (Youtube/Drive) — hệ thống **chỉ lưu link, không lưu file video**.
- Nhận xét chung cho cả trận.
- Nhận xét **từng cầu thủ**: chỉ số kỹ năng (gồm số bàn ghi được / số bàn cứu thua), hiển thị biểu đồ,
  ghi chú.
- **Thả tim vote MVP** — mỗi người **tối đa 1 tim / trận**.

> **Ràng buộc bắt buộc:** `UNIQUE(tran_dau_id, nguoi_vote_id)` ở tầng **database**, không chỉ chặn ở UI.
> Đây là quy tắc bất di bất dịch #8 trong `CLAUDE.md`.

## FR-11 — Xóa trận đấu

Xóa trận + toàn bộ dữ liệu liên quan: đội hình (`DOIHINH_TRANDAU`), sơ đồ chiến thuật
(`SODO_CHIENTHUAT`), đánh giá (`DANHGIA_CAUTHU`), vote MVP (`VOTE_MVP`).

### Khuyến nghị

- Chỉ cho **xóa cứng** trận ở trạng thái *"đã lên lịch"*.
- Trận *"đã diễn ra"* nên **archive** thay vì xóa — dữ liệu đã diễn ra là đầu vào của thống kê (FR-13,
  FR-14); xóa cứng sẽ làm sai lệch lịch sử.

## Tham chiếu

- Bảng `TRAN_DAU`, `DOIHINH_TRANDAU`, `SODO_CHIENTHUAT`, `DANHGIA_CAUTHU`, `VOTE_MVP`, `DOI_THU`,
  `LOI_MOI_DOI_THU` — xem [ERD](../database/erd.md).
