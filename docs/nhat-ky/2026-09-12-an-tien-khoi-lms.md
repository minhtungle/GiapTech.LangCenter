# 2026-09-12 (FR-14 + N18) — Bỏ tiền khỏi LMS thay vì đồng bộ hai sổ

Nợ N18 ghi: *"Số đã thu ở CRM không chảy sang sổ học phí LMS — cùng một khoản tiền phải ghi hai
lần nếu muốn cả hai sổ đúng."* Mức **Cao**, treo từ 09/09.

## Đo trước, và con số cho thấy lỗ hổng thật

```
    họ tên      |          khoá           |  cam kết   | đã thu CRM | đã thu LMS
 Nguyễn Thu Hà  | IELTS 6.5 cấp tốc       | 10.800.000 |  8.000.000 |          0
 Trần Minh Đức  | (sản phẩm)              |    240.000 |    240.000 |          0
```

Thu Hà đã đóng **8 triệu** ở CRM, sổ LMS ghi **0** — hệ thống đang báo cô ấy nợ toàn bộ 10.8
triệu. Không phải lỗi lý thuyết.

## Ba phương án tôi đưa ra đều sai hướng

Tôi hỏi chủ sản phẩm chọn giữa: (1) sinh khoản thu LMS khi duyệt vào lớp, (2) đồng bộ hai chiều
liên tục, (3) sổ LMS đọc động cả tiền CRM.

Cả ba đều nhận **"có hai sổ"** là tiền đề. Câu trả lời:

> *"LMS không quản lý tiền học nữa, cũng không hiển thị tiền. Bảo mật thông tin — chỉ CRM mới
> nắm được số tiền."*

Hai sổ lệch nhau **vì có hai sổ**. Bỏ một sổ thì không còn gì để đồng bộ — và giải quyết luôn một
vấn đề khác mà N18 không nhắc tới: tiền học là dữ liệu nhạy cảm, để nó nằm trong DTO của LMS là
tự tạo bề mặt rò rỉ.

Bài học: khi đưa phương án cho người quyết, nên hỏi cả **"tiền đề có đúng không"**, đừng chỉ hỏi
chọn cái nào.

## Làm gì: ẩn, không xoá

Chủ sản phẩm chốt thêm hai điểm: **giữ bảng và cột**, chỉ ẩn khỏi giao diện LMS. Đây là lựa chọn
**đảo ngược được** — quan trọng với một thay đổi lớn.

| Bỏ | Giữ |
|---|---|
| Mục Học phí ở sidebar LMS | Bảng `KHOAN_THU_HOC_PHI` + 2 dòng dữ liệu |
| Tab Học phí trong chi tiết lớp | Cột `LOP_HOC.hoc_phi`, `LOP_HOC_HOC_VIEN.hoc_phi_ap_dung` |
| Tiền trong `LopHocDto` / `HocVienTrongLopDto` | 5 endpoint `/hoc-phi` + route `/lms/hoc-phi` |

Route giữ lại để bookmark không chết, và bật lại chỉ cần thêm một dòng menu.

### Bỏ cổng quyền là CHẶT hơn, không lỏng hơn

Trước đây hai trường tiền gác bằng `IPhamViHocPhi.DuocXemTienCuaLop` — cổng sinh ra sau vụ rò rỉ
07/09. Nay bỏ cổng, luôn trả `null`.

Nghe như nới lỏng, nhưng ngược lại: **không còn nhánh nào trả ra số tiền**, nên không còn chỗ để
sai. Cổng cũ có hai nhánh (`xemTien ? ... : null`) và nhánh "được xem" là thứ đã từng lọt.

Giữ nguyên *trường* trong DTO thay vì xoá: xoá là breaking change, mà `null` đã có nghĩa "không
được xem" từ trước nên frontend xử lý sẵn.

## Test: ba cái cũ nay SAI, gộp thành một cái mạnh hơn

`RoRiHocPhiTests` có ba test khẳng định **có người thấy được tiền**:
`Hoc_vien_chi_thay_muc_hoc_phi_cua_chinh_minh`, `Quan_tri_van_thay_du_moi_so_tien`. Quy tắc mới
làm chúng sai.

Thay bằng `Khong_ai_thay_tien_hoc_qua_API_cua_LMS_ke_ca_quan_tri` — duyệt **cả bốn vai trò**
(quản trị, giáo viên, trợ giảng, học viên), kèm **chiều ngược**: dữ liệu vẫn còn trong DB.

Chiều ngược đó bắt buộc. Thiếu nó thì **xoá sạch cột tiền** cũng làm test xanh — mà xoá cột là
điều chủ sản phẩm đã nói rõ là không muốn.

### Sáu test nghiệp vụ chuyển sang đọc DB

`XepLopTests` và `LopHocTests` có sáu test canh *"FR-21 ghi đúng học phí từ đơn CRM"*,
*"học phí áp dụng là snapshot không đổi theo lớp"*, *"sửa tên lớp không mất học phí"* (quy tắc #1).

Những hành vi đó **vẫn phải đúng** — chỉ là không kiểm qua API LMS được nữa. Thêm helper
`HocPhiTrongDb` / `TienTrongDb` đọc thẳng `LOP_HOC_HOC_VIEN` và `LOP_HOC`.

Đây là điểm dễ làm sai: dễ nhất là xoá luôn sáu test đó cho xanh. Nhưng chúng canh dữ liệu mà CRM
đọc — bỏ đi là mất lưới an toàn ở đúng chỗ liên quan tiền.

## Kiểm chứng

Đột biến: trả lại `l.HocPhi` cho API → đỏ đúng test, thông báo *"quản trị KHÔNG được thấy học
phí lớp"*.

Chạy thật trên API:

```
ADMIN đọc lớp → hocPhi = None
DB có giữ 9.000.000 không? → 9000000.00
```

Đúng thiết kế: API ẩn, DB giữ.

- **440 test xanh** (66 + 374; giảm 2 vì gộp ba test rò rỉ thành một)
- build 0 warning, `tsc` + `vite build` + `oxlint` sạch

## Việc còn lại

Dữ liệu cũ của Thu Hà (8 triệu ở CRM) **không cần điền bù nữa** — CRM đã có đủ, và LMS không còn
sổ để mà thiếu. Đây là lợi ích phụ của hướng giải quyết này: không có bước migrate dữ liệu nào.
