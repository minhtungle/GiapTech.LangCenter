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
