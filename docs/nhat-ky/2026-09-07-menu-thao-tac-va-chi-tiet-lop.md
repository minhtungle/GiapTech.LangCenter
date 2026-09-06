# 07/09/2026 — Menu thao tác trong bảng + view chi tiết lớp học

Hai yêu cầu giao diện, hoá ra gắn với nhau: ba nút "Lịch / Bài tập / Học viên" trong bảng lớp
học chính là các tab sẽ chuyển vào view chi tiết, nên gom menu và tách view phải làm cùng lúc.

## `MenuThaoTac`: hai quyết định kỹ thuật

**`position: fixed` chứ không `absolute`.** `Table` bọc trong `overflow-x-auto`, nên menu
`absolute` bị **khung cuộn cắt mất ở dòng cuối** — đúng chỗ hay bấm nhất vì bản ghi mới thường
nằm dưới cùng. Đổi sang `fixed` với toạ độ chụp từ `getBoundingClientRect()`.

Hệ quả: cuộn **phải đóng** menu, nếu không nó đứng yên còn dòng của nó trôi đi. Bắt `scroll`
với `capture: true` để nghe được cả cuộn bên trong khung con.

**Mở lên trên khi gần đáy màn hình**, cùng lý do.

Nhóm phá huỷ (xoá, huỷ) đặt cuối, tô đỏ, có đường kẻ tách — để không bấm nhầm khi menu vừa đổ
ra ngay dưới con trỏ.

## `KhungNoiDung`: tái dùng modal làm tab mà không chép code

`LichVaDiemDanh`, `BaiTapCuaLop`, `HocVienCuaLop` viết ra để mở dạng modal. Làm tab thì phải
render thẳng. Chép sang file mới là hàng trăm dòng và từ đó hai bản trôi khỏi nhau.

Giải bằng một component bọc: `nhung=true` render thẳng, `nhung=false` bọc `Modal`. Mỗi file chỉ
đổi **thẻ ngoài cùng**, phần thân không đụng tới.

### Một lỗi tự tạo ra rồi tự bắt

Lần đầu thay thẻ đóng, tôi dùng `rindex("</Modal>")` — nhưng ba file đều có **Modal lồng nhau**,
nên nó bắt phải thẻ đóng của component *khác* trong cùng file. `tsc` không báo gì vì cả hai đều
hợp lệ về cú pháp.

Bắt được bằng cách liệt kê từng cặp `<Modal>`/`</Modal>` theo số dòng rồi ghép tay. Bài học:
với JSX lồng nhau, đừng khớp thẻ đóng bằng "cái cuối cùng".

## `tsc --noEmit` bỏ sót lỗi mà `npm run build` bắt được

Sau khi tách `HocVienCuaLop` ra file riêng, `LopHoc.tsx` còn dùng nó mà không import.
`npx tsc --noEmit` trả **exit 0**; `npm run build` (chạy `tsc -b`) báo đúng 6 lỗi, gồm cả
`Cannot find name 'HocVienCuaLop'`.

Nguyên nhân là hai lệnh đọc cấu hình khác nhau (`tsc -b` theo project references). **Từ nay chỉ
dùng `npm run build` làm chuẩn kiểm TypeScript.**

## View chi tiết: 6 tab tại `/lop-hoc/:id`

| Tab | Nguồn |
|---|---|
| Tổng quan | **Mới** — 4 thông số + thông tin lớp |
| Học viên · Lịch & điểm danh · Bài tập | Chuyển từ modal, dùng `KhungNoiDung` |
| Học phí · Tài liệu | Tái dùng màn đã có, thêm prop `lopHocId` |

**Tab lưu ở query `?tab=`** chứ không ở state: gửi link cho đồng nghiệp thì họ mở đúng tab, F5
không mất chỗ, nút Back quay về danh sách. Đổi tab dùng `replace: true` — 6 lần bấm tab không
được sinh 6 mục lịch sử.

Hai màn tái dùng chỉ cần thêm prop vì **API đã hỗ trợ sẵn** `lopHocId` (tài liệu) và
`lopHocId` (học phí) — không phải sửa backend dòng nào. Khi nhúng thì ẩn ô lọc lớp: lớp đã cố
định, để ô đó chỉ gây nhầm.

Tab Tổng quan gọi `/hoc-phi/cong-no` với `retry: false` — học viên xem lớp của mình sẽ nhận 403
ở đó (đúng theo `IPhamViHocPhi`), không phải lỗi cần thử lại ba lần.

**Tên lớp trong bảng là liên kết** tới trang chi tiết. Thao tác chính phải bấm thẳng được; menu
chỉ dành cho việc không đoán được.

## Áp menu cho 4 màn

| Màn | Trước | Sau |
|---|---|---|
| Lớp học | 6 nút icon | Menu 7 mục (thêm "Xem chi tiết") |
| Tài khoản | 3 nút | Menu 3 mục |
| Người dùng · Học phí | 2 nút | Menu 2 mục |

## Kiểm chứng

- Frontend `npm run build` + `oxlint` sạch. Backend không đụng: **227 test xanh**, 0 warning.
- **Chạy thật**: 6 API mà view chi tiết gọi đều trả 200; số liệu tab Tổng quan khớp dữ liệu đã
  kiểm ở phiên trước (sĩ số 3/3, buổi 1/4, đã thu 3.600.000 ₫, còn nợ 14.400.000 ₫); tab Tài
  liệu lọc đúng theo lớp (1 tài liệu, không phải toàn bộ).

## Bổ sung cùng ngày

### Nhãn tab hiện ra chuỗi khoá i18n

Người dùng thấy `lopHoc.tab_tong-quan` thay vì "Tổng quan". Nguyên nhân: mã tab dùng gạch
**ngang** cho URL đẹp (`tong-quan`) còn khoá i18n tôi đặt gạch **dưới** (`tab_tong_quan`), nên
`t(`lopHoc.tab_${x}`)` ghép ra khoá không tồn tại — và i18next trả về nguyên chuỗi khoá cho
người dùng nhìn thấy. Bốn trong sáu tab hỏng.

Sửa bằng cách gắn nhãn thẳng vào định nghĩa tab (`{ ma, khoa }`) thay vì ghép chuỗi. Ghép động
chỉ an toàn khi khoá **trùng khít** giá trị enum — như `t(`trangThaiLopHoc.${tt}`)`; đã rà lại
toàn bộ các chỗ còn lại trong `src/`, tất cả đều thuộc dạng an toàn đó.

### Sửa thông tin lớp ngay trong tab Tổng quan

Người dùng đã ở trang của đúng lớp đó rồi, bắt mở thêm modal chỉ để đổi một ô là thừa. Modal ở
màn danh sách vẫn giữ vì ở đó chưa chọn lớp nào.

Tách `FormLopHoc` thành component dùng chung thay vì chép: form có 12 trường, logic ẩn/hiện
phòng học vs link học, và **ba quy ước null tinh tế** (`null` = không gửi, `''` = chủ động xoá,
`boGioiHanSucChua` = bỏ giới hạn). Hai bản sao sẽ trôi khỏi nhau và một bên âm thầm xoá dữ
liệu — đúng lỗi quy tắc #1 đã xảy ra hai lần trong dự án này. `LopHoc.tsx` gọn từ 691 → 353
dòng.

Form dùng `defaultValue` nên không tự cập nhật sau khi lưu; `key` ghép từ chính các trường form
ghi đè để remount với dữ liệu mới.

**Kiểm chạy thật**: đổi phòng học P.201 → P.305, các trường khác (học phí, sức chứa, ghi chú,
trợ giảng) còn nguyên; đã trả lại nguyên trạng.

## Còn nợ

- **N2 vẫn còn**: menu hiện đủ mục cho mọi người, kể cả người không có quyền — bấm vào mới nhận
  403. Đây là chỗ hợp lý nhất để nối hook quyền vì mọi thao tác nay đi qua một component.
- Tab Bài kiểm tra chưa có (nợ N1 — schema xong, chưa có API/UI).
- Chưa có breadcrumb; hiện chỉ có liên kết "← Lớp học" ở đầu view chi tiết.
