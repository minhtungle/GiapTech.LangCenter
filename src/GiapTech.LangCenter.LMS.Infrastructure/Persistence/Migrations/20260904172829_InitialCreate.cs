using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.LMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TENANT",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ma_trung_tam = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false),
                    ten_trung_tam = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ten_viet_tat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    logo_url = table.Column<string>(type: "text", nullable: true),
                    anh_bia_url = table.Column<string>(type: "text", nullable: true),
                    mo_ta = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    dia_chi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    lien_he = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    so_tai_khoan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ten_ngan_hang = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    chu_tai_khoan = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    anh_qr_url = table.Column<string>(type: "text", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "NGUOI_DUNG",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    so_dien_thoai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    dia_chi = table.Column<string>(type: "text", nullable: true),
                    phai_doi_mat_khau = table.Column<bool>(type: "boolean", nullable: false),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nguoi_dung", x => x.id);
                    table.ForeignKey(
                        name: "fk_nguoi_dung_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QUYEN",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten_quyen = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mo_ta = table.Column<string>(type: "text", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quyen", x => x.id);
                    table.ForeignKey(
                        name: "fk_quyen_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "REFRESH_TOKEN",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nguoi_dung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    het_han = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    thu_hoi_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_token_nguoi_dung_nguoi_dung_id",
                        column: x => x.nguoi_dung_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TOKEN_DATLAI_MATKHAU",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nguoi_dung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    het_han = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    da_dung_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_token_datlai_matkhau", x => x.id);
                    table.ForeignKey(
                        name: "fk_token_datlai_matkhau_nguoi_dung_nguoi_dung_id",
                        column: x => x.nguoi_dung_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NGUOIDUNG_QUYEN",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nguoi_dung_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quyen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nguoidung_quyen", x => x.id);
                    table.ForeignKey(
                        name: "fk_nguoidung_quyen_nguoi_dung_nguoi_dung_id",
                        column: x => x.nguoi_dung_id,
                        principalTable: "NGUOI_DUNG",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_nguoidung_quyen_quyens_quyen_id",
                        column: x => x.quyen_id,
                        principalTable: "QUYEN",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QUYEN_CHUC_NANG",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quyen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten_chuc_nang = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    hanh_dong = table.Column<int>(type: "integer", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quyen_chuc_nang", x => x.id);
                    table.ForeignKey(
                        name: "fk_quyen_chuc_nang_quyens_quyen_id",
                        column: x => x.quyen_id,
                        principalTable: "QUYEN",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_nguoi_dung_tenant_id_username",
                table: "NGUOI_DUNG",
                columns: new[] { "tenant_id", "username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nguoidung_quyen_nguoi_dung_id_quyen_id",
                table: "NGUOIDUNG_QUYEN",
                columns: new[] { "nguoi_dung_id", "quyen_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nguoidung_quyen_quyen_id",
                table: "NGUOIDUNG_QUYEN",
                column: "quyen_id");

            migrationBuilder.CreateIndex(
                name: "ix_nguoidung_quyen_tenant_id",
                table: "NGUOIDUNG_QUYEN",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_quyen_tenant_id_ten_quyen",
                table: "QUYEN",
                columns: new[] { "tenant_id", "ten_quyen" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quyen_chuc_nang_quyen_id_ten_chuc_nang_hanh_dong",
                table: "QUYEN_CHUC_NANG",
                columns: new[] { "quyen_id", "ten_chuc_nang", "hanh_dong" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quyen_chuc_nang_tenant_id",
                table: "QUYEN_CHUC_NANG",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_nguoi_dung_id_thu_hoi_luc",
                table: "REFRESH_TOKEN",
                columns: new[] { "nguoi_dung_id", "thu_hoi_luc" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_token_hash",
                table: "REFRESH_TOKEN",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_ma_trung_tam",
                table: "TENANT",
                column: "ma_trung_tam",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_token_datlai_matkhau_nguoi_dung_id_da_dung_luc",
                table: "TOKEN_DATLAI_MATKHAU",
                columns: new[] { "nguoi_dung_id", "da_dung_luc" });

            migrationBuilder.CreateIndex(
                name: "ix_token_datlai_matkhau_token_hash",
                table: "TOKEN_DATLAI_MATKHAU",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NGUOIDUNG_QUYEN");

            migrationBuilder.DropTable(
                name: "QUYEN_CHUC_NANG");

            migrationBuilder.DropTable(
                name: "REFRESH_TOKEN");

            migrationBuilder.DropTable(
                name: "TOKEN_DATLAI_MATKHAU");

            migrationBuilder.DropTable(
                name: "QUYEN");

            migrationBuilder.DropTable(
                name: "NGUOI_DUNG");

            migrationBuilder.DropTable(
                name: "TENANT");
        }
    }
}
