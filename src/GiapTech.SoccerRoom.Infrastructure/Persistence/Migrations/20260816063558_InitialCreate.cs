using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Migrations
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
                    ma_doi = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ten_doi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ten_viet_tat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ngay_thanh_lap = table.Column<DateOnly>(type: "date", nullable: true),
                    logo_url = table.Column<string>(type: "text", nullable: true),
                    anh_bia_url = table.Column<string>(type: "text", nullable: true),
                    mo_ta = table.Column<string>(type: "text", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "CAU_THU",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ho_ten = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    anh_dai_dien = table.Column<string>(type: "text", nullable: true),
                    ngay_sinh = table.Column<DateOnly>(type: "date", nullable: true),
                    ngay_tham_gia = table.Column<DateOnly>(type: "date", nullable: true),
                    ghi_chu = table.Column<string>(type: "text", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cau_thu", x => x.id);
                    table.ForeignKey(
                        name: "fk_cau_thu_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DOI_THU",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten_doi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    lien_he = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ghi_chu = table.Column<string>(type: "text", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doi_thu", x => x.id);
                    table.ForeignKey(
                        name: "fk_doi_thu_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QUY",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ten_quy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    thoi_han = table.Column<DateOnly>(type: "date", nullable: true),
                    ghi_chu = table.Column<string>(type: "text", nullable: true),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quy", x => x.id);
                    table.ForeignKey(
                        name: "fk_quy_tenants_tenant_id",
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
                    cau_thu_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nguoi_dung", x => x.id);
                    table.ForeignKey(
                        name: "fk_nguoi_dung_cau_thu_cau_thu_id",
                        column: x => x.cau_thu_id,
                        principalTable: "CAU_THU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_nguoi_dung_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LOI_MOI_DOI_THU",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doi_thu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    thoi_gian_de_xuat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    ghi_chu = table.Column<string>(type: "text", nullable: true),
                    tran_dau_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loi_moi_doi_thu", x => x.id);
                    table.ForeignKey(
                        name: "fk_loi_moi_doi_thu_doi_thu_doi_thu_id",
                        column: x => x.doi_thu_id,
                        principalTable: "DOI_THU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_loi_moi_doi_thu_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRAN_DAU",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    thoi_gian = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    doi_thu_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ty_so_nha = table.Column<int>(type: "integer", nullable: true),
                    ty_so_khach = table.Column<int>(type: "integer", nullable: true),
                    ket_qua = table.Column<int>(type: "integer", nullable: false),
                    trang_thai = table.Column<int>(type: "integer", nullable: false),
                    link_video = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    nhan_xet_chung = table.Column<string>(type: "text", nullable: true),
                    ghi_chu = table.Column<string>(type: "text", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tran_dau", x => x.id);
                    table.ForeignKey(
                        name: "fk_tran_dau_doi_thu_doi_thu_id",
                        column: x => x.doi_thu_id,
                        principalTable: "DOI_THU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tran_dau_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DONGGOP_QUY",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cau_thu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    so_tien_can_dong = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    so_tien_da_dong = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ngay_dong = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ghi_chu = table.Column<string>(type: "text", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_donggop_quy", x => x.id);
                    table.ForeignKey(
                        name: "fk_donggop_quy_cau_thu_cau_thu_id",
                        column: x => x.cau_thu_id,
                        principalTable: "CAU_THU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_donggop_quy_quys_quy_id",
                        column: x => x.quy_id,
                        principalTable: "QUY",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_donggop_quy_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
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
                name: "DANHGIA_CAUTHU",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tran_dau_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cau_thu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    so_ban_ghi_duoc = table.Column<int>(type: "integer", nullable: false),
                    so_ban_cuu_thua = table.Column<int>(type: "integer", nullable: false),
                    chi_so_ky_nang = table.Column<string>(type: "jsonb", nullable: true),
                    ghi_chu = table.Column<string>(type: "text", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_danhgia_cauthu", x => x.id);
                    table.ForeignKey(
                        name: "fk_danhgia_cauthu_cau_thu_cau_thu_id",
                        column: x => x.cau_thu_id,
                        principalTable: "CAU_THU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_danhgia_cauthu_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_danhgia_cauthu_tran_daus_tran_dau_id",
                        column: x => x.tran_dau_id,
                        principalTable: "TRAN_DAU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DOIHINH_TRANDAU",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tran_dau_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cau_thu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vi_tri = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    la_du_bi = table.Column<bool>(type: "boolean", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doihinh_trandau", x => x.id);
                    table.ForeignKey(
                        name: "fk_doihinh_trandau_cau_thu_cau_thu_id",
                        column: x => x.cau_thu_id,
                        principalTable: "CAU_THU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_doihinh_trandau_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_doihinh_trandau_tran_daus_tran_dau_id",
                        column: x => x.tran_dau_id,
                        principalTable: "TRAN_DAU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SODO_CHIENTHUAT",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tran_dau_id = table.Column<Guid>(type: "uuid", nullable: false),
                    so_do_json = table.Column<string>(type: "jsonb", nullable: false),
                    ghi_chu_chien_thuat = table.Column<string>(type: "text", nullable: true),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sodo_chienthuat", x => x.id);
                    table.ForeignKey(
                        name: "fk_sodo_chienthuat_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sodo_chienthuat_tran_daus_tran_dau_id",
                        column: x => x.tran_dau_id,
                        principalTable: "TRAN_DAU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VOTE_MVP",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tran_dau_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nguoi_vote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cau_thu_duoc_vote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ngay_tao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ngay_cap_nhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vote_mvp", x => x.id);
                    table.ForeignKey(
                        name: "fk_vote_mvp_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "TENANT",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vote_mvp_tran_dau_tran_dau_id",
                        column: x => x.tran_dau_id,
                        principalTable: "TRAN_DAU",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cau_thu_tenant_id",
                table: "CAU_THU",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_danhgia_cauthu_cau_thu_id",
                table: "DANHGIA_CAUTHU",
                column: "cau_thu_id");

            migrationBuilder.CreateIndex(
                name: "ix_danhgia_cauthu_tenant_id",
                table: "DANHGIA_CAUTHU",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_danhgia_cauthu_tran_dau_id_cau_thu_id",
                table: "DANHGIA_CAUTHU",
                columns: new[] { "tran_dau_id", "cau_thu_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_doi_thu_tenant_id",
                table: "DOI_THU",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_doihinh_trandau_cau_thu_id",
                table: "DOIHINH_TRANDAU",
                column: "cau_thu_id");

            migrationBuilder.CreateIndex(
                name: "ix_doihinh_trandau_tenant_id",
                table: "DOIHINH_TRANDAU",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_doihinh_trandau_tran_dau_id_cau_thu_id",
                table: "DOIHINH_TRANDAU",
                columns: new[] { "tran_dau_id", "cau_thu_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_donggop_quy_cau_thu_id",
                table: "DONGGOP_QUY",
                column: "cau_thu_id");

            migrationBuilder.CreateIndex(
                name: "ix_donggop_quy_quy_id_cau_thu_id",
                table: "DONGGOP_QUY",
                columns: new[] { "quy_id", "cau_thu_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_donggop_quy_tenant_id",
                table: "DONGGOP_QUY",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_loi_moi_doi_thu_doi_thu_id",
                table: "LOI_MOI_DOI_THU",
                column: "doi_thu_id");

            migrationBuilder.CreateIndex(
                name: "ix_loi_moi_doi_thu_tenant_id_trang_thai",
                table: "LOI_MOI_DOI_THU",
                columns: new[] { "tenant_id", "trang_thai" });

            migrationBuilder.CreateIndex(
                name: "ix_nguoi_dung_cau_thu_id",
                table: "NGUOI_DUNG",
                column: "cau_thu_id");

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
                name: "ix_quy_tenant_id_trang_thai",
                table: "QUY",
                columns: new[] { "tenant_id", "trang_thai" });

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
                name: "ix_sodo_chienthuat_tenant_id",
                table: "SODO_CHIENTHUAT",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sodo_chienthuat_tran_dau_id",
                table: "SODO_CHIENTHUAT",
                column: "tran_dau_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_ma_doi",
                table: "TENANT",
                column: "ma_doi",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tran_dau_doi_thu_id",
                table: "TRAN_DAU",
                column: "doi_thu_id");

            migrationBuilder.CreateIndex(
                name: "ix_tran_dau_tenant_id_thoi_gian",
                table: "TRAN_DAU",
                columns: new[] { "tenant_id", "thoi_gian" });

            migrationBuilder.CreateIndex(
                name: "ix_vote_mvp_tenant_id_cau_thu_duoc_vote_id",
                table: "VOTE_MVP",
                columns: new[] { "tenant_id", "cau_thu_duoc_vote_id" });

            migrationBuilder.CreateIndex(
                name: "UQ_VOTE_MVP_tran_dau_nguoi_vote",
                table: "VOTE_MVP",
                columns: new[] { "tran_dau_id", "nguoi_vote_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DANHGIA_CAUTHU");

            migrationBuilder.DropTable(
                name: "DOIHINH_TRANDAU");

            migrationBuilder.DropTable(
                name: "DONGGOP_QUY");

            migrationBuilder.DropTable(
                name: "LOI_MOI_DOI_THU");

            migrationBuilder.DropTable(
                name: "NGUOIDUNG_QUYEN");

            migrationBuilder.DropTable(
                name: "QUYEN_CHUC_NANG");

            migrationBuilder.DropTable(
                name: "SODO_CHIENTHUAT");

            migrationBuilder.DropTable(
                name: "VOTE_MVP");

            migrationBuilder.DropTable(
                name: "QUY");

            migrationBuilder.DropTable(
                name: "NGUOI_DUNG");

            migrationBuilder.DropTable(
                name: "QUYEN");

            migrationBuilder.DropTable(
                name: "TRAN_DAU");

            migrationBuilder.DropTable(
                name: "CAU_THU");

            migrationBuilder.DropTable(
                name: "DOI_THU");

            migrationBuilder.DropTable(
                name: "TENANT");
        }
    }
}
