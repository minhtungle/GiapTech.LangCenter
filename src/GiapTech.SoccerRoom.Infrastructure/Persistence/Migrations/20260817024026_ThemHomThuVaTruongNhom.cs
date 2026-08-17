using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemHomThuVaTruongNhom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "la_truong_nhom",
                table: "NGUOI_DUNG",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LOI_MOI_THAM_GIA",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tran_dau_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nguoi_gui_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loi_nhan = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    han_tra_loi = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    da_dong = table.Column<bool>(type: "boolean", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loi_moi_tham_gia", x => x.id);
                    table.ForeignKey(
                        name: "fk_loi_moi_tham_gia_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_loi_moi_tham_gia_tran_daus_tran_dau_id",
                        column: x => x.tran_dau_id,
                        principalTable: "TRAN_DAU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PHAN_HOI_THAM_GIA",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    loi_moi_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cau_thu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tra_loi = table.Column<int>(type: "integer", nullable: false),
                    ghi_chu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    thoi_gian_tra_loi = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_phan_hoi_tham_gia", x => x.id);
                    table.ForeignKey(
                        name: "fk_phan_hoi_tham_gia_cau_thu_cau_thu_id",
                        column: x => x.cau_thu_id,
                        principalTable: "CAU_THU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_phan_hoi_tham_gia_loi_moi_tham_gia_loi_moi_id",
                        column: x => x.loi_moi_id,
                        principalTable: "LOI_MOI_THAM_GIA",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_phan_hoi_tham_gia_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_loi_moi_tham_gia_tenant_id",
                table: "LOI_MOI_THAM_GIA",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_loi_moi_tham_gia_tran_dau_id",
                table: "LOI_MOI_THAM_GIA",
                column: "tran_dau_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_phan_hoi_tham_gia_cau_thu_id",
                table: "PHAN_HOI_THAM_GIA",
                column: "cau_thu_id");

            migrationBuilder.CreateIndex(
                name: "ix_phan_hoi_tham_gia_loi_moi_id_cau_thu_id",
                table: "PHAN_HOI_THAM_GIA",
                columns: new[] { "loi_moi_id", "cau_thu_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_phan_hoi_tham_gia_tenant_id",
                table: "PHAN_HOI_THAM_GIA",
                column: "tenant_id");

            // Bật cờ trưởng nhóm cho admin của các CLB ĐÃ TỒN TẠI.
            //
            // Seeder chỉ chạy khi tạo CLB mới, nên không có bước này thì mọi CLB đang chạy
            // không có ai gửi được lời mời đăng ký — tính năng hòm thư nằm chết cho tới khi
            // có người tự vào bật cờ, mà họ chưa biết cờ đó tồn tại.
            migrationBuilder.Sql("""
                UPDATE "NGUOI_DUNG" SET la_truong_nhom = true WHERE username = 'admin';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PHAN_HOI_THAM_GIA");

            migrationBuilder.DropTable(
                name: "LOI_MOI_THAM_GIA");

            migrationBuilder.DropColumn(
                name: "la_truong_nhom",
                table: "NGUOI_DUNG");
        }
    }
}
