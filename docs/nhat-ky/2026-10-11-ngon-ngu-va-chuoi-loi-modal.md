# 10–11/10/2026 — Danh mục ngôn ngữ, và một chuỗi lỗi tự gây ra ở component dùng chung

> 14 commit. Phần đáng ghi lại không phải tính năng mới, mà là **chuỗi bốn lỗi liên tiếp**
> sinh ra từ việc sửa component dùng chung — và bài học rút ra.

## Danh mục ngôn ngữ (FR-32)

Bảng `NGON_NGU` riêng thay vì enum trong mã: mỗi trung tâm dạy một bộ ngôn ngữ khác nhau và
bộ đó đổi theo thời gian. `KHOA_HOC`/`SAN_PHAM` trỏ tới bằng FK thay vì lưu chuỗi, nên đổi
tên là một lần ghi.

`ngon_ngu_id` **nullable** — 52 khoá học hiện có giữ nguyên, chưa gắn. Chủ sản phẩm chọn
phương án này thay vì bắt gán ngay (quy tắc #1).

Lọc theo ngôn ngữ đi qua một cấp nữa: ngôn ngữ nằm ở *mặt hàng*, không ở đơn. Kiểm trên dữ
liệu thật — gắn một khoá (224 đơn) thì doanh thu 1057 → 224, khách 939 → 201.

**FK là `SetNull` nhưng handler vẫn CHẶN xoá** khi còn khoá đang gắn. Lý do: `SetNull` không
nổ ở tầng DB, nó âm thầm gỡ nhãn của mọi khoá — không lỗi nào, chỉ là hôm sau bộ lọc trống
và không ai biết vì sao.

## Chuỗi bốn lỗi, mỗi cái sinh ra từ cái trước

Đây là phần đáng đọc nhất của ngày.

| # | Thay đổi | Hỏng cái gì | Vì sao không thấy trước |
|---|---|---|---|
| 1 | Khoá cột bảng bằng `sticky` | Menu thao tác chui xuống dưới các dòng | `sticky` tạo **stacking context**, `z-50` hết tác dụng |
| 2 | Đưa menu ra `document.body` | "Bấm ngoài thì đóng" tính nhầm cả bấm vào menu | Menu không còn trong `boc` để `contains()` thấy |
| 3 | Kẻ chân modal hết bề ngang | Lộ ra tiêu đề/nút trôi khi cuộn | Đường kẻ rõ hơn làm lỗi **có sẵn** đập vào mắt |
| 4 | Thêm `flex` vào `<dialog>` | **MỌI modal hiện cùng lúc, chồng lên nhau** | `<dialog>` chưa mở có `display: none` của trình duyệt; class `flex` ghi đè nó |

Lỗi #4 nghiêm trọng nhất và cũng dễ chẩn sai nhất: nhìn ảnh thì tưởng *"modal mất tiêu đề"*,
thực ra là hộp xác nhận của modal khác đang đè lên. Đo mới ra: **2 dialog `display: flex`
trong khi 0 cái có thuộc tính `open`**. Nếu đi sửa "tiêu đề bị mất" thì đã sửa nhầm chỗ.

Chủ sản phẩm nhận xét: *"bạn đang làm phần này lỗi phần khác"*. Đúng.

### Bài học đã ghi vào tài liệu

Thêm mục **"⚠️ Sửa component DÙNG CHUNG"** vào `ui-ux-nguyen-tac.md` — bốn câu phải trả lời
trước khi đổi một component có 30–50 nơi gọi. Câu số 3 (*"chú thích cũ ở đó còn đúng không?"*)
cứu được chính ngày hôm đó: `Modal` ghi *"KHÔNG dùng overflow-y-auto, sẽ cắt dropdown"* —
đúng lúc viết, **sai từ khi** `SelectTimKiem` đổi sang `position: fixed`.

## Modal: chắp vá → chuẩn

Chủ sản phẩm chỉ ra cách đúng: *"header và footer cố định, body dài thì cho scroll"*. Tôi đã
làm bằng `sticky bottom-0` — một mẹo, và nó vẫn hỏng khi nội dung đủ dài.

Dựng lại ba phần thật, và **`min-h-0` ở body là mấu chốt**: trong flex column, con mặc định có
`min-height: auto` nên không co nhỏ hơn nội dung ⇒ `overflow-y-auto` không bao giờ kích hoạt.

Để không phải sửa 31 chỗ gọi, `Modal` **tự tách** `<ModalChan>` khỏi `children`. Nút
`type="submit"` bị tách khỏi `<form>` vẫn chạy nhờ `form="<id>"` — thuộc tính chuẩn HTML5.

Phát hiện kèm theo: **20/30 file tự viết hàng nút** thay vì dùng `<ModalChan>`, nên chỉ một
phần ba modal có footer cố định. Đã chuyển hết 28 chỗ.

## Hai lỗi dữ liệu đáng nhớ

**Modal dùng lại dữ liệu của lần mở trước.** `<dialog>` không unmount khi đóng, mà React chỉ
đọc `defaultValue` ở lần dựng đầu. Nguy hiểm hơn hiển thị sai: mở sửa dòng B, thấy số của
dòng A, bấm Lưu là **ghi đè B bằng số của A**. Đếm ra 15+ màn, 70+ ô cùng dính.

**Xoá doanh thu nổ 500.** FK `Restrict` có chủ ý, nhưng handler không kiểm gì nên lệnh rơi
xuống DB và nổ `23503`. Trên dữ liệu thật 1029/1056 đơn có lần thu ⇒ gần như **mọi** lệnh xoá
đều 500. Codebase đã xử lý đúng bẫy này ở `XoaKhoaHocHandler` từ trước — handler xoá đơn chỉ
là không được làm theo.

## Việc kế tiếp

- Hai test `NhanXetBuoiHocTests` và 9 tệp E2E đỏ sẵn — nợ có trước, đã xác nhận bằng cách lùi
  code. Chưa đụng.
- `frontend/edu-temp/` (60 MB) vẫn chờ quyết định giấy phép.
