# Nguồn ảnh banner mặc định

**Tệp**: `banner-hoc-tap.jpg` (900×1200, ~87 KB)

| | |
|---|---|
| Nguồn | [Unsplash](https://unsplash.com/photos/mQVWb7kUoOE) — ảnh của Brooke Cagle |
| Giấy phép | [Unsplash License](https://unsplash.com/license) — dùng được cho mục đích thương mại, **không bắt buộc ghi công** |
| Ngày tải | 22/09/2026 |

## Vì sao chọn ảnh này

Chủ sản phẩm yêu cầu *"tìm ảnh thật phù hợp để làm mặc định, liên quan tới học tập"*. Đã xem
ba ứng viên:

| Ảnh | Vì sao không chọn |
|---|---|
| Lớp học có máy chiếu | Nhiều chi tiết và có CHỮ trên màn chiếu — chọi với logo và tiêu đề phủ lên trên |
| Sách + táo + khối chữ ABC | Đọc ra lớp **trẻ em**; trung tâm ngoại ngữ còn dạy người lớn |
| **Nhóm người lớn học cùng nhau** ✅ | Đúng đối tượng của phần mềm, không khí thân thiện, tông tối hợp chữ trắng phủ lên |

## Vì sao 900×1200 (dọc), không phải ảnh ngang gốc

Banner là **cột dọc** chiếm ~45% chiều ngang, cao hết màn. Ảnh ngang 1400×933 đặt vào đó sẽ bị
`object-cover` cắt mất hai bên — tức tải về nhiều byte rồi vứt đi. Cắt sẵn về tỉ lệ 3:4 quanh
tâm ảnh: cùng khung nhìn, nhẹ hơn.

Chất lượng 72, progressive JPEG — 87 KB cho ảnh nền toàn màn là mức chấp nhận được.
