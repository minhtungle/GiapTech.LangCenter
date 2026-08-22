using System.Text.RegularExpressions;
using GiapTech.SoccerRoom.Domain.Common;

namespace GiapTech.SoccerRoom.Application.UnitTests.KienTruc;

/// <summary>
/// Danh sách chỉ số kỹ năng tồn tại ở HAI nơi: <see cref="ChiSoKyNang.HopLe"/> (Domain, để
/// validate) và <c>CHI_SO_KY_NANG</c> trong <c>frontend/src/components/ChamChiSo.tsx</c> (để vẽ
/// radar và dựng ô chấm điểm).
///
/// Cùng lý do với <see cref="MauAoDongBoTests"/>: thêm chỉ số ở frontend mà quên backend thì
/// người dùng chấm xong bị chặn `CHI_SO_KY_NANG_KHONG_HOP_LE`; thêm ở backend mà quên frontend
/// thì không ai chấm được chỉ số đó.
///
/// Trước 20/08/2026 backend KHÔNG có danh sách này — API nhận cả `{"tanCong": 99}` và cả khoá
/// bịa như `{"tocDo": 8}`. Test này biến ràng buộc thành thứ CI bắt được.
/// </summary>
public class ChiSoKyNangDongBoTests
{
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
    public void Danh_sach_chi_so_frontend_va_backend_khop_nhau()
    {
        var tep = Path.Combine(
            TimGocRepo(), "frontend", "src", "components", "ChamChiSo.tsx");
        Assert.True(File.Exists(tep), $"Không thấy {tep}");

        var khoi = Regex.Match(
            File.ReadAllText(tep),
            @"export const CHI_SO_KY_NANG\s*=\s*\[(?<than>.*?)\]\s*as const",
            RegexOptions.Singleline);
        Assert.True(khoi.Success, "Không tìm thấy khai báo CHI_SO_KY_NANG trong ChamChiSo.tsx");

        var maFe = Regex.Matches(khoi.Groups["than"].Value, @"'(?<ma>[A-Za-z]+)'")
            .Select(m => m.Groups["ma"].Value)
            .ToList();

        Assert.NotEmpty(maFe);

        var thieuOBackend = maFe.Except(ChiSoKyNang.HopLe).ToList();
        var thieuOFrontend = ChiSoKyNang.HopLe.Except(maFe).ToList();

        Assert.True(
            thieuOBackend.Count == 0,
            $"Frontend có chỉ số backend chưa biết: {string.Join(", ", thieuOBackend)}. "
            + "Thêm vào Domain/Common/ChiSoKyNang.cs, không thì người dùng chấm xong bị chặn.");

        Assert.True(
            thieuOFrontend.Count == 0,
            $"Backend có chỉ số frontend chưa biết: {string.Join(", ", thieuOFrontend)}. "
            + "Thêm vào CHI_SO_KY_NANG trong ChamChiSo.tsx, không thì không ai chấm được.");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("""{"tanCong":1}""", true)]
    [InlineData("""{"tanCong":10}""", true)]
    [InlineData("""{"tanCong":5,"theLuc":7}""", true)]
    [InlineData("""{}""", true)]
    [InlineData("""{"tanCong":0}""", false)]
    [InlineData("""{"tanCong":11}""", false)]
    [InlineData("""{"tanCong":99}""", false)]
    [InlineData("""{"tanCong":-1}""", false)]
    [InlineData("""{"tanCong":7.5}""", false)]
    [InlineData("""{"tocDo":8}""", false)]
    [InlineData("""{"tanCong":"cao"}""", false)]
    [InlineData("khong-phai-json", false)]
    public void Kiem_json_chi_so(string? json, bool hopLe)
    {
        // Chấm THIẾU (chỉ vài chỉ số) và không chấm gì đều hợp lệ: trưởng nhóm không đủ thời gian
        // chấm 6 tiêu chí cho 14 người mỗi trận.
        //
        // Số thực (7.5) bị từ chối: thang là số nguyên 1–10, trung bình tính ở tầng hiển thị.
        // Cho số thực vào DB thì hai nơi làm tròn khác nhau và con số lệch.
        Assert.Equal(hopLe, ChiSoKyNang.KiemJson(json) is null);
    }
}
