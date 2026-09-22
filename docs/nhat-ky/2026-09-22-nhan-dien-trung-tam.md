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

---

## Bổ sung: bố cục lại màn đăng nhập

Chủ sản phẩm phản hồi ngay sau khi xem:

> - màn hình đăng nhập đang hơi trống
> - đưa khung đăng nhập sang phải, bên trái để hiển thị 1 khung banner được setting trong
>   thiết lập như logo, nếu chưa có hãy để mặc định, nhớ làm responsive cho các thiết bị
> - footer có kèm thông tin cũng được thiết lập và 1 dòng bản quyền phần mềm của GiapTex

### Dữ liệu đã có sẵn

`TENANT.anh_bia_url` tồn tại từ đầu và Thiết lập đã có ô tải ảnh bìa — chỉ chưa ai dùng tới.
Không phải thêm cột nào; chỉ cần mở đường cho nó ra màn đăng nhập.

### Ba trạng thái banner, không phải hai

Chỗ dễ sót nhất là **trạng thái chưa gõ mã**: người dùng mở trang là thấy ngay, mà lúc đó chưa
biết trung tâm nào. Nếu chỉ làm hai nhánh "có ảnh bìa / có logo" thì màn hình lúc mới vào vẫn
trống nửa bên trái — đúng cái đang phải chữa.

Nên banner luôn có nội dung: chưa gõ mã thì hiện tên phần mềm và một câu giới thiệu.

### Hỏi trước hai điều

| Câu hỏi | Chốt |
|---|---|
| Footer hiện thông tin gì? | **Chỉ dòng bản quyền GiapTex** |
| Chưa có ảnh bìa thì banner hiện gì? | **Gradient + logo + tên** |

Câu đầu quan trọng vì nó **đảo một quyết định của chính hôm nay**: sáng nay chủ sản phẩm chọn
KHÔNG lộ địa chỉ/liên hệ cho người chưa đăng nhập. Chốt mới: địa chỉ hiện ở **banner** (chỉ sau
khi gõ đúng mã), còn `lienHe` vẫn nằm trong danh sách cấm — số điện thoại là thứ người dò dùng
được ngay, khác một dòng địa chỉ vốn in trên biển hiệu.

### Test đỏ oan vì tôi kiểm nhầm thứ

Viết `await expect(trangDt.locator('h2')).toHaveCount(0)` cho màn điện thoại → đỏ.

Soi lại: banner **đúng là đã ẩn** (`visible=false`), nhưng `hidden lg:flex` của Tailwind ẩn
bằng CSS nên phần tử vẫn nằm trong DOM. Kiểm số lượng là kiểm nhầm thứ. Đổi sang `toBeHidden()`.

Đáng nhớ vì nó khác hẳn các lần đỏ trước: lần này **sản phẩm đúng, test sai**, và nếu tôi tin
test thì đã đi sửa CSS đang chạy tốt.

### Hai chi tiết giao diện chỉ thấy khi soi ảnh

- **Nội dung banner nằm lệch xuống dưới**: bản đầu dùng `justify-between` với một `<div/>` giữ
  chỗ. Đổi sang neo tuyệt đối tên phần mềm ở góc trên, khối giữa `justify-center`.
- **Footer dừng ở mép banner**: vì tôi đặt nó trong cột phải. Đưa ra ngoài, trải ngang cả hai.

Cả hai đều không test nào bắt được.

### Mutation

| Mutant | Kết quả |
|---|---|
| Bỏ nội dung mặc định (lại "hơi trống") | chết |
| Banner không ẩn trên màn hẹp | chết |
| Bỏ dòng bản quyền GiapTex | chết |

---

## Bổ sung lần 2: tên ngắn · ảnh thật · đóng tự đăng ký

> - GiapTech LangCenter thay bằng LangCenter thôi
> - phần banner tự tìm ảnh thật phù hợp để làm mặc định, liên quan tới học tập
> - ẩn nút tạo trung tâm đi

### Chọn ảnh: xem ba cái rồi mới quyết

Tải ba ứng viên từ Unsplash (giấy phép cho dùng thương mại, không bắt buộc ghi công) và **xem
từng cái** thay vì lấy cái đầu tiên:

| Ảnh | Quyết |
|---|---|
| Lớp học có máy chiếu | loại — nhiều chi tiết và có **chữ** trên màn chiếu, chọi với logo/tiêu đề phủ lên |
| Sách + táo + khối chữ ABC | loại — đọc ra lớp **trẻ em**, mà trung tâm ngoại ngữ dạy cả người lớn |
| Nhóm người lớn học cùng nhau | **chọn** — đúng đối tượng, tông tối hợp chữ trắng |

Cắt sẵn về **3:4 dọc** rồi mới đưa vào repo: banner là cột dọc, ảnh ngang 1400×933 đặt vào sẽ
bị `object-cover` cắt mất hai bên — tức tải về nhiều byte rồi vứt đi. 87 KB sau khi cắt và nén.

Ghi nguồn + giấy phép + lý do chọn vào `banner-hoc-tap.NGUON.md` cạnh tệp ảnh.

### "Ẩn nút" hoá ra là câu hỏi, không phải lệnh

Tắt cờ `dangKyTrungTam` là xong phần "ẩn nút". Nhưng test
`Co_khop_voi_hanh_vi_that_cua_endpoint_dang_ky` **đỏ ngay**:

> Điểm mấu chốt: cờ phải nói ĐÚNG sự thật. Cờ true mà endpoint trả 404 (hoặc ngược lại) còn
> tệ hơn không có cờ.

Cờ nói "tắt" mà endpoint vẫn tạo được tenant — cờ nói dối. Test này viết từ trước, và nó đúng.

Hỏi chủ sản phẩm: **ẩn nút hay đóng hẳn?** Chốt **đóng hẳn**. Nên endpoint trả 404 khi cờ tắt,
và cờ `/tinh-nang` với chốt chặn đọc **chung một nguồn** (`TinhNang.ChoTuDangKy`) để không bao
giờ lệch nhau nữa.

Hai lựa chọn nhỏ đáng ghi:

- **404 chứ không 403**: 403 xác nhận "có endpoint này, chỉ là bạn không được phép".
- **Mặc định TẮT**: quên cấu hình ở môi trường thật thì hậu quả là "không ai tạo được trung
  tâm" (phiền, dễ thấy) chứ không phải "ai cũng tạo được" (âm thầm, nguy hiểm).

Hệ quả phải xử: **mỗi test E2E tự tạo một trung tâm** qua endpoint này. Nếu đóng mà không mở
đường cho test thì cả bộ 47 test đỏ. `ApiFactory` tự bật cờ; bộ E2E cần
`CHO_TU_DANG_KY=true` — đã ghi vào CLAUDE.md.

### Một chi tiết chỉ thấy khi soi ảnh

Chữ "Hà Nội, Việt Nam" nằm đúng vùng laptop sáng trong ảnh nền → khó đọc. Đổi lớp phủ tối từ
gradient **dọc** sang **chéo**, đậm nhất ở góc dưới-trái nơi có logo, tên và địa chỉ.
