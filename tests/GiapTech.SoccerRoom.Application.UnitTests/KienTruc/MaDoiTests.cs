using GiapTech.SoccerRoom.Domain.Common;

namespace GiapTech.SoccerRoom.Application.UnitTests.KienTruc;

/// <summary>Mã trung tâm 7 ký tự sinh tự động — định danh CLB khi đăng nhập (FR-01).</summary>
public class MaTrungTamTests
{
    [Fact]
    public void Ma_sinh_ra_dung_7_ky_tu()
    {
        for (var i = 0; i < 50; i++)
            Assert.Equal(7, MaTrungTam.Sinh().Length);
    }

    /// <summary>
    /// Người dùng phải đọc mã qua điện thoại và chép tay, nên bộ ký tự KHÔNG được chứa
    /// cặp dễ nhầm 0/O và 1/I/L.
    /// </summary>
    [Theory]
    [InlineData('0')]
    [InlineData('O')]
    [InlineData('1')]
    [InlineData('I')]
    [InlineData('L')]
    public void Ma_khong_chua_ky_tu_de_nham(char kyTuCam)
    {
        Assert.DoesNotContain(kyTuCam, MaTrungTam.BoKyTu);

        for (var i = 0; i < 200; i++)
            Assert.DoesNotContain(kyTuCam, MaTrungTam.Sinh());
    }

    [Fact]
    public void Ma_chi_gom_chu_hoa_va_so()
    {
        var ma = MaTrungTam.Sinh();
        Assert.All(ma, c => Assert.True(char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c)));
    }

    /// <summary>Không phải kiểm tra ngẫu nhiên nghiêm ngặt, chỉ bắt lỗi sinh ra hằng số.</summary>
    [Fact]
    public void Hai_lan_sinh_lien_tiep_khong_trung_nhau()
    {
        var tap = new HashSet<string>();
        for (var i = 0; i < 500; i++) tap.Add(MaTrungTam.Sinh());

        Assert.True(tap.Count > 490, $"Chỉ sinh được {tap.Count}/500 mã khác nhau — nghi RNG hỏng.");
    }

    [Theory]
    [InlineData("a3k9m2p", "A3K9M2P")]
    [InlineData("  A3K9M2P  ", "A3K9M2P")]
    [InlineData("A3k9M2p", "A3K9M2P")]
    public void Chuan_hoa_bo_khoang_trang_va_chuyen_hoa(string nhap, string mongDoi)
        => Assert.Equal(mongDoi, MaTrungTam.ChuanHoa(nhap));

    [Theory]
    [InlineData("A3K9M2P", true)]
    [InlineData("a3k9m2p", true)]      // chuẩn hoá trước khi kiểm
    [InlineData("A3K9M2", false)]      // thiếu 1 ký tự
    [InlineData("A3K9M2PQ", false)]    // thừa 1 ký tự
    [InlineData("A3K9M2O", false)]     // chứa O — không thuộc bộ ký tự
    [InlineData("A3K9M21", false)]     // chứa 1 — không thuộc bộ ký tự
    [InlineData("", false)]
    public void Hop_le_kiem_dung_dinh_dang(string ma, bool mongDoi)
        => Assert.Equal(mongDoi, MaTrungTam.HopLe(ma));
}
