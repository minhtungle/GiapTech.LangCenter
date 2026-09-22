# 22/09/2026 — Nhận diện trung tâm ở đăng nhập và sidebar

## Yêu cầu

> Tôi muốn khi nhập đúng mã trung tâm tại đăng nhập sẽ load đúng thông tin trung tâm như trong
> thiết lập (logo, tên, ...), bên trong giao diện quản trị cũng vậy.

## Đo trước khi làm

Dữ liệu đã có sẵn và đầy đủ — `TENANT` có `logo_url`, `ten_viet_tat`, `dia_chi`, `lien_he`. Cái
thiếu là đường đưa nó ra màn hình:

| Chỗ | Đang hiện gì | Vì sao |
|---|---|---|
| Màn đăng nhập | chỉ **tên** | `TenTrungTamTheoMaDto` cố ý chỉ có một trường |
| Sidebar | ô chữ cái suy từ tên | đọc JWT, mà token không có logo |

## Rào cản thật: ảnh khoá sau xác thực

`GET /anh/{khoa}` gác bằng `Anh.Xem` và nhận **khoá tự do**. Thử với khoá logo thật:

```
khong token -> 401
co token    -> 200 (162 KB)
```

Không thể mở endpoint đó cho ẩn danh: nó phục vụ **mọi** ảnh, gồm ảnh học viên và ảnh CCCD.

Hỏi chủ sản phẩm và chốt: **endpoint riêng chỉ trả logo**. Người gọi đưa *mã trung tâm*, server
tự tra khoá — không chọn được ảnh nào khác.

## Ba lần hệ thống tự chặn tôi

### 1. `TaiVe` từ chối vì không biết tenant

Viết xong endpoint, gọi thử: **404**, dù `coLogo` trả `true`. Truy ra `MinioLuuTruAnh.TaiVe`:

```csharp
// QUY TẮC #2 — chặn đọc ảnh của trung tâm khác.
if (_tenant.TenantId is not { } tenantId) return null;
```

Người gọi chưa đăng nhập ⇒ không có tenant ⇒ null. Chốt chặn này đúng và tôi **không nới**.
Thay vào đó controller tự đặt phạm vi bằng `DatPhamVi(tenantId)` — id do *server* tra từ mã,
không phải người gọi đưa vào. Khác biệt đó là toàn bộ vấn đề.

### 2. Test đếm endpoint ẩn danh đỏ

```
Có 7 endpoint ẩn danh (trước là 6): ... AuthController.Logo ...
Nếu thêm là có chủ ý, cập nhật số này kèm lý do.
```

Đúng việc của nó: buộc dừng lại viết ra lý do thay vì thêm âm thầm. Đã cập nhật số kèm ba lý do
(người gọi không chọn khoá · thứ lộ ra vốn công khai · là endpoint đọc, có rate limit).

### 3. Test khoá cứng danh sách field đỏ

`Chi_tra_TEN_khong_tra_gi_khac` so **chính xác** danh sách field trả về. Thêm hai trường là đỏ.
Cũng đúng việc: nó bắt tôi xác nhận rằng nới thêm là **có chủ ý** và chỉ nới đúng hai thứ vô
hại. Đổi tên test thành `Chi_tra_NHAN_DIEN_khong_tra_gi_khac`, giữ nguyên tinh thần khoá cứng.

## Một mutation SỐNG — và nó chỉ ra test của tôi vô nghĩa

Mutation "sidebar quay lại đọc JWT" → **test vẫn xanh**.

Lý do: test chỉ kiểm sidebar hiện đúng tên trung tâm, mà tên trong JWT và tên trong thiết lập
**giống hệt nhau** ở trung tâm vừa tạo. Hai nguồn cho cùng kết quả nên không phân biệt được.

Sửa: **đổi tên ở Thiết lập rồi mới kiểm**. Token vẫn mang tên cũ, nên nếu sidebar đọc JWT thì
nó hiện tên cũ và test đỏ. Chạy lại mutation: chết.

Đây chính là lý do phải chạy mutation chứ không chỉ đếm test xanh. Bài học lặp lại của 17/09
(test dùng `goto` tự chữa lỗi đang kiểm): **test xanh không có nghĩa là test kiểm được gì**.

## Ranh giới giữ lại, có test canh

| Chốt chặn | Mutation thử | Kết quả |
|---|---|---|
| `/anh/{khoa}` vẫn khoá với ẩn danh | đổi thành `[AllowAnonymous]` | chết |
| Endpoint ẩn danh không trả địa chỉ | thêm `DiaChi` vào DTO | chết |
| Sidebar đọc thiết lập, không đọc JWT | bỏ `useNhanDienTrungTam` | chết (sau khi sửa test) |

588 test backend · 33 vitest xanh.
