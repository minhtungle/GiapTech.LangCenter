using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Application.UnitTests;

/// <summary>
/// Tình trạng buổi học suy từ giờ (18/09/2026).
///
/// Chủ sản phẩm báo: *"trạng thái buổi học chưa chuẩn, buổi đã qua vẫn hiện đã lên lịch"*. Trên
/// dữ liệu thật lúc đó: **129/129 buổi đều `DaLenLich`, 85 buổi đã qua**.
///
/// Đây là PHÉP TÍNH thuần nên test ở tầng unit: không cần DB, và chạy được mọi mốc thời gian mà
/// integration test phải dựng dữ liệu mới thử tới.
/// </summary>
public class TinhTrangBuoiHocTests
{
    private static readonly DateTimeOffset BayGio = new(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);

    private static TinhTrangBuoiHoc Tinh(
        TrangThaiBuoiHoc tt, int giobatDau, int gioKetThuc)
        => TinhTrangBuoiHocExt.TinhTinhTrang(
            tt,
            BayGio.AddHours(giobatDau),
            BayGio.AddHours(gioKetThuc),
            BayGio);

    /// <summary>
    /// Buổi còn `DaLenLich` thì GIỜ quyết định — đây là ca chữa lỗi người dùng báo.
    /// </summary>
    [Theory]
    // Chưa tới giờ.
    [InlineData(2, 4, TinhTrangBuoiHoc.ChuaBatDau)]
    // Đang trong khoảng.
    [InlineData(-1, 1, TinhTrangBuoiHoc.DangDienRa)]
    // Giờ đã qua mà chưa ai chốt → PHẢI là "chưa chốt", KHÔNG phải "đã lên lịch".
    [InlineData(-4, -2, TinhTrangBuoiHoc.ChuaChot)]
    public void Buoi_da_len_lich_thi_gio_quyet_dinh(
        int batDau, int ketThuc, TinhTrangBuoiHoc mongDoi)
        => Assert.Equal(mongDoi, Tinh(TrangThaiBuoiHoc.DaLenLich, batDau, ketThuc));

    /// <summary>
    /// Hai đầu khoảng tính là ĐANG DIỄN RA.
    ///
    /// Người dùng mở màn đúng lúc vào lớp là tình huống thường xuyên nhất; để nó rơi vào "chưa
    /// bắt đầu" thì nút vào lớp không hiện. Dùng `&lt;`/`&gt;` thay cho `&lt;=`/`&gt;=` là sai
    /// đúng ở hai phút này — khoảng thời gian quá hẹp để ai bắt được bằng tay.
    /// </summary>
    [Fact]
    public void Dung_phut_bat_dau_va_ket_thuc_deu_la_dang_dien_ra()
    {
        var batDauDungLuc = TinhTrangBuoiHocExt.TinhTinhTrang(
            TrangThaiBuoiHoc.DaLenLich, BayGio, BayGio.AddHours(2), BayGio);
        Assert.Equal(TinhTrangBuoiHoc.DangDienRa, batDauDungLuc);

        var ketThucDungLuc = TinhTrangBuoiHocExt.TinhTinhTrang(
            TrangThaiBuoiHoc.DaLenLich, BayGio.AddHours(-2), BayGio, BayGio);
        Assert.Equal(TinhTrangBuoiHoc.DangDienRa, ketThucDungLuc);

        // Quá một giây là hết "đang diễn ra" — chốt biên trên.
        var vuaQua = TinhTrangBuoiHocExt.TinhTinhTrang(
            TrangThaiBuoiHoc.DaLenLich, BayGio.AddHours(-2), BayGio.AddSeconds(-1), BayGio);
        Assert.Equal(TinhTrangBuoiHoc.ChuaChot, vuaQua);
    }

    /// <summary>
    /// Trạng thái người đặt **THẮNG** giờ.
    ///
    /// Chiều này quan trọng không kém: buổi đã huỷ mà đang trong khung giờ của nó thì KHÔNG
    /// được hiện "đang diễn ra" — người dùng sẽ vào lớp trống. Buổi chốt sớm vẫn là "đã xong".
    /// Thiếu test này thì một bản sửa chỉ xét giờ vẫn xanh ở test trên.
    /// </summary>
    [Theory]
    [InlineData(TrangThaiBuoiHoc.DaHoanThanh, TinhTrangBuoiHoc.DaXong)]
    [InlineData(TrangThaiBuoiHoc.DaHuy, TinhTrangBuoiHoc.DaHuy)]
    [InlineData(TrangThaiBuoiHoc.ChuyenLich, TinhTrangBuoiHoc.ChuyenLich)]
    public void Trang_thai_nguoi_dat_thang_gio(
        TrangThaiBuoiHoc daLuu, TinhTrangBuoiHoc mongDoi)
    {
        // Thử ở CẢ BA vị trí thời gian — trạng thái người đặt phải thắng ở mọi vị trí.
        Assert.Equal(mongDoi, Tinh(daLuu, 2, 4));     // chưa tới giờ
        Assert.Equal(mongDoi, Tinh(daLuu, -1, 1));    // đang trong khung giờ
        Assert.Equal(mongDoi, Tinh(daLuu, -4, -2));   // đã qua
    }

    /// <summary>
    /// Mọi giá trị của <see cref="TrangThaiBuoiHoc"/> phải có nhánh xử lý.
    ///
    /// Thêm trạng thái mới mà quên map thì nó rơi vào nhánh `_` và suy theo GIỜ — nghĩa là một
    /// trạng thái người đặt bị giờ ghi đè, âm thầm. Test này buộc người thêm phải quay lại đây.
    /// </summary>
    [Fact]
    public void Moi_trang_thai_da_luu_deu_duoc_map()
    {
        foreach (var tt in Enum.GetValues<TrangThaiBuoiHoc>())
        {
            // Buổi ĐÃ QUA: nếu trạng thái được map thì kết quả phải khác `ChuaChot`
            // (chỉ `DaLenLich` mới ra `ChuaChot`).
            var kq = Tinh(tt, -4, -2);

            if (tt is TrangThaiBuoiHoc.DaLenLich)
                Assert.Equal(TinhTrangBuoiHoc.ChuaChot, kq);
            else
                Assert.NotEqual(TinhTrangBuoiHoc.ChuaChot, kq);
        }
    }
}
