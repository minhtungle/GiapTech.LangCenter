using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Application.UnitTests.KienTruc;

/// <summary>
/// `ChucNang.MoLoiVaoHeThong` — thao tác nào KHÔNG mở lối vào hệ thống con (18/09/2026).
///
/// Sinh ra từ một lỗi thật: vừa cấp `TieuChiDanhGia.TuLam` cho nhóm "Học viên" (để họ chấm giáo
/// viên theo tiêu chí) thì học viên "vào được HRM", `Layout` đưa sang sidebar nhân sự và họ
/// **mất luôn menu Lớp học** — chỉ còn thấy "Tổng quan".
///
/// Test ở tầng unit vì đây là một LUẬT thuần, không cần DB. Bộ integration
/// `ChamRiengGiaoVienTroGiangTests` kiểm hệ quả đầu-cuối (`/toi/he-thong` của học viên thật).
/// </summary>
public class MoLoiVaoHeThongTests
{
    /// <summary>
    /// `TuLam` trên tiêu chí = đọc danh mục để tự đi chấm ⇒ KHÔNG mở lối vào HRM.
    /// </summary>
    [Fact]
    public void Doc_tieu_chi_de_cham_khong_mo_loi_vao_hrm()
        => Assert.False(ChucNang.MoLoiVaoHeThong(ChucNang.TieuChiDanhGia, HanhDong.TuLam));

    /// <summary>
    /// **Chiều ngược, và là phần dễ làm quá tay**: các thao tác QUẢN LÝ danh mục tiêu chí vẫn
    /// phải mở lối vào HRM.
    ///
    /// Không có test này thì một bản sửa kiểu `chucNang != TieuChiDanhGia` (loại cả chức năng
    /// thay vì đúng một thao tác) vẫn xanh — và người phụ trách danh mục tiêu chí sẽ không vào
    /// được HRM nếu đó là quyền HRM duy nhất của họ. Trên dữ liệu hiện tại điều đó chưa lộ ra
    /// vì chỉ quản trị giữ quyền này, mà quản trị còn 16 quyền HRM khác che đi.
    /// </summary>
    [Theory]
    [InlineData(HanhDong.Xem)]
    [InlineData(HanhDong.Them)]
    [InlineData(HanhDong.Sua)]
    public void Quan_ly_danh_muc_tieu_chi_van_mo_loi_vao_hrm(HanhDong hanhDong)
        => Assert.True(ChucNang.MoLoiVaoHeThong(ChucNang.TieuChiDanhGia, hanhDong));

    /// <summary>
    /// Ngoại lệ phải HẸP: mọi cặp (chức năng, thao tác) khác đều mở lối vào bình thường.
    ///
    /// Canh để danh sách ngoại lệ không phình ra một cách im lặng — cùng khuôn "danh sách ngoại
    /// lệ có khai lý do" của bảy test canh kiến trúc.
    /// </summary>
    [Fact]
    public void Chi_co_dung_mot_ngoai_le()
    {
        var ngoaiLe = (
            from cn in ChucNang.TatCa
            from hd in ChucNang.ThaoTacCua(cn)
            where !ChucNang.MoLoiVaoHeThong(cn, hd)
            select $"{cn}.{hd}").ToList();

        Assert.Equal([$"{ChucNang.TieuChiDanhGia}.{HanhDong.TuLam}"], ngoaiLe);
    }
}
