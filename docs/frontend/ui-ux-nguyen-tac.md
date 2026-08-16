# Nguyên tắc UI/UX (bắt buộc khi code frontend)

## 1. Tối giản, mật độ thông tin cao

- **Bảng màu**: 1 màu chủ đạo + 1 màu nhấn + màu trạng thái cố định. Xem
  [design-tokens.md](./design-tokens.md).
- **Datatable**: mật độ dòng gọn (compact density), lọc/sort **ngay trên tiêu đề cột**, không tách bộ lọc
  ra panel riêng cho các trường hợp đơn giản.
- **Trang thống kê**: KPI card gọn ở đầu → biểu đồ chi tiết bên dưới.
- Tùy chọn nâng cao **ẩn dưới "Xem thêm"**, không bày hết ra màn hình chính.

## 2. Luồng dễ hiểu

- **Sidebar cố định** theo 5 module + **breadcrumb** ở mọi trang con.
- **Thêm/Cập nhật trận đấu** = wizard 3 bước, đúng 3 tab đã đặc tả ở
  [FR-10](../nghiep-vu/lich-thi-dau.md#fr-10--thêm--cập-nhật-trận-đấu). **Cho phép lưu nháp giữa chừng** —
  người dùng không bị mất dữ liệu khi rời tab.
- **Tạo tài khoản** = wizard tuần tự đúng quy trình
  [FR-03](../nghiep-vu/quan-tri-he-thong.md#quy-trình-chuẩn-tạo-tài-khoản-wizard-tuần-tự): hồ sơ cầu thủ
  → nhóm quyền → tài khoản.
- **Empty-state** luôn có nút hành động + hướng dẫn ngắn, không để màn hình trắng.
- **Toast** nhất quán vị trí/thời gian. **Không dùng `alert()`** hay modal chặn luồng cho thông báo
  thông thường.

## 3. Thiết bị mục tiêu

| Nhóm thao tác | Ưu tiên | Lý do |
|---|---|---|
| Vẽ sơ đồ chiến thuật (FR-10 tab b), ma trận phân quyền (FR-05) | **Desktop** | Thao tác kéo-thả, bảng nhiều chiều |
| Xem lịch, vote MVP, xem tiến độ quỹ (Player) | **Responsive tốt** | Sẽ tái dùng cho mobile app qua cùng API |

## 4. Quy ước màu trạng thái

Dùng **thống nhất** ở mọi module, không đổi nghĩa theo ngữ cảnh:

| Màu | Ý nghĩa |
|---|---|
| 🟢 Xanh | Thắng · Đã đóng đủ quỹ |
| 🔴 Đỏ | Thua · Quá hạn đóng quỹ |
| 🟡 Vàng | Hòa · Đang chờ |

Áp dụng cả ở Calendar (FR-08), Datatable (FR-08), biểu đồ (FR-13) và danh sách quỹ (FR-15).

## 5. Đa ngôn ngữ

Mọi chuỗi hiển thị đi qua `react-i18next`. Message lỗi từ API là **mã lỗi**, frontend tra bảng dịch —
xem [cqrs-mediatr.md](../backend/cqrs-mediatr.md#trả-lỗi). Không hard-code tiếng Việt trong component.
