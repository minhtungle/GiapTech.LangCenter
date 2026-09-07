# 07/09/2026 — View chi tiết buổi học + nhận xét hai chiều

Yêu cầu: bấm vào một buổi thì mở view riêng; trong view quay lại lịch được, và **chuyển sang
buổi khác không cần quay về**; nội dung gồm thông tin buổi, điểm danh, nhận xét của học viên,
của giảng viên, bài tập, tài liệu.

## "Nhận xét của học viên, giảng viên" là hai thứ khác nhau, không phải một

Đọc nhanh thì tưởng là một ô nhận xét dùng chung. Thực tế là hai chiều ngược nhau, và tôi đã
hỏi lại để chốt: **giáo viên nhận xét từng học viên trong buổi** *và* **học viên nhận xét về
buổi**. Hai chiều này lưu ở hai chỗ khác nhau, có lý do:

| Chiều | Lưu ở | Vì sao |
|---|---|---|
| GV → từng học viên | Cột `DIEM_DANH.nhan_xet` | Bảng đó đã có `UNIQUE(buoi_hoc_id, hoc_vien_id)` — **đúng độ mịn cần thiết**, không phải tạo bảng để có lại đúng ràng buộc ấy |
| Học viên → buổi | Bảng `NHAN_XET_BUOI_HOC` | **Quyền** khác (học viên không được đụng `DIEM_DANH` — đó là bằng chứng chuyên cần) và **vòng đời** khác (học viên vắng vẫn nhận xét được) |

Nếu nhồi nhận xét của học viên thành một cột nữa trong `DIEM_DANH` thì phải cấp cho học viên
quyền ghi vào bảng điểm danh của chính mình — mở đúng cái cửa mà `TrangThaiTuKhai` được dựng ra
để khỏi phải mở.

## Ranh giới đọc: chỗ dễ làm sai nhất

Học viên **chỉ đọc nhận xét của mình**. Cho họ đọc của bạn cùng lớp thì nhận xét thành diễn đàn
công khai và không ai nói thật nữa. Lọc ở **handler**; ẩn ở frontend thì gọi API trực tiếp vẫn
đọc được — đây là bài học đã trả giá ở vụ rò rỉ học phí sáng nay.

"Đọc được tất cả" suy từ quyền **đã có**: `DiemDanh.Sua` (giáo viên của lớp) hoặc
`LopHocToanTrungTam.Xem` (quản trị). Cố ý **không** thêm chức năng phân quyền thứ 18 — ai chốt
điểm danh của buổi thì đương nhiên đọc được phản hồi về buổi đó, và mỗi hằng mới lại làm admin
của trung tâm đã tồn tại bị 403 cho tới khi chạy bổ khuyết quyền.

DTO trả cờ **`cuaToi`** thay vì để frontend tự suy. Bản đầu tôi viết `ds.length === 1 ? ds[0]`
— sai: giáo viên đọc được mọi nhận xét, nên lớp chỉ có một học viên đã gửi thì suy kiểu đó nạp
nhận xét của **học viên** vào form của **giáo viên**, bấm Gửi là ghi đè nhầm chủ. Đọc DTO thật
của backend mới thấy `CuaToi` đã có sẵn.

## Lỗi tìm ra khi kiểm tay: xoá buổi có nhận xét trả 500

`NHAN_XET_BUOI_HOC → BUOI_HOC` là Restrict (nhận xét là ý kiến đã phát biểu, xoá buổi không
được cuốn nó đi). Nhưng `XoaBuoiHocHandler` chỉ kiểm `DIEM_DANH`, nên FK nổ ở tầng DB và API
trả `500 LOI_HE_THONG`.

Cái đắt không phải mã 500 mà là **người dùng mất đường đi**: họ không hiểu vì sao, và cũng
không biết rằng việc cần làm là **huỷ** buổi chứ không phải xoá. Nay trả
`400 BUOI_HOC_DA_CO_NHAN_XET` kèm bản dịch nói rõ nên huỷ.

Đáng chú ý: comment ngay bên trên chỗ thiếu đã **báo trước** đúng tình huống này ("`DIEM_DANH`
là Restrict nên nếu lọt qua đây sẽ nổ ở tầng DB với thông báo khó hiểu") — thêm bảng mới có FK
Restrict mà không đọc lại danh sách kiểm của handler xoá là bẫy sẽ còn lặp. 12 test mới, và tôi
đã thử xoá dòng kiểm để chắc test thật sự đỏ.

Không test nào đỏ trước đó, vì chưa có test nào xoá một buổi **có nhận xét**. Lại đúng khuôn
đã gặp cả tuần: test xanh không chứng minh gì về đường đi mà test không đi qua.

## Lỗi thứ hai: giáo viên bị "bạn không thuộc lớp này"

Người dùng báo ngay sau khi nhận bản trên. Backend **đúng** và tôi giữ nguyên: kênh
`NHAN_XET_BUOI_HOC` là của học viên, cho giáo viên gửi vào đó thì thống kê hài lòng thành vô
nghĩa. Sai là ở **UI của tôi** — hiện form cho mọi vai trò, nên giáo viên nhập xong mới biết
mình không được phép.

Đáng ghi lại vì đây là một dạng lỗi khác với các lỗi tuần này: không phải rò rỉ, không phải mất
dữ liệu, mà là **mời người dùng làm một việc chắc chắn thất bại**. Test API xanh hết — chúng
kiểm "giáo viên gửi thì bị chặn", đúng như thiết kế. Không test nào hỏi "vậy sao UI lại cho họ
thấy form".

Sửa: `BuoiHocDto` thêm cờ `toiLaHocVien` (tính trong cùng phép chiếu, không thêm truy vấn). Và
thay vì chỉ **ẩn** form, giáo viên thấy câu giải thích chỉ đúng chỗ ghi nhận xét của họ — ẩn
không thôi thì họ đi tìm mà không biết tìm ở đâu.

## Chuyển buổi giữ nguyên tab

`?tab=` không đổi khi `id` đổi. Người điểm danh lần lượt 20 buổi không phải bấm lại tab Điểm
danh 20 lần. Đổi **tab** dùng `replace: true` (6 lần bấm tab không sinh 6 mục lịch sử), còn đổi
**buổi** thì không — chuyển buổi *là* một bước điều hướng, Back phải quay về buổi vừa xem.

## Việc dọn kèm theo

- `BangDiemDanh` trước là `function` local trong `LichVaDiemDanh.tsx` và **hard-code `Modal`**,
  nên không dùng làm tab được. Tách ra file riêng, bọc `KhungNoiDung` như các component khác.
- `BuoiHocDto` khai local trong `LichVaDiemDanh.tsx` và **thiếu 6 trường** (`lopHocId`,
  `tenLopHoc`, `phongHoc`, `linkHoc`, `ghiChu`, `giaoVienId`) — đủ cho bảng, không đủ cho view
  chi tiết. Chuyển sang `buoiHocTypes.ts` dùng chung.
- `GET /bai-tap` nhận `?buoiHocId=`: lọc ở **server**. Lọc phía client thì `queryKey` giống
  nhau nên hai view dùng chung cache — mở view buổi rồi về view lớp sẽ thấy danh sách bị cắt.
- Tab Tài liệu hiện tài liệu của **lớp** kèm câu giải thích: tài liệu chỉ có FK tới lớp, không
  tới buổi. Nói rõ còn hơn để người dùng tưởng mình đang xem tài liệu riêng của buổi.

## Kiểm chứng

- `dotnet build` 0 warning · **277 test xanh** (54 unit + 223 integration), trong đó 13 test mới.
- Kiểm tay trên PostgreSQL thật: quy tắc #1 trên cột `nhan_xet` (gửi thiếu trường → giữ nguyên;
  gửi chuỗi rỗng → xoá), ranh giới đọc theo 4 vai trò, gửi lại = sửa, cách ly tenant hai chiều,
  buổi đã chốt vẫn nhận xét được, và bug xoá-buổi ở trên.
- Hai lần thử phá code để chắc test bắt được: bỏ điều kiện null của `nhan_xet` → test quy tắc #1
  đỏ; đảo điều kiện lọc quyền → 3 test đỏ.

## Nợ ghi nhận

- **N12** `Token_bi_sua_chu_ky_thi_bi_tu_choi` vẫn chập chờn: đỏ 1 lần trong lượt chạy cả
  solution (stack trace chỉ vào `BuocDoiMatKhauMiddleware`), xanh 4 lượt liên tiếp sau đó. Chưa
  tìm ra nguyên nhân, chưa sửa.
- Tài liệu **theo buổi** (FK `buoi_hoc_id` cho `TAI_LIEU_LOP_HOC`) — hiện chỉ có theo lớp.
