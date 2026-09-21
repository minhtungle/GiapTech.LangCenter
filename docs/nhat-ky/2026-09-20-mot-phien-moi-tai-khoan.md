# 20/09/2026 — Một phiên mỗi tài khoản · mở cổng chia sẻ · dọn tenant rác

## Yêu cầu

> chỉ cho phép 1 người đăng nhập tài khoản cùng lúc

## Hai câu hỏi phải hỏi trước khi code

Cả hai đều đổi hẳn khối lượng việc, và không suy ra được từ code:

| Câu hỏi | Chốt |
|---|---|
| Người thứ hai đăng nhập thì xử ai? | **Đẩy phiên cũ ra** (như Facebook/Zalo) |
| Nhanh tới mức nào? | **Ngay lập tức** |

Câu thứ hai quan trọng hơn vẻ ngoài của nó. JWT là **stateless** — server không tra DB mỗi
request — nên nếu chỉ thu hồi refresh token thì phiên cũ vẫn gọi API bình thường tới **60 phút**
(hạn access token). Một tiếng hai người dùng song song thì không còn là "chỉ 1 người cùng lúc".
Chọn "ngay lập tức" nghĩa là phải thêm một middleware tra DB mỗi request.

## Cách làm: dùng lại `jti`, không thêm claim

`TAI_KHOAN.phien_hien_tai` lưu `jti` của access token phát ở lần đăng nhập gần nhất.
`PhienDuyNhatMiddleware` so `jti` trong token với cột đó; lệch là **401 `PHIEN_DA_BI_DAY_RA`**.

Chọn `jti` có sẵn thay vì thêm claim mới là vì **quy tắc #1**: thêm claim là thay đổi phá vỡ
tương thích với mọi token đang lưu hành — người đang mở app sẽ bị đá ra ngay lúc triển khai.
`jti` đã nằm trong mọi token từ trước.

Hai trường hợp **cố ý cho qua**: token không có `jti`, và `phien_hien_tai` rỗng. Đó là token
phát trước hôm nay; chặn thì đá hàng loạt người đang dùng, cái giá không đáng.

## Hai lỗi TỰ GÂY, đều do kiểm trên hệ thống thật

Không lỗi nào lộ ra khi đọc code. Cả hai chỉ hiện khi gọi API thật.

### 1. Người vừa đăng nhập cũng bị chặn

Middleware cache `phien_hien_tai` 10 giây cho khỏi tra DB mỗi request. Nhưng quên **xoá cache
lúc đăng nhập** ⇒ máy B đăng nhập xong gọi API nhận **401**: cache vẫn giữ phiên của máy A.

Người bị đá ra đúng là A, nhưng B cũng không vào được — sai với cả hai. Thêm `IPhienService`
để handler xoá cache ngay sau khi ghi phiên mới.

Đáng chú ý: mutation test "bỏ lời gọi xoá cache" **không biên dịch được** vì
`TreatWarningsAsErrors` bắt tham số thừa. Phải bỏ cả lời gọi *và* tham số mới ra mutant thật —
và nó chết với đúng thông điệp `Actual: Unauthorized`, tức lỗi tôi đã gặp.

### 2. Làm mới token tự đá mình ra

`LamMoiTokenHandler` phát access token mới (⇒ `jti` mới) nhưng không chuyển `phien_hien_tai`.
Hệ quả: **cứ 60 phút người dùng lại văng về màn đăng nhập** — tính năng chống hai người dùng
chung lại quay ra cắn chính người đang dùng một mình.

Test `Lam_moi_token_tra_ve_cap_token_moi` (có sẵn từ trước) bắt được ngay. Một lý do nữa để
chạy cả bộ chứ không chỉ test mình vừa viết.

## Một test cũ đỏ — và nó đúng

`QuyenCuaToiTests.Moi_vai_tro_doc_duoc_mui_gio_trung_tam` lấy client `manager` **trước**, rồi
gọi `TaoVaDangNhap` (bên trong tự đăng nhập bằng chính `manager`) ⇒ client đầu bị chính nó đá
ra, nhận 401.

Đây là **giới hạn của test, không phải lỗi sản phẩm**: người thật không mở hai phiên cùng một
nick rồi mong cả hai chạy. Sửa bằng cách lấy client `manager` sau cùng, kèm chú thích để người
sau không sửa nhầm theo hướng khác.

## Chuyện mất nhiều thời gian nhất, và không đáng

Test E2E mới đỏ khi chạy cả bộ nhưng xanh khi chạy một mình. Tôi đuổi theo nó khá lâu:

- Đầu tiên nghi phiên chéo giữa các test — sai, mỗi test tự tạo tenant riêng.
- Rồi nghi hạn mức tần suất — sai, cờ `GIOI_HAN_TAN_SUAT=false` vẫn đang bật.
- Nhiều lần đọc output rỗng vì `grep` trong lệnh nền không flush, mất thêm mấy vòng.

Cuối cùng chạy hai test liền nhau và ghi thẳng ra file thì thấy: **test đỏ là
`modal-long-nhau`**, không phải test của tôi, và nó đỏ ở bước "Duyệt vào lớp" — chẳng liên quan
gì tới phiên. Chạy lại lần nữa thì đỏ ở hai test khác hẳn. Đó là **flaky do chờ cứng
`waitForTimeout`** dưới tải, nợ đã biết từ trước.

Bài học: khi test đỏ *khác nhau mỗi lần chạy*, đó là tín hiệu flaky — nên xác định sớm thay vì
truy nguyên nhân cho từng lần đỏ. Và phải ghi output ra file mình kiểm soát, đừng tin `grep`
qua nhiều lớp nền.

Còn một lần tự bẫy nữa: API báo "address already in use" và tôi tưởng nó chết, thực ra nó
**đang chạy** suốt — `pgrep` của tôi không khớp vì tên tiến trình bị cắt ngắn thành `GiapTech.`.

## Dọn tenant rác (nợ N11 tái diễn)

**444 → 6 tenant** (441 tenant E2E). Tạo trung tâm từ 3,5s xuống 2,4s. W686AE9 nguyên vẹn:
75 người, 8 lớp, 12 buổi. Dry-run trên bản sao trước, backup trước khi chạy.

Nợ này quay lại đúng hai ngày sau lần dọn trước — mỗi lượt chạy E2E lại thêm ~40 tenant. Vẫn
chưa có `globalTeardown`.

## Giới hạn đã biết

Đổi mật khẩu thu hồi refresh token của phiên khác nhưng **không** đổi `phien_hien_tai`, nên
access token của máy kia còn dùng được tối đa 60 phút. Chặt hơn thì phải đưa `jti` vào
`ICurrentUser` — chưa làm vì ngoài phạm vi yêu cầu, và đã ghi lại thay vì lặng lẽ mở rộng.

## Ngoài lề: mở cổng chia sẻ qua Internet

Chủ sản phẩm muốn cho người khác xem thử. Tôi kiểm chứng rủi ro trước khi mở và **nêu ra**: ai
có link đều vào được quyền quản trị (bộ ba `W686AE9`/`admin`/`123456` nằm trong repo), tạo được
trung tâm mới (nợ N3), và dữ liệu có cột `cccd`, `so_tai_khoan`. Chủ sản phẩm xác nhận dữ liệu
không thật ⇒ mở bằng `cloudflared` (đường hầm tạm, đóng terminal là hết).

Cấu hình chia sẻ để ở `vite.chia-se.config.ts` **riêng**, không sửa `vite.config.ts` gốc: lỡ
commit thì thành cửa mở thường trực mà không ai nhớ đã bật.
