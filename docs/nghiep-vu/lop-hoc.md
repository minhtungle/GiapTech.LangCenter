# Module Lớp học (FR-07 → FR-08)

Quản lý lớp học của trung tâm: thông tin lớp, phân công giáo viên/trợ giảng, danh sách học viên.

## FR-07 — Lớp học

CRUD lớp học.

**Trường dữ liệu:** tên lớp, giáo viên chính (1), trợ giảng (nhiều), **khoá học (tối đa 3)**,
hình thức học (trực tuyến / tại lớp / kết hợp), phòng học hoặc link, học phí, sức chứa tối đa,
ngày khai giảng, ngày kết thúc, ghi chú.

### Vòng đời

```
Nháp ──[Hoàn tất]──> Sắp khai giảng ──(tới ngày)──> Đang học ──> Đã kết thúc
  │                         │                          │
  └──[Xoá cứng]             └──────[Huỷ lớp]───────────┘
```

- **Nháp**: lớp đang tạo dở. Chỉ **người tạo** nhìn thấy — giáo viên không nên thấy tên mình
  trong một lớp admin còn đang cân nhắc, họ sẽ tưởng đã được phân công.
- **Xoá cứng chỉ áp dụng cho lớp Nháp.** Lớp đã hoàn tất thì dùng **Huỷ** — nó có thể đã có
  học viên, điểm danh, học phí, và xoá những thứ đó là mất dữ liệu không phục hồi (quy tắc #1).
- **Sắp khai giảng / Đang học không lưu vào cột** — suy từ ngày lúc đọc. Lưu thì phải có một
  job đổi trạng thái lúc nửa đêm, job chết là lớp kẹt sai trạng thái mà không ai biết.

### Quy tắc

- **Tên lớp duy nhất trong trung tâm**, nhưng **lớp nháp không chiếm tên**. Nếu tính cả nháp
  thì: admin A tạo nháp rồi bỏ ngang, ba tháng sau admin B tạo lớp cùng tên và nhận lỗi trùng
  — trong khi danh sách lớp không hiện nháp nên B không thấy lớp nào trùng cả.
- **Giáo viên chính đúng một người**, bắt buộc. Không được đồng thời là trợ giảng của chính
  lớp đó.
- Giáo viên và trợ giảng phải **đang hoạt động** và **thuộc cùng trung tâm** — gán người của
  trung tâm khác bị từ chối (`NHAN_SU_KHONG_HOP_LE`).
- **Học phí `null` ≠ `0`**: `null` là chưa nhập, `0` là lớp miễn phí (lớp thử, học bổng).
  Bắt buộc có giá trị khi hoàn tất lớp.
- **Sức chứa `null` = không giới hạn.** Vì `null` đã mang nghĩa "không gửi" trong lệnh cập
  nhật (quy tắc #1), muốn *bỏ* giới hạn phải gửi cờ riêng `boGioiHanSucChua`.
- Xoá tài khoản giáo viên đang dạy bị **chặn** — buộc bàn giao lớp trước.
- **Lớp gán được tối đa 3 khoá học** (`LOP_HOC_KHOA_HOC`, 12/09/2026 — đóng nợ N19).

  Dùng để đối chiếu khi duyệt học viên từ CRM: khoá trong đơn không nằm trong số khoá lớp dạy
  thì **cảnh báo** người duyệt (`KHOA_HOC_KHONG_KHOP_LOP`), nhưng **vẫn cho phép** nếu họ đồng ý
  — xem [FR-21](./crm.md#fr-21--yêu-cầu-xếp-lớp-crm--lms).

  Giới hạn 3 ép ở **validator**, không ở schema: con số do nghiệp vụ đặt, đổi nó không nên cần
  migration. `Distinct()` trước khi đếm — gửi cùng một khoá ba lần không phải 3 khoá.

  **Rỗng là hợp lệ**: wizard cho lưu nháp trước khi biết dạy khoá nào, và mọi lớp tạo trước
  12/09/2026 đều rỗng. Lớp rỗng thì không cảnh báo gì khi duyệt.

  **Không kiểm `dang_ban`** khi gán: khoá ngừng bán vẫn đang được dạy ở lớp đã mở (FR-19 — đã
  bán thì ngừng bán, không xoá). Chặn ở đây sẽ không sửa nổi lớp cũ khi khoá của nó ngừng bán.

  Lệnh cập nhật giữ quy ước `null = giữ nguyên` (quy tắc #1): form không gửi `khoaHocIds` thì
  khoá đang gán còn nguyên; danh sách rỗng mới là chủ động bỏ hết.

### Phạm vi truy cập — "chỉ lớp mình phụ trách"

`[RequirePermission]` chỉ quyết định **có gọi được endpoint hay không**; nó không lọc dữ liệu.
Global Query Filter chỉ lọc theo **tenant**. Phạm vi bên trong tenant do `IPhamViLopHoc` lo:

| Người dùng | Thấy gì |
|---|---|
| Có `LopHocToanTrungTam.Xem` (admin) | Mọi lớp của trung tâm, kể cả nháp |
| Giáo viên / trợ giảng | Lớp mình phụ trách + lớp mình tự tạo |
| Học viên | Lớp mình đang học |

Lọc áp ở **cả danh sách lẫn chi tiết**. Chi tiết dễ bị quên nhất: danh sách lọc đúng mà chi
tiết không lọc thì gõ thẳng id vào URL là đọc được lớp người khác (IDOR). Trả **404**, không
phải 403 — 403 xác nhận lớp đó tồn tại, tự nó là rò rỉ thông tin.

### Tab "Lịch học" — lịch của MỌI lớp (21/09/2026)

Yêu cầu chủ sản phẩm: *"phần lớp học — bổ sung chế độ xem dạng lịch như lịch học"*.

Khác lịch **trong một lớp** (`LichVaDiemDanh`): cái kia trả lời *"lớp này học những buổi nào"*,
còn đây trả lời *"tuần này trung tâm dạy những gì, có lớp nào trùng giờ không"*.

| | Lịch trong một lớp | Tab Lịch học (mới) |
|---|---|---|
| Dữ liệu | `GET /lop-hoc/{id}/buoi-hoc` — tải một lần | `GET /buoi-hoc?tu=&den=` — **tải theo tháng** |
| Nhãn ô | `Buổi 3` | **Tên lớp** (`hienTenLop`) |
| Bộ lọc | không | lọc theo lớp |

**Là NÚT CHUYỂN VIEW Bảng / Lịch** (sửa 21/09/2026), cùng khuôn với Lịch & điểm danh để người
dùng chỉ phải học một lần.

> Bản đầu tôi làm thành TAB riêng, lập luận rằng bảng liệt kê *lớp* còn lịch vẽ *buổi* nên bộ
> lọc không dùng chung được. Chủ sản phẩm chọn cách chuyển view — và đúng hơn: đây vẫn là "xem
> lớp học", chỉ khác cách trình bày, nên một thanh tab riêng làm màn hình có **hai tầng điều
> hướng cho cùng một thứ**. Mỗi view tự có bộ lọc của mình: bảng có tìm kiếm + trạng thái lớp,
> lịch có khoảng ngày + lọc lớp.

**Tháng rỗng KHÔNG được thay cả tấm lịch bằng thông báo** (sửa 21/09/2026). Bản đầu hiện
`TrangTrong` khi `buoi.length === 0`, nên người dùng bấm ‹ đi xa vài tháng là **lịch biến mất
cùng với nút điều hướng** — kẹt luôn, không quay lại được. FullCalendar đã có `noEventsText`
cho tháng rỗng; không cần lớp chặn nào ở ngoài.

**Lọc theo khoảng ngày** (21/09/2026) — chọn được **một đầu hoặc cả hai**. Đầu còn trống lấy
theo tháng đang xem, không để rỗng: endpoint bắt buộc cả `tu` lẫn `den`, thiếu một đầu là 400.
Khi đang lọc ngày, nút chuyển tháng của lịch không đổi dữ liệu nữa — có một dòng chú thích nói
rõ điều đó, kèm nút **Bỏ lọc**.

**Tải theo tháng, không gộp từ bảng danh sách.** Bảng có phân trang — gộp buổi từ đó thì lịch
chỉ có buổi của 20 lớp đang hiện, và bấm sang trang 2 lịch đổi nội dung. Lịch báo mốc tháng
đang xem qua `onDoiThang`, trang cha tải lại khoảng đó (rộng hơn **một tháng mỗi bên**, vì view
tuần ở đầu/cuối tháng có hiển thị vài ngày của tháng kề).

**Ở view THÁNG của lịch nhiều lớp, nhãn ẩn giờ** (`displayEventTime`): ô ngày rộng ~130px,
"18 giờ " chiếm gần một phần ba nên tên lớp bị cắt thành `IELTS 6.5 cấ` — không phân biệt được
K1 với K6. Giờ vẫn đọc được ở tooltip, view Tuần và view Danh sách. Lịch trong một lớp không ẩn
(nhãn chỉ là "Buổi 3", còn thừa chỗ).

**Phạm vi dữ liệu do BACKEND quyết**: giáo viên chỉ thấy buổi của lớp mình dạy, học viên chỉ
thấy lớp mình học — `IPhamViLopHoc` lọc ở `LayLichTheoKhoangHandler`, không phải màn này ẩn.
Kiểm chứng 21/09 với nick `co.lan`: chỉ thấy đúng 3 lớp cô dạy.

Canh bởi `e2e/lich-tat-ca-lop.spec.ts` (nhiều lớp cùng hiện · nhãn mang tên lớp · lọc thu hẹp
đúng).

## FR-08 — Học viên trong lớp

Thêm / gỡ học viên, xem danh sách.

### Quy tắc

- Một học viên **một bản ghi** trong một lớp (`UNIQUE(lop_hoc_id, hoc_vien_id)` ở tầng DB —
  quy tắc #8, chống bấm hai lần và import trùng).
- **Sức chứa đếm người đang học**, không đếm người đã nghỉ: lớp 20 chỗ có 5 người nghỉ thì vẫn
  nhận thêm được.
- **Tên nhân viên kinh doanh** (`KHACH_HANG.nguoi_tao_id`) hiện trong modal thông tin học viên
  (12/09/2026), gác riêng bằng `KhachHang.Xem`.

  Endpoint danh sách học viên gác bằng `LopHoc.Xem` — quyền mà **giáo viên và học viên đều có**.
  Không gác riêng thì họ đọc được ai bán khách nào; đúng cái bẫy đã làm rò rỉ học phí 07/09/2026.
  Học viên **không** thấy kể cả dòng của chính mình (khác học phí): ai bán mình không phải thông
  tin của mình. Canh bởi `RoRiHocPhiTests.Giao_vien_va_hoc_vien_khong_thay_ten_nhan_vien_kinh_doanh`
  — có cả chiều ngược (admin PHẢI thấy).

  `null` có **ba nghĩa**: không đủ quyền · học viên thêm tay không qua CRM · khách tạo trước
  12/09/2026. UI ẩn hẳn dòng thay vì hiện "—".
- **`hoc_phi_ap_dung` là snapshot** chốt lúc vào lớp, mặc định bằng học phí lớp. Cho phép miễn
  giảm cá nhân, và quan trọng hơn: **sửa học phí lớp không đổi hồi tố công nợ** của người đã
  đóng theo giá cũ.
- Rời lớp đổi `trang_thai`, **không xoá hàng** — xoá thì điểm danh và học phí của họ thành dữ
  liệu treo không giải thích được.

## Mã lỗi

| Mã | Khi nào |
|---|---|
| `NHAN_SU_KHONG_HOP_LE` | Giáo viên/trợ giảng không tồn tại, đã nghỉ, hoặc thuộc trung tâm khác |
| `VUOT_SO_KHOA_HOC_CUA_LOP` | Gán quá 3 khoá cho một lớp |
| `KHOA_HOC_KHONG_HOP_LE` | Khoá không tồn tại hoặc thuộc trung tâm khác |
| `GIAO_VIEN_TRUNG_TRO_GIANG` | Giáo viên chính đồng thời là trợ giảng của chính lớp đó |
| `CHUA_CHON_HINH_THUC_HOC` | Thiếu hình thức học (JSON thiếu trường enum sẽ thành 0) |
| `CHUA_NHAP_HOC_PHI` | Hoàn tất lớp khi học phí còn `null` |
| `LOP_DA_HOAN_TAT` | Hoàn tất một lớp đã rời trạng thái nháp |
| `CHI_XOA_DUOC_LOP_NHAP` | Xoá cứng lớp đã hoàn tất |
| `VUOT_SUC_CHUA` | Thêm học viên vượt sức chứa |
| `HOC_VIEN_DA_TRONG_LOP` | Học viên đã có trong lớp |
| `HOC_VIEN_KHONG_HOP_LE` | Học viên không tồn tại, đã nghỉ, hoặc thuộc trung tâm khác |

## Chưa làm

- **Bước 2 của wizard** (sinh lịch buổi học tự động theo tần suất) — thuộc module Buổi học.
- **Nhân bản lớp** — cần lưu tần suất sinh lịch trước.
- **Import học viên từ Excel** — tách thành FR riêng vì nó chạm luồng tạo tài khoản hàng loạt.
