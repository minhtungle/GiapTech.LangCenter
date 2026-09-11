# 2026-09-11 — "Duyệt rồi mà vẫn còn trong danh sách chờ"

Một báo cáo, **hai lỗi khác nhau**, tìm ra ở hai lượt. Phần 1 là cái bẫy click ở tầng UI; phần 2
là bế tắc thật trong handler duyệt. Đường đi tới kết luận đáng ghi hơn chính bản vá.

# Phần 1 — Cái bẫy click: hai modal, hai nút cùng tên

## Báo cáo ban đầu và hai lần thu hẹp

> *"Sau khi duyệt học viên vào lớp thì danh sách học viên vẫn tồn tại dữ liệu đó"*

Lần hỏi lại đầu tiên thu hẹp được: không phải màn Học viên (LMS), mà là
`/lms/lop-hoc?tab=cho-xep-lop` **và** tab Học viên trong view chi tiết lớp.

Nếu dừng ở suy luận thì rất dễ đi sai. Ba giả thuyết đầu đều **sai**, và đều nghe hợp lý:

| Giả thuyết | Vì sao bác được |
|---|---|
| Query hàng chờ quên lọc trạng thái | Đọc handler: lọc `TrangThai == (request.TrangThai ?? DangCho)` — đúng |
| Frontend không invalidate cache sau khi duyệt | `lamMoi()` invalidate cả `cho-xep-lop`, `lop-hoc`, `khach-hang` — đúng |
| `staleTime: 30_000` chặn refetch | `invalidateQueries` refetch query đang active bất kể `staleTime` |

## Đo bằng dữ liệu thật, không suy từ code

Dựng kịch bản đầu-cuối qua API trên PostgreSQL thật: khoá học → khách → đơn → yêu cầu xếp lớp →
lớp → duyệt. Kết quả: **HTTP 204, hàng chờ về 0 dòng, học viên vào lớp với học phí từ đơn CRM**.
Backend đúng hoàn toàn.

Làm tiếp cho **từ chối**: `TrangThai = TuChoi`, cũng rời hàng chờ ngay. Tức hành vi mà chủ sản
phẩm yêu cầu ("duyệt hoặc từ chối rồi thì không còn trong danh sách chờ") **đã đúng sẵn**.

Vậy lỗi nằm ở đâu?

## Chính test của mình sập bẫy, và đó là manh mối

Viết E2E tái hiện trên trình duyệt thật. Lần chạy đầu cho kết quả kỳ lạ:

```
SAU DUYET, con o tab cho (khong F5)? false   ← trông như đã xong
SAU F5, con o tab cho?                true   ← dòng QUAY LẠI
SO DONG trong tab hoc vien:           0
API hang cho con:                     1 dong ← chưa ghi gì cả
```

Đây **đúng** hiện tượng người dùng báo. Mà test chỉ làm một việc: bấm nút `.last()` có nhãn
"Duyệt vào lớp".

Xem ảnh chụp thì rõ: **hai modal chồng nhau, cả hai đều có nút "Duyệt vào lớp"**.

## Nguyên nhân thật

`showModal()` đưa dialog vào **top layer**. `::backdrop` của dialog trên chỉ che *nội dung trang
thường* — **không che dialog khác cũng đang ở top layer**. Nên:

- Modal "Duyệt học viên vào lớp" mở hộp xác nhận → hộp xác nhận nằm đè lên
- Nút của modal dưới **vẫn hiện ra ngay bên dưới** nút hộp trên, cách ~77px
- Cả hai **cùng nhãn** vì `i18n.xepLop.duyet = 'Duyệt vào lớp'` được dùng cho **cả** nút modal
  **lẫn** `nhanDongY` của hộp xác nhận
- Bấm nhầm nút dưới → mở lại chính hộp xác nhận đó. **Không ghi gì. Không lỗi gì.**

Im lặng là phần tệ nhất: không có 4xx, không có toast, không có gì để người dùng biết mình vừa
bấm hụt. Họ đóng modal, tin là đã duyệt, và phát hiện ra khi F5.

Điều này cũng giải thích vì sao hiện tượng **không xảy ra mọi lần** — bấm trúng nút trên thì mọi
thứ chạy đúng.

### Chú thích sẵn có trong code đã cảnh báo đúng rủi ro này

`useXacNhan` có dòng:

> *"Đóng TRƯỚC khi chạy: nếu hành động mở modal khác thì hai lớp modal chồng nhau và người dùng
> không biết bấm vào đâu."*

Đúng rủi ro, nhưng ở đây **thứ tự ngược lại**: modal mở trước, hộp xác nhận mở sau — nên biện
pháp đó không với tới.

## Bản vá: sửa ở `Modal`, không ở màn Chờ xếp lớp

Cám dỗ là đổi nhãn nút cho khác nhau. Nhưng đó là vá triệu chứng: hai nút vẫn cạnh nhau, vẫn bấm
nhầm được, và **mọi màn khác vẫn còn nguyên cái bẫy** — 20 màn dùng `Modal`, 23 chỗ dùng
`useXacNhan`.

Sửa nguyên nhân: giữ ngăn xếp dialog đang mở; lớp không phải trên cùng bị `inert` + mờ còn 40%.

```ts
const dangMo: HTMLDialogElement[] = []

function capNhatLopTrenCung() {
  dangMo.forEach((d, i) => {
    const duoiCung = i < dangMo.length - 1
    d.inert = duoiCung
    d.classList.toggle('opacity-40', duoiCung)
    d.classList.toggle('backdrop:bg-transparent', duoiCung)
  })
}
```

Ba chi tiết không hiển nhiên:

- **`inert` chứ không `pointer-events: none`**: cái sau chặn chuột nhưng vẫn cho **tab tới nút
  rồi gõ Enter** — cùng một lỗi qua đường bàn phím.
- **Dọn khi unmount lúc đang mở** (đổi route, cha ngừng render): nhánh `!mo` không chạy, dialog
  nằm lại trong ngăn xếp và khoá `inert` **vĩnh viễn** lên mọi modal mở sau đó. Một `useEffect`
  cleanup riêng lo việc này.
- **Tắt backdrop của lớp dưới**: backdrop cộng dồn làm nền đen đặc thêm ở mỗi lớp.

## Kiểm chứng

`e2e/modal-long-nhau.spec.ts` canh năm điều, trong đó điều quan trọng nhất là **bấm nút lớp dưới
không được mở thêm modal nào**.

Đột biến chứng minh test có răng: đổi `d.inert = duoiCung` thành `d.inert = false` → **đỏ đúng
dòng** "modal dưới phải inert khi hộp xác nhận mở".

- 430 test backend xanh (65 unit + 365 integration), build 0 warning
- E2E: **16 xanh / 4 đỏ**, cả 4 đỏ là **nợ N16** có sẵn (test lạc hậu thời dự án bóng đá). Đã
  kiểm chéo bằng cách tạm gỡ bản vá: 4 test đó **đỏ y hệt** khi không có thay đổi này
- `tsc -b`, `vite build`, `oxlint` sạch

## Bài học

**"Dữ liệu không lưu" không đồng nghĩa với "backend sai".** Ở đây mọi tầng dữ liệu đều đúng;
thứ hỏng là cú bấm không bao giờ tới được handler.

Và: khi test tự động của mình mắc đúng lỗi mà người dùng mắc, đó không phải chuyện phiền toái
của test — đó là **bản tái hiện chính xác nhất** của lỗi.

---

# Phần 2 — Bế tắc thật: duyệt xong vẫn "đã trong lớp"

Sau khi vá cái bẫy click, người dùng báo lại — và lần này là **lỗi khác hoàn toàn**:

> *"Tôi vẫn thấy Nguyễn Thu Hà còn trong danh sách chờ lớp, khi ấn duyệt thì vào lớp thì báo
> học viên đã trong lớp này"*

Có mã lỗi cụ thể (`HOC_VIEN_DA_TRONG_LOP`) nên lần này truy được thẳng vào dữ liệu thật.

## Nhật ký hệ thống kể toàn bộ câu chuyện

```
14:44:06  ThemHocVienVaoLopCommand   thành công, 1 bản ghi
14:44:14  DuyetVaoLopCommand         THẤT BẠI  HOC_VIEN_DA_TRONG_LOP
14:44:25  DuyetVaoLopCommand         THẤT BẠI  HOC_VIEN_DA_TRONG_LOP
15:01:18  DuyetVaoLopCommand         THẤT BẠI  HOC_VIEN_DA_TRONG_LOP
16:14:36  DuyetVaoLopCommand         THẤT BẠI  HOC_VIEN_DA_TRONG_LOP
16:14:39  DuyetVaoLopCommand         THẤT BẠI  HOC_VIEN_DA_TRONG_LOP
16:14:42  DuyetVaoLopCommand         THẤT BẠI  HOC_VIEN_DA_TRONG_LOP
16:14:56  DuyetVaoLopCommand         THẤT BẠI  HOC_VIEN_DA_TRONG_LOP
```

**Bảy lần** bấm duyệt, bảy lần cùng một lỗi. Đây là giá trị thật của FR-16: không có nhật ký thì
phải đoán người dùng đã làm gì.

## Bế tắc nằm ở đâu

`ThemHocVienVaoLopCommand` (thêm học viên vào lớp bằng tay) **không đóng yêu cầu xếp lớp chờ
nào** — nó chỉ biết về lớp, không biết CRM đang có đơn chờ cho người đó.

Sau đó `DuyetVaoLopCommand` gặp người đã trong lớp thì `throw`:

```csharp
var daCo = await db.LopHocHocViens
    .Where(hv => hv.LopHocId == lop.Id && hocVienIds.Contains(hv.HocVienId))
    .AnyAsync(ct);
if (daCo) throw new AppException("HOC_VIEN_DA_TRONG_LOP");
```

Kết quả: **duyệt thì 400, mà hàng chờ không tự sạch.** Không có đường nào ra. Người điều phối chỉ
còn cách bấm lại và bấm lại.

## Vì sao "bỏ qua" là đúng, không phải "âm thầm bỏ sót"

Đích của việc duyệt là *"người này vào lớp này"*. Nếu họ **đã** ở trong lớp thì đích đã đạt —
`throw` ở đây là lỗi altitude: handler đang coi trạng thái mong muốn là trạng thái lỗi.

Ghi danh thêm một dòng nữa mới là cái sai thật: `UNIQUE(lop_hoc_id, hoc_vien_id)` chặn ở tầng DB,
và nếu lọt thì học viên bị **tính học phí hai lần**.

## Hai quyết định nghiệp vụ phải hỏi, không tự quyết

Điều tra xong thì lộ ra ca dữ liệu phức tạp hơn báo cáo: Thu Hà mua **hai khoá khác nhau** —
IELTS 6.5 cấp tốc (10.800.000đ) và Giao tiếp cơ bản (4.500.000đ) — nên có **hai** yêu cầu chờ,
hoàn toàn hợp lệ. Mà trung tâm chỉ có **một** lớp.

Hai câu không suy được từ code:

**1. Duyệt một yêu cầu có đóng lây yêu cầu kia không?**
→ Chủ sản phẩm: *"duyệt yêu cầu nào thì yêu cầu đó thành đã xếp, vì mỗi yêu cầu là riêng biệt"*.
Đúng: mỗi yêu cầu là một đơn riêng, một khoá riêng, cần một lớp riêng. Đóng lây sẽ làm **mất một
khoá khách đã trả tiền**.

**2. Học viên được học nhiều lớp cùng lúc không?**
→ Có. Khớp với ràng buộc DB sẵn có: `UNIQUE(lop_hoc_id, hoc_vien_id)` chỉ chặn **vào trùng cùng
một lớp**, không chặn nhiều lớp.

## Điểm tiền bạc: học phí lấy số nào

Thêm tay chốt học phí = **giá lớp** (5.000.000đ). Đơn CRM = **10.800.000đ**. Giữ giá lớp thì sổ
học phí LMS **thiếu 5.800.000đ**.

Đây là ghi đè số tiền đã chốt — quy tắc #1 buộc phải hỏi. Chủ sản phẩm chọn **cập nhật theo đơn
CRM**, vì đơn mới là số khách thật sự nợ. Chỉ ghi đè ở **đúng đường duyệt yêu cầu**: ở đó người
điều phối đang tuyên bố *"đơn CRM này ứng với chỗ trong lớp này"*.

## Kiểm chứng trên chính ca kẹt

Không đụng dữ liệu của trung tâm thật (không biết mật khẩu admin, và **không đặt lại** — đó là
dữ liệu người dùng). Thay vào đó dựng lại **y hệt** trong tenant thử: 2 khoá đúng giá, 2 đơn,
1 lớp giá 5tr, thêm tay trước.

```
TRƯỚC:  2 yêu cầu DangCho · học phí trong lớp 5.000.000
DUYỆT đơn IELTS →  HTTP 204   (trước đây: 400)
SAU:    1 yêu cầu chờ — "Giao tiếp cơ bản" 4.500.000 ✔ vẫn chờ lớp của nó
        học phí trong lớp 10.800.000 ✔ theo đơn CRM
        1 dòng ghi danh ✔ không nhân đôi
```

## Mã lỗi giữ nguyên ở chỗ nó đúng

`HOC_VIEN_DA_TRONG_LOP` **vẫn còn** ở `ThemHocVienVaoLopHandler`. Ở đường thêm tay nó đúng: người
dùng đang chọn sai người, và có đường thoát rõ ràng (chọn người khác). Chỉ ở đường **duyệt** nó
mới tạo bế tắc. Test `Them_hoc_vien_da_co_trong_lop_bi_tu_choi` canh phần đó, không bị sửa.

## Bài học

**Cùng một mã lỗi có thể đúng ở endpoint này và sai ở endpoint kia.** Sai/đúng không nằm ở điều
kiện, mà ở chỗ **người dùng có đường thoát hay không**.

Và: bảy lần bấm lại trong nhật ký là tín hiệu thiết kế, không phải thao tác cẩu thả của người
dùng. Người ta bấm lại vì hệ thống không cho họ lối nào khác.
