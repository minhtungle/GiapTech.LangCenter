using System.Text.RegularExpressions;
using GiapTech.SoccerRoom.Domain.Common;

namespace GiapTech.SoccerRoom.Application.UnitTests.KienTruc;

/// <summary>
/// Bảng màu áo tồn tại ở HAI nơi: <see cref="MauAo.HopLe"/> (Domain, để validate) và
/// <c>BANG_MAU_AO</c> trong <c>frontend/src/components/soDo/loaiSan.ts</c> (để vẽ, có kèm mã
/// hex và màu chữ).
///
/// Không gộp được vì frontend cần mã màu CSS mà Domain không nên biết tới. Nhưng hai danh
/// sách **mã** phải khớp: thêm màu ở frontend mà quên backend thì người dùng chọn xong bị
/// chặn với lỗi "Màu áo không hợp lệ"; thêm ở backend mà quên frontend thì màu lưu được nhưng
/// áo hiện ra không màu.
///
/// Test này biến ràng buộc đó thành thứ CI bắt được.
/// </summary>
public class MauAoDongBoTests
{
    /// <summary>Lần ngược lên thư mục gốc repo để tìm frontend — test chạy từ bin/Debug.</summary>
    private static string TimGocRepo()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "frontend")))
            d = d.Parent;

        return d?.FullName
            ?? throw new DirectoryNotFoundException(
                "Không tìm thấy thư mục gốc repo (chứa 'frontend/') từ " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Bang_mau_frontend_va_backend_khop_nhau()
    {
        var tep = Path.Combine(TimGocRepo(), "frontend", "src", "components", "soDo", "loaiSan.ts");
        Assert.True(File.Exists(tep), $"Không thấy {tep}");

        var noiDung = File.ReadAllText(tep);

        // Cắt đúng phần khai báo BANG_MAU_AO, không quét cả tệp: chuỗi 'trang'/'do' còn xuất
        // hiện ở chỗ khác (mã vị trí, tên biến) và sẽ lọt vào kết quả.
        var khoi = Regex.Match(
            noiDung,
            @"export const BANG_MAU_AO:\s*MauAo\[\]\s*=\s*\[(?<than>.*?)\]",
            RegexOptions.Singleline);
        Assert.True(khoi.Success, "Không tìm thấy khai báo BANG_MAU_AO trong loaiSan.ts");

        var maFe = Regex.Matches(khoi.Groups["than"].Value, @"ma:\s*'(?<ma>[A-Za-z]+)'")
            .Select(m => m.Groups["ma"].Value)
            .ToList();

        Assert.NotEmpty(maFe);

        // So theo TẬP HỢP chứ không theo thứ tự: thứ tự chỉ ảnh hưởng cách xếp ô trên UI.
        var thieuOBackend = maFe.Except(MauAo.HopLe).ToList();
        var thieuOFrontend = MauAo.HopLe.Except(maFe).ToList();

        Assert.True(
            thieuOBackend.Count == 0,
            $"Frontend có màu backend chưa biết: {string.Join(", ", thieuOBackend)}. " +
            "Thêm vào Domain/Common/MauAo.cs, nếu không người dùng chọn xong sẽ bị chặn.");

        Assert.True(
            thieuOFrontend.Count == 0,
            $"Backend có màu frontend chưa vẽ: {string.Join(", ", thieuOFrontend)}. " +
            "Thêm vào BANG_MAU_AO kèm mã hex và màu chữ, nếu không áo sẽ hiện không màu.");
    }

    /// <summary>Mã màu trùng nhau sẽ làm nút chọn màu có hai ô giống hệt, bấm cái nào cũng như nhau.</summary>
    [Fact]
    public void Khong_co_ma_mau_trung()
    {
        Assert.Equal(MauAo.HopLe.Length, MauAo.HopLe.Distinct().Count());
    }

    [Theory]
    [InlineData("trang", true)]
    [InlineData("xanhDuong", true)]
    [InlineData("xanhLa", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Kiem_ma_hop_le(string? ma, bool mongDoi)
    {
        Assert.Equal(mongDoi, MauAo.LaMaHopLe(ma));
    }
}
