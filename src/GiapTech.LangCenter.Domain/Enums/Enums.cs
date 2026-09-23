namespace GiapTech.LangCenter.Domain.Enums;

/// <summary>Trạng thái tài khoản đăng nhập.</summary>
/// <summary>
/// Trạng thái TÀI KHOẢN — còn đăng nhập được không.
///
/// Đừng nhầm với <see cref="TrangThaiNhanSu"/>: vô hiệu hoá tài khoản của một giáo viên đã
/// nghỉ KHÔNG có nghĩa họ biến mất khỏi lịch sử lớp học.
/// </summary>
public enum TrangThaiNguoiDung
{
    HoatDong = 0,
    VoHieuHoa = 1
}

/// <summary>
/// Trạng thái NHÂN SỰ — người này còn thuộc trung tâm không.
///
/// Tách khỏi <see cref="TrangThaiNguoiDung"/> vì hai câu hỏi khác nhau. Trước 07/09/2026 chỉ
/// có một cột gánh cả hai, nên vô hiệu hoá tài khoản giáo viên đã nghỉ thì không phân công
/// được họ vào lớp cũ nữa.
/// </summary>
public enum TrangThaiNhanSu
{
    DangLamViec = 0,
    DaNghi = 1
}

/// <summary>
/// Thao tác trong hệ phân quyền động (FR-05).
/// Quyền hiệu lực = hợp của mọi nhóm quyền gán cho tài khoản.
/// </summary>
/// <summary>
/// Thao tác trên một chức năng. Lưu vào `QUYEN_CHUC_NANG.hanh_dong` dưới dạng **số nguyên**.
///
/// ⚠️ **KHÔNG đổi giá trị số của mục đã có, không chèn giữa.** Quyền đang lưu trong DB trỏ tới
/// con số, nên đổi 2 thành 3 là âm thầm biến quyền "Sửa" của mọi nhóm thành "Xoá". Thêm mục mới
/// thì lấy số kế tiếp ở cuối dải của nó.
///
/// **Bốn thao tác cơ bản (0-3)** áp cho mọi chức năng kiểu CRUD.
/// **Thao tác đặc thù (10+)** chỉ áp cho chức năng thật sự có việc đó — xem
/// <see cref="Common.ChucNang.ThaoTacCua"/>. Mỗi cái sinh ra vì gộp vào 4 thao tác cơ bản sẽ
/// buộc cấp chung hai việc khác hẳn nhau về mức nguy hiểm.
/// </summary>
public enum HanhDong
{
    // ---------- Cơ bản: 0-3, KHÔNG được đổi ----------
    Xem = 0,
    Them = 1,
    Sua = 2,
    Xoa = 3,

    // ---------- Đặc thù: 10+ ----------

    /// <summary>
    /// Duyệt / từ chối đơn từ hệ thống khác. Tách khỏi `Sua` vì duyệt một đơn xếp lớp là quyết
    /// định **không đảo ngược được** và ảnh hưởng tới bên bán, khác hẳn sửa tên lớp.
    /// </summary>
    Duyet = 10,

    /// <summary>
    /// Chốt sổ — sau đó dữ liệu không sửa được nữa. Chốt buổi học sinh bản ghi **Vắng mặc
    /// định** cho mọi người chưa điểm danh, hệ quả lan sang học phí và thống kê.
    /// </summary>
    Chot = 11,

    /// <summary>
    /// Vô hiệu hoá nhưng **giữ lịch sử** — khác `Xoa` (mất hẳn) và khác `Sua` (vẫn dùng được).
    /// Huỷ lớp, huỷ buổi học. Ma trận 4 thao tác không diễn đạt được khái niệm này.
    /// </summary>
    Huy = 12,

    /// <summary>Sinh hàng loạt bản ghi — sinh lịch tạo tới 500 buổi và xoá lịch cũ.</summary>
    SinhLich = 13,

    /// <summary>
    /// Ghi nhận **tiền thật đã nhận**. Tách khỏi `Them` vì tạo một đơn hàng (cam kết) khác hẳn
    /// ghi một phiếu thu (tiền đã vào) — việc thứ hai cần đối soát kế toán.
    /// </summary>
    ThuTien = 14,

    /// <summary>Chấm điểm bài làm. Tách khỏi `Sua` vì điểm là thứ học viên nhìn vào.</summary>
    Cham = 15,

    /// <summary>
    /// Thao tác trên dữ liệu **của chính mình** — tự điểm danh, nộp bài, đánh dấu đã học, gửi
    /// nhận xét. Handler luôn lấy id người dùng **từ token**, không nhận từ client.
    ///
    /// Đây là thao tác của HỌC VIÊN. Trước đây nó chiếm ô `Them` trống của chức năng, nên
    /// `DiemDanh.Them` (học viên tự điểm danh) nằm cùng chức năng với `DiemDanh.Sua` (giáo viên
    /// ghi điểm danh cả lớp) — hai vai trò khác nhau trong một dòng ma trận.
    /// </summary>
    TuLam = 16,

    /// <summary>
    /// Đọc tệp nhạy cảm: hợp đồng, CCCD scan, sao kê. Tách khỏi `Xem` vì xem danh sách nhân sự
    /// không đồng nghĩa được đọc hợp đồng của họ.
    /// </summary>
    DocTep = 17,

    /// <summary>Tải lên / xoá tệp đính kèm. Tách khỏi `Sua` (sửa thông tin) và `Xoa` (xoá bản ghi).</summary>
    QuanLyTep = 18,

    /// <summary>
    /// Sửa thứ liên quan trực tiếp tới dòng tiền: giá niêm yết, mã QR chuyển khoản. Tách vì đổi
    /// QR nhận tiền không được cùng quyền với đổi logo.
    /// </summary>
    CauHinhTien = 19,

    /// <summary>
    /// Sửa **ma trận quyền** của một nhóm — leo thang đặc quyền không giới hạn. Tách khỏi `Sua`
    /// (đổi tên nhóm) vì một endpoint duy nhất cho phép tự cấp mọi quyền cho nhóm của mình.
    /// </summary>
    CauHinhQuyen = 20,

    /// <summary>Hoàn tất wizard — lớp rời trạng thái nháp, hiện với mọi người. Một chiều.</summary>
    HoanTat = 21,

    /// <summary>Xếp / gỡ nhân sự khỏi cơ cấu tổ chức. Tách khỏi `Sua` (đổi tên phòng ban).</summary>
    XepNhanSu = 22,

    /// <summary>Gửi yêu cầu xếp lớp từ CRM sang LMS (FR-21) — tách khỏi `Sua` đơn hàng.</summary>
    GuiXepLop = 23,

    /// <summary>
    /// Cho nội dung trang đích **lên Internet** (FR-30).
    ///
    /// Tách khỏi `Sua` vì hậu quả khác hẳn: sửa sai thì sửa lại, còn xuất bản sai là người
    /// ngoài đã đọc được. Người soạn nội dung và người quyết định công khai thường là hai
    /// người khác nhau.
    /// </summary>
    XuatBan = 24,

    /// <summary>
    /// Chuyển một liên hệ từ trang đích sang khách hàng CRM (FR-30).
    ///
    /// Là **cầu nối LDP → CRM**, nên tách riêng: người xem danh sách liên hệ chưa chắc được
    /// phép tạo khách hàng trong CRM.
    /// </summary>
    ChuyenCrm = 25
}

/// <summary>
/// Vai trò nghiệp vụ của tài khoản — dùng để LỌC danh sách (chọn giáo viên, chọn học viên),
/// KHÔNG dùng để gác quyền.
///
/// Gác quyền là việc của hệ phân quyền động (quy tắc #9): hai người cùng
/// <see cref="GiaoVien"/> có thể có nhóm quyền hoàn toàn khác nhau. Trộn hai khái niệm này
/// là quay lại `[Authorize(Roles=...)]` mà dự án cố tình tránh.
/// </summary>
public enum LoaiNguoiDung
{
    /// <summary>
    /// Nhân sự vận hành **không thuộc kinh doanh**: hành chính, nhân sự, kế toán, IT…
    ///
    /// Đây là giá trị MẶC ĐỊNH của <see cref="Entities.NguoiDung"/> và là vai trò của tài khoản
    /// quản trị do `TenantSeeder` tạo — nên nó phải giữ nghĩa RỘNG. Đã có lúc tính đổi nhãn nó
    /// thành "Nhân viên kinh doanh" (yêu cầu 16/09/2026) nhưng làm vậy sẽ gọi chính người quản
    /// trị hệ thống là nhân viên kinh doanh ở mọi trung tâm mới.
    /// </summary>
    NhanVien = 0,
    GiaoVien = 1,
    TroGiang = 2,
    HocVien = 3,

    /// <summary>
    /// Nhân viên kinh doanh / tư vấn tuyển sinh (thêm 16/09/2026 theo yêu cầu chủ sản phẩm:
    /// *"vai trò nhân viên => nhân viên kinh doanh. tránh nhầm lẫn"*).
    ///
    /// **Vai trò RIÊNG, không phải đổi tên <see cref="NhanVien"/>.** Lúc rà dữ liệu thật thấy
    /// `NhanVien` đang gồm cả người nhân sự và người quản trị hệ thống, nên đổi nhãn sẽ tạo ra
    /// một nhầm lẫn mới thay vì bỏ nhầm lẫn cũ.
    ///
    /// Giá trị **4**, không chen vào giữa: DB lưu số nguyên, đổi số của giá trị đang có là làm
    /// sai toàn bộ dữ liệu cũ một cách im lặng (quy tắc #1).
    ///
    /// Về phân quyền thì giống <see cref="NhanVien"/> — cả hai đều là "nhân sự", đều vào màn
    /// HRM và đều xếp được vào cơ cấu tổ chức. Khác biệt nằm ở chỗ **lọc và thống kê**: giờ mới
    /// trả lời được "cho tôi xem doanh thu theo từng nhân viên kinh doanh" mà không phải suy từ
    /// phòng ban.
    /// </summary>
    NhanVienKinhDoanh = 4
}

/// <summary>
/// Nhóm tiêu chí đánh giá (FR-29, 16/09/2026) — quyết định **ai chấm và chấm ở đâu**.
///
/// Không gộp một danh mục chung: tiêu chí "Chủ động tìm khách" không có nghĩa với giáo viên, và
/// hiện nó trên phiếu chấm buổi học chỉ làm học viên bỏ trống.
/// </summary>
public enum NhomTieuChi
{
    /// <summary>Chấm nhân viên kinh doanh — do QUẢN LÝ chấm theo kỳ tháng.</summary>
    KinhDoanh = 0,

    /// <summary>
    /// Chấm chất lượng giảng dạy — do HỌC VIÊN chấm trên từng buổi học.
    ///
    /// Dùng chung **bộ tiêu chí** cho giáo viên và trợ giảng (cùng câu hỏi: truyền đạt, nhiệt
    /// tình…), nhưng **chấm RIÊNG từng người**: xem `DiemTieuChi.NguoiDuocChamId`. Sửa lại
    /// 18/09/2026 — bản đầu (16/09) chấm chung một phiếu cho cả buổi, làm xếp hạng trợ giảng ở
    /// FR-29 thực chất là điểm của giáo viên.
    /// </summary>
    GiangDay = 1
}

/// <summary>Hình thức tổ chức lớp học.</summary>
public enum HinhThucHoc
{
    /// <summary>
    /// Giá trị 0 CỐ Ý để trống nghĩa: JSON thiếu trường enum sẽ deserialize thành 0, nếu 0 là
    /// một hình thức thật thì client quên gửi sẽ âm thầm ghi sai. Validator chặn giá trị này.
    /// </summary>
    ChuaChon = 0,
    Online = 1,
    Offline = 2,
    KetHop = 3
}

/// <summary>
/// Trạng thái vòng đời lớp học.
///
/// Chỉ LƯU ba giá trị do con người quyết định: <see cref="Nhap"/>, <see cref="DaHuy"/>,
/// <see cref="DaKetThuc"/>. Hai giá trị còn lại suy từ ngày lúc đọc — xem
/// <c>LopHoc.TrangThaiHienThi</c>. Làm vậy để không cần một background job chỉ để đổi trạng
/// thái lúc nửa đêm, và không bao giờ có chuyện job chết làm lớp kẹt ở "sắp khai giảng".
/// </summary>
public enum TrangThaiLopHoc
{
    /// <summary>Đang tạo dở qua wizard, chưa hoàn tất.</summary>
    Nhap = 0,
    SapKhaiGiang = 1,
    DangHoc = 2,
    DaKetThuc = 3,
    DaHuy = 4
}

/// <summary>Trạng thái của một học viên TRONG một lớp cụ thể.</summary>
public enum TrangThaiHocVienTrongLop
{
    DangHoc = 0,
    BaoLuu = 1,
    ChuyenLop = 2,
    DaNghi = 3
}

/// <summary>
/// Trạng thái buổi học — **chỉ những gì CON NGƯỜI quyết định**.
///
/// Cố ý KHÔNG có `ChuaBatDau`/`DangDienRa`: hai thứ đó suy được từ giờ của buổi so với hiện
/// tại, luôn đúng, không cần job chạy nền. Lưu thành cột thì phải có job đổi trạng thái theo
/// giờ — job chết là trạng thái đứng im và sai âm thầm, đúng kiểu lỗi khó tìm nhất. Chủ sản
/// phẩm chốt phương án suy theo giờ ngày 18/09/2026.
///
/// Phần suy theo giờ nằm ở <see cref="Common.TinhTrangBuoiHoc"/>.
/// </summary>
public enum TrangThaiBuoiHoc
{
    /// <summary>Mặc định khi sinh lịch. Kết hợp với giờ để ra "chưa bắt đầu"/"đang diễn ra"/"chưa chốt".</summary>
    DaLenLich = 0,
    DaHoanThanh = 1,
    DaHuy = 2,

    /// <summary>
    /// Buổi đã chuyển sang lịch khác (18/09/2026) — khác `DaHuy`: huỷ là bỏ hẳn, chuyển lịch là
    /// buổi vẫn sẽ diễn ra nhưng vào thời điểm khác.
    ///
    /// Không khoá buổi: chuyển lịch rồi còn phải sửa được giờ, nếu khoá thì chính việc chuyển
    /// lịch thành bất khả thi.
    /// </summary>
    ChuyenLich = 3
}

/// <summary>
/// Trạng thái điểm danh. Dùng chung cho CẢ giá trị học viên tự khai lẫn giá trị chính thức —
/// tách hai enum thì mọi phép so "giáo viên có sửa khác lời khai không" phải map qua lại.
/// </summary>
public enum TrangThaiDiemDanh
{
    CoMat = 0,
    Vang = 1,
    DiMuon = 2,
    VangCoPhep = 3
}

/// <summary>Ai đặt ra trạng thái điểm danh CHÍNH THỨC.</summary>
public enum NguonDiemDanh
{
    HocVienTuKhai = 0,
    GiaoVien = 1,
    QuanTri = 2
}

/// <summary>Trạng thái bài học viên nộp cho bài tập.</summary>
public enum TrangThaiBaiNop
{
    DaNop = 0,
    NopMuon = 1,
    DaCham = 2
}

/// <summary>
/// Hình thức bài kiểm tra.
///
/// Giai đoạn này chỉ làm <see cref="NopFile"/>; giữ sẵn <see cref="TracNghiemOnline"/> trong
/// enum để thêm ngân hàng câu hỏi sau này không phải đổi schema.
/// </summary>
public enum LoaiBaiKiemTra
{
    NopFile = 0,
    TracNghiemOnline = 1
}

/// <summary>Trạng thái bài kiểm tra.</summary>
public enum TrangThaiBaiKiemTra
{
    Nhap = 0,
    DaPhatHanh = 1,
    DaDong = 2
}

/// <summary>Phân loại tài liệu giảng dạy.</summary>
public enum LoaiTaiLieu
{
    GiaoTrinh = 0,
    BaiGiang = 1,
    ThamKhao = 2,
    DeThi = 3,
    Khac = 4
}

/// <summary>
/// Phương thức thu học phí.
///
/// Hệ thống KHÔNG xử lý tiền: không gọi cổng thanh toán, không đối chiếu sao kê, không tự ghi
/// nhận. Tiền đi trực tiếp giữa hai bên, thủ quỹ vào nhập tay số đã nhận.
/// </summary>
public enum PhuongThucThanhToan
{
    TienMat = 0,
    ChuyenKhoan = 1,
    Khac = 2
}

/// <summary>
/// Loại thao tác trong nhật ký hệ thống.
///
/// Không dùng lại <see cref="HanhDong"/> của hệ phân quyền: ở đó `Xem` là một hành động cần
/// quyền, còn ở đây nhật ký **không ghi việc đọc** — ghi mọi lượt xem sẽ làm bảng phình gấp
/// hàng chục lần mà gần như không ai tra tới.
/// </summary>
public enum HanhDongNhatKy
{
    Them = 0,
    Sua = 1,
    Xoa = 2,

    /// <summary>Đăng nhập, đổi mật khẩu, làm mới token — không phải CRUD trên dữ liệu.</summary>
    XacThuc = 3,

    /// <summary>Thao tác không rơi vào bốn loại trên.</summary>
    Khac = 9
}

/// <summary>
/// Đơn vị tiền của khoá học và đăng ký (FR-18/FR-19).
///
/// Danh mục ĐÓNG — không cho nhập chuỗi tuỳ ý, nếu không sẽ có "usd", "USD ", "Đô" cùng tồn
/// tại và không tổng hợp được. Thêm đơn vị mới: thêm hằng ở đây và bản dịch ở frontend.
/// </summary>
public enum DonViTien
{
    VND = 0,
    USD = 1,
    EUR = 2,
    CAD = 3
}

/// <summary>Hình thức liên hệ khi chăm sóc khách hàng (FR-17).</summary>
public enum HinhThucChamSoc
{
    GoiDien = 0,
    ZaloFacebook = 1,
    Email = 2,
    GapTrucTiep = 3,
    Khac = 4
}

/// <summary>
/// Trạng thái khách trong phễu bán hàng (FR-17).
///
/// **Không lưu thành cột trên `KHACH_HANG`.** Trạng thái hiện tại suy từ lần chăm sóc mới nhất
/// — hai chỗ lưu cùng một thông tin thì chúng lệch nhau ngay lần đầu ai đó sửa lịch sử mà quên
/// cột kia. Khách chưa có lần chăm sóc nào coi là <see cref="Moi"/>.
/// </summary>
public enum TrangThaiKhachHang
{
    Moi = 0,
    DangTuVan = 1,
    DaMua = 2,
    TuChoi = 3
}

/// <summary>
/// Khách đến từ đâu (13/09/2026) — **lưu thành cột**, khác `TrangThaiKhachHang` ở trên.
///
/// Vì sao không suy từ `CreatedById is null`: null ở đó đã mang sẵn hai nghĩa khác — khách tạo
/// trước 12/09/2026 (chưa có cột audit), và người tạo đã bị xoá (FK `SET NULL`). Chồng nghĩa
/// thứ ba lên thì "khách tự đăng ký" và "dữ liệu cũ" thành không phân biệt được, mà đó đúng là
/// con số báo cáo doanh số của nhân viên kinh doanh dựa vào.
/// </summary>
public enum NguonKhachHang
{
    /// <summary>Nhân viên tạo hồ sơ — `CreatedById` là người phụ trách. Mặc định, giữ nguyên
    /// nghĩa cho mọi hàng đã có.</summary>
    NhanVienTao = 0,

    /// <summary>Khách tự tìm đến / tự đăng ký. Không có nhân viên nào phụ trách, nên đơn của
    /// khách này KHÔNG tính vào doanh số cá nhân.</summary>
    TuDangKy = 1,

    /// <summary>
    /// Khách để lại thông tin qua **form trên trang đích công khai** (FR-30, 24/09/2026).
    ///
    /// Tách khỏi <see cref="TuDangKy"/> dù cùng nghĩa "không có nhân viên phụ trách": CRM cần
    /// đo được **kênh landing** mang về bao nhiêu khách, và gộp chung thì không tách ra được.
    /// Giống `TuDangKy`, đơn của khách này KHÔNG tính vào doanh số cá nhân.
    /// </summary>
    TuLanding = 2
}

/// <summary>
/// **Tag vai trò của phòng ban** (FR-22, thêm 16/09/2026) — cho biết phòng đó làm việc gì, để
/// module khác nhận diện đúng nhóm khi lọc dữ liệu.
///
/// ## Vì sao cần
///
/// Trước đây bộ lọc "đội nhóm" ở CRM liệt kê **mọi** phòng ban, kể cả phòng Đào tạo và các
/// phòng chỉ mang tính mô tả trong sơ đồ tổ chức. Chọn phòng Đào tạo để xem doanh thu là câu
/// hỏi vô nghĩa — nó không bán hàng — nhưng người dùng vẫn phải đọc qua nó mỗi lần lọc.
///
/// ## `null` KHÁC `KinhDoanh`
///
/// Phòng **không có tag** (`TagVaiTro == null`) chỉ tồn tại để diễn tả cơ cấu: nó hiện trong
/// cây tổ chức, xếp được nhân sự, nhưng **không xuất hiện ở bộ lọc của module nào**. Đây là
/// mặc định cho mọi phòng đã có khi thêm tính năng này (migration không đoán tag — quy tắc #1).
///
/// ## Một tag, không phải nhiều
///
/// Chốt 16/09/2026 với chủ sản phẩm: mỗi phòng đúng một vai trò. Nhiều tag thì khi lọc doanh
/// thu theo "nhóm kinh doanh", phòng mang cả tag Kinh doanh và Giáo viên vẫn hiện — người đọc
/// không hiểu vì sao phòng Đào tạo lại nằm trong danh sách đội bán hàng.
/// </summary>
public enum TagVaiTroPhongBan
{
    /// <summary>Nhóm bán hàng — phòng duy nhất xuất hiện ở bộ lọc đội nhóm của CRM.</summary>
    KinhDoanh = 0,

    /// <summary>Nhóm giáo viên đứng lớp.</summary>
    GiaoVien = 1,

    /// <summary>Nhóm trợ giảng.</summary>
    TroGiang = 2
}

/// <summary>Trạng thái một khoá học trực tuyến (FR-26).</summary>
public enum TrangThaiKhoaOnline
{
    /// <summary>Đang soạn — học viên KHÔNG thấy, kể cả đã ghi danh.</summary>
    Nhap = 0,

    /// <summary>Đang mở — học viên được ghi danh thì học được.</summary>
    DangMo = 1,

    /// <summary>
    /// Ngừng cấp mới. Người ĐANG học vẫn học tiếp — đóng khoá không được cắt quyền của người
    /// đã trả tiền. Khác `Nhap` ở chỗ đó.
    /// </summary>
    NgungCapMoi = 2
}

/// <summary>Loại đơn hàng (FR-18/FR-20) — suy từ khoá ngoại nào có giá trị, không lưu cột.</summary>
public enum LoaiDonHang
{
    KhoaHoc = 0,
    SanPham = 1
}

/// <summary>Trạng thái một yêu cầu xếp lớp (FR-21).</summary>
public enum TrangThaiYeuCauXepLop
{
    /// <summary>Sale đã gửi, chờ bên đào tạo xếp vào lớp.</summary>
    DangCho = 0,

    /// <summary>Đã xếp vào một lớp cụ thể.</summary>
    DaXep = 1,

    /// <summary>Huỷ yêu cầu (khách đổi ý, hoàn tiền…) — do BÊN BÁN thu lại.</summary>
    DaHuy = 2,

    /// <summary>
    /// Bên đào tạo **từ chối** xếp lớp — khác `DaHuy` ở chỗ ai quyết định.
    ///
    /// Tách hai trạng thái vì hai câu chuyện khác nhau khi đọc lịch sử: `DaHuy` là "khách rút",
    /// `TuChoi` là "trung tâm chưa nhận được". Gộp thành một thì không trả lời được câu hỏi
    /// đầu tiên của người bán: tại sao học viên của tôi chưa vào lớp.
    ///
    /// Kèm `LyDoTuChoi` bắt buộc.
    /// </summary>
    TuChoi = 3
}

/// <summary>
/// Loại liên kết mạng xã hội (FR-23).
///
/// Giá trị 0 là `Facebook` — mạng phổ biến nhất ở thị trường này, nên JSON thiếu trường sẽ
/// deserialize thành lựa chọn hợp lý nhất thay vì một loại lạ.
/// </summary>
public enum LoaiMxh
{
    Facebook = 0,
    Zalo = 1,
    LinkedIn = 2,
    Telegram = 3,
    Khac = 4
}
