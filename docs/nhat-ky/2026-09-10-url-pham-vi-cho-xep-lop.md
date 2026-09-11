# 2026-09-10 (LMS) — URL `/lms`, phạm vi học viên, gộp "Chờ xếp lớp"

Ba việc trong cùng một buổi, đều **bắt đầu từ một nhận xét của chủ sản phẩm** chứ không phải từ
kế hoạch — và cả ba đều lộ ra thứ nặng hơn cái được hỏi.

## 1. URL LMS thiếu tiền tố

> *"vào crm và hrm, url có dạng /crm/ hoặc /hrm/ còn lms thì không"*

Đúng. Nguyên nhân: **LMS dựng trước khi tách ba hệ thống con** (08/09), nên 7 route của nó
(`/lop-hoc`, `/hoc-vien`, `/buoi-hoc`, `/tai-lieu`, `/hoc-phi`) không có tiền tố.

Cái giá thật không phải chuyện URL xấu, mà nằm trong `Layout.tsx`: vì LMS không có tiền tố, bộ
chuyển hệ thống phải giữ một **danh sách 5 đường hardcode** để suy ra đang ở hệ thống con nào.
Thêm một màn LMS mới mà quên khai vào danh sách đó → sidebar hiện **sai hệ thống**, lỗi im lặng
không có lỗi biên dịch. Nay cả ba đều suy từ tiền tố, còn đúng một bảng tra và danh sách hardcode
đã xoá được.

### Chỗ nguy hiểm nhất: 63 dòng gọi API trông y hệt route

Trong 38 chỗ nhắc `/lop-hoc`, `/hoc-vien`…, có **63 dòng là lời gọi API** (`api.get('/lop-hoc/…')`)
chứ không phải điều hướng. Find-replace hàng loạt sẽ phá sạch mọi lời gọi API của LMS.

Cách làm an toàn: chỉ sửa dòng có ngữ cảnh điều hướng (`navigate(`, `to=`, `to:`), **loại mọi dòng
chứa `api.`**, rồi **kiểm chéo hai chiều**:

```
KIỂM 1: có lời gọi API nào thành /lms/ không?   → rỗng ✅
KIỂM 2: còn dòng điều hướng nào chưa đổi?        → rỗng ✅
```

`NguoiDung.tsx` giữ nguyên `duong: '/hoc-vien'` vì đó là **endpoint**, không phải route — file đó
vốn đã tách `duong` (API) khỏi `duongChiTiet` (route) đúng vì lý do này.

### Chuyển tiếp phải giữ `:id` và query

Đường cũ vẫn chuyển sang `/lms/...`, nhưng `<Navigate to="/lms/lop-hoc">` **tĩnh** sẽ làm mất id
và `?tab=lich` — link tới đúng một lớp/buổi cụ thể sẽ lặng lẽ rơi về danh sách. Dùng component đọc
`useLocation()`:

```tsx
const { pathname, search, hash } = useLocation()
return <Navigate to={`/lms${pathname}${search}${hash}`} replace />
```

`replace` để Back không kẹt vòng (trang mới → đường cũ → lại chuyển tiếp sang mới).

Đột biến: bỏ `search`/`hash` → test đỏ đúng dòng khẳng định query sống sót.

## 2. "Học viên và giáo viên dùng chung module lớp học?"

Câu trả lời: **dùng chung**, và hệ thống đã làm đúng vậy. "Chỉ thấy lớp đang quản lý" là **lọc
hàng**, không phải chức năng khác — cả ba vai trò cùng gọi `GET /lop-hoc` với cùng quyền
`LopHoc.Xem`, `IPhamViLopHoc` quyết định thấy hàng nào.

Tách module riêng sẽ **tệ hơn**: phải nhân bản điểm danh, bài tập, buổi học, tài liệu ba lần; và
**không thêm lớp an toàn nào**, vì rò rỉ xảy ra ở tầng truy vấn chứ không ở tầng route.

### Nhưng câu hỏi làm lộ một lỗ hổng vùng phủ test

Chỉ có test cho **giáo viên** (`Giao_vien_chi_thay_lop_minh_phu_trach`). Nhánh **học viên**
(`l.HocViens.Any(...)`) trong `PhamViLopHoc.LocTheoPhamVi` **không test nào canh** — mà đây là chỗ
rò rỉ nặng nhất nếu sai: học viên thấy mọi lớp của trung tâm.

Chứng minh bằng đột biến: bỏ nhánh học viên khỏi bộ lọc → **13/14 test trong `LopHocTests` vẫn
xanh**. Tức trước đó ai refactor làm mất nhánh đó sẽ không có gì báo.

Thêm `Hoc_vien_chi_thay_lop_minh_dang_hoc`, cùng khuôn test giáo viên: kiểm danh sách **và** kiểm
**IDOR** (gõ thẳng id lớp mình không học → **404**, không phải 403 — 403 tự xác nhận lớp đó tồn
tại). Có bước `hoan-tat` để lớp rời trạng thái nháp, nếu không test sẽ xanh **vì lý do sai** (lớp
nháp chỉ người tạo thấy).

## 3. Gộp "Chờ xếp lớp" vào màn Lớp học

> *"không cần tách module riêng, sẽ hiển thị chung lớp học cho ai có quyền"*

Làm thành **tab** của `/lms/lop-hoc` (`?tab=cho-xep-lop`), bỏ mục menu riêng. `ChoXepLop` nhận prop
`nhung` để bỏ tiêu đề/link quay lại khi nằm trong tab — cùng quy ước sẵn có với `LichVaDiemDanh`,
`BaiTapCuaLop`, nên không phải nhân bản 332 dòng vào `LopHoc.tsx`.

### Việc gộp lộ ra một lỗi phân quyền thật

Mục menu cũ gác bằng `LopHoc.**Xem**`, còn **cả ba endpoint** chờ xếp lớp đòi `LopHoc.**Sua**`.
`Xem` là quyền **giáo viên và học viên cũng có** → họ thấy menu "Chờ xếp lớp", bấm vào và nhận
**403**. Một mục menu chết.

Kiểm bằng curl chứ không suy từ code:

```
GIÁO VIÊN có quyền gì trên LopHoc?  →  ['Xem']
GET /lop-hoc/cho-xep-lop            →  403
```

Ba chi tiết để người quyền thấp không bị đổi trải nghiệm:

- **Thanh tab chỉ hiện khi có >1 tab được phép** — người chỉ có `LopHoc.Xem` thấy đúng bảng lớp
  như trước, không có thanh tab một mục vô nghĩa.
- Gõ thẳng `?tab=cho-xep-lop` khi không có quyền thì rơi về danh sách, không phải tab trắng.
- Query đếm hàng chờ (badge trên nhãn tab) có `enabled` theo quyền — không có nó thì giáo viên và
  học viên bắn một request chắc chắn 403 **mỗi lần** mở màn Lớp học.

Đột biến: gác tab lại bằng `Xem` (đúng lỗi cũ) → test đỏ ngay tại "giáo viên không được thấy tab".

## Bốn lần tự sai trong buổi này

1. **`getByText('ĐÀO TẠO')`** — nhãn hoa bằng CSS `uppercase`, text trong DOM là "Đào tạo".
2. **`getByLabel('Lọc trạng thái')`** — nhãn thật là "Lọc **theo** trạng thái". Ảnh chụp lúc đỏ
   cho thấy **app hoàn toàn đúng**: giáo viên không có thanh tab, không có menu, chỉ có bảng lớp.
3. **Đoán 3 tên trường khi gọi API bằng curl** (`taoTaiKhoan`, thiếu `troGiangIds`, thiếu
   `hinhThuc`) — app đúng cả 3 lần. Riêng `HinhThucHoc.ChuaChon = 0` là **thiết kế phòng vệ có chủ
   ý**: JSON thiếu trường enum sẽ deserialize thành 0, nếu 0 là một hình thức thật thì client quên
   gửi sẽ âm thầm ghi sai.
4. **`url-he-thong-con.spec.ts` đỏ vì hết ngân sách 30s**, không phải lỗi app — test đó đi qua ~12
   lần điều hướng, thanh tab mới thêm chút thời gian render và đẩy nó qua vạch (chạy thật 31.6s).
   Nới `test.setTimeout(90s)` thay vì bỏ bước kiểm: mỗi bước canh một thứ khác nhau.

Bài học chung: **khi test đỏ, xem ảnh chụp trước khi sửa code**. Ba trong bốn lần trên, app đúng
và test sai.
