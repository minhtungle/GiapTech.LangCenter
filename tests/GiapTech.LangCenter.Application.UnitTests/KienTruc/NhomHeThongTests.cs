using GiapTech.LangCenter.Domain.Common;

namespace GiapTech.LangCenter.Application.UnitTests.KienTruc;

/// <summary>
/// Canh việc nhóm chức năng theo hệ thống (HRM · CRM · LMS) luôn đầy đủ và không mâu thuẫn.
///
/// Vì sao cần test: sidebar và màn phân quyền dựng từ `ChucNang.HeThongCua`. Thêm hằng mới mà
/// quên khai hệ thống thì nó âm thầm thành "dùng chung" — hiện ở sidebar của cả ba hệ thống.
/// Không có gì đỏ, chỉ người dùng thấy module lạ trong hệ thống không liên quan.
/// </summary>
public class NhomHeThongTests
{
    /// <summary>
    /// Mọi chức năng phải **hoặc** thuộc một hệ thống **hoặc** nằm trong danh sách dùng chung.
    /// Đây là test buộc người thêm hằng mới phải dừng lại quyết định, cùng tinh thần với
    /// `CachLyTenantTests` hỏi chiều ngược về entity không bị lọc.
    /// </summary>
    [Fact]
    public void Moi_chuc_nang_phai_duoc_khai_he_thong_hoac_dung_chung()
    {
        var chuaKhai = ChucNang.TatCa
            .Where(cn => ChucNang.HeThongCua(cn) is null && !ChucNang.DungChung.Contains(cn))
            .ToList();

        Assert.True(chuaKhai.Count == 0,
            $"Chức năng chưa khai hệ thống: {string.Join(", ", chuaKhai)}.\n"
            + "Thêm vào `TheoHeThong` nếu nó thuộc một hệ thống, hoặc vào `DungChung` nếu cả "
            + "ba hệ thống đều cần. Bỏ qua thì nó hiện ở sidebar của MỌI hệ thống.");
    }

    /// <summary>
    /// Chức năng dùng chung KHÔNG được đồng thời gán cho một hệ thống — hai nguồn sự thật thì
    /// sidebar lọc theo cái nào là không xác định.
    /// </summary>
    [Fact]
    public void Chuc_nang_dung_chung_khong_thuoc_he_thong_nao()
    {
        foreach (var cn in ChucNang.DungChung)
            Assert.Null(ChucNang.HeThongCua(cn));
    }

    /// <summary>Danh sách dùng chung phải nằm trong danh mục chính, không phải tên gõ tay.</summary>
    [Fact]
    public void Chuc_nang_dung_chung_deu_co_trong_danh_muc()
    {
        foreach (var cn in ChucNang.DungChung)
            Assert.Contains(cn, ChucNang.TatCa);
    }

    /// <summary>
    /// Mỗi hệ thống phải có ít nhất một chức năng riêng. Hệ thống rỗng thì bộ chuyển hệ thống
    /// đưa người dùng tới một sidebar trống — bấm vào không hiểu vì sao chẳng có gì.
    /// </summary>
    [Theory]
    [InlineData(HeThong.Hrm)]
    [InlineData(HeThong.Crm)]
    [InlineData(HeThong.Lms)]
    public void Moi_he_thong_co_it_nhat_mot_chuc_nang(HeThong heThong)
        => Assert.NotEmpty(ChucNang.ChucNangCua(heThong));

    /// <summary>
    /// Tổng ba hệ thống + dùng chung = đúng danh mục, không thiếu không lặp. Bắt được cả lỗi
    /// khai một chức năng vào hai hệ thống (Dictionary chặn sẵn) lẫn lỗi đếm lệch.
    /// </summary>
    [Fact]
    public void Ba_he_thong_cong_dung_chung_phu_kin_danh_muc()
    {
        var gom = Enum.GetValues<HeThong>()
            .SelectMany(ChucNang.ChucNangCua)
            .Concat(ChucNang.DungChung)
            .ToList();

        Assert.Equal(ChucNang.TatCa.Count, gom.Count);
        Assert.Equal(ChucNang.TatCa.OrderBy(x => x), gom.OrderBy(x => x));
    }
}
