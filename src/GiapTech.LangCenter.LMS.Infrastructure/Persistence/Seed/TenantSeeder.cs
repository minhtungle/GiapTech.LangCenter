using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Entities;
using GiapTech.LangCenter.LMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Infrastructure.Persistence.Seed;

/// <summary>Khởi tạo trung tâm mới với admin mặc định và nhóm quyền đầy đủ.</summary>
public class TenantSeeder(AppDbContext db, IPasswordHasher hasher, ICurrentTenant currentTenant)
    : ITenantSeeder
{
    /// <summary>Tên nhóm quyền quản trị — dùng lại khi cần kiểm tra/khôi phục.</summary>
    public const string NhomQuyenQuanTri = "Quản trị viên";

    /// <summary>Số lần thử sinh mã trước khi bỏ cuộc — xem <see cref="SinhMaChuaDungAsync"/>.</summary>
    private const int SoLanThuSinhMa = 10;

    public async Task<Tenant> TaoTenantMoiAsync(
        string tenTrungTam, string matKhauAdmin = "123456", CancellationToken ct = default)
    {
        var maTrungTam = await SinhMaChuaDungAsync(ct);

        var tenant = new Tenant { MaTrungTam = maTrungTam, TenTrungTam = tenTrungTam };
        db.Tenants.Add(tenant);

        // SaveChanges tự gán tenant_id theo ICurrentTenant, nhưng lúc tạo trung tâm mới thì
        // context chưa có tenant nào. Đặt phạm vi tường minh để các bản ghi bên dưới
        // nhận đúng tenant vừa tạo.
        using var _ = currentTenant.DatPhamVi(tenant.Id);

        // Nhóm quyền đầy đủ: admin có toàn quyền QUA DỮ LIỆU, không qua ngoại lệ trong code
        // (xem QuyenAuthorizationHandler).
        var quyenQuanTri = new Quyen
        {
            TenantId = tenant.Id,
            TenQuyen = NhomQuyenQuanTri,
            MoTa = "Toàn quyền trên mọi chức năng"
        };
        db.Quyens.Add(quyenQuanTri);

        foreach (var chucNang in ChucNang.TatCa)
        {
            foreach (var hanhDong in Enum.GetValues<HanhDong>())
            {
                db.QuyenChucNangs.Add(new QuyenChucNang
                {
                    TenantId = tenant.Id,
                    QuyenId = quyenQuanTri.Id,
                    TenChucNang = chucNang,
                    HanhDong = hanhDong
                });
            }
        }

        var admin = new NguoiDung
        {
            TenantId = tenant.Id,
            Username = "admin",
            PasswordHash = hasher.Bam(matKhauAdmin),
            // Mật khẩu mặc định ai cũng biết → bắt buộc đổi trước khi vào hệ thống (FR-01).
            PhaiDoiMatKhau = true,
            TrangThai = TrangThaiNguoiDung.HoatDong
        };
        db.NguoiDungs.Add(admin);

        db.NguoiDungQuyens.Add(new NguoiDungQuyen
        {
            TenantId = tenant.Id,
            NguoiDungId = admin.Id,
            QuyenId = quyenQuanTri.Id
        });

        await db.SaveChangesAsync(ct);
        return tenant;
    }

    /// <summary>
    /// Sinh mã 7 ký tự chưa ai dùng.
    ///
    /// Với 31^7 ≈ 27 tỷ tổ hợp, xác suất trùng ở quy mô này gần như bằng 0, nhưng vẫn kiểm
    /// tra và thử lại — không dựa vào may mắn cho một ràng buộc UNIQUE. Nếu 10 lần đều trùng
    /// thì gần như chắc chắn có gì đó sai (DB hỏng, RNG kẹt) chứ không phải xui, nên ném lỗi
    /// thay vì lặp vô hạn.
    /// </summary>
    private async Task<string> SinhMaChuaDungAsync(CancellationToken ct)
    {
        for (var i = 0; i < SoLanThuSinhMa; i++)
        {
            var ma = MaTrungTam.Sinh();

            var daDung = await db.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(t => t.MaTrungTam == ma, ct);

            if (!daDung) return ma;
        }

        throw new InvalidOperationException(
            $"Không sinh được mã trung tâm chưa dùng sau {SoLanThuSinhMa} lần thử.");
    }
}
