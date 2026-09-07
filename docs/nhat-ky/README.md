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

> **Gỡ 08/09/2026:** sáu nhật ký ngày 16–21/08 mô tả nghiệp vụ của **dự án tiền thân**
> (`GiapTech.SoccerRoom` — sàn đối thủ, quỹ CLB, đăng ký đá trận qua link). Chúng không còn liên
> quan tới dự án này và gây hiểu nhầm khi đọc; nội dung đầy đủ còn trong git history.
>
> Những bài học **vẫn còn hiệu lực** đã gộp vào
> [bài học từ dự án tiền thân](./2026-08-bai-hoc-du-an-tien-than.md) — code hiện tại còn dẫn
> chiếu tới chúng (quy tắc #1, hạn mức phải kiểm được bằng con số, chốt còn người quản trị cuối).

| Ngày | Nội dung chính |
|---|---|
| [2026-09-08](./2026-09-08-ba-he-thong.md) | **Ba hệ thống con HRM · CRM · LMS** — bộ chuyển, sidebar lọc theo hệ thống, tab phân quyền; nhóm chức năng dùng chung |
| [2026-09-07 (nhật ký)](./2026-09-07-xac-nhan-va-nhat-ky.md) | **Xác nhận mọi thao tác** + **nhật ký hệ thống** (FR-16) — vá 2 lỗi: ChangeTracker rỗng, mật khẩu lộ |
| [2026-09-07 (lịch)](./2026-09-07-lich-calendar.md) | **View calendar** bằng FullCalendar 6 + `GET /toi/cau-hinh` cho múi giờ trung tâm |
| [2026-09-07 (buổi học)](./2026-09-07-bo-sung-buoi-hoc.md) | **Bổ sung buổi** vào lịch đã có + **khoá buổi đã chốt** — vá 2 lỗ hổng sửa/huỷ buổi đã chốt |
| [2026-09-07 (quyền UI)](./2026-09-07-an-menu-theo-quyen.md) | **Ẩn menu/nút theo quyền** — endpoint `/toi/quyen` + hook `useQuyen`, đóng nợ N2 |
| [2026-09-07 (bảo mật)](./2026-09-07-ro-ri-hoc-phi.md) | **Rò rỉ học phí** qua DTO module lớp học — giáo viên thấy mức miễn giảm, học viên thấy học phí bạn cùng lớp |
| [2026-09-07 (buổi)](./2026-09-07-chi-tiet-buoi-hoc.md) | **View chi tiết buổi học 5 tab** tại `/buoi-hoc/:id` + **nhận xét hai chiều**; xoá buổi có nhận xét từng trả 500 |
| [2026-09-07 (UI)](./2026-09-07-menu-thao-tac-va-chi-tiet-lop.md) | **Menu thao tác trong bảng** + **view chi tiết lớp học 6 tab** tại `/lop-hoc/:id` |
| [2026-09-07](./2026-09-07-tach-nguoi-dung-tai-khoan.md) | **Tách người dùng khỏi tài khoản** — 3 bảng hồ sơ vai trò; migration viết tay giữ mật khẩu; vá cổng quyền và lọc vai trò |
| [2026-09-06](./2026-09-06.md) | **LMS giai đoạn 0** — vá base (HoTen, ILuuTruTep), 16 chức năng (nay 20), 4 nhóm quyền |
| [2026-09-05 (gd4)](./2026-09-05-gd4-hoc-phi.md) | **LMS giai đoạn 4** — học phí: công nợ tính động, phạm vi tiền tách khỏi phạm vi lớp; dọn dứt điểm `docs/` |
| [2026-09-05 (gd1–3)](./2026-09-05-gd1-3-nghiep-vu-cot-loi.md) | **LMS giai đoạn 1–3** — lớp học, sinh lịch + điểm danh hai nguồn, học liệu & tệp đính kèm |
| [2026-09-05](./2026-09-05.md) | **Tách base cho dự án LMS** — bỏ nghiệp vụ bóng đá, đổi tên, 4 lỗi tìm ra |
| [16–21/08](./2026-08-bai-hoc-du-an-tien-than.md) | **Bài học từ dự án tiền thân** — quy tắc #1, rate limit, chốt còn người quản trị |
