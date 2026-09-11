# 2026-09-12 (FR-07 + FR-21) — Lớp gán khoá học, cảnh báo lệch khi duyệt

Chủ sản phẩm yêu cầu hai việc nối nhau: lớp gán được tối đa 3 khoá, và dựa vào đó cảnh báo khi
duyệt học viên vào lớp không dạy khoá của họ. Đây đúng là **nợ N19** đã ghi từ 09/09 — lần này
có đủ yêu cầu nghiệp vụ để làm.

## Vì sao bảng trung gian, không phải 3 cột

Cám dỗ là `khoa_hoc_1_id`, `khoa_hoc_2_id`, `khoa_hoc_3_id` — "tối đa 3" nghe như ba cột.

Nhưng truy vấn *"lớp nào dạy khoá X"* sẽ thành `WHERE k1 = X OR k2 = X OR k3 = X`, và **quên một
cột là lọt âm thầm**. Thêm khoá thứ tư về sau phải đổi schema. Ba cột cũng không ép được UNIQUE
"một khoá gán một lần vào một lớp" — gán cùng khoá vào cả ba cột thì DB không chặn được.

Bảng `LOP_HOC_KHOA_HOC` với `UNIQUE(lop_hoc_id, khoa_hoc_id)` (quy tắc #8) giải quyết cả ba.

**Giới hạn 3 ép ở validator, KHÔNG ở schema.** Con số này do nghiệp vụ đặt và có thể đổi; ép ở
schema thì đổi từ 3 sang 4 phải viết migration. `Distinct()` trước khi đếm — gửi cùng một khoá ba
lần không phải 3 khoá.

### FK về `KHOA_HOC` là RESTRICT, không Cascade

Cascade nghe tiện: xoá khoá thì tự bỏ liên kết. Nhưng nó **âm thầm phá căn cứ đối chiếu**: lớp
mất khoá, và lần duyệt học viên tiếp theo không cảnh báo gì nữa dù lẽ ra phải cảnh báo.

RESTRICT buộc người xoá gỡ khỏi lớp trước. Cùng lý lẽ với FR-19 (*khoá đã bán thì ngừng bán,
không xoá*).

Điều này **tự chứng minh ngay trong phiên**: script dọn tenant tạm của tôi đụng đúng ràng buộc
đó và dừng lại đúng chỗ — phải thêm `DELETE FROM LOP_HOC_KHOA_HOC` trước `KHOA_HOC`.

## Cảnh báo, không phải chặn

Chủ sản phẩm nói rõ: *"thông báo nếu khoá học không khớp để người duyệt lưu ý (vẫn cho phép nếu
đồng ý)"*.

Đây không phải sự nhân nhượng — có ca hợp lệ thật: học bù, lớp ghép, khoá tương đương chưa kịp
gán. Chặn cứng sẽ tạo đúng **bế tắc** như `DON_DA_DUOC_XEP_LOP` và `HOC_VIEN_DA_TRONG_LOP` vừa
phải bỏ hôm qua.

Cơ chế hai bước:

```
Lần 1 (BoQuaCanhBaoKhoaHoc = false)  →  400 KHOA_HOC_KHONG_KHOP_LOP + DuLieu
Người duyệt đọc, đồng ý
Lần 2 (BoQuaCanhBaoKhoaHoc = true)   →  204, ghi danh bình thường
```

Ba quyết định nhỏ, mỗi cái chặn một cách sai:

- **Cờ mặc định `false`**, không phải true. Mặc định true thì client cũ (hoặc ai gọi API trực
  tiếp) bỏ qua cảnh báo mà **không biết là có** — cảnh báo tồn tại trên giấy, vô hiệu trên thực
  tế.
- **Dùng `AppException.DuLieu`**, không nhét tên khoá vào chuỗi message. Middleware trả `DuLieu`
  về client, nên frontend dựng được câu *"Đơn thuộc khoá X, nhưng lớp Y dạy khoá Z"* mà API vẫn
  chỉ trả **mã lỗi** (quy tắc #3).
- **Lớp chưa gán khoá thì KHÔNG cảnh báo.** Mọi lớp tạo trước 12/09/2026 đều rỗng; cảnh báo hết
  sẽ thành tiếng ồn và người duyệt học cách bấm qua mà không đọc — đúng thứ làm cảnh báo mất tác
  dụng khi cần nhất.

### Không kiểm `dang_ban` khi gán

Khoá ngừng bán **vẫn đang được dạy** ở các lớp đã mở. Chặn gán khoá ngừng bán nghe hợp lý nhưng
sẽ làm **không sửa nổi lớp cũ** khi khoá của nó ngừng bán: mở form sửa, bấm Lưu, nhận lỗi về một
trường mình không đụng tới.

## Quy tắc #1 ở lệnh cập nhật

`khoaHocIds = null` nghĩa **giữ nguyên**, danh sách rỗng mới là **bỏ hết**. Cùng quy ước đã dùng
cho `phongHoc`, `linkHoc`, `ghiChu` trong lệnh này.

Và `.Include(l => l.KhoaHocs)` trước `RemoveRange` — thiếu `Include` thì `RemoveRange` thành
**no-op im lặng**, đúng lỗi đã gặp 10/09/2026 với `LIEN_KET_MXH`.

Form frontend **luôn gửi tường minh** `khoaHocIds`: nếu bỏ trường đi thì người dùng bỏ hết khoá
trên form mà dữ liệu không đổi — im lặng và khó hiểu.

## Một lỗi trong chính cách tôi đo

Bước kiểm "gán khoá thứ 4 phải bị chặn" trả **HTTP 200**. Tưởng validator hỏng.

Hoá ra dữ liệu test của tôi là `[KA, KB, KC, KA]` — trùng `KA`, nên `Distinct()` cho ra 3 và hợp
lệ đúng theo thiết kế. Chạy lại với 4 khoá thật khác nhau: **400 `VUOT_SO_KHOA_HOC_CUA_LOP`**.

Bài học nhỏ: khi phép đo cho kết quả bất ngờ, kiểm dữ liệu đầu vào trước khi kết luận code sai.

## Kiểm chứng

Migration `ThemLopHocKhoaHoc` — chỉ `CreateTable`, không đụng bảng nào có dữ liệu (quy tắc #1).
Áp lên DB dev: 37 → 38 bảng, dữ liệu tenant `W686AE9` nguyên vẹn (8 người, 1 lớp, 3 khoá, 4 đơn).

```
1. Tạo lớp gán 3 khoá        →  OK, cả 3 khoá trả về đúng
2. Gán 4 khoá khác nhau      →  400 VUOT_SO_KHOA_HOC_CUA_LOP
3. Duyệt đơn khoá khác       →  400 KHOA_HOC_KHONG_KHOP_LOP
                                khoaCuaDon=['Giao tiếp cơ bản']
                                khoaCuaLop=['IELTS 6.5 cấp tốc']
4. Đồng ý bỏ qua             →  204, học phí 4.500.000 (từ đơn CRM, không phải giá lớp)
5. Duyệt đúng khoá           →  204, không cảnh báo
```

- **436 test backend xanh** (65 unit + 371 integration), build 0 warning
- 4 test mới; đột biến (coi như không bao giờ lệch) làm **đỏ đúng** test cảnh báo, 28 test khác
  vẫn xanh
- `tsc -b` + `oxlint` sạch; i18n 859 khoá đủ bản dịch
- Lần này `Token_bi_sua_chu_ky_thi_bi_tu_choi` **không chớp nháy** (nợ N12 vẫn để ngỏ)

### Một lỗi i18n mà script không bắt được

Thêm `KHOA_HOC_KHONG_HOP_LE` vào `i18n.ts` mà mã đó **đã có sẵn** ở dòng khác →
`error TS1117: An object literal cannot have multiple properties with the same name`.

`check-i18n-keys.py` báo "mọi khoá đều có bản dịch" vì nó kiểm chiều *thiếu*, không kiểm chiều
*trùng*. `tsc` bắt được. Đáng thêm kiểm trùng vào script — ghi lại để làm sau.

## Còn lại của N19

Nợ N19 hạ từ **Trung bình** xuống **Thấp**: đã có dữ liệu để làm, chỉ còn việc **ưu tiên sắp
xếp** lớp cùng khoá lên đầu trong hộp thoại chọn lớp. Hiện danh sách vẫn theo thứ tự cũ, người
điều phối tự đọc tên khoá — nhưng nay nếu chọn sai thì có cảnh báo đỡ.
