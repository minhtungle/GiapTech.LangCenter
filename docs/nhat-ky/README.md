# Nhật ký tiến độ

Mỗi ngày làm việc một file: `YYYY-MM-DD.md`.

## Mục đích

Ghi lại **bối cảnh mà git log không có**: vì sao chọn phương án này thay vì phương án kia, thử gì
thất bại, phát hiện gì ngoài dự kiến. Diff nói "làm gì", commit message nói "vì sao ở mức thay đổi
đó — nhật ký nói vì sao ở mức **ngày làm việc**.

## Cấu trúc một mục

```markdown
# YYYY-MM-DD

## Đã làm
Ngắn gọn, kèm mã FR hoặc số commit.

## Quyết định
Chọn gì, loại gì, vì sao. Quyết định lớn/khó đảo ngược → viết ADR, ở đây chỉ ghi con trỏ.

## Vướng mắc & phát hiện
Lỗi đã bắt được, thứ tưởng đúng mà sai, giới hạn chưa gỡ được.

## Việc kế tiếp
Định làm gì phiên sau.
```

## Quy ước

- Ghi **cuối mỗi đợt việc**, ngay sau khi commit — không dồn cuối tuần rồi viết lại từ trí nhớ.
- Không lặp nội dung đã có ở `docs/` khác; nếu một quyết định đủ lớn thì viết
  [ADR](../kien-truc/adr/) và ở đây chỉ dẫn link.
- Ghi cả thứ **chưa xong** và **chưa kiểm chứng được** — đó thường là thông tin có giá trị nhất khi
  đọc lại.

## Mục lục

> **Gỡ 08/09/2026:** sáu nhật ký ngày 16–21/08 mô tả nghiệp vụ của **dự án tiền thân**
> (`GiapTech.SoccerRoom` — sàn đối thủ, quỹ CLB, đăng ký đá trận qua link). Chúng không còn liên
> quan tới dự án này và gây hiểu nhầm khi đọc; nội dung đầy đủ còn trong git history.
>
> Những bài học **vẫn còn hiệu lực** đã gộp vào
> [bài học từ dự án tiền thân](./2026-08-bai-hoc-du-an-tien-than.md) — code hiện tại còn dẫn
> chiếu tới chúng (quy tắc #1, hạn mức phải kiểm được bằng con số, chốt còn người quản trị cuối).

| Ngày | Nội dung chính |
|---|---|
| [2026-09-20](./2026-09-20-mot-phien-moi-tai-khoan.md) | **Một phiên mỗi tài khoản** — đăng nhập nơi khác đẩy phiên cũ ra, hiệu lực ngay. Điểm then chốt: JWT stateless nên **chỉ thu hồi refresh token là chưa đủ** (phiên cũ sống thêm 60 phút), phải chặn ở middleware. Dùng lại `jti` có sẵn thay vì thêm claim, vì thêm claim là phá vỡ tương thích với token đang lưu hành (quy tắc #1). **Hai lỗi tự gây, đều chỉ lộ khi gọi API thật**: cache phiên quên xoá lúc đăng nhập ⇒ chính người vừa đăng nhập nhận 401; và làm mới token không chuyển phiên ⇒ cứ 60 phút người dùng tự văng ra. Một test cũ đỏ nhưng **nó đúng** — test lấy hai phiên cùng một nick, điều mà tính năng này cấm. Mất nhiều thời gian nhất là đuổi theo một test đỏ trong bộ mà xanh khi chạy riêng: hoá ra **flaky có sẵn** (đỏ ở test khác nhau mỗi lần), không liên quan. Dọn nợ N11 tái diễn: 444 → 6 tenant |
| [2026-09-18](./2026-09-18-trang-thai-buoi-va-cham-tieu-chi.md) | **Trạng thái buổi học · màu trạng thái · chấm tiêu chí riêng GV/trợ giảng · sửa "lưu điểm danh không lưu được"**. Đo trước khi sửa: **129/129 buổi đều `DaLenLich`, 85 buổi đã qua** — lỗi đúng với mọi buổi. Tách hai lớp khái niệm: DB chỉ lưu thứ con người quyết định, còn "chưa bắt đầu/đang diễn ra/chưa chốt" thì **suy từ giờ** (lưu thành cột thì cần job chạy nền, job chết là sai âm thầm). Lỗi nặng nhất là **tự gây**: cấp quyền đọc tiêu chí cho học viên làm họ "vào được HRM" ⇒ `Layout` đổi sidebar và học viên **mất menu Lớp học** — không test đơn vị nào bắt được, E2E đổi nick hôm qua bắt hộ. Một lỗi nữa tìm bằng **mắt chứ không bằng test**: lịch hiện buổi 18:00 thành "11 giờ" vì FullCalendar không có plugin múi giờ nên âm thầm rơi về UTC (có từ trước, `git stash` xác nhận). Và một mutant sống chỉ đúng một phần — khác biệt không quan sát được trên dữ liệu hiện tại nhưng sẽ lộ với vai trò "chỉ phụ trách tiêu chí", nên thêm test kiểm chính cái luật. **Cuối ngày**: "lưu điểm danh không lưu được" — validator đúng, nhưng lỗi trả theo từng dòng trong `duLieu.truong` mà `layMaLoi` chỉ đọc `errorCode` tầng ngoài ⇒ người dùng thấy đúng một câu "dữ liệu chưa hợp lệ", không biết thiếu ở đâu trong 6 dòng. Mã `THIEU_LY_DO_VANG` **đã có bản dịch từ trước**, chỉ là chưa bao giờ tới mắt người dùng. Tôi tự bẫy mình **ba lần** khi thăm dò (gửi sai tên trường rồi tưởng backend lỗi; script không trả lời hộp xác nhận nên không có request nào bay đi; locator `nth(i)` co lại giữa vòng lặp vì `aria-invalid` mất khi điền xong) — ảnh chụp màn hình gỡ ra cả ba. Và **22/43 E2E đỏ vì tôi để bật hạn mức tần suất**, không phải regression |
| [2026-09-17](./2026-09-17-doi-nick-giu-quyen-cu.md) | **Đổi nick xong giao diện vẫn giữ quyền nick cũ** — mọi `queryKey` là hằng, không mang danh tính người đăng nhập, cộng `staleTime: Infinity` ⇒ học viên thấy nguyên menu quản trị, phải Ctrl+Shift+R. Chữa bằng `queryClient.clear()` lúc đổi phiên. Đáng nhớ không phải bản sửa mà là **test đầu tiên vô dụng mà vẫn xanh**: nó `goto('/dang-nhap')` — tức tải lại trang, tức tự làm luôn cái Ctrl+Shift+R đang cần kiểm — nên xanh cả khi đã gỡ sạch bản sửa; mutation test lộ ra, đọc code thì không. Vòng mutation đầu còn cho kết luận **ngược hẳn** vì API chạy thiếu `GIOI_HAN_TAN_SUAT=false`: mutant "chết" do dính rate limit chứ không do assertion — test đỏ phải đọc *lý do* đỏ. Giữ cả hai `clear()` dù gỡ một vẫn xanh: một lo *đúng*, một lo *kín* |
| [2026-09-16 (FR-29)](./2026-09-16-fr29-thong-ke-nhan-su.md) | **Thống kê nhân sự + module tiêu chí chấm thang 5**. Bẫy nặng nhất: `BUOI_HOC.giao_vien_id = null` nghĩa là *giáo viên chính của lớp*, nên đếm thẳng cột đó thì mọi giáo viên ra 0 buổi — dữ liệu thật có 12/12 buổi đều null. Test canh kiến trúc đỏ ngay khi HRM đọc dữ liệu CRM/LMS: đúng việc của nó, đã khai cầu nối vào ADR-0005. Và một bug tự gây chỉ chuỗi đầu-cuối bắt được: controller dùng DTO riêng cho thân request nên trường `DiemTieuChis` **rơi âm thầm** — API trả 200, handler chạy đúng theo `null`, không lỗi nào |
| [2026-09-16 (HRM)](./2026-09-16-hrm-gop-tab.md) | **Gộp ba màn HRM thành một trang ba tab** + **đặt tên tệp hồ sơ, hạn mức 10 tệp** + **vai trò Nhân viên kinh doanh**. Yêu cầu cuối đọc như đổi một dòng nhãn, nhưng rà dữ liệu thật thì `NhanVien` đang gồm cả nhân sự và **tài khoản quản trị** (seeder gán) — đổi nhãn là gọi người quản trị là sale ở mọi tenant mới, ngược đúng mục đích "tránh nhầm lẫn". Hỏi lại và chốt thêm vai trò riêng. Cuối ngày đổi bộ lọc thống kê sang từ ngày → đến ngày: handler thống kê so `< den` (khác Doanh thu `<= den`) nên phải gửi ngày hôm sau, thiếu là **mất trọn ngày cuối kỳ** mà không có gì báo, sĩ số trên cây cơ cấu bấm được để ra danh sách người. Lỗi đáng nhớ là của chính tôi và **không test backend nào đỏ**: cây chỉ đếm người `DangLamViec` còn `/nhan-su` trả cả người đã nghỉ, nên bấm vào số `1` ra `2` dòng — đứt ở hai mắt (link không mang tham số, màn nhận không đọc tham số), mỗi mắt tự nó trông vẫn đúng, chỉ E2E đi hết chuỗi mới thấy. Lần sửa đầu còn bị mất do hai lượt ghi file đè nhau, chạy lại test mới lộ. Suýt viết một test rỗng ("học viên không lọt vào màn nhân sự" — tập đó luôn rỗng vì đã chặn ở hai đường), đã thay bằng test kiểm chính chốt chặn |
| [2026-09-14 (FR-28)](./2026-09-14-thong-ke-crm.md) | **Thống kê CRM với biểu đồ**. Ba lần trong ngày thứ bắt lỗi KHÔNG phải mắt tôi: script kiểm màu báo 3 lỗi không nhìn ra (xanh lá đọc thành xám, tím lẫn xanh dương khi mù màu), tiêm đột biến lộ ra test **không phân biệt nổi hai mốc doanh số** vì dùng chung một tài khoản, và ảnh chụp bắt 5 lỗi bố cục — nặng nhất là chọn "Elearning" mà vẫn hiện "17 tr doanh thu" ngay trên dòng "không đo tiền" |
| [2026-09-14 (quyền)](./2026-09-14-ma-tran-quyen.md) | **Ma trận quyền theo thao tác thật**. Đếm trước khi sửa: **31/108 ô không endpoint nào đọc** — nên cách dùng hợp lý nhất của màn phân quyền là tick hết. Thêm 14 thao tác đặc thù (`Chot`, `Duyet`, `ThuTien`…) vì 4 ô CRUD không tách nổi "trợ giảng ghi điểm danh nhưng không chốt buổi". Giữa đường phát hiện **học viên đọc được nhận xét riêng của bạn cùng lớp** (`NhanXetBuoiHoc.Xem` nghĩa là "của MỌI người"). Một lần tách hụt phải hoàn lại vì lệnh ghi tên và giá trong cùng một command. 540 → **106 ô**; dọn 57 hàng chết sau khi chạy thử trên DB bản sao. Cuối ngày chủ sản phẩm nói "form quá xấu" — đúng: tôi sửa cái bảng 15 cột ba lần mà không lần nào hỏi **bảng có còn đúng không**. Đổi trục sang lưới thẻ, modal sang trang riêng |
| [2026-09-13 (FR-25→27)](./2026-09-13-elearning.md) | **Học trực tuyến** + **ba lỗ hổng trong lưới canh của chính dự án**: lưới ranh giới không phủ `QuanTri/`; test chiều ngược tôi viết để vá nó **không thể đỏ**; và `Modal` giữ children khi đóng nên sửa Bài 1 hiện nội dung Bài 2 — `tsc` xanh, 456 test xanh, chỉ E2E thấy. Cột audit 37 bảng **không có FK nào** dù chú thích nói có |
| [2026-09-12 (FR-15)](./2026-09-12-fr15-tong-quan.md) | **FR cuối cùng chạy đầu-cuối — 24/24**. Làm **khác kế hoạch hai chỗ**: "3 dashboard" → **một màn** (`IPhamViLopHoc` đã lọc đúng phạm vi từng người, ba bản sao là ba chỗ phải sửa), và "cảnh báo nợ học phí" → **bỏ** (LMS không hiển thị tiền từ cùng ngày). Một test **xanh cả khi đột biến** vì dữ liệu rỗng làm hai nhánh trùng kết quả; một test **dương tính giả** vì `baiNopChuaCham` chứa chuỗi "no" |
| [2026-09-12 (FR-14)](./2026-09-12-an-tien-khoi-lms.md) | **Đóng nợ N18 theo hướng khác hẳn**: không đồng bộ hai sổ mà **bỏ tiền khỏi LMS** — chỉ CRM nắm số tiền. Ba phương án tôi đưa ra đều nhận "có hai sổ" là tiền đề; chủ sản phẩm bác chính tiền đề đó. Giữ nguyên bảng và cột (đảo ngược được), chỉ ẩn khỏi API + UI |
| [2026-09-12 (Test)](./2026-09-12-dong-no-test.md) | **Đóng nợ N16 + N12** — E2E từ 16/4 đỏ thành **20 xanh**. Hai test `quan-tri` lạc hậu **sâu hơn** nợ mô tả: trỏ màn Tài khoản trong khi địa chỉ đã sang `/hrm/nhan-su`. N12 **không phải chớp nháy**: đổi ký tự cuối chữ ký base64url mà ký tự đó chỉ mang 2 bit — 16 nhóm cho ra cùng chữ ký |
| [2026-09-12 (Audit + đổi tên)](./2026-09-12-doi-ten-that-bai.md) | **4 cột audit** trên 37/37 bảng (xong) · **thử đổi toàn bộ sang tiếng Anh rồi HOÀN NGUYÊN**. Hai sai lầm: tưởng tách được "đổi DB" khỏi "đổi code" (`UseSnakeCaseNamingConvention` suy tên cột TỪ property nên chúng là một), và regex hàng loạt gây **3 lỗi ngữ nghĩa mà build vẫn xanh** |
| [2026-09-12 (FR-08)](./2026-09-12-tab-hoc-vien-nvkd.md) | **Tab Học viên gọn lại** (thêm học viên vào modal) + **tên nhân viên kinh doanh** gác riêng bằng `KhachHang.Xem`. Phát hiện **lỗ hổng trong `RanhGioiHeThongConTests`**: nó chỉ quét namespace nên `db.KhachHangs` — cầu nối chéo qua `IAppDbContext` — lọt hoàn toàn |
| [2026-09-12 (FR-07+21)](./2026-09-12-lop-gan-khoa-canh-bao-lech.md) | **Lớp gán khoá học (tối đa 3)** + **cảnh báo lệch khoá khi duyệt** — đóng nợ N19. Bảng trung gian chứ không 3 cột (quên một cột là lọt âm thầm); FK **RESTRICT** để xoá khoá không âm thầm phá căn cứ đối chiếu; cảnh báo **không chặn** vì có ca hợp lệ (học bù, lớp ghép). Cờ mặc định `false` để client cũ vẫn thấy cảnh báo |
| [2026-09-12 (FR-21)](./2026-09-12-trang-thai-tham-gia-lop.md) | **Trạng thái tham gia lớp suy động** từ `LOP_HOC_HOC_VIEN` thay vì suy từ trạng thái yêu cầu — gỡ khỏi lớp / huỷ lớp **tự động đúng**, không cần đồng bộ ngược. Bỏ chốt `DON_DA_DUOC_XEP_LOP` (chặn oan 3 ca thật). Phát hiện: **chưa có đường kết thúc lớp** → nợ N26 |
| [2026-09-11 (UI + Xếp lớp)](./2026-09-11-modal-long-nhau.md) | **Hai lỗi từ cùng một báo cáo.** (1) `::backdrop` không che dialog khác trong top layer: hai nút **cùng nhãn "Duyệt vào lớp"** chồng nhau, bấm nhầm lớp dưới thì không ghi gì và không báo lỗi gì → vá tầng `Modal` bằng `inert`. (2) **Bế tắc thật**: thêm học viên bằng tay không đóng yêu cầu chờ, nên duyệt luôn `HOC_VIEN_DA_TRONG_LOP` — nhật ký ghi **7 lần** bấm liên tiếp; cùng một mã lỗi **đúng ở endpoint thêm tay, sai ở endpoint duyệt** |
| [2026-09-10 (LMS)](./2026-09-10-url-pham-vi-cho-xep-lop.md) | **URL `/lms`** cho đồng nhất ba hệ thống · **phạm vi lớp của học viên** (nhánh chưa test nào phủ) · gộp **Chờ xếp lớp** thành tab — lộ ra mục menu gác `Xem` trong khi endpoint đòi `Sua`, giáo viên bấm vào nhận 403 |
| [2026-09-10 (HRM)](./2026-09-10-hrm-ho-so-mo-rong.md) | **FR-23 hồ sơ mở rộng** — bảng `LIEN_KET_MXH` nhiều dòng, cột FK thứ sáu của `TEP_DINH_KEM`; thiếu `Include` làm `RemoveRange` thành no-op im lặng; chia tab view chi tiết, `{/*…*/}` trong expression container báo lỗi lệch 70 dòng |
| [2026-09-08 (CRM)](./2026-09-08-crm.md) | **Nghiệp vụ CRM** (FR-17 → FR-19) — khách hàng · doanh thu đa tiền tệ · khoá học; vá script i18n quét namespace lồng |
| [2026-09-09 (Xếp lớp)](./2026-09-09-xep-lop.md) | **FR-21 yêu cầu xếp lớp** — cầu nối CRM → LMS, học phí lấy từ đơn CRM; gộp tab mua hàng; script Python ngắt `Layout.tsx` còn 7 dòng |
| [2026-09-09 (HRM)](./2026-09-09-hrm-co-cau.md) | **FR-22 cơ cấu tổ chức** — cây phòng ban, chống chu trình, mọi vai trò nhân sự xếp được vào phòng; 4 chỗ tự sửa sau khi rà lại; quên migration làm hỏng môi trường dev |
| [2026-09-09 (Kiến trúc)](./2026-09-09-kien-truc-ep-bang-test.md) | **Không đổi sang ABP** — ép bộ kiến trúc đang có bằng test (3 → 6 test canh); phát hiện 9 cột `text` vô hạn; ADR-0005 |
| [2026-09-08](./2026-09-08-ba-he-thong.md) | **Ba hệ thống con HRM · CRM · LMS** — bộ chuyển, sidebar lọc theo hệ thống, tab phân quyền; nhóm chức năng dùng chung |
| [2026-09-07 (nhật ký)](./2026-09-07-xac-nhan-va-nhat-ky.md) | **Xác nhận mọi thao tác** + **nhật ký hệ thống** (FR-16) — vá 2 lỗi: ChangeTracker rỗng, mật khẩu lộ |
| [2026-09-07 (lịch)](./2026-09-07-lich-calendar.md) | **View calendar** bằng FullCalendar 6 + `GET /toi/cau-hinh` cho múi giờ trung tâm |
| [2026-09-07 (buổi học)](./2026-09-07-bo-sung-buoi-hoc.md) | **Bổ sung buổi** vào lịch đã có + **khoá buổi đã chốt** — vá 2 lỗ hổng sửa/huỷ buổi đã chốt |
| [2026-09-07 (quyền UI)](./2026-09-07-an-menu-theo-quyen.md) | **Ẩn menu/nút theo quyền** — endpoint `/toi/quyen` + hook `useQuyen`, đóng nợ N2 |
| [2026-09-07 (bảo mật)](./2026-09-07-ro-ri-hoc-phi.md) | **Rò rỉ học phí** qua DTO module lớp học — giáo viên thấy mức miễn giảm, học viên thấy học phí bạn cùng lớp |
| [2026-09-07 (buổi)](./2026-09-07-chi-tiet-buoi-hoc.md) | **View chi tiết buổi học 5 tab** tại `/buoi-hoc/:id` + **nhận xét hai chiều**; xoá buổi có nhận xét từng trả 500 |
| [2026-09-07 (UI)](./2026-09-07-menu-thao-tac-va-chi-tiet-lop.md) | **Menu thao tác trong bảng** + **view chi tiết lớp học 6 tab** tại `/lop-hoc/:id` |
| [2026-09-07](./2026-09-07-tach-nguoi-dung-tai-khoan.md) | **Tách người dùng khỏi tài khoản** — 3 bảng hồ sơ vai trò; migration viết tay giữ mật khẩu; vá cổng quyền và lọc vai trò |
| [2026-09-06](./2026-09-06.md) | **LMS giai đoạn 0** — vá base (HoTen, ILuuTruTep), 16 chức năng (nay 20), 4 nhóm quyền |
| [2026-09-05 (gd4)](./2026-09-05-gd4-hoc-phi.md) | **LMS giai đoạn 4** — học phí: công nợ tính động, phạm vi tiền tách khỏi phạm vi lớp; dọn dứt điểm `docs/` |
| [2026-09-05 (gd1–3)](./2026-09-05-gd1-3-nghiep-vu-cot-loi.md) | **LMS giai đoạn 1–3** — lớp học, sinh lịch + điểm danh hai nguồn, học liệu & tệp đính kèm |
| [2026-09-05](./2026-09-05.md) | **Tách base cho dự án LMS** — bỏ nghiệp vụ bóng đá, đổi tên, 4 lỗi tìm ra |
| [16–21/08](./2026-08-bai-hoc-du-an-tien-than.md) | **Bài học từ dự án tiền thân** — quy tắc #1, rate limit, chốt còn người quản trị |
