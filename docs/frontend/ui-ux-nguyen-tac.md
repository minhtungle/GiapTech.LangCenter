# Nguyên tắc UI/UX (bắt buộc khi code frontend)

## 1. Tối giản, mật độ thông tin cao

- **Bảng màu**: 1 màu chủ đạo + 1 màu nhấn + màu trạng thái cố định. Xem
  [design-tokens.md](./design-tokens.md).
- **Datatable**: mật độ dòng gọn (compact density), lọc/sort **ngay trên tiêu đề cột**, không tách bộ lọc
  ra panel riêng cho các trường hợp đơn giản.
- **Trang thống kê**: KPI card gọn ở đầu → biểu đồ chi tiết bên dưới.
- Tùy chọn nâng cao **ẩn dưới "Xem thêm"**, không bày hết ra màn hình chính.

## 2. Luồng dễ hiểu

- **Sidebar** theo 5 module + **breadcrumb** ở mọi trang con. **Thu gọn được** (chỉ còn icon,
  nhớ lựa chọn trong `localStorage`); trên mobile chuyển thành ngăn kéo vì sidebar cố định
  chiếm quá nhiều bề ngang.
- **Thêm/Cập nhật trận đấu** = wizard 3 bước, đúng 3 tab đã đặc tả ở
  [FR-10](../nghiep-vu/lich-thi-dau.md#fr-10--thêm--cập-nhật-trận-đấu). **Cho phép lưu nháp giữa chừng** —
  người dùng không bị mất dữ liệu khi rời tab.
- **Tạo tài khoản** = wizard tuần tự đúng quy trình
  [FR-03](../nghiep-vu/quan-tri-he-thong.md#quy-trình-chuẩn-tạo-tài-khoản-wizard-tuần-tự): hồ sơ cầu thủ
  → nhóm quyền → tài khoản.
- **Empty-state** luôn có nút hành động + hướng dẫn ngắn, không để màn hình trắng.
- **Thêm/cập nhật không cần chuyển view → dùng modal**, không chèn form vào giữa danh sách:
  chèn form đẩy bảng xuống, người dùng mất ngữ cảnh dòng đang thao tác. Modal dùng thẻ
  `<dialog>` của trình duyệt để có sẵn focus trap và Esc.
- **Select có nhiều lựa chọn → dùng `SelectTimKiem`** (chọn một) hoặc **`SelectTimKiemNhieu`**
  (chọn nhiều, hiển thị chip), không dùng `<select>` cơ bản. Tìm kiếm **bỏ dấu tiếng Việt**:
  gõ "nguyen" ra "Nguyễn".
- **Modal chứa dropdown: không đặt `overflow-y-auto` ở thân modal** — nó tạo scroll container
  làm dropdown `position: absolute` bị cắt, người dùng phải cuộn mới thấy hết. Giới hạn chiều
  cao ở chính thẻ `<dialog>` và cho dialog cuộn.
- **Không dùng `prompt()` / `alert()` của trình duyệt** cho nhập liệu — không style được, không
  dịch được, và trông như lỗi trang web.
- **Toast** nhất quán vị trí/thời gian. **Không dùng `alert()`** hay modal chặn luồng cho thông báo
  thông thường.

## 3. Danh tính CLB trên giao diện

Sau khi đăng nhập, mọi chỗ hiển thị CLB dùng **tên đội** làm dòng chính, **mã đội** làm dòng phụ
nhỏ bên dưới. Người dùng nhận ra CLB của mình qua tên; mã 7 ký tự chỉ cần khi đăng nhập hoặc đọc
cho người khác.

Tên đội nằm trong claim `ten_doi` của JWT để sidebar hiển thị được ngay khi tải trang. Đổi tên ở
FR-06 phải gọi `capNhatTenDoi()` — token đang cầm vẫn mang tên cũ tới lần làm mới kế tiếp.

## 4. Thiết bị mục tiêu

| Nhóm thao tác | Ưu tiên | Lý do |
|---|---|---|
| Vẽ sơ đồ chiến thuật (FR-10 tab b), ma trận phân quyền (FR-05) | **Desktop** | Thao tác kéo-thả, bảng nhiều chiều |
| Xem lịch, vote MVP, xem tiến độ quỹ (Player) | **Responsive tốt** | Sẽ tái dùng cho mobile app qua cùng API |

## 5. Quy ước màu trạng thái

Dùng **thống nhất** ở mọi module, không đổi nghĩa theo ngữ cảnh:

| Màu | Ý nghĩa |
|---|---|
| 🟢 Xanh | Thắng · Đã đóng đủ quỹ |
| 🔴 Đỏ | Thua · Quá hạn đóng quỹ |
| 🟡 Vàng | Hòa · Đang chờ |

Áp dụng cả ở Calendar (FR-08), Datatable (FR-08), biểu đồ (FR-13) và danh sách quỹ (FR-15).

## 6. Đa ngôn ngữ

Mọi chuỗi hiển thị đi qua `react-i18next`. Message lỗi từ API là **mã lỗi**, frontend tra bảng dịch —
xem [cqrs-mediatr.md](../backend/cqrs-mediatr.md#trả-lỗi). Không hard-code tiếng Việt trong component.
