# Module Buổi học & Điểm danh (FR-09 → FR-10)

## FR-09 — Buổi học

Lịch học của một lớp, sinh tự động theo tần suất rồi sửa được từng buổi.

### Sinh lịch tự động (bước 2 của wizard tạo lớp)

**Đầu vào:** ngày khai giảng, các thứ trong tuần, giờ bắt đầu/kết thúc, điều kiện dừng
(số buổi **hoặc** ngày kết thúc — đúng một trong hai), danh sách ngày nghỉ.

**Ngày lễ ảnh hưởng khác nhau tuỳ điều kiện dừng** — đây là chỗ dễ hiểu nhầm nhất:

| Dừng theo | Gặp ngày lễ | Kết quả |
|---|---|---|
| **Số buổi** | Bỏ qua, đi tiếp | Vẫn **đủ số buổi**, ngày kết thúc lùi ra. Học viên đóng tiền cho 24 buổi thì phải có 24 buổi |
| **Ngày kết thúc** | Bỏ hẳn | **Ít buổi đi**. UI phải nói rõ con số thật, không thì admin tưởng vẫn đủ |

**Ngày khai giảng rơi đúng thứ được chọn thì tính là buổi 1.** Không rơi vào thứ nào thì buổi 1
là ngày hợp lệ kế tiếp.

**Chặn:** tần suất rỗng, giờ kết thúc không sau giờ bắt đầu (không hỗ trợ lớp qua đêm), số buổi
ngoài 1–500, ngày kết thúc trước khai giảng. Có van chống lặp vô hạn 10 năm ngày cho trường hợp
danh sách nghỉ trùng khít với tần suất.

**Sinh lại lịch xoá hết buổi cũ** — chỉ cho phép khi lớp **chưa có buổi nào được điểm danh**
(`LICH_DA_CO_DIEM_DANH`). Đã có thì sửa từng buổi.

### Thời gian và múi giờ

Ba mốc đều là `DateTimeOffset`, lưu UTC. **Không có cột "ngày học" riêng**: lọc theo ngày dùng
khoảng `[00:00, 24:00)` giờ địa phương so với `bat_dau`. Cột riêng là dữ liệu thừa và sẽ sai âm
thầm nếu trung tâm đổi múi giờ — chỉ buổi sát ranh giới ngày lệch, rất khó phát hiện.

⚠️ **Hạ tầng**: cần `tzdata` + `icu-libs` trong image. Thiếu một trong hai thì
`TimeZoneInfo.FindSystemTimeZoneById` ném — và **chỉ ném trên production**, máy dev luôn chạy.
`IMuiGioTrungTam` bọc lại với fallback về UTC + log Error để không sập cả module.


### Trạng thái buổi: thứ người ĐẶT vs thứ SUY từ giờ (18/09/2026)

Chủ sản phẩm báo: *"trạng thái buổi học chưa chuẩn, buổi đã qua vẫn hiện đã lên lịch"*. Đúng vậy
trên dữ liệu thật lúc đó: **129/129 buổi đều `DaLenLich`, trong đó 85 buổi đã qua** — chưa buổi
nào được chốt, nên bảng và lịch nói mọi buổi đều "Đã lên lịch".

Chia làm **hai lớp khái niệm**, đừng gộp:

| | Ai quyết | Giá trị |
|---|---|---|
| `BUOI_HOC.trang_thai` (`TrangThaiBuoiHoc`) | **con người** | `DaLenLich` · `DaHoanThanh` · `ChuyenLich` · `DaHuy` |
| `tinhTrang` (`TinhTrangBuoiHoc`) — chỉ để HIỂN THỊ | **suy ra** | `ChuaBatDau` · `DangDienRa` · `ChuaChot` · `DaXong` · `ChuyenLich` · `DaHuy` |

Luật suy (một chỗ duy nhất: `Domain/Common/TinhTrangBuoiHoc.cs`):

- Trạng thái người đặt **luôn thắng giờ** — buổi đã huỷ không bao giờ hiện "đang diễn ra" dù
  đang trong khung giờ của nó, buổi chốt sớm vẫn là "đã xong".
- Chỉ khi còn `DaLenLich` thì giờ mới quyết định: chưa tới giờ → `ChuaBatDau`, trong khoảng
  `[BatDau, KetThuc]` → `DangDienRa`, đã quá → **`ChuaChot`** (việc tồn đọng, không phải trạng
  thái bình thường).

**Vì sao SUY chứ không thêm cột.** Lưu `ChuaBatDau`/`DangDienRa` vào DB thì phải có job chạy nền
đổi trạng thái theo giờ; job chết là trạng thái đứng im và sai âm thầm — đúng kiểu lỗi khó tìm
nhất. Suy từ giờ thì luôn đúng, không có gì để hỏng. Chủ sản phẩm chốt phương án này 18/09/2026.

**Hai bản sao logic, có chủ ý**: backend (`TinhTrangBuoiHocExt`) trả `tinhTrang` đúng tại thời
điểm gọi API; frontend (`lib/tinhTrangBuoi.ts`) tính lại vì màn lịch mở cả buổi sáng và buổi 9h
phải tự chuyển trạng thái mà không chờ tải lại. `TinhTrangBuoiHocTests` (backend) và
`tinhTrangBuoi.test.ts` (frontend) cố tình lặp lại **cùng bộ ca** để hai bên không trôi khỏi nhau.

**Đặt trạng thái**: `POST /buoi-hoc/{id}/trang-thai` với `{ trangThai }`, gác bằng `BuoiHoc.Sua`
(ai sửa được giờ/phòng thì cũng đặt được trạng thái). Endpoint **từ chối** `ChuaBatDau`/
`DangDienRa`/`ChuaChot` — chúng suy từ giờ, đặt tay là tạo hai nguồn sự thật cho cùng câu hỏi.
Buổi đã chốt phải **mở lại** (`DaLenLich`) trước khi đổi sang trạng thái khác; xem mục dưới.

`ChuyenLich` khác `DaHuy`: huỷ là bỏ hẳn, chuyển lịch là buổi vẫn diễn ra nhưng vào thời điểm
khác — nên `ChuyenLich` **không khoá** buổi, còn phải sửa được giờ.

### Màu của từng trạng thái

Yêu cầu 18/09/2026: *"thêm màu sắc cho từng trạng thái khi hiển thị trên bảng và lịch để dễ nhận
biết đúng"*. Khai **một chỗ** (`MAU_TINH_TRANG` + `CLASS_TINH_TRANG`) cho cả badge ở bảng và sự
kiện trên lịch — hai chỗ tô khác nhau thì cùng một buổi đọc ra hai nghĩa.

| Tình trạng | Màu | Vì sao |
|---|---|---|
| Chưa bắt đầu | xám | chưa có gì xảy ra, không cần hút mắt |
| Đang diễn ra | xanh dương, đậm | việc đang xảy ra NGAY BÂY GIỜ, thấy đầu tiên |
| **Chưa chốt** | vàng cảnh báo | **việc tồn đọng** — chính ô chữa lỗi được báo |
| Đã xong | xanh lá | |
| Chuyển lịch | tím | không phải lỗi (đỏ), không phải chờ (vàng) |
| Đã huỷ | đỏ nhạt, gạch ngang | |

"Chưa bắt đầu" **không** dùng `--primary`: primary của dự án là xanh lá, cùng họ với `--status-ok`
("đã xong") — trên chú giải hai ô đứng cạnh nhau gần như một màu, đúng cái mà yêu cầu muốn tránh.

Lịch có **chú giải màu** phía trên: bảng có chữ trong badge, còn lịch chỉ có màu nên không có
chú giải thì phải bấm từng buổi mới biết màu nghĩa gì.

### Buổi ĐÃ KHOÁ — không sửa, không huỷ, không xoá

Chốt buổi (`TrangThai = DaHoanThanh`) là **khoá** nó. Từ lúc đó điểm danh trở thành bằng chứng
chuyên cần, nên:

| Thao tác | Buổi chưa chốt | Buổi đã chốt |
|---|---|---|
| Sửa giờ / giáo viên / phòng | ✅ | ❌ `BUOI_HOC_DA_KHOA` |
| Huỷ buổi | ✅ | ❌ |
| Xoá hẳn | ✅ nếu chưa có điểm danh | ❌ |
| Bị lịch sinh mới ghi đè | ✅ | ❌ giữ nguyên |

Đổi giờ một buổi đã chốt sẽ làm bản ghi điểm danh nói về một thời điểm không còn tồn tại. Huỷ
nó là nói rằng buổi ấy chưa từng diễn ra, trong khi cả lớp đã được ghi có mặt hay vắng.

Buổi `DaHuy` **không** khoá: huỷ rồi thì lên lịch lại là chuyện bình thường.

Một chỗ duy nhất quyết định điều này — `BuoiHoc.DaKhoa` ở Domain. Mọi handler hỏi qua đó thay
vì tự so `TrangThai`, để thêm trạng thái khoá mới sau này không phải sửa sáu nơi.

### Hai cách đưa buổi vào lịch

| Cách | Endpoint | Xoá buổi cũ? | Dùng khi |
|---|---|---|---|
| **Sinh lịch** | `POST /lop-hoc/{id}/sinh-lich` | **Có** — buổi chưa học | Nhập sai tần suất lúc đầu, muốn làm lại |
| **Thêm buổi** | `POST /lop-hoc/{id}/sinh-them-buoi` | Không | Lớp kéo dài thêm, hoặc dạy bù một buổi |

Trước 07/09/2026 chỉ có cách thứ nhất, nên không có đường bổ sung buổi mà không mất lịch cũ.

**Thêm một buổi = để `SoBuoi = 1`** và tích đúng thứ của ngày đó. Từng có lệnh
`ThemBuoiHocCommand` riêng cho buổi lẻ, nhưng hai lệnh làm cùng một việc ở hai mức số lượng và
hai nút cạnh nhau với tên gần giống nhau ("Thêm buổi" / "Sinh thêm buổi") gây nhầm — đã gộp.

Khi gộp, các trường của buổi lẻ chuyển sang lệnh còn lại: `LaHocBu`, `GiaoVienId`, `PhongHoc`,
`LinkHoc`, `GhiChu`. Bỏ chúng đi sẽ **mất hẳn khả năng ghi buổi dạy bù** và biến cột
`la_hoc_bu` thành cột chết. Các trường này chỉ hiện ở giao diện khi thêm buổi, không hiện khi
sinh lịch chính khoá — cả một lịch không thể là "học bù".

**Sinh lịch giữ nguyên buổi đã chốt** và đánh số buổi mới TIẾP theo `MAX(ThuTu)` — không bắt
đầu lại từ 1, vì `UNIQUE(LopHocId, ThuTu)` sẽ nổ và vì hai buổi cùng số thứ tự thì học viên
không biết đâu là buổi nào.

Cả hai cách bổ sung đều **chặn trùng giờ** với buổi đang có (`BUOI_HOC_TRUNG_GIO`). Không im
lặng bỏ qua buổi trùng: người dùng chọn nhầm ngày sẽ tưởng đã thêm 8 buổi trong khi chỉ thêm
được 3.

**Xoá buổi không đánh số lại các buổi sau.** Học viên và giáo viên đã quen "buổi 12"; đổi số
hàng loạt làm mọi ghi chú ngoài hệ thống sai theo. Khoảng trống trong dãy số chấp nhận được.

### Hai kiểu xem lịch

| Kiểu | Dùng khi |
|---|---|
| **Bảng** (mặc định) | Điểm danh, xem số liệu từng buổi, thao tác |
| **Lịch** (tháng / tuần / danh sách) | Nhìn tổng quát cả khoá, phát hiện khoảng trống và trùng giờ |

Bảng là mặc định vì đó là chỗ điểm danh — việc làm thường xuyên nhất. Bấm một buổi trên lịch
mở ngay bảng điểm danh của buổi đó.

Màu trên lịch **khớp badge ở bảng** để hai chỗ không nói khác nhau: đã lên lịch (màu chủ đạo),
đã hoàn thành (xanh), đã huỷ (mờ + gạch ngang). Buổi bù thêm viền nhấn thay vì đổi màu nền, để
vẫn đọc được trạng thái.

Trục giờ giới hạn **6h–22h**: trung tâm ngoại ngữ không dạy đêm, để trục 24 tiếng thì buổi tối
bị nén thành một dải mỏng.

### Ưu tiên buổi SẮP TỚI lên đầu (16/09/2026)

Yêu cầu chủ sản phẩm: *"phần lịch học hãy ưu tiên buổi sắp tới lên đầu"*.

Bảng chia **hai nhóm có tiêu đề**, không chỉ đảo thứ tự:

| Nhóm | Xếp thế nào | Vì sao |
|---|---|---|
| **SẮP TỚI** | gần nhất trước | "buổi tới dạy gì, phòng nào" là việc thường ngày — phải là dòng đầu |
| **ĐÃ QUA** | mới nhất trước | buổi vừa dạy hay được xem lại nhất (điểm danh, nhận xét) |

Buổi **đã huỷ** xuống nhóm "đã qua" dù ngày còn ở tương lai: nó không còn là việc phải làm. Dòng
nhóm "đã qua" hiện mờ hơn để phân biệt được cả khi tiêu đề nhóm đã cuộn khỏi tầm mắt. **Số buổi
vẫn hiện** ở cột đầu nên không mất ngữ cảnh giáo trình.

> **Sắp ở FRONTEND, KHÔNG sửa `OrderBy` của API.** Endpoint `/lop-hoc/{id}/buoi-hoc` có **bốn màn
> khác** dùng, trong đó `ChiTietBuoiHoc` suy *"buổi trước / buổi sau"* từ **vị trí trong mảng** —
> đổi thứ tự ở API thì nút "buổi sau" nhảy về quá khứ, một lỗi im lặng không có gì báo. Thứ tự
> theo `ThuTu` vẫn là **hợp đồng của API**; nhóm/sắp lại chỉ là cách *màn lịch* trình bày.
>
> `e2e/lich-uu-tien-buoi-sap-toi.spec.ts` canh **cả hai chiều**: màn lịch sắp đúng, VÀ API giữ thứ
> tự theo số buổi. Đột biến đổi `OrderBy` ở backend làm đỏ đúng assert thứ hai.

Mốc so sánh là hai thời điểm tuyệt đối nên **không phụ thuộc múi giờ** — khác các phép cắt kỳ theo
ngày, chỗ đó bắt buộc dùng múi giờ trung tâm (xem `ThongKeCrmDtos`).

Khung **Lịch** không đổi: FullCalendar mở ở tháng hiện tại nên buổi sắp tới đã nằm trong tầm mắt.

### Huỷ khác xoá

| | Giữ bản ghi | Dùng khi |
|---|---|---|
| **Huỷ** (`POST /buoi-hoc/{id}/huy`) | Có, `TrangThai = DaHuy` | Buổi đã lên lịch nhưng không diễn ra — nghỉ lễ, giáo viên ốm |
| **Xoá** (`DELETE /buoi-hoc/{id}`) | Không | Lên nhầm buổi, chưa ai điểm danh |

Xoá bị chặn nếu buổi đã có điểm danh (`BUOI_HOC_DA_CO_DIEM_DANH`) — chặn sớm với mã lỗi rõ
ràng thay vì để khoá ngoại `Restrict` nổ ở tầng DB.

### Quy tắc
- `UNIQUE(lop_hoc_id, thu_tu)` ở tầng DB. Xoá/thêm buổi thì **đánh số lại liên tục** 1..n.
- Buổi có thể **override giáo viên** (`giao_vien_id` null = dùng giáo viên chính của lớp),
  phòng học, link. Kiểm trùng lịch chạy trên **giáo viên hiệu lực**, không phải giáo viên lớp.
- **Huỷ buổi, không xoá** — giữ lịch sử.
- Xoá lớp đã có buổi bị chặn ở tầng DB (`Restrict`), không chỉ ở tầng ứng dụng.

### Kiểm trùng lịch giáo viên

**Cảnh báo, không chặn** — giáo viên dạy online hai lớp cùng lúc là hợp lệ.

- Giao khoảng dùng dấu `<` nghiêm ngặt: 08:00–10:00 và 10:00–12:00 **không** trùng. Dùng `<=`
  sẽ sinh cảnh báo giả cho gần như mọi giáo viên dạy liên tiếp.
- **Loại trừ chính lớp đang sửa.** Thiếu bước này thì sửa lịch một lớp đã lưu sẽ báo mọi buổi
  "trùng với chính nó" — người dùng thấy 24 cảnh báo vô nghĩa rồi học cách bỏ qua mọi cảnh báo.

## FR-10 — Điểm danh

**Hai nguồn:** học viên tự khai và giáo viên xác nhận.

### Hai cột trạng thái, không phải một

| Cột | Ý nghĩa |
|---|---|
| `trang_thai_tu_khai` (nullable) | Học viên tự khai. `null` ≠ `Vắng` — không tự điểm danh là bình thường |
| `trang_thai_chinh_thuc` | **Nguồn sự thật duy nhất cho mọi báo cáo** |
| `nguon_ghi_nhan` | Ai đặt ra giá trị chính thức |

Gộp một cột thì sau khi giáo viên ghi đè sẽ **không còn biết học viên đã khai gì**. Mất thông
tin đó có giá thật: tranh chấp *"em có điểm danh mà sao bị tính vắng"* là tình huống thường
xuyên, và tỷ lệ khai-sai là chỉ dấu gian lận. Muốn tính lại về sau thì phải đổi schema **và**
không có dữ liệu quá khứ. Chi phí giữ: một cột `integer` nullable.

### Quy tắc

- **Giáo viên luôn thắng.** Ghi đè là **một chiều**: học viên bấm lại sau khi bị đánh vắng
  không sửa ngược được.
- **Học viên tự điểm danh chỉ trong khung `[giờ bắt đầu − 15 phút, giờ kết thúc]`.**
- Endpoint tự điểm danh **không nhận id học viên** — lấy từ token. Không phải "handler nhớ
  kiểm" mà là **không có tham số nào để lạm dụng**: dù có quyền `DiemDanh.Them`, học viên vẫn
  không có đường điểm danh hộ người khác.
- **Vắng bắt buộc có lý do** (`Vang` hoặc `VangCoPhep`) — báo cáo vắng không lý do là báo cáo
  vô dụng.
- Bảng điểm danh trả **mọi học viên đang học**, kể cả người chưa có bản ghi. Chỉ trả bản ghi đã
  có thì buổi chưa ai điểm danh ra bảng rỗng, giáo viên không có gì để bấm.
- Học viên vào lớp **sau** buổi này không xuất hiện trong bảng — nếu không, báo cáo chuyên cần
  tính họ vắng những buổi diễn ra trước khi họ vào.
- **Chốt buổi** sinh đủ bản ghi cho người chưa điểm danh, mặc định Vắng. Không có bước này thì
  buổi không ai điểm danh cho `COUNT(*) = 0` — báo cáo đọc ra "không có dữ liệu" thay vì "cả
  lớp vắng", hai thứ khác hẳn nhau.
- `UNIQUE(buoi_hoc_id, hoc_vien_id)` ở tầng DB (quy tắc #8).

### Vắng phải có lý do — và UI phải NÓI RÕ ai thiếu (18/09/2026)

Validator bắt buộc: vắng (có phép hay không) mà `LyDoVang` rỗng thì cả lệnh bị từ chối
(`THIEU_LY_DO_VANG`). Cố ý — *"báo cáo vắng không lý do là báo cáo vô dụng"*.

Nhưng **quy tắc đúng không có nghĩa là màn hình dùng được**. Người dùng báo *"lưu điểm danh đang
lỗi không lưu được"*, tái hiện đúng vậy:

1. Bảng mặc định cho mọi học viên là **Vắng**, ô lý do **trống**.
2. Giáo viên đổi vài người sang Có mặt rồi bấm Lưu → **400**.
3. Lỗi trả theo TỪNG DÒNG trong `duLieu.truong`
   (`{"DanhSach[2]":["THIEU_LY_DO_VANG"], ...}`), nhưng `layMaLoi` chỉ đọc `errorCode` ở tầng
   ngoài ⇒ màn hình hiện đúng một câu **"Dữ liệu nhập vào chưa hợp lệ"**.

Người dùng biết là sai mà không biết sai ở đâu trong 6 dòng, cũng không biết có đường nào khác.
Đó là lý do nó *"lỗi không lưu được"* chứ không phải *"thiếu lý do vắng"*.

Chữa ở frontend, ba việc cùng nhau:

| Việc | Vì sao cần |
|---|---|
| Liệt kê **tên** học viên còn thiếu (không chỉ đếm) | lớp 20 người thì con số vẫn buộc dò từng dòng |
| Viền đỏ + `aria-invalid` ở **đúng ô** thiếu | dòng cảnh báo nói "ai", viền nói "gõ vào đâu" |
| Khoá nút Lưu kèm `title` giải thích | cho bấm để nhận 400 là bắt đi một vòng vô nghĩa; nút mờ không lời giải thích thì trông như màn treo |

Câu cảnh báo còn **chỉ đường thoát**: dùng **"Chốt buổi"** nếu không muốn nhập tay — lệnh chốt tự
ghi `"Không điểm danh"` cho người chưa khai (xem `ChotBuoiHandler`). Sự bất đối xứng này là chủ ý:
chốt là *"tôi kết luận cả buổi"*, còn lưu là *"tôi ghi đúng từng người"* — nên lưu không tự bịa
lý do thay giáo viên.

Ngoài ra `onError` nay lấy **mã lỗi cụ thể đầu tiên** trong `duLieu.truong` để hiện thay mã chung
— các mã này đều đã có bản dịch, chỉ là trước đây bị bỏ đi.

> Canh bởi `e2e/luu-diem-danh-thieu-ly-do.spec.ts`, **cả hai chiều**: chặn kèm chỉ dẫn khi thiếu,
> và **lưu được** khi đã đủ. Thiếu chiều thứ hai thì bản sửa "khoá nút vĩnh viễn" cũng xanh —
> mutation test xác nhận ca đó bị bắt.

## FR-09b — Nhận xét quanh buổi học

Hai chiều, **hai cơ chế lưu khác nhau** — đừng gộp:

| Chiều | Lưu ở | Ai ghi |
|---|---|---|
| Giáo viên nhận xét **từng học viên** trong buổi | `DIEM_DANH.nhan_xet` | Người chốt điểm danh |
| Học viên nhận xét về **buổi** | `NHAN_XET_BUOI_HOC` | Học viên đang học của lớp |

Nhận xét của giáo viên đặt vào `DIEM_DANH` chứ không tạo bảng riêng: bảng đó đã có
`UNIQUE(buoi_hoc_id, hoc_vien_id)`, tức **đúng độ mịn cần thiết** — một dòng cho mỗi cặp
(buổi, học viên). Khác `ly_do_vang`: lý do nói *vì sao không có mặt*, nhận xét nói *về việc học*.

Nhận xét của học viên thì **phải** là bảng riêng, vì hai thứ khác nhau ở gốc:

- **Quyền khác nhau** — học viên ghi được ở đây nhưng không được đụng `DIEM_DANH` (đó là bằng
  chứng chuyên cần).
- **Vòng đời khác nhau** — học viên **vắng** vẫn nhận xét được về buổi họ không dự.

### Quy tắc

- **Học viên chỉ đọc nhận xét CỦA MÌNH.** Cho đọc của bạn cùng lớp thì nhận xét thành diễn đàn
  công khai và không ai nói thật nữa. Lọc ở **handler**, không ở frontend — ẩn ở frontend thì
  gọi API trực tiếp vẫn đọc được.
- **Đọc được tất cả** = có `DiemDanh.Sua` (giáo viên của lớp) **hoặc** `LopHocToanTrungTam.Xem`
  (quản trị). Cố ý **không** thêm chức năng phân quyền thứ 18: ai chốt điểm danh của buổi thì
  đương nhiên đọc được phản hồi về buổi đó, và thêm hằng mới lại làm admin của trung tâm cũ bị
  403 cho tới khi chạy bổ khuyết quyền.
- Lệnh gửi nhận xét **không nhận id học viên** — lấy từ token, cùng cách với tự điểm danh.
- **Gửi lần hai là SỬA**, không tạo bản mới (`UNIQUE(buoi_hoc_id, hoc_vien_id)`).
- `muc_hai_long` 1–5 **nullable** — không ép cho điểm mới gửi được góp ý. **Từ 18/09/2026 form
  không còn chấm trường này**; xem mục dưới.
- DTO trả cờ **`cuaToi`** để UI biết bản nào nạp vào form sửa. Đừng suy từ *"danh sách có một
  phần tử"*: giáo viên đọc được mọi nhận xét, lớp chỉ một học viên đã gửi thì suy kiểu đó sẽ nạp
  nhận xét của **học viên** vào form của **giáo viên**, bấm Gửi là ghi đè nhầm chủ.
- **UI chỉ hiện form gửi nhận xét cho học viên của lớp** — `BuoiHocDto.toiLaHocVien`. Giáo viên
  thấy câu giải thích kèm đường dẫn tới chỗ ghi nhận xét của họ (tab Điểm danh) thay vì một form
  bấm Gửi là lỗi. Cờ này là **thông tin dựng UI**, không phải lớp bảo vệ — handler vẫn tự kiểm.
- **Buổi đã chốt vẫn nhận xét được** — học viên thường góp ý *sau* khi buổi kết thúc.
- **Xoá buổi đã có nhận xét bị chặn** (`BUOI_HOC_DA_CO_NHAN_XET`) — FK là Restrict, thiếu kiểm ở
  handler thì API trả 500 thay vì nói rõ "hãy huỷ buổi thay vì xoá". Đã gặp thật 07/09/2026.

### Chấm tiêu chí RIÊNG từng người đứng lớp (18/09/2026)

Chủ sản phẩm: *"phần đánh sao cho mức hài lòng cần thay bằng tiêu chí đánh giá cho giáo viên và
trợ giảng như đã quy định tại HRM, bố trí lại giao diện phần nhận xét cho thuận tiện hiển thị và
thao tác"*.

Bản 16/09 đã có điểm tiêu chí (FR-29) nhưng **chấm chung cho cả buổi**, nên xếp hạng trợ giảng ở
thống kê nhân sự thực chất là điểm của giáo viên: trợ giảng giỏi trong lớp có giáo viên bị chấm
thấp sẽ chịu oan, và ngược lại. Nay mỗi người một cột điểm.

**Schema**: thêm `DIEM_TIEU_CHI.nguoi_duoc_cham_id` (nullable, FK Restrict tới `NGUOI_DUNG`).
UNIQUE đổi từ `(nhan_xet_buoi_hoc_id, tieu_chi_id)` sang
`(nhan_xet_buoi_hoc_id, tieu_chi_id, nguoi_duoc_cham_id) NULLS NOT DISTINCT`:

- Giữ khoá cũ mà cho chấm riêng thì điểm người thứ hai **bị chặn ở tầng DB** — lỗi lúc chạy.
- `NULLS NOT DISTINCT` là bắt buộc: mặc định Postgres coi mọi `NULL` là khác nhau, thiếu cờ này
  thì một tiêu chí có nhiều điểm "chấm chung" trong cùng phiếu — đúng cái khoá cũ đang chặn.
- `CHECK` chặn `nguoi_duoc_cham_id` ở phiếu nhân viên kinh doanh: người được chấm đã là
  `PHIEU_DANH_GIA_NHAN_VIEN.nhan_vien_id`, thêm nữa là hai nguồn sự thật.

**Ba loại điểm cùng tồn tại** (quy tắc #1 — dữ liệu cũ không được biến mất):

| Điểm | Thuộc về ai khi thống kê |
|---|---|
| tiêu chí có `nguoi_duoc_cham_id` | đúng người đó, KHÔNG chia cho người khác |
| tiêu chí `nguoi_duoc_cham_id = NULL` (trước 18/09) | cả buổi — mọi người dạy buổi đó |
| `muc_hai_long` (phiếu không có tiêu chí) | cả buổi |

**Người được chấm phải THỰC SỰ đứng lớp buổi đó** — kiểm ở handler
(`NGUOI_DUOC_CHAM_KHONG_DUNG_LOP`). Đây là tham số client gửi; không kiểm thì học viên chấm được
giáo viên lớp khác và điểm chảy vào xếp hạng của người vô can.

`GET /buoi-hoc/{id}/nguoi-dung-lop` trả danh sách để dựng phiếu (giáo viên **hiệu lực** của buổi
+ trợ giảng của lớp, bỏ trùng). Frontend không tự ghép từ `tenGiaoVien`/`tenTroGiangs`: hai
trường đó chỉ có tên, mà chấm điểm cần `id`, và ghép sai khi buổi dùng giáo viên riêng.

**Quyền đọc danh mục tiêu chí.** Trước 18/09 màn nhận xét gọi endpoint quản lý danh mục (gác
`TieuChiDanhGia.Xem` — quyền HRM), học viên nhận **403**, frontend `catch` trả rỗng rồi **âm thầm
rơi về chấm sao** — chính là lỗi được báo. Chữa bằng hai bước:

1. Thêm thao tác `TieuChiDanhGia.TuLam` = *đọc danh mục để tự đi chấm*, và endpoint riêng
   `GET /tieu-chi-danh-gia/de-cham` (chỉ tên + mô tả, luôn lọc `DangDung`). Không nới quyền của
   endpoint cũ: đó là màn QUẢN LÝ danh mục, cấp cho học viên là mở một phần HRM cho họ.
2. `TieuChiDanhGia` thuộc **HRM**, nên vừa cấp ô này là học viên "vào được HRM" ⇒ `Layout` đưa
   sang sidebar nhân sự và họ **mất luôn menu Lớp học**. Chữa bằng `ChucNang.MoLoiVaoHeThong`:
   một danh sách hẹp các cặp (chức năng, thao tác) **không mở lối vào** hệ thống của chúng.
   Cùng tinh thần với `DungChung`. Canh bởi `MoLoiVaoHeThongTests` (cả hai chiều: `TuLam` không
   mở lối, nhưng `Xem`/`Them`/`Sua` vẫn mở).

   > Lỗi này do E2E `doi-nick-khong-giu-quyen-cu` bắt được, không phải do tsc hay lint.

Trung tâm lập **trước 18/09** cần chạy `scripts/cap-quyen-tieu-chi-cho-hoc-vien.sql` — nhóm
"Học viên" của họ chưa có ô này (`BoKhuyetQuyenQuanTri` cố ý chỉ vá nhóm quản trị).

**Giao diện**: mỗi người đứng lớp một khối, kèm nhãn vai trò; trong khối là **bảng** tiêu chí ×
mức 1–5. Dạng bảng thay vì cột sao dọc: 2 người × N tiêu chí xếp dọc thì phiếu dài mấy màn hình.
Ô "mức hài lòng" chung **bỏ khỏi form** nhưng vẫn **hiện để đọc** ở nhận xét cũ.

## Mã lỗi

| Mã | Khi nào |
|---|---|
| `TAN_SUAT_TRONG` | Chưa chọn thứ nào trong tuần |
| `NGUOI_DUOC_CHAM_KHONG_DUNG_LOP` | Chấm điểm cho người không đứng lớp buổi đó (18/09/2026) |
| `TRANG_THAI_KHONG_HOP_LE` | Đặt trạng thái buổi bằng giá trị ngoài enum |
| `GIO_KET_THUC_KHONG_HOP_LE` | Giờ kết thúc ≤ giờ bắt đầu |
| `SO_BUOI_KHONG_HOP_LE` | Số buổi ngoài 1–500 |
| `NGAY_KET_THUC_TRUOC_KHAI_GIANG` | Ngày kết thúc trước ngày khai giảng |
| `DIEU_KIEN_DUNG_KHONG_HOP_LE` | Không chọn, hoặc chọn cả hai cách kết thúc |
| `KHONG_SINH_DUOC_BUOI_NAO` | Khoảng ngày không chứa thứ nào được chọn |
| `CHAN_TREN_SINH_BUOI` | Không đủ số buổi trong 10 năm quét |
| `GIO_KHONG_TON_TAI_DO_DOI_GIO` | Giờ rơi vào khoảng bị bỏ qua khi đổi giờ mùa |
| `LICH_DA_CO_DIEM_DANH` | Sinh lại lịch khi đã có điểm danh |
| `BUOI_DA_HUY` | Điểm danh cho buổi đã huỷ |
| `NGOAI_KHUNG_GIO_DIEM_DANH` | Tự điểm danh ngoài khung cho phép |
| `KHONG_THUOC_LOP_NAY` | Tự điểm danh ở lớp mình không học |
| `HOC_VIEN_KHONG_THUOC_LOP` | Điểm danh cho người ngoài lớp |
| `THIEU_LY_DO_VANG` | Vắng mà không ghi lý do |
| `BUOI_HOC_DA_CO_NHAN_XET` | Xoá buổi đã có nhận xét của học viên — huỷ thay vì xoá |

## Chưa làm

- **Kiểm trùng lịch giáo viên ở UI** — API đã có (`KiemTrungLichQuery`), chưa nối vào wizard.
- **Danh mục ngày lễ hệ thống** — hiện nhập tay từng ngày.
- **Nhân bản lớp** — cần lưu tần suất sinh lịch vào `LOP_HOC` trước.
