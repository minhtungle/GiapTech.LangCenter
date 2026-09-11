# 2026-09-12 (FR-21) — Trạng thái tham gia lớp: suy động thay vì suy từ yêu cầu

Chủ sản phẩm mô tả bốn hành vi mong muốn cho luồng xếp lớp. Ba cái đầu gần như đã có; cái thứ tư
lộ ra một **lỗi thiết kế nguồn dữ liệu** mà ba cái đầu chỉ là triệu chứng.

## Bốn yêu cầu, đối chiếu với code

| Yêu cầu | Hiện trạng | Khoảng cách |
|---|---|---|
| CRM **luôn** cho gửi yêu cầu thêm lớp | Chặn khi đang chờ **và** khi đã xếp | Bỏ 1 chốt |
| Hiện "đã tham gia lớp nào" | Có `TenLopDaXep`, **suy từ yêu cầu** | Sai nguồn |
| Duyệt/từ chối cập nhật lịch sử + trạng thái | Lịch sử ✅ · trạng thái sai nguồn | Hết lệch khi sửa nguồn |
| **Gỡ khỏi lớp / lớp đóng → cập nhật trạng thái** | **Không có gì** | Đây là gốc |

## Gốc vấn đề: một trường mang hai nghĩa

```csharp
public string? TenLopDaXep => CacLanGuiXepLop
    .FirstOrDefault(x => x.TrangThai == TrangThaiYeuCauXepLop.DaXep)?.TenLopHoc;
```

Yêu cầu `DaXep` là **sự kiện quá khứ**: *"đã từng được duyệt vào lớp X"*. Còn *"đang học lớp
nào"* là **trạng thái hiện tại**. Một trường không mang được cả hai.

`GoHocVienKhoiLopCommand` chỉ có đúng hai dòng — `Remove(hv)` rồi `SaveChanges` — **không chạm
`YEU_CAU_XEP_LOP`** (đúng: yêu cầu là lịch sử, không nên sửa lùi). Nên gỡ học viên khỏi lớp thì
CRM vẫn báo "Đã vào lớp X" **mãi mãi**.

## Hai cách chữa, và vì sao chọn cách khó bỏ sót

**Đồng bộ ngược** — khi gỡ/đóng lớp thì sửa `YEU_CAU_XEP_LOP.TrangThai`. Phải nhớ gọi ở **mọi**
chỗ đổi ghi danh: gỡ người, huỷ lớp, kết thúc lớp, đổi trạng thái học viên, chuyển lớp. Quên một
chỗ là hai bảng nói hai chuyện — và không có gì báo.

**Suy động** — đọc thẳng `LOP_HOC_HOC_VIEN` mỗi lần hỏi. Không có gì để quên đồng bộ, vì không
có bản sao nào cả.

Chọn suy động. Cùng nguyên tắc đã dùng cho **công nợ FR-14** (*"tính động, không lưu cột"*), và
nó biến yêu cầu thứ tư của chủ sản phẩm từ *"phải viết thêm"* thành *"tự động đúng"*.

## Bỏ chốt `DON_DA_DUOC_XEP_LOP`

Chốt cũ: *"đã xếp lớp rồi thì không gửi lại — học viên đang học, gửi thêm là xếp lớp hai lần"*.

Lý lẽ đó **chỉ đúng khi giả định một đơn ↔ một lớp vĩnh viễn**. Thực tế có ba ca hợp lệ bị chặn
oan: bị gỡ khỏi lớp cần xếp lại; lớp kết thúc/huỷ mà còn buổi chưa học; học thêm lớp, học lại,
đổi ca.

Chốt **đang chờ** thì giữ — chủ sản phẩm chốt: *"mỗi khóa chỉ được gửi yêu cầu tiếp khi yêu cầu
hiện tại được duyệt hoặc từ chối"*. Hai yêu cầu cùng chờ trên một đơn làm người điều phối thấy
hai dòng trùng mà không biết duyệt cái nào. Partial unique index ở DB giữ nguyên.

Nay một đơn có **nhiều lần `DaXep`** là chuyện bình thường — và không còn gây mâu thuẫn, vì
trạng thái hiện tại không đọc từ đó nữa.

## Phân biệt "đang học" và "đã học"

```csharp
var dangHoc = cacLop
    .Where(l => l.TrangThaiLop is not (TrangThaiLopHoc.DaKetThuc or TrangThaiLopHoc.DaHuy)
                && l.TrangThaiHocVien is TrangThaiHocVienTrongLop.DangHoc
                    or TrangThaiHocVienTrongLop.BaoLuu)
    .ToList();
var daHoc = cacLop.Except(dangHoc).ToList();
```

**Hai chiều** mới đủ: lớp có thể đóng (`DaKetThuc`/`DaHuy`) *hoặc* người có thể rời
(`DaNghi`/`ChuyenLop`). Kiểm một chiều thôi là bỏ sót nửa số ca.

`BaoLuu` **vẫn tính là đang tham gia**: bảo lưu là tạm nghỉ có phép, chỗ trong lớp vẫn giữ.

Không xoá lớp đã đóng khỏi kết quả — đẩy sang `CacLopDaHoc`, vì người bán vẫn cần trả lời *"em
đã từng học lớp nào"*.

## Một chỗ không làm được sạch: nợ N19

`LOP_HOC` chưa có FK về `KHOA_HOC`, nên **không biết lớp nào ứng với đơn nào**. Ghi danh nối theo
`hoc_vien_id` (con người), còn đơn thì theo `dang_ky_id`.

Kết quả: mọi đơn khoá học của một khách **cùng thấy** danh sách lớp của người đó. Đủ để trả lời
*"đang học lớp nào"*, chưa đủ để nói *"đơn này ứng với lớp này"*. Ghi rõ giới hạn trong DTO và
đặc tả thay vì giả vờ đã chính xác.

## Phát hiện ngoài dự kiến: không có đường kết thúc lớp

Khi viết nhánh `DaKetThuc`, tôi `grep` xem chỗ nào set nó — **không có chỗ nào**. Chỉ có
`POST /lop-hoc/{id}/huy` → `DaHuy`.

Vòng đời trong tài liệu ghi *"nháp → sắp khai giảng → đang học → kết thúc"*, nhưng bước cuối
**chưa chạy được**. Logic mới đã xử lý đúng `DaKetThuc` (test canh qua `DaHuy` vì đó là đường duy
nhất gọi được), chỉ chờ có endpoint. Ghi thành **nợ N26** thay vì tự ý thêm — thêm một endpoint
đổi vòng đời lớp là quyết định nghiệp vụ, không phải việc phụ của lần sửa này.

## Kiểm chứng đầu-cuối trên PostgreSQL thật

```
1. Đã gửi, chưa duyệt   →  dangThamGiaLop=False           dangChoXepLop=True
2. Sau khi DUYỆT        →  dangThamGiaLop=True  'IELTS A1'  đang học: [(A1,Nhap,DangHoc)]
3. Gửi lần 2 khi đã học →  HTTP 200 (trước 12/09: 400)     số lần gửi=2, vẫn đang học A1
4. GỠ khỏi lớp          →  dangThamGiaLop=False  None      đang học: []
5. Vào lại rồi HUỶ LỚP  →  dangThamGiaLop=False            đã học: [(A1, DaHuy)]
```

Bước 4 và 5 là hai yêu cầu mà trước đây **không có cơ chế nào** đáp ứng.

- 432 test backend xanh (65 unit + 367 integration), build 0 warning
- Thêm 3 test; **xoá** `Don_da_xep_lop_thi_khong_gui_lai_duoc` vì nó canh đúng hành vi vừa bỏ
- Xoá khoá i18n `DON_DA_DUOC_XEP_LOP` — backend không còn phát, để lại thì người đọc tưởng chốt
  đó còn
- `Token_bi_sua_chu_ky_thi_bi_tu_choi` đỏ khi chạy toàn bộ, xanh khi chạy riêng — **nợ N12** có
  sẵn, đã kiểm chéo

## Bài học

**Trước khi thêm cơ chế đồng bộ, hỏi xem bản sao đó có cần tồn tại không.** Yêu cầu thứ tư của
chủ sản phẩm nghe như *"phải viết thêm luồng cập nhật"*; hoá ra chỉ cần **bỏ bản sao** thì nó
tự đúng.

Và: một trường mang hai nghĩa (*sự kiện quá khứ* + *trạng thái hiện tại*) sẽ đúng cho tới lần
đầu hai nghĩa đó rẽ nhau — ở đây là cú gỡ học viên khỏi lớp.
