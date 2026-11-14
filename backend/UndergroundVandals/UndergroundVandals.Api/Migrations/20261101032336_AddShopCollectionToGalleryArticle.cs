using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UndergroundVandals.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddShopCollectionToGalleryArticle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShopCollectionSlug",
                table: "GalleryArticles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShopCollectionTitle",
                table: "GalleryArticles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShopCollectionSlug",
                table: "GalleryArticles");

            migrationBuilder.DropColumn(
                name: "ShopCollectionTitle",
                table: "GalleryArticles");
        }
    }
}
