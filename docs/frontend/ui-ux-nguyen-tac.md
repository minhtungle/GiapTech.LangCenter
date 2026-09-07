# Nguyên tắc UI/UX (bắt buộc khi code frontend)

## 1. Tối giản, mật độ thông tin cao

- **Bảng màu**: 1 màu chủ đạo + 1 màu nhấn + màu trạng thái cố định. Xem
  [design-tokens.md](./design-tokens.md).
- **Datatable**: mật độ dòng gọn (compact density), lọc/sort **ngay trên tiêu đề cột**, không tách bộ lọc
  ra panel riêng cho các trường hợp đơn giản.
- **Trang thống kê**: KPI card gọn ở đầu → biểu đồ chi tiết bên dưới.
- Tùy chọn nâng cao **ẩn dưới "Xem thêm"**, không bày hết ra màn hình chính.

## 2. Luồng dễ hiểu

- **Sidebar** theo 5 module + **breadcrumb** ở mọi trang con. **Thu gọn được** (chỉ còn icon,
  nhớ lựa chọn trong `localStorage`); trên mobile chuyển thành ngăn kéo vì sidebar cố định
  chiếm quá nhiều bề ngang.
- **Tạo lớp học** = wizard nhiều bước, xem
  [FR-07](../nghiep-vu/lop-hoc.md). **Cho phép lưu nháp giữa chừng** — người dùng không bị
  mất dữ liệu khi rời tab, và lớp nháp không chiếm tên.
- **Tạo tài khoản** = wizard tuần tự đúng quy trình
  [FR-03](../nghiep-vu/quan-tri-he-thong.md#quy-trình-chuẩn-tạo-tài-khoản-wizard-tuần-tự): hồ sơ
  con người → nhóm quyền → tài khoản.
- **Empty-state** luôn có nút hành động + hướng dẫn ngắn, không để màn hình trắng.
- **Bảng nhập nhiều dòng → một nút Lưu cho cả bảng**, không lưu theo từng ô.
  - Lưu theo ô (`onBlur`) cộng với việc hỏi xác nhận trước mọi thao tác ghi sẽ thành hỏi mỗi
    lần rời một ô — chấm lớp 20 học viên là 20 hộp thoại. Đã xảy ra với bảng chấm điểm.
  - Chỉ gửi **dòng đã sửa**, và gửi **tuần tự**: mỗi lượt là một bản ghi nhật ký và một lần
    `SaveChanges`.
  - Nút hiện số dòng đã sửa chưa lưu, và tắt khi không có gì để lưu.
- **Giờ và ngày hiển thị theo MÚI GIỜ TRUNG TÂM**, không phải múi giờ máy người xem. Lấy từ
  `GET /toi/cau-hinh` qua hook `useMuiGio()`.
  - Buổi 18:00 giờ Việt Nam mở trên máy đặt UTC+9 sẽ hiện 20:00; với **lịch** thì lệch giờ
    còn làm buổi **nhảy sang ô ngày khác** — sai rõ hơn nhiều so với bảng.
  - Không lấy từ `/thiet-lap`: endpoint đó gác bằng `ThietLapChung.Xem` nên giáo viên và học
    viên không gọi được, mà họ chính là người xem lịch nhiều nhất.
- **Lịch dạng calendar dùng FullCalendar 6** (MIT), không tự viết: lưới tháng, trục giờ, điều
  hướng và hàng chục ca biên về múi giờ là công việc nhiều tháng.
  - Chọn nó thay `react-big-calendar` vì có `timeZone` sẵn, mọi gói view cùng một phiên bản
    **ổn định** (v7 các view còn beta), và chỉ kéo theo `preact` — trong khi bên kia mang cả
    moment, luxon, lodash và globalize.
  - **Map `--fc-*` sang design token** trong `lich-buoi-hoc.css`, không ghi màu cứng: nếu không,
    đổi bảng màu hoặc bật chế độ tối sẽ để lại một khối lịch màu lạ.
  - **Tải theo yêu cầu** (`lazy` + `Suspense`): FullCalendar ~230 kB và chỉ dùng khi người
    dùng chủ động bật chế độ Lịch. Import thẳng thì mọi màn đều phải tải nó.
- **Ẩn menu và nút theo quyền bằng `useQuyen()`** (`frontend/src/lib/quyen.ts`), đọc từ
  `GET /toi/quyen`.
  - `coQuyen('HocPhi', 'Them')` cho nút; `an: !coQuyen(...)` cho mục trong `MenuThaoTac`.
  - **Đây là tiện lợi, KHÔNG phải bảo vệ.** Mọi endpoint vẫn tự gác quyền của nó, và dữ liệu
    nhạy cảm phải được backend che TRƯỚC khi rời máy chủ — ẩn ở client chỉ để người dùng khỏi
    bấm vào rồi mới biết mình không được phép.
  - Trong lúc **chưa biết quyền** (`dangTai`) thì hiện đủ: ẩn trước rồi hiện lại làm menu nhấp
    nháy mỗi lần tải trang. Bấm nhầm lúc đó cùng lắm nhận 403.
  - Ẩn hết mục trong một nhóm menu thì **bỏ luôn nhóm** — tiêu đề không có mục nào bên dưới
    trông như giao diện hỏng.
- **Nút thao tác trong bảng → gom vào menu `MenuThaoTac`**, không bày icon trần ra hàng ngang.
  Bảng lớp học từng có 6 nút mỗi dòng, chiếm gần nửa bề ngang và biến cột thao tác thành một
  dải icon phải rê chuột từng cái để đoán. Menu giữ cột hẹp và **có chữ**.
  - Nhóm phá huỷ (xoá, huỷ, gỡ) đặt cuối, `nguyHiem: true`, có `ngatNhom` tách khỏi nhóm trên
    — để không bấm nhầm khi menu vừa mở ra dưới con trỏ.
  - Menu định vị `fixed` chứ không `absolute`: `Table` bọc trong `overflow-x-auto` nên menu
    `absolute` bị khung cuộn **cắt mất ở dòng cuối** — đúng chỗ hay bấm nhất.
- **Thao tác chính của một dòng phải bấm thẳng được**, không giấu trong menu: tên lớp là liên
  kết tới trang chi tiết. Menu chỉ dành cho việc không đoán được.
- **Bản ghi có nhiều mặt → trang riêng có tab**, không phải nhiều modal. Lớp học có 6 tab
  (tổng quan, học viên, lịch, bài tập, học phí, tài liệu); trước đây mỗi thứ một modal nên xem
  sang mặt khác của cùng một lớp phải đóng modal, tìm lại dòng, mở modal khác.
  - **Tab lưu ở query `?tab=`**, không ở state: gửi link cho đồng nghiệp thì họ mở đúng tab,
    F5 không mất chỗ, nút Back hoạt động đúng mong đợi.
  - Đổi tab dùng `replace: true` — 6 lần bấm tab không được sinh 6 mục lịch sử.
  - Component đang mở dạng modal muốn tái dùng làm tab thì bọc `KhungNoiDung` với prop `nhung`,
    **không chép nội dung sang file mới** — hai bản sao sẽ trôi khỏi nhau.
- **Thêm/cập nhật không cần chuyển view → dùng modal**, không chèn form vào giữa danh sách:
  chèn form đẩy bảng xuống, người dùng mất ngữ cảnh dòng đang thao tác. Modal dùng thẻ
  `<dialog>` của trình duyệt để có sẵn focus trap và Esc.
- **Select có nhiều lựa chọn → dùng `SelectTimKiem`** (chọn một) hoặc **`SelectTimKiemNhieu`**
  (chọn nhiều, hiển thị chip), không dùng `<select>` cơ bản. Tìm kiếm **bỏ dấu tiếng Việt**:
  gõ "nguyen" ra "Nguyễn".
- **Modal chứa dropdown: không đặt `overflow-y-auto` ở thân modal** — nó tạo scroll container
  làm dropdown `position: absolute` bị cắt, người dùng phải cuộn mới thấy hết. Giới hạn chiều
  cao ở chính thẻ `<dialog>` và cho dialog cuộn.
- **Không dùng `prompt()` / `alert()` của trình duyệt** cho nhập liệu — không style được, không
  dịch được, và trông như lỗi trang web.
- **Toast** nhất quán vị trí/thời gian. **Không dùng `alert()`** hay modal chặn luồng cho thông báo
  thông thường.

## 3. Danh tính trung tâm trên giao diện

Sau khi đăng nhập, mọi chỗ hiển thị trung tâm dùng **tên trung tâm** làm dòng chính, **mã trung
tâm** làm dòng phụ nhỏ bên dưới. Người dùng nhận ra trung tâm của mình qua tên; mã 7 ký tự chỉ
cần khi đăng nhập hoặc đọc cho người khác.

Tên trung tâm nằm trong claim `ten_trung_tam` của JWT để sidebar hiển thị được ngay khi tải
trang. Đổi tên ở FR-06 phải gọi `capNhatTenTrungTam()` — token đang cầm vẫn mang tên cũ tới lần
làm mới kế tiếp.

## 4. Thiết bị mục tiêu

| Nhóm thao tác | Ưu tiên | Lý do |
|---|---|---|
| Ma trận phân quyền (FR-05), bảng điểm danh, bảng chấm điểm | **Desktop** | Bảng nhiều chiều, nhập liệu hàng loạt |
| Xem lịch học, điểm danh, nộp bài, tra công nợ (giáo viên · học viên) | **Responsive tốt** | Nhóm dùng nhiều nhất, và sẽ tái dùng cho mobile app qua cùng API |

## 5. Quy ước màu trạng thái

Dùng **thống nhất** ở mọi module, không đổi nghĩa theo ngữ cảnh:

| Màu | Token | Ý nghĩa trong LMS |
|---|---|---|
| 🟢 Xanh | `win` | **Xong · đạt · đủ**: lớp đang học, buổi đã chốt, bài đã chấm, học phí đã đủ, thao tác thành công |
| 🔴 Đỏ | `lose` | **Hỏng · quá hạn · bị từ chối**: lớp đã huỷ, nộp muộn, học phí quá hạn, vắng khác lời khai |
| 🟡 Vàng | `draw` | **Đang chờ · cần chú ý**: lớp nháp, còn nợ học phí, buộc đổi mật khẩu, giáo viên riêng |

⚠️ Tên token còn là `win`/`lose`/`draw` — **di sản từ dự án bóng đá**, nghĩa hiện tại là ba cột
bên trên chứ không phải kết quả trận đấu. Đổi tên là nợ **N8** trong
[`ke-hoach.md`](../ke-hoach.md); tới lúc đó đọc tên token phải hiểu theo bảng này.

Áp dụng thống nhất ở lịch học (FullCalendar), mọi bảng danh sách, và nhật ký hệ thống.

## 6. Đa ngôn ngữ

Mọi chuỗi hiển thị đi qua `react-i18next`. Message lỗi từ API là **mã lỗi**, frontend tra bảng dịch —
xem [cqrs-mediatr.md](../backend/cqrs-mediatr.md#trả-lỗi). Không hard-code tiếng Việt trong component.

## Ô nhập văn bản dài

Mọi trường **có thể dài** dùng `<Textarea>`, không dùng `<Input>`: nhận xét, ghi chú, mô tả,
nhận xét buổi học. `<input>` một dòng cắt nội dung khỏi tầm nhìn ngay khi vượt bề rộng ô —
người dùng gõ một đoạn nhận xét rồi không đọc lại được đoạn đầu, phải rê con trỏ mới thấy.

Trường ngắn (tên, phòng học, URL, ngày) vẫn dùng `Input`.

## Xác nhận thao tác ghi

**Cập nhật 07/09/2026 — chủ sản phẩm chọn hỏi xác nhận trước MỌI thao tác thêm/sửa/xoá, kể cả
bấm Lưu trong form.** Trước đó chỉ hỏi khi thao tác phá huỷ. Dùng `useXacNhan()` (xem
`frontend/src/lib/xacNhan.tsx`), không dùng `confirm()` của trình duyệt.

Ba quy tắc:

1. **Nói rõ mất gì, kèm số lượng** — "Xoá 12 buổi chưa học và sinh lịch mới? 3 buổi đã chốt vẫn
   được giữ", không phải "Bạn có chắc không?".
2. **Một xác nhận cho một ĐƠN VỊ CÔNG VIỆC, không phải cho mỗi ô.** Bảng nhập nhiều dòng gom
   thành một nút Lưu rồi hỏi một lần — hỏi từng ô thì chấm điểm lớp 20 học viên là 20 hộp thoại,
   người dùng bấm Đồng ý theo phản xạ và hộp thoại mất hết tác dụng. Đã xảy ra thật với bảng
   chấm điểm (07/09/2026).
3. **Focus mặc định vào nút Huỷ** — gõ Enter theo quán tính phải rơi vào hành động an toàn.

Không dùng `confirm()` của trình duyệt: nó không nói được cụ thể mất gì và không dịch được
theo ngôn ngữ đang chọn.
