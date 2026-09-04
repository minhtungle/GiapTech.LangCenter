using GiapTech.LangCenter.LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GiapTech.LangCenter.LMS.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("REFRESH_TOKEN");
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();

        // Tra cứu lúc làm mới token đi thẳng từ hash → phải là index duy nhất.
        b.HasIndex(x => x.TokenHash).IsUnique();

        // Thu hồi toàn bộ phiên của một người dùng (đổi mật khẩu, vô hiệu hoá tài khoản).
        b.HasIndex(x => new { x.NguoiDungId, x.ThuHoiLuc });

        b.HasOne(x => x.NguoiDung).WithMany()
            .HasForeignKey(x => x.NguoiDungId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TokenDatLaiMatKhauConfig : IEntityTypeConfiguration<TokenDatLaiMatKhau>
{
    public void Configure(EntityTypeBuilder<TokenDatLaiMatKhau> b)
    {
        b.ToTable("TOKEN_DATLAI_MATKHAU");
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.NguoiDungId, x.DaDungLuc });

        b.HasOne(x => x.NguoiDung).WithMany()
            .HasForeignKey(x => x.NguoiDungId).OnDelete(DeleteBehavior.Cascade);
    }
}
