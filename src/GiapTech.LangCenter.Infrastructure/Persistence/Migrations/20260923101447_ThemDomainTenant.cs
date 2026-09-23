using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiapTech.LangCenter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThemDomainTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "domain_landing",
                table: "TENANT",
                type: "character varying(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "domain_quan_tri",
                table: "TENANT",
                type: "character varying(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_domain_landing",
                table: "TENANT",
                column: "domain_landing",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_domain_quan_tri",
                table: "TENANT",
                column: "domain_quan_tri",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tenant_domain_landing",
                table: "TENANT");

            migrationBuilder.DropIndex(
                name: "ix_tenant_domain_quan_tri",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "domain_landing",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "domain_quan_tri",
                table: "TENANT");
        }
    }
}
