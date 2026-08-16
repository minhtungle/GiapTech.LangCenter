# Frontend

React + TypeScript trên nền **shadcn-admin** (Vite + TailwindCSS + shadcn/ui + Radix UI) — xem
[ADR-0002](../kien-truc/adr/0002-frontend-shadcn-admin.md).

| Nội dung | Tài liệu |
|---|---|
| Nguyên tắc UI/UX bắt buộc khi code màn hình | [ui-ux-nguyen-tac.md](./ui-ux-nguyen-tac.md) |
| Design token (màu/chữ/khoảng cách) → biến shadcn | [design-tokens.md](./design-tokens.md) |

## Thư viện

| Nhu cầu | Thư viện |
|---|---|
| Bảng dữ liệu | TanStack Table |
| Gọi API / cache | TanStack Query |
| Form + validate | React Hook Form + Zod |
| Biểu đồ | Recharts |
| Đa ngôn ngữ | react-i18next (JSON namespace, lazy-load, lưu lựa chọn ở `localStorage`) |

## Quy tắc

1. **API trả mã lỗi, frontend dịch** — mọi message hiển thị đi qua `react-i18next`, không hard-code
   chuỗi tiếng Việt trong component.
2. Ưu tiên **httpOnly cookie** cho token khi khả thi; hạn chế lưu token trong `localStorage`
   (xem [SECURITY.md](../../SECURITY.md)).
3. Trang style-guide trực quan (Storybook hoặc HTML tĩnh) là **nguồn tham chiếu duy nhất** khi code từng
   màn hình — dựng trước khi code màn hình nghiệp vụ đầu tiên.
