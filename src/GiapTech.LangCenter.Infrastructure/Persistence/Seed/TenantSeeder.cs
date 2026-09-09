using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Infrastructure.Persistence.Seed;

/// <summary>Khởi tạo trung tâm mới với admin mặc định và nhóm quyền đầy đủ.</summary>
public class TenantSeeder(AppDbContext db, IPasswordHasher hasher, ICurrentTenant currentTenant)
    : ITenantSeeder
{
    /// <summary>Tên nhóm quyền quản trị — dùng lại khi cần kiểm tra/khôi phục.</summary>
    public const string NhomQuyenQuanTri = NhomQuyenMacDinh.QuanTri;

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
        // (xem QuyenAuthorizationHandler). Vòng lặp `ChucNang.TatCa` chứ không liệt kê tay —
        // nhờ vậy module thêm sau này tự thuộc về admin của trung tâm mới.
        var quyenQuanTri = ThemNhomQuyen(
            tenant.Id, NhomQuyenMacDinh.QuanTri, "Toàn quyền trên mọi chức năng",
            ChucNang.TatCa.Select(cn => (cn, Enum.GetValues<HanhDong>())).ToArray());

        // Ba nhóm còn lại khai TƯỜNG MINH ma trận — xem NhomQuyenMacDinh.
        ThemNhomQuyen(tenant.Id, NhomQuyenMacDinh.GiaoVien,
            "Phụ trách trọn vẹn lớp được phân công", NhomQuyenMacDinh.CuaGiaoVien);

        ThemNhomQuyen(tenant.Id, NhomQuyenMacDinh.TroGiang,
            "Hỗ trợ giáo viên; không xoá buổi học, không ra đề kiểm tra",
            NhomQuyenMacDinh.CuaTroGiang);

        ThemNhomQuyen(tenant.Id, NhomQuyenMacDinh.HocVien,
            "Chỉ xem và nộp bài của chính mình", NhomQuyenMacDinh.CuaHocVien);

        // Hai bản ghi: CON NGƯỜI và TÀI KHOẢN của họ (tách từ 07/09/2026).
        var nguoiAdmin = new NguoiDung
        {
            TenantId = tenant.Id,
            HoTen = "Quản trị viên",
            LoaiNguoiDung = LoaiNguoiDung.NhanVien,
            TrangThaiNhanSu = TrangThaiNhanSu.DangLamViec
        };
        db.NguoiDungs.Add(nguoiAdmin);

        db.HoSoNhanViens.Add(new HoSoNhanVien
        {
            TenantId = tenant.Id,
            NguoiDungId = nguoiAdmin.Id,
            ChucVu = "Quản trị hệ thống"
        });

        var admin = new TaiKhoan
        {
            TenantId = tenant.Id,
            Username = "admin",
            PasswordHash = hasher.Bam(matKhauAdmin),
            NguoiDungId = nguoiAdmin.Id,
            // Mật khẩu mặc định ai cũng biết → bắt buộc đổi trước khi vào hệ thống (FR-01).
            PhaiDoiMatKhau = true,
            TrangThai = TrangThaiNguoiDung.HoatDong
        };
        db.TaiKhoans.Add(admin);

        db.NguoiDungQuyens.Add(new NguoiDungQuyen
        {
            TenantId = tenant.Id,
            TaiKhoanId = admin.Id,
            QuyenId = quyenQuanTri.Id
        });

        await db.SaveChangesAsync(ct);
        return tenant;
    }

    /// <summary>
    /// Thêm một nhóm quyền kèm ma trận (chức năng × thao tác) của nó.
    ///
    /// Chưa gọi SaveChanges — mọi bản ghi của seeder ghi trong MỘT transaction ở cuối, để
    /// trung tâm tạo dở dang không bao giờ tồn tại.
    /// </summary>
    private Quyen ThemNhomQuyen(
        Guid tenantId, string tenQuyen, string moTa,
        IReadOnlyCollection<(string ChucNang, HanhDong[] HanhDongs)> maTran)
    {
        var quyen = new Quyen { TenantId = tenantId, TenQuyen = tenQuyen, MoTa = moTa };
        db.Quyens.Add(quyen);

        foreach (var (chucNang, hanhDongs) in maTran)
        {
            foreach (var hanhDong in hanhDongs)
            {
                db.QuyenChucNangs.Add(new QuyenChucNang
                {
                    TenantId = tenantId,
                    QuyenId = quyen.Id,
                    TenChucNang = chucNang,
                    HanhDong = hanhDong
                });
            }
        }

        return quyen;
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
