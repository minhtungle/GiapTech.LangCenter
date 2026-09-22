# `cho-di-dao.json` — nguồn và giấy phép

| | |
|---|---|
| **Tên gốc** | Dog Walk |
| **Tác giả** | Satvir Singh (LottieFiles `/kk1xdtsntf`) |
| **Trang gốc** | https://lottiefiles.com/animations/dog-walk-8qhcG3IRly |
| **Giấy phép** | [Lottie Simple License](https://lottiefiles.com/page/license) |
| **Tải về** | 22/09/2026 |
| **Kích thước** | 29 KB |

## Giấy phép cho phép gì

Lottie Simple License cho **dùng thương mại, không bắt buộc ghi công**: được tải, sao chép,
sửa đổi, công bố, phân phối và trình diễn công khai, kể cả cho mục đích thương mại.

Ghi nguồn ở đây **không phải vì bắt buộc**, mà vì hai lý do thực tế:

1. Người sau nhìn tệp JSON 29 KB sẽ không biết nó từ đâu, có được dùng không, sửa được không.
2. Nếu sau này cần đổi hoặc gỡ, có đường lần về trang gốc.

## Vì sao chọn tệp NÀY

Đã so bốn ứng viên tải về từ API công khai của LottieFiles:

| Tệp | Cỡ | Ảnh nhúng | Kết luận |
|---|---|---|---|
| **Dog Walk** (chọn) | **29 KB** | **0** | Vector thuần, nhẹ nhất, nét vẽ phẳng hợp giao diện |
| walking | 60 KB | 0 | To gấp đôi, chỉ 1 lớp nên chuyển động đơn điệu |
| walk2 | 63 KB | **56** | Nhúng 56 ảnh base64 ⇒ mờ khi phóng to, nặng |
| pet walking in park | — | — | Tải không được |

**Ảnh nhúng là tiêu chí loại quan trọng nhất**: Lottie nhúng ảnh raster mất hẳn ưu thế vector
— phóng to bị vỡ, và dung lượng phình lên (tệp 364 KB từng thử có 25 ảnh base64 bên trong).

Đã xem ảnh động thật trước khi chọn, không chọn theo tên tệp.
