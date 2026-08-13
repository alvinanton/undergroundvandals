using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UndergroundVandals.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIsArchivedAndHashtags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Hashtags",
                table: "MediaItems",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "MediaItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hashtags",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "MediaItems");
        }
    }
}
