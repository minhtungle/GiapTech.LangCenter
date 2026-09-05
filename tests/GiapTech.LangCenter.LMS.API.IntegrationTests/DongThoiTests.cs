using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

/// <summary>
/// Ràng buộc "chỉ một" phải chặn ở TẦNG DB, không chỉ ở tầng ứng dụng.
///
/// Bài học từ rà soát 20/08: ràng buộc kiểu này rất dễ được kiểm bằng `AnyAsync` rồi `Add`.
/// Hai request song song đều thấy "chưa có" và đều ghi — 5 request đồng thời cho ra 5 bản ghi.
///
/// **Bộ test này KHÔNG mô phỏng được đua thật** — provider InMemory không có UNIQUE index và
/// không chạy song song ở tầng DB. Nó canh phần kiểm được: ràng buộc **được khai** trong model,
/// và tầng ứng dụng trả mã lỗi đúng cho request thứ hai.
/// </summary>
public class DongThoiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>
    /// Mọi ràng buộc "chỉ một" của tầng hệ thống phải là UNIQUE INDEX trong model, không phải
    /// chỉ `if` trong handler.
    ///
    /// Test này là thứ CI bắt được khi ai đó thêm một ràng buộc kiểu này mà quên tầng DB.
    /// Thêm entity nghiệp vụ mới có ràng buộc "chỉ một" → thêm một dòng InlineData ở đây.
    /// </summary>
    [Theory]
    // Mã trung tâm là một nửa bộ ba đăng nhập — trùng mã là hai trung tâm cùng cửa vào.
    [InlineData("Tenant", new[] { "MaTrungTam" })]
    // Username chỉ duy nhất TRONG tenant, không phải toàn cục (quy tắc multi-tenant).
    [InlineData("NguoiDung", new[] { "TenantId", "Username" })]
    [InlineData("Quyen", new[] { "TenantId", "TenQuyen" })]
    // Không lặp cùng một (nhóm quyền, chức năng, thao tác).
    [InlineData("QuyenChucNang", new[] { "QuyenId", "TenChucNang", "HanhDong" })]
    [InlineData("NguoiDungQuyen", new[] { "NguoiDungId", "QuyenId" })]
    // Tra cứu lúc làm mới token đi thẳng từ hash → trùng hash là nhầm phiên của người khác.
    [InlineData("RefreshToken", new[] { "TokenHash" })]
    [InlineData("TokenDatLaiMatKhau", new[] { "TokenHash" })]
    public void Rang_buoc_chi_mot_phai_co_UNIQUE_o_tang_DB(string tenEntity, string[] cot)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.AppDbContext>();

        var entity = db.Model.GetEntityTypes()
            .Single(e => e.ClrType.Name == tenEntity);

        var index = entity.GetIndexes().FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(cot));

        Assert.True(index is not null,
            $"{tenEntity} thiếu index trên ({string.Join(", ", cot)}) — "
            + "không thì hai request song song đều ghi được. "
            + "Kiểm ở tầng ứng dụng (`AnyAsync` rồi `Add`) KHÔNG đủ.");

        Assert.True(index!.IsUnique,
            $"Index trên {tenEntity}({string.Join(", ", cot)}) phải UNIQUE.");
    }

    /// <summary>
    /// Username duy nhất theo TENANT, không phải toàn cục.
    ///
    /// Kiểm cả chiều ngược: nếu ai đó "sửa" thành UNIQUE(username) thì hai trung tâm không
    /// cùng có tài khoản "admin" được nữa — chính lý do dự án không dùng cả Identity stack.
    /// </summary>
    [Fact]
    public void UNIQUE_username_phai_gom_ca_tenant_id()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.AppDbContext>();

        var nguoiDung = db.Model.GetEntityTypes().Single(e => e.ClrType.Name == "NguoiDung");

        var chiUsername = nguoiDung.GetIndexes().Any(i =>
            i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == "Username");

        Assert.False(chiUsername,
            "UNIQUE(username) toàn cục sẽ chặn hai trung tâm cùng có tài khoản 'admin'. "
            + "Phải là UNIQUE(tenant_id, username).");
    }

    [Fact]
    public async Task Vi_pham_rang_buoc_tra_400_khong_phai_500()
    {
        // Nếu ràng buộc chặn mà middleware không xử lý, người dùng nhận 500 "Lỗi hệ thống" và
        // tưởng app hỏng — trong khi thực ra họ bấm hai lần và lần thứ hai bị chặn ĐÚNG.
        var client = await Client();

        var than = new
        {
            Username = "trungusername",
            MatKhau = "matkhau123", HoTen = "Test trungusername",
            Email = (string?)null,
            SoDienThoai = (string?)null,
            DiaChi = (string?)null,
            QuyenIds = Array.Empty<Guid>(),
            PhaiDoiMatKhau = false
        };

        var lan1 = await client.PostAsJsonAsync("/api/v1/tai-khoan", than);
        lan1.EnsureSuccessStatusCode();

        var lan2 = await client.PostAsJsonAsync("/api/v1/tai-khoan", than);

        // Tầng ứng dụng bắt trước → 400 với mã nghiệp vụ. UNIQUE ở DB là lưới cuối cho đua.
        Assert.Equal(HttpStatusCode.BadRequest, lan2.StatusCode);
        Assert.Equal("USERNAME_DA_TON_TAI",
            (await lan2.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Cùng một username tạo được ở HAI trung tâm khác nhau — chiều ngược của test trên.
    ///
    /// Không có test này thì ai đó "sửa" lỗi trùng username bằng cách bỏ tenant_id khỏi UNIQUE
    /// mà bộ test vẫn xanh.
    /// </summary>
    [Fact]
    public async Task Cung_username_tao_duoc_o_hai_trung_tam()
    {
        var a = await Client(factory.MaTrungTamA);
        var b = await Client(factory.MaTrungTamB);

        var than = new
        {
            Username = "trunggiuahaitenant",
            MatKhau = "matkhau123", HoTen = "Test trunggiuahaitenant",
            Email = (string?)null,
            SoDienThoai = (string?)null,
            DiaChi = (string?)null,
            QuyenIds = Array.Empty<Guid>(),
            PhaiDoiMatKhau = false
        };

        (await a.PostAsJsonAsync("/api/v1/tai-khoan", than)).EnsureSuccessStatusCode();
        (await b.PostAsJsonAsync("/api/v1/tai-khoan", than)).EnsureSuccessStatusCode();
    }

    private async Task<HttpClient> Client(string? maTrungTam = null)
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap", new
        {
            MaTrungTam = maTrungTam ?? factory.MaTrungTamA,
            Username = "manager",
            MatKhau = "manager123"
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }
}
