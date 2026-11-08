using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UndergroundVandals.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCoverAndThumbnailToMediaAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCover",
                table: "MediaAssets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "MediaAssets",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCover",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                table: "MediaAssets");
        }
    }
}
