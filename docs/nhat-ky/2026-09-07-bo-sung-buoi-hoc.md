# 07/09/2026 — Bổ sung buổi học và khoá buổi đã chốt

Yêu cầu: *"đang chỉ có sinh buổi ghi đè lịch đang có"* — cần đường bổ sung buổi.

Khi hỏi lại, chủ dự án nêu thêm một khái niệm chưa có trong hệ thống: **buổi đã khoá** (đã học
xong và được quản trị khoá) thì không được xoá hay đổi thông tin. Đào vào thì đó mới là phần
quan trọng nhất — và là chỗ đang hở.

## Hai lỗ hổng phát hiện khi khảo sát

`CapNhatBuoiHocHandler` và `HuyBuoiHocHandler` **không kiểm trạng thái buổi**. Nghĩa là sửa
được giờ và huỷ được cả buổi **đã chốt** — làm bản ghi điểm danh nói về một thời điểm không
còn tồn tại, hoặc nói rằng buổi ấy chưa từng diễn ra trong khi cả lớp đã được ghi có mặt.

Chốt chặn duy nhất trước đó nằm ở `SinhLichChoLop` (`LICH_DA_CO_DIEM_DANH`), nhưng nó chặn cả
lịch chứ không phải từng buổi, và hai đường sửa/huỷ đi vòng qua nó.

## `BuoiHoc.DaKhoa` — một chỗ duy nhất

```csharp
public bool DaKhoa => TrangThai == TrangThaiBuoiHoc.DaHoanThanh;
```

Đặt ở Domain chứ không rải `if (TrangThai == ...)` khắp handler: thêm trạng thái khoá mới sau
này chỉ phải sửa một dòng. Buổi `DaHuy` **không** khoá — huỷ rồi thì lên lịch lại là bình thường.

Bốn nơi hỏi qua nó: sửa, huỷ, xoá, và sinh lịch.

## `sinh-lich` nay giữ buổi đã chốt

Trước: `RemoveRange(buoiCu)` — xoá sạch, hoặc từ chối hoàn toàn nếu có điểm danh.

Nay tách hai nhóm: buổi đã khoá **giữ nguyên**, buổi chưa học thì xoá và sinh lại. Buổi mới
đánh số **tiếp** theo `MAX(ThuTu)` của nhóm khoá — không bắt đầu lại từ 1, vì
`UNIQUE(LopHocId, ThuTu)` sẽ nổ và vì hai buổi cùng số thì học viên không biết đâu là buổi nào.

Ngày khai giảng/kết thúc của lớp tính trên **cả** buổi khoá lẫn buổi mới, không chỉ buổi mới.

## Ba cách đưa buổi vào lịch

| Cách | Xoá buổi cũ? | Dùng khi |
|---|---|---|
| `sinh-lich` | Có — buổi chưa học | Nhập sai tần suất lúc đầu |
| `sinh-them-buoi` | Không | Lớp kéo dài thêm |
| `POST /lop-hoc/{id}/buoi-hoc` | Không | Dạy bù, ôn tập |

`FormSinhLich` ở frontend dùng cho **cả** `sinh-lich` lẫn `sinh-them-buoi` qua prop `noiTiep`:
đầu vào giống hệt (tần suất, giờ, điều kiện dừng), chỉ khác endpoint và lời cảnh báo. Viết hai
form gần giống nhau là mời gọi chúng lệch nhau.

## Lỗi tự bắt được khi kiểm tay

Thử thêm buổi bù hai lần thì được **hai buổi y hệt** ngày 20/11. `SinhThemBuoi` có chặn trùng
giờ nhưng `ThemBuoiHoc` thì không — tôi viết sau và quên. Bấm nút hai lần là ra hai buổi trùng.

Đã vá và thêm test `Them_buoi_trung_gio_bi_tu_choi`.

## Chi tiết cố ý

- **Xoá buổi không đánh số lại các buổi sau.** Học viên và giáo viên đã quen "buổi 12"; đổi số
  hàng loạt làm mọi ghi chú ngoài hệ thống sai theo. Khoảng trống trong dãy số chấp nhận được.
- **Huỷ khác xoá**: huỷ giữ bản ghi (buổi có lên lịch nhưng không diễn ra), xoá là gỡ buổi lên
  nhầm. Xoá bị chặn nếu đã có điểm danh — chặn sớm với mã lỗi rõ thay vì để `Restrict` nổ ở
  tầng DB với thông báo khó hiểu.
- **Ba hộp xác nhận riêng**, mỗi cái nói đúng hậu quả của nó. Hộp chung chung "Bạn chắc chắn?"
  không giúp người dùng phân biệt xoá 1 buổi với xoá cả lịch. Nút "Sinh lại" viền đỏ vì nó là
  nút duy nhất xoá dữ liệu.
- `ThemBuoiHoc` **dùng lại `SinhLichBuoiHoc.Sinh`** với đúng một ngày, thay vì tự đổi giờ địa
  phương sang tuyệt đối. Hàm đó đã lo chuyện bắt giờ không tồn tại do đổi giờ mùa; chép logic
  tinh tế sang chỗ thứ hai là cách chắc chắn để hai bản lệch nhau.

## Kiểm chứng

- **250 test xanh** (54 unit + 196 integration), build 0 warning, frontend sạch.
- `BoSungBuoiHocTests` — 12 test: thêm buổi lẻ, sinh thêm nối tiếp, chặn trùng giờ (cả hai
  đường), ba chốt khoá, xoá buổi, và test cốt lõi `Sinh_lai_lich_giu_nguyen_buoi_da_chot`.
- **Chạy thật**: sửa/huỷ/xoá buổi đã chốt đều trả `BUOI_HOC_DA_KHOA`; sinh thêm 3 buổi tháng 12
  nối tiếp thành #7–#9 không đụng lịch cũ; trùng giờ trả `BUOI_HOC_TRUNG_GIO`. Dữ liệu thử đã
  dọn về nguyên trạng 4 buổi.

## Còn nợ

- `PhanQuyenVaCachLyTests.Token_bi_sua_chu_ky_thi_bi_tu_choi` **không ổn định**: đỏ một lần khi
  chạy toàn bộ (mất 12 giây), xanh khi chạy riêng và khi chạy lại. Không liên quan thay đổi này
  nhưng cần điều tra — test chớp nháy làm mất niềm tin vào cả bộ test.
- Chưa có kiểm trùng lịch **giáo viên** khi thêm buổi lẻ (nợ N4 — API đã có, chưa nối UI).
