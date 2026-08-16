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

| Ngày | Nội dung chính |
|---|---|
| [2026-08-16](./2026-08-16.md) | Khởi tạo dự án → xác thực + cụm quản trị chạy thật trên PostgreSQL |
