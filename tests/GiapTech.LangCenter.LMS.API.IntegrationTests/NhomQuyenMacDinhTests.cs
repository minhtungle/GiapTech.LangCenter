using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Entities;
using GiapTech.LangCenter.LMS.Domain.Enums;
using GiapTech.LangCenter.LMS.Infrastructure.Persistence;
using GiapTech.LangCenter.LMS.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

/// <summary>
/// Bốn nhóm quyền dựng sẵn khi tạo trung tâm, và cơ chế bổ khuyết quyền cho trung tâm cũ.
///
/// Đây là chỗ dễ hỏng âm thầm nhất của hệ phân quyền động: sai ma trận thì giáo viên bị chặn
/// khỏi lớp mình dạy (hoặc tệ hơn, trợ giảng xoá được buổi học), mà không có lỗi nào hiện ra
/// — chỉ có 403 ở đúng một endpoint mà không ai thử.
/// </summary>
public class NhomQuyenMacDinhTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client()
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = "manager", MatKhau = "manager123" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    [Fact]
    public async Task Trung_tam_moi_co_du_bon_nhom_quyen()
    {
        var client = await Client();
        var quyens = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen");

        var ten = quyens!.Select(q => q.GetProperty("tenQuyen").GetString()).ToList();

        Assert.Contains("Quản trị viên", ten);
        Assert.Contains("Giáo viên", ten);
        Assert.Contains("Trợ giảng", ten);
        Assert.Contains("Học viên", ten);
    }

    /// <summary>
    /// Nhóm quản trị phải phủ MỌI chức năng × MỌI thao tác.
    ///
    /// Kiểm bằng `ChucNang.TatCa` chứ không bằng con số cứng: thêm module mới là danh mục dài
    /// ra, đếm cứng thì mỗi lần thêm lại phải sửa test — trong khi điều cần canh là "admin
    /// không bị bỏ sót chức năng nào".
    /// </summary>
    [Fact]
    public void Nhom_quan_tri_phu_moi_chuc_nang()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var nhom = db.Quyens.IgnoreQueryFilters()
            .Single(q => q.TenantId == factory.TenantAId
                         && q.TenQuyen == TenantSeeder.NhomQuyenQuanTri);

        var coTrongDb = db.QuyenChucNangs.IgnoreQueryFilters()
            .Where(qcn => qcn.QuyenId == nhom.Id)
            .Select(qcn => new { qcn.TenChucNang, qcn.HanhDong })
            .ToList()
            .Select(x => (x.TenChucNang, x.HanhDong))
            .ToHashSet();

        var thieu = (from cn in ChucNang.TatCa
                     from hd in Enum.GetValues<HanhDong>()
                     where !coTrongDb.Contains((cn, hd))
                     select $"{cn}.{hd}").ToList();

        Assert.True(thieu.Count == 0,
            "Nhóm quản trị thiếu quyền: " + string.Join(", ", thieu));
    }

    /// <summary>
    /// Ba khác biệt cốt lõi giữa Giáo viên và Trợ giảng, lấy thẳng từ ma trận đặc tả.
    ///
    /// Chính ba ô này là lý do `BaiTap` và `BaiKiemTra` phải là hai chức năng riêng — gộp lại
    /// thì không diễn đạt được "toàn quyền bài tập nhưng chỉ xem bài kiểm tra".
    /// </summary>
    [Theory]
    // Trợ giảng KHÔNG được xoá buổi học; giáo viên thì được.
    [InlineData("Giáo viên", ChucNang.BuoiHoc, HanhDong.Xoa, true)]
    [InlineData("Trợ giảng", ChucNang.BuoiHoc, HanhDong.Xoa, false)]
    // Trợ giảng KHÔNG ra đề kiểm tra; giáo viên thì được.
    [InlineData("Giáo viên", ChucNang.BaiKiemTra, HanhDong.Them, true)]
    [InlineData("Trợ giảng", ChucNang.BaiKiemTra, HanhDong.Them, false)]
    // Nhưng trợ giảng VẪN toàn quyền với bài tập.
    [InlineData("Trợ giảng", ChucNang.BaiTap, HanhDong.Xoa, true)]
    // Học viên chỉ đọc lớp, không sửa.
    [InlineData("Học viên", ChucNang.LopHoc, HanhDong.Sua, false)]
    // Học viên tự điểm danh được (endpoint tự lấy người dùng từ token, không nhận id).
    [InlineData("Học viên", ChucNang.DiemDanh, HanhDong.Them, true)]
    // KHÔNG nhóm nào ngoài quản trị được thấy mọi lớp của trung tâm.
    [InlineData("Giáo viên", ChucNang.LopHocToanTrungTam, HanhDong.Xem, false)]
    [InlineData("Trợ giảng", ChucNang.LopHocToanTrungTam, HanhDong.Xem, false)]
    [InlineData("Học viên", ChucNang.LopHocToanTrungTam, HanhDong.Xem, false)]
    public void Ma_tran_quyen_cua_tung_nhom_dung_dac_ta(
        string tenNhom, string chucNang, HanhDong hanhDong, bool mongDoi)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var nhom = db.Quyens.IgnoreQueryFilters()
            .Single(q => q.TenantId == factory.TenantAId && q.TenQuyen == tenNhom);

        var co = db.QuyenChucNangs.IgnoreQueryFilters()
            .Any(qcn => qcn.QuyenId == nhom.Id
                        && qcn.TenChucNang == chucNang
                        && qcn.HanhDong == hanhDong);

        Assert.Equal(mongDoi, co);
    }

    /// <summary>
    /// Bổ khuyết quyền: trung tâm tạo TRƯỚC khi thêm chức năng mới phải được cấp bù, nếu không
    /// admin của họ nhận 403 trên toàn bộ tính năng mới — âm thầm, và rất khó chẩn vì đăng
    /// nhập vẫn được và mọi màn cũ vẫn chạy.
    ///
    /// Mô phỏng bằng cách XOÁ vài hàng quyền rồi chạy lại bước bổ khuyết.
    /// </summary>
    [Fact]
    public async Task Bo_khuyet_cap_lai_quyen_con_thieu_cho_nhom_quan_tri()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var nhom = db.Quyens.IgnoreQueryFilters()
            .Single(q => q.TenantId == factory.TenantCId
                         && q.TenQuyen == TenantSeeder.NhomQuyenQuanTri);

        // Giả lập "trung tâm cũ chưa biết chức năng LopHoc".
        var thieu = db.QuyenChucNangs.IgnoreQueryFilters()
            .Where(q => q.QuyenId == nhom.Id && q.TenChucNang == ChucNang.LopHoc)
            .ToList();
        Assert.NotEmpty(thieu);

        db.QuyenChucNangs.RemoveRange(thieu);
        await db.SaveChangesAsync();

        Assert.False(db.QuyenChucNangs.IgnoreQueryFilters()
            .Any(q => q.QuyenId == nhom.Id && q.TenChucNang == ChucNang.LopHoc));

        await scope.ServiceProvider.GetRequiredService<BoKhuyetQuyenQuanTri>().ChayAsync();

        var sauKhiBu = db.QuyenChucNangs.IgnoreQueryFilters()
            .Count(q => q.QuyenId == nhom.Id && q.TenChucNang == ChucNang.LopHoc);
        Assert.Equal(Enum.GetValues<HanhDong>().Length, sauKhiBu);
    }

    /// <summary>
    /// Chiều ngược: bổ khuyết KHÔNG được đụng tới nhóm khác.
    ///
    /// Bổ khuyết chỉ nhắm nhóm "Quản trị viên". Nếu nó cấp bừa cho mọi nhóm thì trợ giảng bỗng
    /// dưng xoá được buổi học và học viên tự sửa điểm — lỗ hổng phân quyền nghiêm trọng, mà
    /// không ai để ý vì nó xảy ra lúc khởi động, im lặng.
    /// </summary>
    [Fact]
    public async Task Bo_khuyet_KHONG_dung_toi_nhom_khac()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var nhomTg = db.Quyens.IgnoreQueryFilters()
            .Single(q => q.TenantId == factory.TenantBId && q.TenQuyen == "Trợ giảng");

        var truoc = db.QuyenChucNangs.IgnoreQueryFilters().Count(q => q.QuyenId == nhomTg.Id);

        await scope.ServiceProvider.GetRequiredService<BoKhuyetQuyenQuanTri>().ChayAsync();

        var sau = db.QuyenChucNangs.IgnoreQueryFilters().Count(q => q.QuyenId == nhomTg.Id);

        Assert.Equal(truoc, sau);

        // Và trợ giảng vẫn KHÔNG xoá được buổi học, cũng không ra được đề kiểm tra.
        Assert.False(db.QuyenChucNangs.IgnoreQueryFilters()
            .Any(q => q.QuyenId == nhomTg.Id
                      && q.TenChucNang == ChucNang.BuoiHoc
                      && q.HanhDong == HanhDong.Xoa));

        Assert.False(db.QuyenChucNangs.IgnoreQueryFilters()
            .Any(q => q.QuyenId == nhomTg.Id
                      && q.TenChucNang == ChucNang.BaiKiemTra
                      && q.HanhDong == HanhDong.Them));
    }
}
