using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Infrastructure.Persistence.Seed;

/// <summary>
/// Bốn nhóm quyền dựng sẵn khi tạo trung tâm mới, theo ma trận phân quyền ở
/// docs/backend/phan-quyen-dong.md.
///
/// Đây chỉ là **điểm khởi đầu**, không phải luật cứng: admin vào màn Phân quyền sửa lại được
/// từng ô, hoặc tạo nhóm hoàn toàn khác. Hệ thống không có chỗ nào hard-code "nếu là giáo
/// viên thì…" — mọi quyết định truy cập đọc từ `QUYEN_CHUC_NANG` (quy tắc #9).
///
/// Vì sao khai tường minh thay vì suy từ vai trò: ma trận trong đặc tả có những ô không suy
/// được bằng quy luật nào (trợ giảng toàn quyền với bài tập nhưng chỉ xem được bài kiểm tra).
/// Viết ra hết thì đọc là hiểu, sửa một ô không sợ vỡ ô khác.
/// </summary>
internal static class NhomQuyenMacDinh
{
    internal const string QuanTri = "Quản trị viên";
    internal const string GiaoVien = "Giáo viên";
    internal const string TroGiang = "Trợ giảng";
    internal const string HocVien = "Học viên";

    private static readonly HanhDong[] DocThoi = [HanhDong.Xem];
    private static readonly HanhDong[] DocGhi = [HanhDong.Xem, HanhDong.Them, HanhDong.Sua];
    private static readonly HanhDong[] ToanBo =
        [HanhDong.Xem, HanhDong.Them, HanhDong.Sua, HanhDong.Xoa];

    /// <summary>
    /// Giáo viên — phụ trách trọn vẹn lớp mình dạy.
    ///
    /// KHÔNG có <see cref="ChucNang.LopHocToanTrungTam"/>: đó chính là thứ giới hạn họ trong
    /// phạm vi lớp được phân công. Có <c>LopHoc.Xem</c> nhưng không có <c>Sua</c> — lớp do
    /// admin lập, giáo viên chỉ dạy.
    /// </summary>
    internal static readonly (string ChucNang, HanhDong[] HanhDongs)[] CuaGiaoVien =
    [
        (ChucNang.LopHoc, DocThoi),
        (ChucNang.BuoiHoc, ToanBo),
        (ChucNang.DiemDanh, DocGhi),
        (ChucNang.BaiTap, ToanBo),
        (ChucNang.BaiNopBaiTap, [HanhDong.Xem, HanhDong.Sua]),   // Sua = chấm điểm
        (ChucNang.BaiKiemTra, ToanBo),
        (ChucNang.BaiLamKiemTra, [HanhDong.Xem, HanhDong.Sua]),
        (ChucNang.TaiLieu, [HanhDong.Xem, HanhDong.Them]),
        // FR-26 — soạn nội dung khoá trực tuyến là chuyên môn. KHÔNG có GhiDanhKhoaOnline:
        // cấp quyền học là việc điều phối, và ai bán được thì không nên tự cấp cho mình.
        (ChucNang.KhoaOnline, ToanBo),
        (ChucNang.HocOnline, DocThoi),                            // xem bài như học viên thấy
        (ChucNang.TaiKhoan, DocThoi),                            // xem học viên lớp mình
        // Đính kèm đề bài và tài liệu — endpoint /tep dùng chung gác bằng chức năng này.
        (ChucNang.Anh, ToanBo),
        (ChucNang.ThongKe, DocThoi),
    ];

    /// <summary>
    /// Trợ giảng — giống giáo viên nhưng KHÔNG xoá buổi học và KHÔNG ra đề kiểm tra.
    ///
    /// Hai khác biệt đó là lý do không gộp <c>BaiTap</c> với <c>BaiKiemTra</c> thành một
    /// chức năng: gộp rồi thì không nói được "toàn quyền bài tập, chỉ xem bài kiểm tra".
    /// </summary>
    internal static readonly (string ChucNang, HanhDong[] HanhDongs)[] CuaTroGiang =
    [
        (ChucNang.LopHoc, DocThoi),
        (ChucNang.BuoiHoc, [HanhDong.Xem, HanhDong.Sua]),        // không Them, không Xoa
        (ChucNang.DiemDanh, DocGhi),
        (ChucNang.BaiTap, ToanBo),
        (ChucNang.BaiNopBaiTap, [HanhDong.Xem, HanhDong.Sua]),
        (ChucNang.BaiKiemTra, DocThoi),
        (ChucNang.BaiLamKiemTra, DocThoi),
        (ChucNang.TaiLieu, DocThoi),
        // Trợ giảng xem được nội dung khoá online nhưng không sửa — cùng lẽ với BaiKiemTra.
        (ChucNang.KhoaOnline, DocThoi),
        (ChucNang.HocOnline, DocThoi),
        (ChucNang.TaiKhoan, DocThoi),
        (ChucNang.Anh, ToanBo),
        (ChucNang.ThongKe, DocThoi),
    ];

    /// <summary>
    /// Học viên — chỉ dữ liệu của chính mình.
    ///
    /// <c>DiemDanh.Them</c> nghe rộng nhưng an toàn: endpoint tự điểm danh không nhận tham số
    /// "điểm danh cho ai", nó lấy người dùng từ token. Không có đường nào để học viên điểm
    /// danh hộ người khác, kể cả khi có quyền này.
    ///
    /// <c>HocPhi.Xem</c> để họ tự tra công nợ; handler lọc theo chính họ.
    /// </summary>
    internal static readonly (string ChucNang, HanhDong[] HanhDongs)[] CuaHocVien =
    [
        (ChucNang.LopHoc, DocThoi),
        (ChucNang.BuoiHoc, DocThoi),
        (ChucNang.DiemDanh, [HanhDong.Xem, HanhDong.Them]),      // Them = tự điểm danh
        (ChucNang.BaiTap, DocThoi),
        (ChucNang.BaiNopBaiTap, [HanhDong.Xem, HanhDong.Them]),
        (ChucNang.BaiKiemTra, DocThoi),
        (ChucNang.BaiLamKiemTra, [HanhDong.Xem, HanhDong.Them]),
        (ChucNang.TaiLieu, DocThoi),
        // FR-26 — `Them` = tự đánh dấu đã học xong một bài. An toàn như `DiemDanh.Them`:
        // endpoint không nhận "đánh dấu cho ai", nó lấy người dùng từ token.
        // KHÔNG có `KhoaOnline` (đó là quyền soạn bài) — học viên đọc bài qua `HocOnline`,
        // và `IPhamViKhoaOnline` lọc về đúng khoá họ được ghi danh.
        (ChucNang.HocOnline, [HanhDong.Xem, HanhDong.Them]),
        (ChucNang.HocPhi, DocThoi),
        // Đính kèm bài nộp. Có Xoa để gỡ tệp nộp nhầm — quyền trên TỆP, không phải trên bài
        // của người khác: handler kiểm tệp phải thuộc bài nộp của chính họ.
        (ChucNang.Anh, [HanhDong.Xem, HanhDong.Them, HanhDong.Xoa]),
        (ChucNang.ThongKe, DocThoi),
    ];
}
