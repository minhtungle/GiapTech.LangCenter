# Nguyên tắc UI/UX (bắt buộc khi code frontend)

## 1. Tối giản, mật độ thông tin cao

- **Bảng màu**: 1 màu chủ đạo + 1 màu nhấn + màu trạng thái cố định. Xem
  [design-tokens.md](design-tokens.md).
- **Datatable**: mật độ dòng gọn (compact density), lọc/sort **ngay trên tiêu đề cột**, không tách bộ lọc
  ra panel riêng cho các trường hợp đơn giản.
- **Trang thống kê**: KPI card gọn ở đầu → biểu đồ chi tiết bên dưới.
- Tùy chọn nâng cao **ẩn dưới "Xem thêm"**, không bày hết ra màn hình chính.

## 2. Luồng dễ hiểu

- **Sidebar** theo 5 module + **breadcrumb** ở mọi trang con. **Thu gọn được** (chỉ còn icon,
  nhớ lựa chọn trong `localStorage`); trên mobile chuyển thành ngăn kéo vì sidebar cố định
  chiếm quá nhiều bề ngang.
- **Tạo lớp học** = wizard nhiều bước, xem
  [FR-07](../06-nghiep-vu/lop-hoc.md). **Cho phép lưu nháp giữa chừng** — người dùng không bị
  mất dữ liệu khi rời tab, và lớp nháp không chiếm tên.
- **Tạo tài khoản** = wizard tuần tự đúng quy trình
  [FR-03](../06-nghiep-vu/quan-tri-he-thong.md#quy-trình-chuẩn-tạo-tài-khoản-wizard-tuần-tự): hồ sơ
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
- **Modal KHÔNG đóng khi bấm ra nền** (chốt 08/09/2026) — chỉ đóng bằng nút ✕, Esc, hoặc Huỷ.
  Form trong dự án này thường dài, và người dùng hay bấm ra ngoài để bỏ focus một ô hoặc đóng
  khung select đang mở: đóng theo cú bấm đó là **mất toàn bộ dữ liệu đang nhập, không cảnh báo**.
  Rủi ro đó lớn hơn tiện lợi của một cú bấm.
- **Select có nhiều lựa chọn → dùng `SelectTimKiem`** (chọn một) hoặc **`SelectTimKiemNhieu`**
  (chọn nhiều, hiển thị chip), không dùng `<select>` cơ bản. Tìm kiếm **bỏ dấu tiếng Việt**:
  gõ "nguyen" ra "Nguyễn".
- **Khung thả xuống của select dùng `position: fixed`**, không `absolute` (đổi 08/09/2026):
  `absolute` nằm trong luồng cuộn của modal nên bị khung cuộn ảnh hưởng, và không tự tránh được
  viền dưới. `fixed` + toạ độ chụp lúc mở đưa khung ra khỏi mọi khung cuộn, kèm **tự mở lên trên
  khi chỗ dưới không đủ**. Đánh đổi: phải tính lại vị trí khi cuộn — xem `useViTriTha` trong
  `SelectTimKiem.tsx`. Cùng cách `MenuThaoTac` thoát `overflow-x-auto` của `Table`.
- Giới hạn chiều cao ở chính thẻ `<dialog>` và cho dialog cuộn, **không** ở thân modal.
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
| Bảng điểm danh, bảng chấm điểm | **Desktop** | Bảng nhiều chiều, nhập liệu hàng loạt |
| Ma trận phân quyền (FR-05) | Desktop trước, **mobile vẫn đọc được** | Từ 14/09/2026 là lưới thẻ, tự xuống một cột |
| Xem lịch học, điểm danh, nộp bài, tra công nợ (giáo viên · học viên) | **Responsive tốt** | Nhóm dùng nhiều nhất, và sẽ tái dùng cho mobile app qua cùng API |

### Thư viện có state nội bộ: mỗi lần dữ liệu đổi là một đường sập app

`@headless-tree` (màn Cơ cấu tổ chức) giữ state **ngoài React**: `expandedItems`,
`focusedItem`, và cache cấu trúc cây. Sau khi xoá một node, state đó vẫn nhắc tới id đã mất —
thư viện hỏi lại dữ liệu và **ném lỗi nếu `dataLoader` trả giá trị falsy**:

```js
// @headless-tree/core 1.7 — core/dist/index.js:1362
if (!data) { throw throwError('sync dataLoader returned undefined') }
```

`!data` bắt cả `null`, nên `getItem: (id) => theoId.get(id) ?? null` là **sập cả app**, không
chỉ sập cây: màn hình trắng tinh, phải F5. Đã xảy ra thật 16/09/2026 (người dùng báo) khi xoá
một phòng ban — xoá thì thành công ở DB, nên reload xong thấy đúng, chỉ có cảm giác hệ thống vỡ.

Quy tắc khi dùng loại thư viện này:

- **`dataLoader` không bao giờ trả falsy.** Trả một *node giữ chỗ* (object thật, id rỗng) rồi
  bỏ nó ở vòng render. Node gốc ảo cũng vậy — nó không có trong DB nhưng thư viện vẫn hỏi.
- **Phải có E2E cho luồng xoá/thêm**, không chỉ cho luồng đọc. Lỗi kiểu này không hiện trong
  unit test (không có DOM), không hiện khi `tsc` (kiểu vẫn đúng), và không hiện ở màn tải lần
  đầu — chỉ hiện sau một thao tác làm dữ liệu đổi.
- E2E phải kiểm **"màn còn sống"** (`#root` còn nội dung, không có lỗi `pageerror`), không chỉ
  kiểm "bản ghi đã biến mất".

### Một state lỗi cho mỗi VÙNG, không dùng chung cho trang và modal

`<Modal>` luôn nằm trong DOM, nên `{maLoi && !moForm && ...}` ở thân trang **không** ngăn được
khối lỗi bên trong form hiện cùng lúc: cùng một lỗi hiện hai chỗ. Tệ hơn, người dùng mở form
thêm mới sau đó sẽ thấy sẵn lỗi của thao tác trước, không liên quan gì tới cái họ đang nhập.

Tách `maLoi` (lỗi trong form) và `maLoiTrang` (lỗi thao tác trên trang), và dọn `maLoiTrang`
mỗi khi mở form.

### ⚠️ Sửa component DÙNG CHUNG: đọc hết mục này trước khi đổi

> Thêm 11/10/2026 sau khi chủ sản phẩm nhận xét: *"bạn đang làm phần này lỗi phần khác"*.
> Ba lần liên tiếp một thay đổi ở component dùng chung làm hỏng thứ đang chạy tốt.

`Modal`, `Table`, `MenuThaoTac`, `Button`, `SelectTimKiem` mỗi cái có **30–50 nơi gọi**. Sửa
một dòng ở đó là sửa đồng thời 50 màn — tiện khi đúng, và hỏng 50 màn khi sai.

**Chuỗi sự cố thật, mỗi cái sinh ra từ cái trước:**

| Thay đổi | Hỏng cái gì | Vì sao không thấy trước |
|---|---|---|
| Khoá cột bảng bằng `sticky` | Menu thao tác chui xuống dưới các dòng | `sticky` tạo *stacking context*, `z-50` hết tác dụng |
| Menu đưa ra `document.body` | "Bấm ngoài thì đóng" tính nhầm cả bấm vào menu | Menu không còn nằm trong `boc` để `contains()` thấy |
| `ModalChan` kẻ hết bề ngang | Lộ ra tiêu đề/nút trôi khi cuộn | Đường kẻ rõ hơn làm lỗi sẵn có đập vào mắt |

**Bốn câu phải trả lời trước khi sửa component dùng chung:**

1. **Thay đổi này tạo stacking context hay scroll container mới không?**
   `position: sticky/fixed`, `overflow` khác `visible`, `transform`, `filter`, `opacity < 1`
   đều tạo. Thứ nổi lên trên (dropdown, menu, tooltip) sẽ bị nhốt hoặc bị cắt.

2. **Có phần tử nào đang định vị theo phần tử tôi đang đổi không?**
   `getBoundingClientRect`, `contains()`, `closest()`, `parentElement` — đổi cây DOM là đổi
   kết quả của chúng. Grep tên component trước khi đổi cấu trúc.

3. **Chú thích cũ ở chỗ tôi sắp sửa có còn đúng không?**
   `Modal` từng ghi *"KHÔNG dùng overflow-y-auto, sẽ cắt dropdown của SelectTimKiem"* — đúng
   lúc viết, **sai từ khi** `SelectTimKiem` đổi sang `position: fixed`. Kiểm lại trước khi
   tin, và **sửa luôn chú thích** nếu nó đã lạc hậu.

4. **Kiểm bằng cái gì?** `tsc`/`build`/vitest **không bắt được** loại lỗi này — JSX hợp lệ,
   kiểu đúng, không phép tính nào sai. Phải mở trình duyệt và **đo**: `getBoundingClientRect`,
   `elementFromPoint`, `scrollTop`. Ảnh chụp để nhìn, số đo để kết luận.

**`<dialog>` + class `display` là bẫy riêng, nhớ kỹ**: `<dialog>` chưa mở có `display: none`
do trình duyệt đặt. Một class `flex`/`grid`/`block` **không điều kiện** sẽ ghi đè cái đó, và
**mọi modal trong trang hiện cùng lúc chồng lên nhau**. Đã xảy ra 11/10/2026: đo được 2 dialog
`display: flex` trong khi 0 cái có thuộc tính `open`; nhìn ảnh thì tưởng "modal mất tiêu đề",
thực ra là hộp xác nhận của modal khác đang đè lên. **Luôn dùng biến thể `open:`** —
`open:flex`, không `flex`.

**Và đừng tin một phép đo duy nhất**: `elementFromPoint` ở tâm dropdown từng báo "bị đè" trong
khi ảnh chụp cho thấy nó hiện đầy đủ — selector bắt nhầm phần tử. Số đo mâu thuẫn với ảnh thì
nhìn ảnh.

### Bố cục modal: một mức khoảng cách, ô lẻ kéo hết hàng (11/10/2026)

Chủ sản phẩm báo "nhiều modal bố trí chưa cân đối". Đếm ra **77 khung lưới** trong các modal
dùng **6 mức gap khác nhau** (`gap-1.5`, `gap-2`, `gap-3`, `gap-4`…) — mỗi modal thưa dày một
kiểu.

Ba thứ sửa, tất cả đặt ở **component dùng chung** thay vì 77 chỗ gọi:

| Sửa ở đâu | Làm gì |
|---|---|
| `Modal` | `[&>form]:gap-4` + `[&>.grid]:gap-4` — một mức khoảng cách cho mọi modal |
| `ModalChan` | `-mx-4 px-4` cho đường kẻ chạy hết bề ngang; `mt-2` thay `mt-4` vì form đã có gap sẵn |
| `NutLocThem` | Ô lọc lẻ cuối cùng kéo hết hàng |

**Ô lẻ phải nhắm `div:last-of-type`, không phải `*:last-child`**: dòng chú thích `<p>` ở cuối
lưới đã tự chiếm 2 cột nên nó mới là `last-child`. Luật cũ nhắm trượt — đo thấy ô lọc vẫn
263px trong khi chú thích 542px.

**`LocDoiNhom` bỏ `w-48` cố định**, dùng `w-full` để khung chứa quyết định. Bề rộng cứng làm
hai ô đó hẹp hơn hẳn các ô cùng hàng (192px so với 263px).

Đo sau khi sửa: cả 4 ô lọc đều **263px**, ô lẻ **542px**.

### Modal: ba phần header · body · footer (chuẩn, 11/10/2026)

Chủ sản phẩm chỉ ra cách làm đúng: *"modal chia thành header, body, footer; header và footer
cố định, body nội dung dài thì cho scroll"*. Đúng chuẩn chung, và là cách duy nhất không phải
chắp vá.

```
<dialog>              flex flex-col overflow-hidden max-h-[...]   (open:flex!)
  ├─ header           shrink-0
  ├─ body             min-h-0 flex-1 overflow-y-auto   ← CHỈ chỗ này cuộn
  └─ footer           shrink-0                          ← anh em THẬT, không sticky
```

**`min-h-0` ở body là bắt buộc.** Trong flex column, con mặc định có `min-height: auto` nên
nó **không co nhỏ hơn nội dung** — `overflow-y-auto` không bao giờ kích hoạt, body cứ dài ra
và đẩy footer xuống khỏi màn hình. Đây là lỗi kinh điển của flexbox, không phải chuyện riêng
của dự án này.

**`<ModalChan>` viết ở đâu cũng được.** `Modal` tự tách nó khỏi `children` (hàm `tachChan`)
rồi dựng làm anh em của body. 31 chỗ gọi không phải sửa, và form viết sau này tự đúng.

**Nút submit vẫn chạy khi footer ra ngoài `<form>`**: `Modal` gắn `id` cho form và thêm
`form="<id>"` vào nút `type="submit"` — thuộc tính chuẩn HTML5, không phải mẹo.

**Mọi hàng nút trong modal dùng `<ModalChan>`**, đừng tự viết `<div className="flex
justify-end gap-2">`. Trước 11/10/2026 có **20/30 file tự viết**, nên chỉ một phần ba số modal
có footer cố định — đã chuyển hết 28 chỗ.

### Modal: tiêu đề và hàng nút ĐỨNG YÊN, chỉ thân cuộn (11/10/2026)

Trước đó cả `<dialog>` cuộn (`max-h` + `overflow-y-auto` ở cấp ngoài), nên form dài làm tiêu
đề trôi mất và nút Lưu phải cuộn xuống đáy mới bấm được.

Cấu trúc nay là **ba tầng**, đừng phá:

```
<dialog>                     flex flex-col overflow-hidden max-h-[...]
  ├─ tiêu đề                 shrink-0          ← không co khi thân dài
  └─ thân                    flex-1 overflow-y-auto   ← CHỈ chỗ này cuộn
       └─ <ModalChan>        sticky bottom-0 bg-card  ← dính đáy khung cuộn
```

**`ModalChan` phải `sticky`, không `fixed`**: mỗi màn tự đặt nó trong `children` nên `Modal`
không pin được từ ngoài. Và nó **bắt buộc có `bg-card`** — nền trong suốt thì nội dung cuộn
qua lộ ra dưới hàng nút.

**`shrink-0` ở tiêu đề là bắt buộc**: trong flex cột, phần tử mặc định co lại khi thiếu chỗ,
nên thiếu nó thì tiêu đề bị bóp dẹt thay vì thân cuộn.

Đo sau khi sửa (form Người dùng, viewport 700px): cuộn 438px mà tiêu đề đứng yên ở `top: 33`,
nút Lưu đứng yên ở `top: 583`.

### `<dialog>` không unmount — modal phải dựng lại nội dung mỗi lần mở (11/10/2026)

`<Modal>` dựng bằng `<dialog>` nên đóng modal **không gỡ nó khỏi DOM**, chỉ đổi thuộc tính
`open`. Mà React chỉ đọc `defaultValue` ở lần dựng **đầu tiên**.

Hệ quả: mọi ô uncontrolled giữ giá trị của lần mở trước. Mở sửa khoá A rồi mở sửa khoá B thì
giá tiền, số buổi… vẫn là của A — và người dùng bấm Lưu là **ghi đè dữ liệu B bằng số của A**.

Chủ sản phẩm báo 11/10/2026: danh sách ghi 7.275.000đ / 15 buổi, modal hiện 0đ / 40 buổi.
Đếm ra **15+ màn, 70+ ô `defaultValue`** cùng dính.

Sửa trong chính `Modal`: `key` đổi theo số lần mở ⇒ React dựng lại cây con ⇒ `defaultValue`
đọc lại. Một chỗ sửa, mọi modal hưởng, form viết sau này tự đúng.

Hai cách còn lại đều tệ hơn: đổi 70+ ô sang controlled (dài, dễ sót, mỗi ô mới lại phải nhớ),
hoặc gắn `key` ở từng nơi gọi `<Modal>` (cũng 20+ chỗ phải nhớ).

**`tsc`, `npm run build` và vitest đều XANH với lỗi này** — JSX hợp lệ, kiểu đúng, không phép
tính nào sai. Chỉ lộ ra khi thật sự mở modal hai lần trong trình duyệt. Nên nó được canh bằng
E2E (`modal-dien-lai-du-lieu.spec.ts`), không phải unit test.

### Phản hồi sau thao tác: vòng xoay khi chạy, thông báo khi xong (11/10/2026)

Chủ sản phẩm báo: gửi mail trông như **treo máy**, và nhiều thao tác xong mà không biết có
thành công không.

**Đo trước khi sửa**: 58/65 lệnh ghi (`onSuccess`) không báo gì khi xong — chỉ 7 lệnh có.

Hai thứ bổ sung, mỗi thứ trả lời một câu:

| Câu hỏi của người dùng | Thứ trả lời |
|---|---|
| "Bấm rồi, nó có chạy không?" | **Vòng xoay trên nút** (`<Button dangXuLy>`) |
| "Chạy xong rồi, có thành công không?" | **Thông báo nổi** góc dưới phải |

#### Vì sao nút cần vòng xoay, trong khi đã có thanh tiến trình trên đỉnh

Thanh đó mảnh 2px, chỉ hiện sau 250ms, và nằm **xa chỗ vừa bấm**. Nút là nơi mắt đang nhìn.

Đổi NHÃN thôi chưa đủ: màn gửi mail đã đổi thành "Đang gửi…" từ trước mà vẫn bị đọc nhầm là
treo — **chữ tĩnh vẫn trông như đứng hình**. Phải có thứ chuyển động.

`dangXuLy` tự khoá nút luôn, nên nơi gọi không phải viết thêm `disabled` — và quan trọng hơn
là không thể QUÊN viết: bấm hai lần gửi hai email.

Với `prefers-reduced-motion`, vòng xoay **quay chậm lại chứ không dừng hẳn** (khác các animation
khác trong dự án): dừng hẳn thì nút chỉ còn mờ đi — đúng trạng thái bị đọc nhầm là treo.

#### Thông báo đặt ở MutationCache, không rải vào từng `onSuccess`

Một chỗ duy nhất (`App.tsx`) thấy mọi lệnh ghi của cả app. Sửa 58 chỗ thì vừa dài vừa chắc
chắn sót, và lệnh viết sau này lại quên tiếp.

Nhãn suy từ **phương thức HTTP**: `delete` → "Đã xoá", còn lại → "Đã lưu". Bắt 19 lệnh xoá tự
khai thì sót ngay từ đầu. Canh bởi `thongBao.test.ts`.

Màn nào **đã có phản hồi riêng** khai `meta: { tuBao: true }` để không nhận hai lời xác nhận
chồng nhau — hiện có hai: gửi mail khách (modal hiện số thư đi được / số lỗi) và nhận xét buổi
học (có dòng "đã gửi" riêng).

**Không báo LỖI qua thông báo nổi** cho lỗi biểu mẫu: lỗi phải nằm cạnh ô gây lỗi để người
dùng biết sửa ở đâu, và `CanhBaoLoi` trong form đã làm việc đó.

### Bảng rộng: cuộn ngang thay vì bóp cột (10/10/2026)

Trước đó `Table` chỉ có `w-full`, nên bảng **luôn vừa khung** — và `overflow-x-auto` sẵn có
không bao giờ kích hoạt. Thay vào đó cột tự bóp, chữ xuống dòng.

Đo màn Doanh thu (10 cột) ở ba bề rộng:

| Bề rộng | Khung | Bảng | Tràn? |
|---|---|---|---|
| 1440 | 1134 | 1132 | không |
| 1280 | 974 | 972 | **không** |
| 1024 | 718 | 954 | có |

Ở 1280 — bề rộng laptop phổ biến nhất — bảng vừa khít một cách tệ hại: "Hình thức thanh toán"
vỡ 3 dòng, mỗi hàng cao 3 lần cần thiết, màn 900px chỉ thấy **4 dòng dữ liệu**.

Nay `Table` có `min-w-[56rem]` + `whitespace-nowrap`: bảng đạt bề rộng thật, hẹp hơn thì cuộn.
Sau khi sửa: bảng 1177px trong khung 972px, hàng cao 53px, **8 dòng** trên cùng màn hình.

Bảng ít cột (≤ 4) truyền `rongToiThieu=""` để tắt — ép `min-width` chỉ tạo thanh cuộn thừa.
Ô cần xuống dòng (ghi chú dài) tự đặt `whitespace-normal`.

**Đừng bọc `Table` trong một `div` có `overflow-x-auto` nữa**: khung ngoài nuốt mất thanh cuộn
của khung trong, cột dính hết tác dụng, và `scrollLeft` kẹt ở 0 (đã gặp thật ở màn Doanh thu).

### Khoá cột đầu và cột thao tác (10/10/2026)

`<Th dinh="dau">` / `<Td dinh="dau">` cho cột đầu, `dinh="cuoi"` cho cột thao tác.

- **Cột đầu** (tên người, tên lớp): cuộn sang phải vẫn biết dòng đang đọc là của ai.
- **Cột thao tác**: nút sửa/xoá luôn trong tầm bấm.

Hai điều kiện để dùng:

1. Bảng phải **≥ 7 cột** — bảng hẹp không cuộn thì cột dính chỉ thêm đường kẻ thừa.
2. Cột cuối phải **thật sự là thao tác**. Màn Học phí có `<Th />` ở cuối nhưng đó là cột
   **trạng thái**, khoá nó là sai — đã thử rồi hoàn tác.

Ô dính mang nền ĐỤC (`bg-card`) nên không ăn `hover:bg-muted/40` của `<tr>`; `lopDinh` xử lý
riêng bằng `[tr:hover_&]`. Bỏ qua thì rê chuột sáng cả hàng trừ hai cột dính, trông như lỗi.

### Bảng chỉ đúng khi ma trận ĐẶC và nhãn NGẮN

Bài học từ ma trận phân quyền (14/09/2026). Nó là bảng chức năng × thao tác, và hỏng dần khi
dữ liệu đổi hình:

| Triệu chứng | Ngưỡng |
|---|---|
| Nhãn cột phải **xoay dọc** mới vừa | Đã quá muộn — người đọc nghiêng đầu |
| Ma trận **thưa** | 49 ô có nghĩa trên 300 vị trí: mắt không lần được hàng nào ứng với cột nào |
| Một cột chỉ dùng cho **một hàng** | `Sinh lịch học` chiếm cột suốt chiều cao bảng vì đúng một chức năng cần |

Khi gặp những dấu hiệu đó, **đổi trục**: mỗi bản ghi một thẻ, thuộc tính nằm *trong* thẻ. Nhãn
nằm ngang đọc bình thường, không còn ô trống, và mỗi thẻ mang được số đếm riêng — thông tin
bảng không có chỗ nào đặt. Đánh đổi: mất khả năng so sánh theo cột, nên chỉ đổi khi việc chính
là **cấu hình từng bản ghi**, không phải **so sánh giữa các bản ghi**.

Đừng dùng `rotate-90` để cứu nhãn cột dài: `transform` không đổi ô mà phần tử chiếm, nên chữ
tràn ra rồi bị cắt (nhãn còn 3 ký tự — "Xen", "Thê"). Cần chữ dọc thật thì dùng `writing-mode`.

### Checkbox trần vs nhãn bấm được

Checkbox 16px không mang chữ, nên trong lưới thưa người dùng phải đối chiếu với đầu cột mới
biết mình đang bật gì. Nhãn bấm được (`<label>` bọc `<input class="sr-only">`) mang luôn tên
thao tác, vùng bấm rộng cả nhãn, trạng thái đọc bằng màu nền thay vì một dấu tích 4px — mà vẫn
giữ Tab, phím Space và trình đọc màn hình. Đừng thay bằng `<div onClick>`.

### Ô trống ≠ checkbox mờ

Tổ hợp không áp dụng thì để **trống**, không hiện checkbox `disabled`. Checkbox mờ vẫn là
checkbox: người đọc phải thử mới biết nó không bấm được.

## 5. Quy ước màu trạng thái

Dùng **thống nhất** ở mọi module, không đổi nghĩa theo ngữ cảnh:

| Màu | Token / `Badge variant` | Ý nghĩa trong LMS |
|---|---|---|
| 🟢 Xanh | `ok` | **Xong · đạt · đủ**: lớp đang học, buổi đã chốt, bài đã chấm, học phí đã đủ, thao tác thành công |
| 🔴 Đỏ | `loi` | **Hỏng · quá hạn · bị từ chối**: lớp đã huỷ, nộp muộn, học phí quá hạn, vắng khác lời khai |
| 🟡 Vàng | `cho` | **Đang chờ · cần chú ý**: lớp nháp, còn nợ học phí, buộc đổi mật khẩu, giáo viên riêng |

**Đừng dùng màu trạng thái cho thông tin bình thường.** Bản đầu của trang phân quyền tô màu
nhấn cho mọi số đếm đã cấp (`3/3`) và cho số tài khoản đang dùng — không cái nào "cần chú ý",
và khi mọi thứ đều sáng màu nhấn thì ô **thật sự** cần cân nhắc (quyền leo thang đặc quyền,
quyền dính tiền) chìm lẫn vào. Phân biệt bằng đậm/nhạt trước; để dành màu cho thứ đáng dừng lại.

Đổi tên từ `win`/`lose`/`draw` ngày 08/09/2026 (nợ N8 — đã xong): tên cũ là di sản của dự án
tiền thân, và "thắng/thua/hoà" không có nghĩa gì trong nghiệp vụ đào tạo.

Áp dụng thống nhất ở lịch học (FullCalendar), mọi bảng danh sách, và nhật ký hệ thống.

## 6. Đa ngôn ngữ

Mọi chuỗi hiển thị đi qua `react-i18next`. Message lỗi từ API là **mã lỗi**, frontend tra bảng dịch —
xem [cqrs-mediatr.md](../03-backend/cqrs-mediatr.md#trả-lỗi). Không hard-code tiếng Việt trong component.

## Ô nhập văn bản dài

Mọi trường **có thể dài** dùng `<Textarea>`, không dùng `<Input>`: nhận xét, ghi chú, mô tả,
nhận xét buổi học. `<input>` một dòng cắt nội dung khỏi tầm nhìn ngay khi vượt bề rộng ô —
người dùng gõ một đoạn nhận xét rồi không đọc lại được đoạn đầu, phải rê con trỏ mới thấy.

Trường ngắn (tên, phòng học, URL, ngày) vẫn dùng `Input`.

**Luôn dùng component `<Textarea>`, đừng viết `<textarea>` thô.** Viết thô là phải chép lại toàn
bộ class, và trên thực tế chép thiếu: ô `ngayLoaiTru` ở màn sinh lịch mất cả focus ring, trạng
thái disabled và trần chiều cao (sửa 09/09/2026).

### Trần và sàn chiều cao (09/09/2026)

`resize-y` không kèm giới hạn thì tay cầm kéo được **vô hạn**. Đo thật: ô ghi chú kéo lên 3082px
trong modal cao 836px, nút Lưu bị đẩy xuống dưới 3667px scroll — form coi như hỏng.

Component đặt sẵn:

| Giới hạn | Giá trị | Vì sao |
|---|---|---|
| Trần | `max-h-72` (288px ≈ 12 dòng) | Quá đó thì nội dung cuộn TRONG ô, không đẩy form dài ra |
| Sàn | `calc(rows * 1.5em + 1rem)` | Theo **chính `rows` của ô đó** — một sàn cứng dùng chung sẽ bóp ô `rows={4}` (100px) xuống còn 64px |

Sàn đặt qua `style` chứ không class (Tailwind không có class động theo prop), và **phải đứng sau
`{...props}`** kèm trải lại `props.style`: đặt trước thì caller truyền `style` ghi đè cả object
và mất sàn — lỗi im lặng, không có cảnh báo biên dịch.

## View chi tiết: xem trước, sửa là hành động có chủ ý

**Tab thông tin của mọi view chi tiết chỉ ĐỌC, kèm một nút mở modal sửa** (chốt 09/09/2026 —
trước đó form sửa hiện sẵn ngay cạnh khối thông tin).

Vì sao: đọc thông tin là việc làm thường xuyên hơn sửa rất nhiều. Để form nằm sẵn thì mỗi lần chỉ
muốn xem số điện thoại hay sĩ số đều phải nhìn qua một form không dùng tới, và ở lưới hai cột thì
khối thông tin bị bóp còn nửa bề rộng.

Đã áp cho: `ChiTietKhachHang` (tab *Thông tin chung*), `ChiTietLopHoc` (tab *Tổng quan*).

Ba điểm dễ sai:

1. **Không cần `key` remount cho form nữa.** Bản cũ phải ghép `key` từ mọi trường để form nạp lại
   giá trị sau khi lưu (các ô dùng `defaultValue` không tự cập nhật). Modal chỉ mount form lúc
   mở, nên bỏ được cả mẹo đó.
2. **State không phải `defaultValue` thì phải reset khi đóng.** Ô select lưu bằng `useState` sẽ
   giữ lựa chọn bỏ dở sang lần mở sau — `onDong` phải gán lại giá trị đang lưu.
3. **Bỏ dòng "Đã lưu" tạm.** Modal tự đóng đã là phản hồi đủ rõ; giữ thêm badge biến mất sau
   2.5s là dư.

Form dùng chung nhiều nơi (`FormLopHoc` còn dùng ở màn tạo lớp) thì **truyền prop**, đừng sửa
component — nó đã có `onHuy` sẵn để modal có nút Huỷ.

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
