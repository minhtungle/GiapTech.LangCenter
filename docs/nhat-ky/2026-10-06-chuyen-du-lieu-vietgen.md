# 2026-10-06

## Đã làm

Chạy thử đầu-cuối việc chuyển dữ liệu **VIETGEN Academy** (hệ cũ, SQL Server → JSON) sang
LangCenter: 173 người dùng · 173 tài khoản · 8 nhóm quyền · 36 khoá học · 939 khách · 1056 đơn ·
1115 lần thu · doanh thu 15.273.437.000đ.

Script nằm ở `scripts/chuyen-doi-vietgen/`. Checklist chạy thật:
[`docs/07-ha-tang/chuyen-du-lieu-vietgen.md`](../07-ha-tang/chuyen-du-lieu-vietgen.md).

## Quyết định

### Nạp vào DB riêng, không vào tenant đang dùng

Chủ sản phẩm lúc đầu muốn nạp thẳng vào W686AE9. Đếm trên DB: tenant đó đang có 76 người dùng ·
32 khách · 85 đơn · 8 lớp. Nạp vào đó thì GUID có thể đụng nhau, SĐT và tên khoá có thể vi phạm
UNIQUE, và **doanh thu trộn 85 đơn cũ với 1056 đơn mới, không tách lại được** — mọi con số đối
soát thành vô nghĩa. Chốt: DB riêng `langcenter_chuyen_doi`, sai thì `DROP DATABASE`.

Điều đáng nói là `01-chuyen-doi.sql` **tự chặn** ca này: nó kiểm 5 điều kiện và dừng nếu trung
tâm không còn trắng. Chốt an toàn viết trước đã làm đúng việc của nó.

### Đổi đuôi username cho cả 173 tài khoản

Hệ cũ dùng `@vietgenacademy.edu.vn`; trung tâm đổi sang `@vietgeneducation.edu.vn`. Kiểm trước
khi sửa: 173 username mới **đều duy nhất**, không va chạm `UNIQUE(tenant_id, username)`.

Hệ quả phải báo người dùng: họ đăng nhập bằng đuôi MỚI, khác email họ quen. Không nói rõ thì ai
cũng gõ đuôi cũ rồi nhận "sai mật khẩu".

### Gộp khách trùng SĐT khi cùng tên

25 nhóm / 52 khách trùng SĐT. Phân loại bằng cách bỏ dấu và bỏ hậu tố số: **17 nhóm là cùng một
người** (một hồ sơ có hậu tố số, hoặc một bản có dấu một bản không), 8 nhóm tên khác hẳn.

Gộp 18 hồ sơ cùng tên về hồ sơ tạo sớm nhất, dồn đơn sang. 957 → **939 khách**, nhưng **1056 đơn
và doanh thu không đổi** — đúng kỳ vọng: gộp khách không được làm mất đơn hay mất tiền.

Chỉ gộp theo SĐT, **không** gộp theo tên khi thiếu SĐT: Việt Nam quá nhiều người trùng tên, gộp
nhầm là trộn hai người thành một và không tách lại được. Có một ca hồ sơ thứ ba trùng tên nhưng
không có SĐT nên giữ riêng — đúng chủ ý.

Số điện thoại dùng chung là chuyện thật (vợ chồng, phụ huynh đăng ký cho con, số rác `00000` gom
3 người khác hẳn nhau), nên 9 hồ sơ khác tên giữ riêng, số ghi vào `ghi_chu`.

### Giữ nguyên ma trận quyền theo thiết kế LangCenter

12 ô thuộc `ChucNang.CanCanNhac` được cấp (`DoanhThu.ThuTien` cho 3 nhóm kinh doanh;
`XepLop.Duyet` + `LopHocToanTrungTam.Xem/Sua` cho điều phối lớp; thêm `LopHoc.HoanTat/Huy`,
`BuoiHoc.Huy` cho điều phối lớp - master). Chủ sản phẩm duyệt cả 12.

## Vướng mắc & phát hiện

### Gộp khách làm lộ một chỗ tra cứu còn sót

Sau khi thêm bảng `gop_ve`, script chết với `KeyError` ở khối thu tiền: nó tra `kh_pttt` theo id
khách **gốc**, mà hồ sơ đó không còn được chèn. Lỗi này có giá trị — nó chỉ ra rằng **mọi** chỗ
tra theo id khách đều phải đi qua `gop_ve`, không chỉ bảng đơn hàng. Sửa một dòng là xong, nhưng
nếu khối đó dùng `.get()` với giá trị mặc định thì đã im lặng ghi sai phương thức thanh toán.

### Đối chiếu ma trận quyền bằng máy, không đọc mắt

Rút `HanhDong` từ `Enums.cs` và `ThaoTacTheoChucNang` từ `ChucNang.cs` bằng regex rồi so với
`THAO_TAC_HOP_LE` của script: khớp tuyệt đối, 97 cặp quyền đã nạp **không có ô chết nào**. Đọc
mắt hai bảng 14 dòng × 10 cột là cách chắc chắn bỏ sót.

### Tái tạo được từng byte

Sinh lại `01-chuyen-doi.sql` từ JSON gốc vào thư mục tạm rồi `diff` với file đang có: giống hệt.
Nghĩa là file SQL đúng là sản phẩm của `sinh_sql.py`, không ai sửa tay — điều này đáng kiểm vì
file dài 10.847 dòng, sửa tay một chỗ là không ai phát hiện.

### Hai con số thoạt nhìn lệch, đều đúng

- API báo `soKhachHang = 947` còn DB có 957: endpoint đếm khách **có đơn**, 10 khách chưa có đơn.
- 548 khách không có SĐT, nhiều hơn 26 khách trùng rất nhiều: **522 khách vốn đã không có SĐT**
  trong hệ cũ, cộng 26 bị gỡ do trùng.

Cả hai đều phải truy ngược dữ liệu gốc mới kết luận được. Nếu dừng ở "con số không khớp" thì đã
báo sai là lỗi chuyển dữ liệu.

### Một thứ tôi thử mà bỏ

Định tự kiểm từng dòng `VALUES` xem có vi phạm `CHECK` không, nhưng tách bằng regex hỏng vì ghi
chú chứa dấu phẩy và nháy đơn — đếm ra 109 dòng thay vì 1056. Bỏ cách đó thay vì tin số sai:
PostgreSQL tự kiểm ở bước nạp, và vì tất cả nằm trong một giao dịch nên vi phạm ở đâu là rollback
sạch.

## Chuẩn bị chạy thật trên VPS

### Một lỗi sẽ nổ giữa chừng, bắt được trước khi chạy

`02-dat-mat-khau-tam.sh` hard-code `psql -U langcenter`. Nhưng `.env.example` của VPS đặt
`POSTGRES_USER=langcenter_app`. Script sẽ lỗi xác thực **ở bước 5 — sau khi `01` đã nạp xong
hơn 3.000 hàng**, tức đúng lúc khó quay lui nhất. Sửa để nó đọc `POSTGRES_USER`/`POSTGRES_DB`
từ môi trường, mặc định về `langcenter` cho dev.

Đây là loại lỗi không test nào ở local bắt được, vì ở local nó đúng. Chỉ đọc `.env.example`
của môi trường đích mới thấy.

### Sinh SQL tại chỗ trên VPS, không `scp` file SQL

`01-chuyen-doi.sql` bị `.gitignore` chặn (chứa dữ liệu cá nhân) nên `git pull` trên VPS không
có nó. Chủ sản phẩm chốt: mang `du-lieu/` lên rồi chạy `sinh_sql.py` tại chỗ.

Cái lợi thật của cách này: nếu luật làm sạch đổi, chỉ cần sửa `sinh_sql.py` và chạy lại — không
có nguy cơ file SQL trên VPS là bản cũ mà không ai biết.

Đã **giả lập đúng quy trình đó**: copy `du-lieu/` + `chuyen-doi/` sang thư mục ngoài repo, xoá
hết file sinh, chạy lại `sinh_sql.py`, rồi `diff` với bản đã kiểm thử — **giống hệt từng byte**
cả 4 file. Nạp bản sinh đó vào một DB trắng thứ ba: 14/14 chỉ số khớp, `02` chạy được từ đường
dẫn ngoài repo.

Giả lập này đáng làm vì nó kiểm thứ mà đọc tài liệu không kiểm được: script có chạy đúng khi
thư mục làm việc khác không, và bản sinh ra có thật sự giống bản đã kiểm thử không.

## Việc kế tiếp

- Chạy thật trên VPS theo checklist — **chủ sản phẩm tự chạy**, không tự động hoá.
- `frontend/edu-temp/` (60MB, chưa theo dõi) vẫn chờ quyết định về giấy phép.
