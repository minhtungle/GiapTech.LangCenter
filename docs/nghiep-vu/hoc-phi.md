# Module Học phí (FR-14)

Ghi nhận tiền học viên đã nộp và theo dõi ai còn nợ.

> **Hệ thống không xử lý tiền.** Không cổng thanh toán, không đối chiếu sao kê ngân hàng,
> không sinh hoá đơn điện tử. Đây là **sổ ghi tay điện tử**: người thu ngân nhận tiền ngoài
> đời rồi vào đây ghi lại. Mọi con số ở đây là *lời khai của trung tâm*, không phải bằng
> chứng giao dịch.

## FR-14 — Thu học phí & công nợ

### Mọi thứ về tiền nằm trong MỘT tab

Trong view chi tiết lớp, **tab Học phí là nơi duy nhất hiện số tiền**. Tab Tổng quan không có
ô "đã thu"/"còn nợ", bảng danh sách lớp không có cột học phí, tab Học viên chỉ hiện cột mức áp
dụng khi người xem được phép.

Lý do không rải ra: số tiền ở nhiều chỗ thì mỗi chỗ là một đường rò rỉ phải nhớ vá, và người
dùng phải đi lùng ba tab để ghép được bức tranh tài chính của lớp.

Đầu tab là ba con số của cả lớp — **tổng phải thu · đã thu · còn nợ** — kèm thanh tiến độ.
Dưới đó là hai bảng:

| Bảng | Trả lời câu hỏi | Nguồn |
|---|---|---|
| **Công nợ** | Ai còn nợ bao nhiêu? | Tính động, không lưu |
| **Sổ thu** | Đã thu những khoản nào? | Bảng `KHOAN_THU_HOC_PHI` |

Học phí **mức chuẩn của lớp** chỉ nhập lúc tạo lớp; sau đó nó thuộc về tab Học phí, không nằm
trong form thông tin lớp.

### Công nợ tính động, không lưu cột

```
Còn nợ = LOP_HOC_HOC_VIEN.hoc_phi_ap_dung − SUM(KHOAN_THU_HOC_PHI.so_tien)
```

Không có cột `da_thu`. Nếu lưu, chỉ cần một lần sửa hoặc xoá khoản thu mà quên cập nhật cột
là hai con số bắt đầu lệch — và người phát hiện sẽ là học viên đang cãi nhau với thu ngân về
số tiền mình còn nợ. Cộng dồn thực hiện trong SQL (`SUM` ở subquery), không kéo khoản thu về
bộ nhớ rồi cộng ở C#.

**Mẫu số là `hoc_phi_ap_dung` của từng học viên**, không phải `LOP_HOC.hoc_phi`. Đó là snapshot
chốt lúc học viên vào lớp, cho phép miễn giảm từng người, và sửa học phí lớp **không** đổi hồi
tố công nợ của người đã đóng.

### Quá hạn

```
Quá hạn = còn nợ > 0
          VÀ lớp đã khai giảng
          VÀ hôm nay > ngày khai giảng + TENANT.so_ngay_canh_bao_no_hoc_phi
```

Ngưỡng do admin cấu hình trong Thiết lập chung, mặc định **14 ngày**. **Lớp chưa khai giảng
không bao giờ quá hạn** dù chưa ai đóng đồng nào — nợ chỉ có nghĩa khi dịch vụ đã bắt đầu.

### Quy tắc

- **Số tiền phải dương** — chặn ở validator (`SO_TIEN_PHAI_DUONG`) **và** ở `CHECK` constraint
  tầng DB. Muốn ghi giảm thì xoá khoản thu, không nhập số âm: số âm làm mọi báo cáo tổng thu
  sai mà không ai nhìn ra.
- **Không UNIQUE** trên `(lớp, học viên)` — nộp nhiều đợt là chuyện thường.
- **Không thu được cho người không thuộc lớp** (`HOC_VIEN_KHONG_THUOC_LOP`): khoản thu đó
  không rơi vào công nợ nào, thành tiền treo không ai tìm thấy.
- **Sửa khoản thu không đổi được học viên hay lớp.** Đó không phải "sửa", đó là chuyển tiền từ
  sổ này sang sổ khác — phải xoá rồi thu lại để còn dấu vết.
- **`NgayThu` bỏ trống = hôm nay.** Cho phép nhập ngày quá khứ (ghi bù sổ giấy).
- `SoPhieu` là số phiếu thu giấy để đối chiếu — tuỳ chọn, không duy nhất (trung tâm có thể
  dùng lại số theo năm).

### Phạm vi truy cập — tách khỏi phạm vi lớp

`IPhamViHocPhi` là **tầng riêng**, không dùng lại `IPhamViLopHoc`. Giáo viên thấy được lớp mình
dạy, nhưng học phí là quan hệ giữa **học viên và trung tâm** — không phải việc của người dạy.
Dùng chung một tầng lọc là vô tình mở sổ thu cho toàn bộ giáo viên.

| Người dùng | Xem sổ | Ghi/sửa/xoá |
|---|---|---|
| Có `HocPhi.*` **và** `LopHocToanTrungTam.Xem` (admin, kế toán) | Toàn bộ | Có |
| Giáo viên / trợ giảng | **Không** | **Không** |
| Học viên | **Chỉ của chính mình** | **Không** |

- Học viên có `HocPhi.Xem` để **tự tra nợ** — nên nếu tầng phạm vi hỏng, họ đọc được sổ của cả
  trung tâm mà endpoint vẫn trả 200. Đây là lý do phạm vi có test riêng, không chỉ test quyền.
- **Truyền thẳng `hocVienId` của người khác không lách được** — lọc nằm ở phạm vi, không ở việc
  tin tham số truy vấn.
- **Đường ghi đi qua đúng cổng với đường sửa** (`DuocGhiSo`, cùng điều kiện với
  `LocKhoanThuDuocSua`). Nếu ghi lỏng hơn sửa thì có người tạo được khoản thu mà không ai —
  kể cả chính họ — gỡ lại được.

### Số tiền không được đi nhờ DTO của module khác

Tầng phạm vi chỉ bảo vệ **module học phí**. Nếu một trường tiền nằm trong DTO của module
khác, nó đi ra qua cổng của module đó và `[RequirePermission(HocPhi, ...)]` vô nghĩa.

Đã xảy ra thật (phát hiện 07/09/2026):

| DTO | Endpoint | Gác bằng | Hậu quả |
|---|---|---|---|
| `LopHocDto.HocPhi` | `GET /lop-hoc`, `GET /lop-hoc/{id}` | `LopHoc.Xem` | Giáo viên, trợ giảng, học viên đều đọc được học phí lớp |
| `HocVienTrongLopDto.HocPhiApDung` | `GET /lop-hoc/{id}/hoc-vien` | `LopHoc.Xem` | Giáo viên biết ai được miễn giảm và giảm bao nhiêu; **học viên đọc được học phí của bạn cùng lớp** |

Cả ba vai trò đều có `LopHoc.Xem` — đó là cửa. Ma trận quyền vẫn đúng, tầng phạm vi vẫn đúng;
lỗi là ở chỗ **trường tiền nằm sai nhà**.

Vá bằng `IPhamViHocPhi.DuocXemTienCuaLop()`: hai handler trả `null` cho người không đủ quyền.
Đây là **che cột**, khác với `LocTheoPhamVi` vốn chỉ **lọc hàng**.

`HocVienTrongLopDto.HocPhiApDung` có thêm một mức: học viên thấy số của **chính mình**
(`xemTien || hv.HocVienId == toi`) — họ cần biết mình phải đóng bao nhiêu.

Canh bởi `RoRiHocPhiTests` (7 test). **Thêm trường tiền vào DTO không thuộc module học phí →
thêm một test ở đó.**

### Bẫy phân quyền: cặp `HocPhi` + `LopHocToanTrungTam`

`ThayToanBoSo` cần **cả hai** quyền. Nghĩa là cấp `LopHocToanTrungTam.Xem` cho nhóm Giáo viên
— nghe rất vô hại, "cho giáo viên xem lịch mọi lớp" — sẽ **mở toàn bộ sổ thu** cho họ nếu nhóm
đó cũng có `HocPhi.Xem`.

Nhóm mặc định an toàn (giáo viên không có `HocPhi`), nhưng admin sửa được ma trận ở màn Phân
quyền. Cặp quyền này có tác dụng phụ tài chính, cần biết trước khi cấp.

### Xoá dữ liệu

| Quan hệ | Delete | Vì sao |
|---|---|---|
| `→ NGUOI_DUNG (học viên)` | **Restrict** | Dữ liệu tiền không được biến mất theo tài khoản |
| `→ LOP_HOC` | **Restrict** | Xoá lớp không được cuốn sạch lịch sử thu |
| `→ NGUOI_DUNG (người thu)` | SetNull | Chỉ là dấu vết ai ghi sổ; Restrict sẽ khoá cứng mọi tài khoản thu ngân vĩnh viễn |

Xoá một **khoản thu** thì công nợ tự tăng lại — đúng như mong đợi, và là hệ quả trực tiếp của
việc không lưu cột `da_thu`.

### Chưa làm

- Phiếu thu in được / xuất PDF.
- Nhắc nợ tự động qua email/SMS (`IEmailSender` đã có sẵn, chưa nối).
- Lịch sử chỉnh sửa khoản thu (ai sửa gì lúc nào) — hiện chỉ lưu người thu ban đầu.
