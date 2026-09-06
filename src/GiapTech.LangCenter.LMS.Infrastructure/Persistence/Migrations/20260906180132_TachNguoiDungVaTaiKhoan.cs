using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.LMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TachNguoiDungVaTaiKhoan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_nguoidung_quyen_nguoi_dung_nguoi_dung_id",
                table: "NGUOIDUNG_QUYEN");

            migrationBuilder.DropForeignKey(
                name: "fk_refresh_token_nguoi_dung_nguoi_dung_id",
                table: "REFRESH_TOKEN");

            migrationBuilder.DropForeignKey(
                name: "fk_token_datlai_matkhau_nguoi_dung_nguoi_dung_id",
                table: "TOKEN_DATLAI_MATKHAU");

            migrationBuilder.DropIndex(
                name: "ix_nguoi_dung_tenant_id_username",
                table: "NGUOI_DUNG");

            migrationBuilder.RenameColumn(
                name: "nguoi_dung_id",
                table: "TOKEN_DATLAI_MATKHAU",
                newName: "tai_khoan_id");

            migrationBuilder.RenameIndex(
                name: "ix_token_datlai_matkhau_nguoi_dung_id_da_dung_luc",
                table: "TOKEN_DATLAI_MATKHAU",
                newName: "ix_token_datlai_matkhau_tai_khoan_id_da_dung_luc");

            migrationBuilder.RenameColumn(
                name: "nguoi_dung_id",
                table: "REFRESH_TOKEN",
                newName: "tai_khoan_id");

            migrationBuilder.RenameIndex(
                name: "ix_refresh_token_nguoi_dung_id_thu_hoi_luc",
                table: "REFRESH_TOKEN",
                newName: "ix_refresh_token_tai_khoan_id_thu_hoi_luc");

            migrationBuilder.RenameColumn(
                name: "nguoi_dung_id",
                table: "NGUOIDUNG_QUYEN",
                newName: "tai_khoan_id");

            migrationBuilder.RenameIndex(
                name: "ix_nguoidung_quyen_nguoi_dung_id_quyen_id",
                table: "NGUOIDUNG_QUYEN",
                newName: "ix_nguoidung_quyen_tai_khoan_id_quyen_id");

            migrationBuilder.RenameColumn(
                name: "trang_thai",
                table: "NGUOI_DUNG",
                newName: "trang_thai_nhan_su");

            migrationBuilder.CreateTable(
                name: "HO_SO_GIAO_VIEN",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nguoi_dung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bang_cap = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    chuyen_mon = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ngay_vao_lam = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ho_so_giao_vien", x => x.id);
                    table.ForeignKey(
                        name: "fk_ho_so_giao_vien_nguoi_dungs_nguoi_dung_id",
                        column: x => x.nguoi_dung_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HO_SO_HOC_VIEN",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nguoi_dung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    truong_lop = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ten_phu_huynh = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    so_dien_thoai_phu_huynh = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ho_so_hoc_vien", x => x.id);
                    table.ForeignKey(
                        name: "fk_ho_so_hoc_vien_nguoi_dungs_nguoi_dung_id",
                        column: x => x.nguoi_dung_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HO_SO_NHAN_VIEN",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nguoi_dung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chuc_vu = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    phong_ban = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ho_so_nhan_vien", x => x.id);
                    table.ForeignKey(
                        name: "fk_ho_so_nhan_vien_nguoi_dungs_nguoi_dung_id",
                        column: x => x.nguoi_dung_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TAI_KHOAN",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    nguoi_dung_id = table.Column<Guid>(type: "uuid", nullable: true),
                    phai_doi_mat_khau = table.Column<bool>(type: "boolean", nullable: false),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tai_khoan", x => x.id);
                    table.ForeignKey(
                        name: "fk_tai_khoan_nguoi_dung_nguoi_dung_id",
                        column: x => x.nguoi_dung_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tai_khoan_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ho_so_giao_vien_nguoi_dung_id",
                table: "HO_SO_GIAO_VIEN",
                column: "nguoi_dung_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ho_so_giao_vien_tenant_id",
                table: "HO_SO_GIAO_VIEN",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ho_so_hoc_vien_nguoi_dung_id",
                table: "HO_SO_HOC_VIEN",
                column: "nguoi_dung_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ho_so_hoc_vien_tenant_id",
                table: "HO_SO_HOC_VIEN",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ho_so_nhan_vien_nguoi_dung_id",
                table: "HO_SO_NHAN_VIEN",
                column: "nguoi_dung_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ho_so_nhan_vien_tenant_id",
                table: "HO_SO_NHAN_VIEN",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tai_khoan_nguoi_dung_id",
                table: "TAI_KHOAN",
                column: "nguoi_dung_id",
                unique: true,
                filter: "nguoi_dung_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tai_khoan_tenant_id_username",
                table: "TAI_KHOAN",
                columns: new[] { "tenant_id", "username" },
                unique: true);

            // ════════════════════════════════════════════════════════════════════════════
            // CHUYỂN DỮ LIỆU — viết tay, KHÔNG được để EF tự sinh.
            //
            // Bản tự sinh xoá thẳng username/password_hash rồi tạo TAI_KHOAN rỗng: mọi tài
            // khoản đang tồn tại mất mật khẩu và không ai đăng nhập được nữa (quy tắc #1).
            //
            // Thứ tự bắt buộc: tạo bảng → chuyển dữ liệu → đổi khoá ngoại → xoá cột cũ.
            // ════════════════════════════════════════════════════════════════════════════

            // 2a. Mỗi NGUOI_DUNG hiện có sinh ra một TAI_KHOAN giữ nguyên username và HASH mật
            //     khẩu. Sinh id MỚI cho tài khoản — dùng lại id người sẽ khiến hai bảng có
            //     cùng id và mọi nhầm lẫn về sau đều im lặng trôi qua.
            migrationBuilder.Sql("""
                INSERT INTO "TAI_KHOAN"
                    (id, username, password_hash, nguoi_dung_id, phai_doi_mat_khau,
                     trang_thai, ngay_tao, ngay_cap_nhat, tenant_id)
                SELECT gen_random_uuid(), n.username, n.password_hash, n.id,
                       n.phai_doi_mat_khau,
                       -- trang_thai của NGUOI_DUNG đã bị RenameColumn thành trang_thai_nhan_su
                       -- ở trên. Hai enum trùng giá trị (0=HoatDong=DangLamViec,
                       -- 1=VoHieuHoa=DaNghi) nên chép thẳng là đúng nghĩa cả hai phía.
                       n.trang_thai_nhan_su,
                       n.ngay_tao, n.ngay_cap_nhat, n.tenant_id
                FROM "NGUOI_DUNG" n;
                """);

            // 2b. NGUOIDUNG_QUYEN / REFRESH_TOKEN / TOKEN_DATLAI_MATKHAU: cột đã được đổi TÊN
            //     thành tai_khoan_id nhưng GIÁ TRỊ vẫn là id người. Trỏ lại sang id tài khoản
            //     tương ứng — không làm bước này thì khoá ngoại ở 2c sẽ nổ, và tệ hơn: nếu
            //     tình cờ khớp thì quyền gán nhầm người.
            migrationBuilder.Sql("""
                UPDATE "NGUOIDUNG_QUYEN" nq
                SET tai_khoan_id = tk.id
                FROM "TAI_KHOAN" tk
                WHERE tk.nguoi_dung_id = nq.tai_khoan_id;
                """);

            migrationBuilder.Sql("""
                UPDATE "REFRESH_TOKEN" rt
                SET tai_khoan_id = tk.id
                FROM "TAI_KHOAN" tk
                WHERE tk.nguoi_dung_id = rt.tai_khoan_id;
                """);

            migrationBuilder.Sql("""
                UPDATE "TOKEN_DATLAI_MATKHAU" t
                SET tai_khoan_id = tk.id
                FROM "TAI_KHOAN" tk
                WHERE tk.nguoi_dung_id = t.tai_khoan_id;
                """);

            // 2c. Hàng mồ côi: token trỏ tới người dùng đã bị xoá trước đây. Không xoá thì
            //     AddForeignKey ngay dưới sẽ thất bại và cả migration rollback.
            migrationBuilder.Sql("""
                DELETE FROM "NGUOIDUNG_QUYEN"
                WHERE tai_khoan_id NOT IN (SELECT id FROM "TAI_KHOAN");
                """);
            migrationBuilder.Sql("""
                DELETE FROM "REFRESH_TOKEN"
                WHERE tai_khoan_id NOT IN (SELECT id FROM "TAI_KHOAN");
                """);
            migrationBuilder.Sql("""
                DELETE FROM "TOKEN_DATLAI_MATKHAU"
                WHERE tai_khoan_id NOT IN (SELECT id FROM "TAI_KHOAN");
                """);

            // 2d. Hồ sơ vai trò cho người đã có: tạo hàng RỖNG đúng theo loai_nguoi_dung hiện
            //     tại. Không tạo cả ba cho mỗi người — hai bảng thừa hàng rỗng sẽ làm mất khả
            //     năng phân biệt "chưa từng là giáo viên" với "là giáo viên nhưng chưa điền".
            //     Trợ giảng (2) dùng chung hồ sơ giáo viên.
            migrationBuilder.Sql("""
                INSERT INTO "HO_SO_GIAO_VIEN" (id, nguoi_dung_id, ngay_tao, tenant_id)
                SELECT gen_random_uuid(), n.id, now(), n.tenant_id
                FROM "NGUOI_DUNG" n WHERE n.loai_nguoi_dung IN (1, 2);
                """);
            migrationBuilder.Sql("""
                INSERT INTO "HO_SO_HOC_VIEN" (id, nguoi_dung_id, ngay_tao, tenant_id)
                SELECT gen_random_uuid(), n.id, now(), n.tenant_id
                FROM "NGUOI_DUNG" n WHERE n.loai_nguoi_dung = 3;
                """);
            migrationBuilder.Sql("""
                INSERT INTO "HO_SO_NHAN_VIEN" (id, nguoi_dung_id, ngay_tao, tenant_id)
                SELECT gen_random_uuid(), n.id, now(), n.tenant_id
                FROM "NGUOI_DUNG" n WHERE n.loai_nguoi_dung = 0;
                """);

            migrationBuilder.AddForeignKey(
                name: "fk_nguoidung_quyen_tai_khoans_tai_khoan_id",
                table: "NGUOIDUNG_QUYEN",
                column: "tai_khoan_id",
                principalTable: "TAI_KHOAN",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_refresh_token_tai_khoans_tai_khoan_id",
                table: "REFRESH_TOKEN",
                column: "tai_khoan_id",
                principalTable: "TAI_KHOAN",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_token_datlai_matkhau_tai_khoan_tai_khoan_id",
                table: "TOKEN_DATLAI_MATKHAU",
                column: "tai_khoan_id",
                principalTable: "TAI_KHOAN",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // Giờ mới an toàn: dữ liệu đã nằm trong TAI_KHOAN.
            migrationBuilder.DropColumn(name: "username", table: "NGUOI_DUNG");
            migrationBuilder.DropColumn(name: "password_hash", table: "NGUOI_DUNG");
            migrationBuilder.DropColumn(name: "phai_doi_mat_khau", table: "NGUOI_DUNG");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_nguoidung_quyen_tai_khoans_tai_khoan_id",
                table: "NGUOIDUNG_QUYEN");

            migrationBuilder.DropForeignKey(
                name: "fk_refresh_token_tai_khoans_tai_khoan_id",
                table: "REFRESH_TOKEN");

            migrationBuilder.DropForeignKey(
                name: "fk_token_datlai_matkhau_tai_khoan_tai_khoan_id",
                table: "TOKEN_DATLAI_MATKHAU");

            migrationBuilder.DropTable(
                name: "HO_SO_GIAO_VIEN");

            migrationBuilder.DropTable(
                name: "HO_SO_HOC_VIEN");

            migrationBuilder.DropTable(
                name: "HO_SO_NHAN_VIEN");

            migrationBuilder.DropTable(
                name: "TAI_KHOAN");

            migrationBuilder.RenameColumn(
                name: "tai_khoan_id",
                table: "TOKEN_DATLAI_MATKHAU",
                newName: "nguoi_dung_id");

            migrationBuilder.RenameIndex(
                name: "ix_token_datlai_matkhau_tai_khoan_id_da_dung_luc",
                table: "TOKEN_DATLAI_MATKHAU",
                newName: "ix_token_datlai_matkhau_nguoi_dung_id_da_dung_luc");

            migrationBuilder.RenameColumn(
                name: "tai_khoan_id",
                table: "REFRESH_TOKEN",
                newName: "nguoi_dung_id");

            migrationBuilder.RenameIndex(
                name: "ix_refresh_token_tai_khoan_id_thu_hoi_luc",
                table: "REFRESH_TOKEN",
                newName: "ix_refresh_token_nguoi_dung_id_thu_hoi_luc");

            migrationBuilder.RenameColumn(
                name: "tai_khoan_id",
                table: "NGUOIDUNG_QUYEN",
                newName: "nguoi_dung_id");

            migrationBuilder.RenameIndex(
                name: "ix_nguoidung_quyen_tai_khoan_id_quyen_id",
                table: "NGUOIDUNG_QUYEN",
                newName: "ix_nguoidung_quyen_nguoi_dung_id_quyen_id");

            migrationBuilder.RenameColumn(
                name: "trang_thai_nhan_su",
                table: "NGUOI_DUNG",
                newName: "trang_thai");

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "NGUOI_DUNG",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "phai_doi_mat_khau",
                table: "NGUOI_DUNG",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "username",
                table: "NGUOI_DUNG",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_nguoi_dung_tenant_id_username",
                table: "NGUOI_DUNG",
                columns: new[] { "tenant_id", "username" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_nguoidung_quyen_nguoi_dung_nguoi_dung_id",
                table: "NGUOIDUNG_QUYEN",
                column: "nguoi_dung_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_refresh_token_nguoi_dung_nguoi_dung_id",
                table: "REFRESH_TOKEN",
                column: "nguoi_dung_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_token_datlai_matkhau_nguoi_dung_nguoi_dung_id",
                table: "TOKEN_DATLAI_MATKHAU",
                column: "nguoi_dung_id",
                principalTable: "NGUOI_DUNG",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
