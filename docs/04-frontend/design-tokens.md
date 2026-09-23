# Design token

> ✅ **Đã chốt.** Giá trị thật nằm trong `frontend/src/index.css` (biến CSS) và
> `frontend/tailwind.config.js` (ánh xạ sang class Tailwind).
>
> | Token | Giá trị | Vai trò |
> |---|---|---|
> | `--primary` | `152 45% 28%` — xanh lá đậm | Trung tính, đủ tương phản với chữ trắng (WCAG AA) |
> | `--accent` | `28 85% 52%` — cam ấm | Nhấn thứ cấp, dùng tiết chế |
> | `--status-ok` | `152 55% 34%` | Xong · đạt · đủ |
> | `--status-loi` | `0 72% 45%` | Hỏng · quá hạn |
> | `--status-cho` | `38 92% 45%` | Đang chờ · cần chú ý |
>
> Có bản dark mode đầy đủ. Trang style-guide trực quan vẫn **chưa dựng**.

## Nguyên tắc

Ánh xạ token của dự án sang **biến theming của shadcn/ui** (`--background`, `--foreground`, `--primary`,
`--secondary`, `--muted`, `--accent`, `--destructive`, `--border`, `--ring`) — **không** tạo hệ biến
song song, vì mọi component shadcn đã đọc sẵn bộ biến này.

Khai báo trong `index.css` dạng CSS variable, hỗ trợ cả light và dark mode theo cơ chế sẵn có của
shadcn-admin.

## Bảng token

### Màu nền tảng

| Token dự án | Biến shadcn | Vai trò |
|---|---|---|
| Màu chủ đạo | `--primary` | Nút chính, link, tab đang chọn, header |
| Màu nhấn | `--accent` | Nhấn mạnh thứ cấp: badge, highlight hàng đang hover |
| Nền / chữ | `--background` / `--foreground` | Nền trang, chữ mặc định |
| Nền phụ | `--muted` / `--muted-foreground` | Nền bảng xen kẽ, chữ phụ, placeholder |
| Viền | `--border` | Viền bảng, input, card |

### Màu trạng thái (cố định, không đổi nghĩa theo ngữ cảnh)

| Token | Ý nghĩa | Ghi chú |
|---|---|---|
| `--status-ok` | Xong · đạt · đủ (lớp đang học, buổi đã chốt, học phí đã đủ, thao tác thành công) | Xanh |
| `--status-loi` | Hỏng · quá hạn · bị từ chối (lớp đã huỷ, nộp muộn, học phí quá hạn) | Đỏ — dùng chung `--destructive` của shadcn |
| `--status-cho` | Đang chờ · cần chú ý (lớp nháp, còn nợ, buộc đổi mật khẩu) | Vàng |

Ba tên này **đổi từ `win`/`lose`/`draw` ngày 08/09/2026** (nợ N8 — đã xong): tên cũ là di sản
của dự án tiền thân và đọc lên gây hiểu sai, vì nghiệp vụ LMS không có "thắng/thua/hoà" nào.
`Badge` dùng cùng ba tên: `variant="ok" | "loi" | "cho"`.

Ba token này là **quy ước xuyên suốt** — xem
[ui-ux-nguyen-tac.md](ui-ux-nguyen-tac.md#5-quy-ước-màu-trạng-thái).

## Quy tắc sử dụng

1. **Không dùng màu hex trực tiếp** trong component. Luôn qua token / class Tailwind ánh xạ token.
2. Màu trạng thái **không** dùng cho mục đích trang trí — người dùng đọc màu để hiểu trạng thái
   lớp học, buổi học, bài nộp và công nợ học phí.
3. Không chỉ dùng màu để truyền tải thông tin — kèm nhãn chữ hoặc icon (người dùng mù màu, in đen trắng).

## Typography & khoảng cách

Giữ nguyên thang mặc định của Tailwind + shadcn-admin. Chỉ đặt token riêng khi có nhu cầu thực tế —
mật độ thông tin cao ưu tiên `text-sm` cho nội dung bảng, tránh tăng cỡ chữ toàn cục.

## Trang style-guide

Dựng trang style-guide trực quan (Storybook hoặc HTML tĩnh) hiển thị toàn bộ token + component đã tùy
biến. Đây là **nguồn tham chiếu duy nhất** khi code màn hình — mọi thắc mắc về màu/khoảng cách tra ở đây
thay vì đọc lại code.
