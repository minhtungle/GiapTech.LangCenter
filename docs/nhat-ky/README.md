# Nhật ký tiến độ

Mỗi ngày làm việc một file: `YYYY-MM-DD.md`.

## Mục đích

Ghi lại **bối cảnh mà git log không có**: vì sao chọn phương án này thay vì phương án kia, thử gì
thất bại, phát hiện gì ngoài dự kiến. Diff nói "làm gì", commit message nói "vì sao ở mức thay đổi
đó — nhật ký nói vì sao ở mức **ngày làm việc**.

## Cấu trúc một mục

```markdown
# YYYY-MM-DD

## Đã làm
Ngắn gọn, kèm mã FR hoặc số commit.

## Quyết định
Chọn gì, loại gì, vì sao. Quyết định lớn/khó đảo ngược → viết ADR, ở đây chỉ ghi con trỏ.

## Vướng mắc & phát hiện
Lỗi đã bắt được, thứ tưởng đúng mà sai, giới hạn chưa gỡ được.

## Việc kế tiếp
Định làm gì phiên sau.
```

## Quy ước

- Ghi **cuối mỗi đợt việc**, ngay sau khi commit — không dồn cuối tuần rồi viết lại từ trí nhớ.
- Không lặp nội dung đã có ở `docs/` khác; nếu một quyết định đủ lớn thì viết
  [ADR](../kien-truc/adr/) và ở đây chỉ dẫn link.
- Ghi cả thứ **chưa xong** và **chưa kiểm chứng được** — đó thường là thông tin có giá trị nhất khi
  đọc lại.

## Mục lục

> Các ngày 16–21/08 thuộc **dự án cũ** (quản lý CLB đá bóng). Giữ lại vì phần lớn bài học là về
> tầng hệ thống và vẫn còn đúng — đặc biệt sự cố quy tắc #1 ngày 16/08 và rate limit ngày 21/08.

| Ngày | Nội dung chính |
|---|---|
| [2026-09-07 (quyền UI)](./2026-09-07-an-menu-theo-quyen.md) | **Ẩn menu/nút theo quyền** — endpoint `/toi/quyen` + hook `useQuyen`, đóng nợ N2 |
| [2026-09-07 (bảo mật)](./2026-09-07-ro-ri-hoc-phi.md) | **Rò rỉ học phí** qua DTO module lớp học — giáo viên thấy mức miễn giảm, học viên thấy học phí bạn cùng lớp |
| [2026-09-07 (UI)](./2026-09-07-menu-thao-tac-va-chi-tiet-lop.md) | **Menu thao tác trong bảng** + **view chi tiết lớp học 6 tab** tại `/lop-hoc/:id` |
| [2026-09-07](./2026-09-07-tach-nguoi-dung-tai-khoan.md) | **Tách người dùng khỏi tài khoản** — 3 bảng hồ sơ vai trò; migration viết tay giữ mật khẩu; vá cổng quyền và lọc vai trò |
| [2026-09-05 (gd4)](./2026-09-05-gd4-hoc-phi.md) | **LMS giai đoạn 4** — học phí: công nợ tính động, phạm vi tiền tách khỏi phạm vi lớp; dọn dứt điểm `docs/` |
| [2026-09-05 (gd1–3)](./2026-09-05-gd1-3-nghiep-vu-cot-loi.md) | **LMS giai đoạn 1–3** — lớp học, sinh lịch + điểm danh hai nguồn, học liệu & tệp đính kèm |
| [2026-09-06](./2026-09-06.md) | **LMS giai đoạn 0** — vá base (HoTen, ILuuTruTep), 16 chức năng, 4 nhóm quyền |
| [2026-09-05](./2026-09-05.md) | **Tách base cho dự án LMS** — bỏ nghiệp vụ bóng đá, đổi tên, 4 lỗi tìm ra |
| [2026-08-21](./2026-08-21.md) | Rate limit (nợ N3) + FR-19 đăng ký đá trận qua link/QR |
| [2026-08-20](./2026-08-20.md) | Hoàn tác khoản đã thu |
| [2026-08-19](./2026-08-19.md) | Thông tin chuyển khoản quỹ |
| [2026-08-18](./2026-08-18.md) | Sàn đối thủ: mở đúng thứ hôm qua vừa chặn |
| [2026-08-17](./2026-08-17.md) | Chọn đối thủ: tạo tại chỗ + tra mã CLB khác |
| [2026-08-16](./2026-08-16.md) | Khởi tạo dự án → xác thực + cụm quản trị chạy thật trên PostgreSQL |
