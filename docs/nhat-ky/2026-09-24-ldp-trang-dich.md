# 24/09/2026 — FR-30: trang đích công khai, hệ thống con thứ tư

Chủ sản phẩm muốn mỗi trung tâm có một **trang giới thiệu công khai** (tham chiếu
edulife.com.vn), và một module LDP trong quản trị để soạn nội dung — song song HRM · CRM · LMS.

## Điều làm LDP khác mọi module trước đó

Đây là lần đầu hệ thống phục vụ **người chưa đăng nhập** bằng dữ liệu của tenant. Ba module
kia đều nằm sau `[RequirePermission]`; LDP cố ý đẩy nội dung ra Internet.

Hệ quả nguy hiểm nhất đã biết từ ADR-0008: Global Query Filter có nhánh `TenantIdHienTai == null`
**tắt filter hoàn toàn**. Request không giải ra tenant thì không trả rỗng mà gom nội dung của
**mọi trung tâm** vào một trang.

## Quyết định lớn nhất: nội dung nhập RIÊNG

Landing **không** đọc `KHOA_HOC`, `HO_SO_GIAO_VIEN`, `LOP_HOC`. Nhập hai lần phiền hơn, nhưng:

- Nối thẳng dữ liệu nghiệp vụ ra trang công khai là mở đường rò rỉ **vĩnh viễn**: mỗi cột ai
  đó thêm vào `KHOA_HOC` sau này đều có nguy cơ xuất hiện trên Internet mà không ai rà.
- Bài học 07/09 (rò rỉ học phí) áp vào chỗ khán giả là **cả Internet**: mọi tầng phân quyền
  lọc **hàng**, không lọc **cột**.
- Nội dung marketing vốn khác nội dung vận hành.

Kèm theo: **DTO công khai tách hẳn DTO quản trị** (`...CongKhaiDto`). Thêm trường cho màn
quản trị không chạm gì tới thứ ra ngoài.

## Bốn chỗ tự bắt được

1. **Tự lừa mình về khoá ảnh.** Chú thích viết "không trả khoá thô" trong khi URL lại *chứa*
   khoá. Đổi sang **id khối/mục**, server tự tra — và chỉ tra trong trang đã xuất bản của
   tenant hiện tại, đúng khuôn `AuthController.Logo`.

2. **Rẽ sớm không kiểm `IsAuthenticated`.** Đường `/t/{mã}` dùng chung tiền tố `/api/v1/ldp`
   với endpoint quản trị, nên rẽ sớm làm **mọi endpoint quản trị của LDP mất tenant**.

3. **Bốn file i18n hỏng im lặng.** Nhãn xoá mục chứa cả `"` lẫn `'`; `tsc` chỉ báo file đầu
   tiên. Quét cả bộ bằng regex mới thấy `vi/en/zh/ko` đều hỏng — sửa một file rồi build lại
   sẽ lộ file sau, mất bốn vòng. Bài học: khi một lớp lỗi có thể lặp ở nhiều file, **quét hết
   trước khi sửa**.

4. **Đổi thiết kế so với chốt ban đầu.** Chủ sản phẩm chốt form "ghi thẳng vào CRM"; tôi làm
   bảng trung gian `LIEN_HE_LANDING` rồi mới chuyển. Lý do: form là endpoint ẩn danh nên nhận
   cả bot, ghi thẳng thì danh sách khách hàng thật bị loãng và không lọc ngược được. Bước
   chuyển chỉ là một nút. **Đã ghi rõ trong FR-30** để chủ sản phẩm đảo ngược nếu muốn.

## Endpoint ẩn danh: 10 → 14

Ba đọc, một **ghi**. Cái ghi (form liên hệ) có bốn lớp: rate limit riêng 5/phút (chặt hơn
`TraCuu` vì mỗi request tạo một hàng DB), honeypot **bỏ qua im lặng**, giới hạn độ dài ở
validator, không trả dữ liệu.

Honeypot trả 204 y như gửi thật — báo lỗi là nói cho người viết bot biết họ bị phát hiện.

Đặt trong `LdpCongKhaiController` **tách file** khỏi `LdpController`: trộn lẫn thì một
`[AllowAnonymous]` thêm nhầm giữa danh sách `[RequirePermission]` rất khó thấy khi review.

## Test kiến trúc làm đúng việc của chúng

Thêm hệ thống con thứ tư làm chúng đỏ ngay và nêu **chính xác** bốn chỗ: thư mục `Ldp` chưa
phân loại, cầu nối LDP→CRM chưa khai, bốn chỗ chốt cứng "ba hệ thống", bound endpoint ẩn danh.

Không test nào trong số đó do tôi viết cho LDP — chúng có sẵn, và chúng bắt người thêm module
mới phải dừng lại khai báo. Đây là giá trị thật của khuôn "danh sách ngoại lệ + test chiều
ngược" mà dự án dùng cho bảy bất biến kiến trúc.

Mutation: bỏ lọc `DaXuatBan` ⇒ đỏ; bỏ lọc `Hien` ⇒ đỏ.


## Bổ sung cùng ngày: dựng lại bố cục trang công khai

Chủ sản phẩm đưa edulife.com.vn làm mẫu và yêu cầu dựng theo.

**Lấy gì:** khuôn bố cục — header dính có nút gọi hành động, hero căn giữa, lưới thẻ khoá học,
băng chuyền ngang cho cảm nhận và giáo viên, dải logo đối tác, các bước đánh số, khối cơ sở,
form cuối trang, nút gọi nổi trên di động. Đây là quy ước chung của landing giáo dục, không
phải của riêng trang nào.

**Không lấy:** chữ nghĩa, hình ảnh, logo, bảng màu. Màu lấy từ design token của chính hệ thống;
nội dung do từng trung tâm tự nhập. Trang dựng ra phải mang thương hiệu của trung tâm dùng nó,
không phải bản sao của một trung tâm khác. Đã ghi rõ ranh giới này trong FR-30 để người sau
không hiểu nhầm "dựng theo" thành "sao chép".

**Ba khối thêm mới:** `QuyTrinh` (bước đánh số — thứ tự mục chính là số bước, không cần cột
riêng), `DoiTac` (chỉ ảnh, không chữ — dải logo mà kèm mô tả thì thành lưới thẻ và mất hiệu
ứng "một hàng thương hiệu"), `CoSo`.

**Băng chuyền dùng CSS `scroll-snap`**, không thêm thư viện carousel: vuốt được trên di động,
bàn phím vẫn cuộn được, và không thêm một phụ thuộc chỉ để cuộn ngang. Hai nút mũi tên chỉ là
tiện ích cho desktop nơi không có thao tác vuốt.

**Chi tiết nhỏ nhưng quan trọng:** hero luôn có lớp phủ trên ảnh nền. Không có nó thì trung tâm
tải lên một ảnh sáng là chữ biến mất — và họ sẽ không hiểu vì sao, vì ở máy họ ảnh trông vẫn
đẹp.
