# 17/09/2026 — Đổi nick xong giao diện vẫn giữ quyền nick cũ

## Người dùng báo

> "tôi bị lỗi khi đăng xuất và đăng nhập tài khoản khác thì giao diện quyền đang ở nick cũ,
> phải ctrl shift R mới refesh được"

Tái hiện đúng như vậy: đăng nhập `admin`, đăng xuất, đăng nhập `hv1` → học viên thấy nguyên
menu quản trị:

```
Tổng quan | Nhân sự | Thống kê nhân sự | Tiêu chí đánh giá | Tài khoản | Phân quyền |
Thiết lập chung | Nhật ký hệ thống
```

Đúng ra chỉ được: `Tổng quan | Lớp học | Khoá trực tuyến | Tài liệu`.

## Gốc rễ

Mọi `queryKey` trong app đều là **hằng** — `['toi-quyen']`, `['toi-he-thong']`,
`['nguoi-dung-ngan']`… — **không mang danh tính người đăng nhập**. Cộng với `staleTime: Infinity`
ở `useQuyen` (quyền chỉ đổi khi admin sửa nhóm quyền, nên cache theo phiên là hợp lý *khi phiên
không đổi*), phiên mới dùng lại nguyên cache phiên cũ.

Quan sát trực tiếp trên trình duyệt xác nhận: cả hai lần đăng nhập chỉ có **một** request
`GET /toi/quyen` — lần thứ hai không gọi lại, lấy thẳng từ cache.

Không rò rỉ dữ liệu: backend vẫn 403 mọi endpoint ngoài quyền. Nhưng hiện menu bấm vào chỉ nhận
lỗi là sai với người dùng, và dữ liệu nghiệp vụ đã tải sẵn là của phiên trước.

## Bản sửa

`queryClient.clear()` ở **cả** `dangNhap` và `dangXuat` (`frontend/src/lib/auth.tsx`).

Không đi thêm `username` vào từng `queryKey`: ~20 chỗ phải nhớ, và chỗ thứ 21 thêm sau này sẽ
quên. `clear()` đúng ngữ nghĩa "phiên mới thì không còn gì của phiên cũ" và chỉ có một chỗ đổi
phiên nên không thể quên.

Ở `dangNhap` phải xoá **trước** khi đặt phiên: đặt phiên làm các query `enabled: daDangNhap`
chạy ngay, clear sau đó vứt luôn kết quả vừa tải và chúng phải gọi lại lần hai.

## Ba điều rút ra

### 1. Test E2E đầu tiên vô dụng mà vẫn xanh

Bản đầu dùng `page.goto('/dang-nhap')` để quay về màn đăng nhập. `goto` là **tải lại trang** ⇒
cache trong bộ nhớ JS mất sạch ⇒ test xanh **kể cả khi đã gỡ hết bản sửa**. Nó kiểm đúng cái
đường đã lành (Ctrl+Shift+R — chính thao tác người dùng đang phải làm tay) và bỏ qua đường hỏng.

Phát hiện ra nhờ **mutation test**, không phải nhờ đọc lại code: gỡ cả hai `qc.clear()` mà test
vẫn xanh thì hoặc bản sửa thừa, hoặc test sai — phải đi tìm cho ra là cái nào.

Sửa: bấm **nút Đăng xuất** rồi điền thẳng form, đúng đường người dùng đi và là đường duy nhất
giữ nguyên `QueryClient`.

### 2. Kết quả mutation test chỉ có nghĩa khi môi trường sạch

Vòng mutation đầu cho kết quả lộn xộn (mutant A "chết", B "sống"). Đọc kỹ thì A không chết vì
assertion mà vì **timeout khi đăng nhập** — ảnh chụp màn hình có dòng *"Bạn thao tác quá nhanh"*.
API đang chạy **không** có `GIOI_HAN_TAN_SUAT=false`, mà mỗi lần chạy test tốn 3 lần đăng nhập
trên hạn mức 10/phút.

Khởi động lại API với cờ đúng rồi chạy lại: A và B **đều sống**, chỉ A+B mới chết. Kết luận ngược
hẳn với vòng đầu. Bài học: test đỏ phải đọc *lý do* đỏ, không chỉ đếm đỏ/xanh.

### 3. Hai `qc.clear()` là thừa có chủ ý

Gỡ một trong hai thì test vẫn xanh — không có test nào giữ từng dòng riêng lẻ. Vẫn giữ cả hai
vì chúng lo hai việc khác nhau:

- `dangNhap` lo **đúng**: kể cả khi vào thẳng không qua `dangXuat` (phiên hết hạn ở tab khác,
  token bị thu hồi, mở lại app rồi đăng nhập nick khác).
- `dangXuat` lo **kín**: máy dùng chung — đăng xuất xong đứng dậy đi, dữ liệu người trước
  (danh sách nhân sự, khách hàng…) không được nằm lại trong RAM tab chờ người sau.

Đã ghi lý do ngay tại chỗ trong code để không ai gỡ vì tưởng dead code.

## Tác dụng phụ: lộ ra một race có sẵn

`hoc-vien-chi-xem-buoi-hoc.spec.ts` đỏ khi chạy cả bộ (xanh khi chạy một mình). Không phải do
bản sửa làm hỏng: test chờ ghi chú *"chỉ có quyền xem"* — vẽ ra từ **quyền** — rồi đếm ngay ô
`select` của bảng điểm danh, vốn đến từ **một query khác**. Cache trống làm khoảng chờ rộng ra
nên lỗi lộ ra.

Đã thêm bước chờ dòng đầu của bảng. Kiểm lại bằng mutation (bỏ `disabled={!duocSua}`) — test vẫn
bắt được, tức nó chỉ hết race chứ không bị làm cùn.

## Kiểm chứng

| Bản | Kết quả |
|---|---|
| Có cả hai `clear()` | xanh |
| Bỏ ở `dangNhap` | xanh (chỗ kia gánh) |
| Bỏ ở `dangXuat` | xanh (chỗ kia gánh) |
| **Bỏ cả hai (lỗi gốc)** | **đỏ** — học viên thấy `Phân quyền` |

558 test backend · 28 vitest · **41 E2E** đều xanh.
