# Tổng thuật — đọc 1 mạch trước khi đào sâu

Hệ thống Quản lý Câu lạc bộ đá bóng là ứng dụng web **multi-tenant**: mỗi CLB đăng ký là 1 tenant độc
lập, dữ liệu cách ly hoàn toàn theo `tenant_id`. Đăng nhập bằng bộ ba **{ID đội, tên đăng nhập, mật
khẩu}**.

## 5 nhóm chức năng

| # | Module | Nội dung | Mã FR |
|---|---|---|---|
| 1 | [Đăng nhập](./nghiep-vu/dang-nhap.md) | Xác thực theo tenant, quên mật khẩu | FR-01 → FR-02 |
| 2 | [Lịch thi đấu](./nghiep-vu/lich-thi-dau.md) | Tạo/sửa/xóa trận, đội hình & sơ đồ chiến thuật, đánh giá sau trận (chỉ số kỹ năng, video, vote MVP bằng tim — mỗi người 1 tim/trận), chấp nhận lời mời đối thủ | FR-07 → FR-11 |
| 3 | [Thống kê](./nghiep-vu/thong-ke.md) | Biểu đồ diễn biến thắng/thua, bảng xếp hạng MVP theo 4 tiêu chí | FR-12 → FR-14 |
| 4 | [Tài chính](./nghiep-vu/tai-chinh.md) | Quản lý quỹ đội, tiến độ đóng góp, nhắc nhở qua SMS/Email | FR-15 → FR-16 |
| 5 | [Quản trị hệ thống](./nghiep-vu/quan-tri-he-thong.md) | Tài khoản, hồ sơ cầu thủ, phân quyền động theo chức năng/thao tác, thiết lập chung CLB | FR-03 → FR-06 |

## 3 actor

- **Admin** — 1 tài khoản mặc định mỗi tenant (`admin`/`123456`), toàn quyền, duy nhất được đổi mật khẩu
  cho tài khoản khác, bắt buộc đổi mật khẩu ở lần đăng nhập đầu.
- **Manager** — tài khoản được cấp quyền theo từng chức năng/thao tác, thường phụ trách vận hành trận
  đấu / tài chính / thống kê.
- **Player** — tài khoản gắn 1 hồ sơ cầu thủ (0..1); xem lịch/thống kê, vote MVP, xem tiến độ quỹ.

## Điểm cần nắm trước khi code

1. **Cách ly tenant** là quy tắc số một — [multi-tenant.md](./backend/multi-tenant.md).
2. **Phân quyền đọc động từ DB**, không dùng role cố định — [phan-quyen-dong.md](./backend/phan-quyen-dong.md).
3. **Vote MVP ràng buộc UNIQUE ở tầng DB**, không chỉ chặn ở UI — [ERD](./database/erd.md#ràng-buộc-nghiệp-vụ-quan-trọng).
4. **API trả mã lỗi**, frontend dịch qua `react-i18next` — [cqrs-mediatr.md](./backend/cqrs-mediatr.md#trả-lỗi).

## Đi tiếp

- Kiến trúc & bản đồ công nghệ: [kien-truc/TONG-QUAN-KIEN-TRUC.md](./kien-truc/TONG-QUAN-KIEN-TRUC.md)
- Quyết định kiến trúc (ADR): [kien-truc/adr/](./kien-truc/adr/)
- Thuật ngữ dễ nhầm: [kien-truc/THUAT-NGU.md](./kien-truc/THUAT-NGU.md)
- Mô hình dữ liệu: [database/erd.md](./database/erd.md)
