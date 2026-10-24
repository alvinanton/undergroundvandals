using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UndergroundVandals.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceTypeAndReferrerProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                table: "WebsiteVisits",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Referrer",
                table: "WebsiteVisits",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "WebsiteVisits");

            migrationBuilder.DropColumn(
                name: "Referrer",
                table: "WebsiteVisits");
        }
    }
}
