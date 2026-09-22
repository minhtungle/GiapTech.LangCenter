using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Domain.Enums;
using GiapTech.LangCenter.Infrastructure.Persistence;
using GiapTech.LangCenter.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.API.IntegrationTests;

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
            new { MaTrungTam = factory.MaTrungTamA, Username = "manager", MatKhau = "manager123456" });
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

        // `ThaoTacCua` chứ không mọi giá trị enum (14/09/2026): từ khi ma trận thưa, quản trị
        // chỉ cần những ô THẬT SỰ có endpoint đọc. Đòi đủ 33 × 18 = 594 ô là đòi cấp cả
        // `NhatKyHeThong.Xoa` (nhật ký không xoá được) và `HocOnline.CauHinhTien` (vô nghĩa).
        var thieu = (from cn in ChucNang.TatCa
                     from hd in ChucNang.ThaoTacCua(cn)
                     where !coTrongDb.Contains((cn, hd))
                     select $"{cn}.{hd}").ToList();

        Assert.True(thieu.Count == 0,
            "Nhóm quản trị thiếu quyền: " + string.Join(", ", thieu));

        // CHIỀU NGƯỢC — không cấp ô nào NGOÀI bảng khai. Thiếu phần này thì test xanh cả khi
        // seeder quay lại cấp bừa mọi thao tác, và ma trận của admin lại đầy ô chết.
        var thua = coTrongDb
            .Where(x => !ChucNang.ThaoTacCua(x.TenChucNang).Contains(x.HanhDong))
            .Select(x => $"{x.TenChucNang}.{x.HanhDong}")
            .OrderBy(x => x)
            .ToList();

        Assert.True(thua.Count == 0,
            "Nhóm quản trị được cấp ô KHÔNG có trong bảng khai (ô chết): "
            + string.Join(", ", thua));
    }

    /// <summary>
    /// Ba khác biệt cốt lõi giữa Giáo viên và Trợ giảng, lấy thẳng từ ma trận đặc tả.
    ///
    /// Chính ba ô này là lý do `BaiTap` và `BaiKiemTra` phải là hai chức năng riêng — gộp lại
    /// thì không diễn đạt được "toàn quyền bài tập nhưng chỉ xem bài kiểm tra".
    /// </summary>
    [Theory]
    // Trợ giảng KHÔNG huỷ buổi học; giáo viên thì được. (`Huy` thay cho `Xoa`: huỷ giữ bản ghi,
    // xoá là mất hẳn — hai việc khác nhau, tách 14/09/2026.)
    [InlineData("Giáo viên", ChucNang.BuoiHoc, HanhDong.Huy, true)]
    [InlineData("Trợ giảng", ChucNang.BuoiHoc, HanhDong.Huy, false)]
    // Trợ giảng KHÔNG soạn khoá trực tuyến; giáo viên thì được — soạn nội dung là chuyên môn.
    // (Thay cho cặp `BaiKiemTra.Them` cũ: từ 14/09/2026 `BaiKiemTra` khai RỖNG trong
    // `ThaoTacTheoChucNang` vì chưa có API nào — nợ N1 — nên không nhóm nào được cấp nó nữa.
    // Giữ ô đó lại là để một hàng `QUYEN_CHUC_NANG` trong DB mà ma trận không hiện, người
    // quản trị không thấy cũng không bỏ được. Canh bởi
    // `MaTranQuyenPhaiKhopThucTeTests.Nhom_quyen_mac_dinh_khong_cap_o_khong_hien...`.
    // Khi FR bài kiểm tra có API thật thì khai vào bảng trước, rồi cấp lại cho giáo viên.)
    [InlineData("Giáo viên", ChucNang.KhoaOnline, HanhDong.Them, true)]
    [InlineData("Trợ giảng", ChucNang.KhoaOnline, HanhDong.Them, false)]
    // Nhưng trợ giảng VẪN toàn quyền với bài tập.
    [InlineData("Trợ giảng", ChucNang.BaiTap, HanhDong.Xoa, true)]
    // Học viên chỉ đọc lớp, không sửa.
    [InlineData("Học viên", ChucNang.LopHoc, HanhDong.Sua, false)]
    // Học viên tự điểm danh được — `TuLam` (endpoint lấy người dùng từ token, không nhận id).
    // Thay cho `Them` cũ: `Them` là quyền VAY MƯỢN vốn gác cả việc gửi nhận xét buổi học.
    [InlineData("Học viên", ChucNang.DiemDanh, HanhDong.TuLam, true)]
    // ...nhưng KHÔNG ghi điểm danh cả lớp, và KHÔNG chốt buổi.
    [InlineData("Học viên", ChucNang.DiemDanh, HanhDong.Sua, false)]
    [InlineData("Học viên", ChucNang.DiemDanh, HanhDong.Chot, false)]
    // Trợ giảng ghi điểm danh được nhưng KHÔNG chốt buổi — chốt là quyết định của GV chính.
    [InlineData("Trợ giảng", ChucNang.DiemDanh, HanhDong.Sua, true)]
    [InlineData("Trợ giảng", ChucNang.DiemDanh, HanhDong.Chot, false)]
    [InlineData("Giáo viên", ChucNang.DiemDanh, HanhDong.Chot, true)]
    // Giáo viên thêm/gỡ học viên lớp mình, nhưng KHÔNG sửa thông tin lớp và KHÔNG huỷ lớp.
    [InlineData("Giáo viên", ChucNang.GhiDanhLop, HanhDong.Them, true)]
    [InlineData("Giáo viên", ChucNang.LopHoc, HanhDong.Sua, false)]
    [InlineData("Giáo viên", ChucNang.LopHoc, HanhDong.Huy, false)]
    // Duyệt đơn xếp lớp là việc điều phối — không nhóm dựng sẵn nào ngoài quản trị có.
    [InlineData("Giáo viên", ChucNang.XepLop, HanhDong.Duyet, false)]
    [InlineData("Trợ giảng", ChucNang.XepLop, HanhDong.Duyet, false)]
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
        // Số thao tác KHAI cho `LopHoc`, không phải mọi giá trị enum: bổ khuyết chỉ cấp ô
        // thật sự có endpoint đọc (14/09/2026). `LopHoc` có 7 thao tác — Xem/Them/Sua/Xoa +
        // HoanTat/Huy/SinhLich — trong khi enum nay có 18 giá trị.
        Assert.Equal(ChucNang.ThaoTacCua(ChucNang.LopHoc).Count, sauKhiBu);
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
                      && q.TenChucNang == ChucNang.KhoaOnline
                      && q.HanhDong == HanhDong.Them));
    }
}
