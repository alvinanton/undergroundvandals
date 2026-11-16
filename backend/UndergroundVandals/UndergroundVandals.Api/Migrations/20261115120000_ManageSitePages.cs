using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using UndergroundVandals.Api.Data;

#nullable disable

namespace UndergroundVandals.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261115120000_ManageSitePages")]
public partial class ManageSitePages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SitePageSettings",
            columns: table => new
            {
                PageKey = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                Title = table.Column<string>(type: "text", nullable: false),
                Message = table.Column<string>(type: "text", nullable: false),
                AnnouncementTitle = table.Column<string>(type: "text", nullable: false),
                AnnouncementMessage = table.Column<string>(type: "text", nullable: false),
                CtaLabel = table.Column<string>(type: "text", nullable: false),
                CtaUrl = table.Column<string>(type: "text", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SitePageSettings", x => x.PageKey));

        migrationBuilder.InsertData(
            table: "SitePageSettings",
            columns: ["PageKey", "Status", "Title", "Message", "AnnouncementTitle", "AnnouncementMessage", "CtaLabel", "CtaUrl", "UpdatedAt"],
            values: new object[,]
            {
                { "home", "Live", "Underground Vandals", "Documenting street art culture, preserving underground graffiti archives, featuring exclusive interviews, and offering official merchandise.", "", "", "", "", new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc) },
                { "archive", "Live", "Street Archive", "High-resolution database of street art, train actions, and graffiti walls worldwide.", "", "", "", "", new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc) },
                { "gallery", "Live", "Gallery & Exclusives", "Exclusive artist spotlights, video premieres, and exhibition retrospectives.", "", "", "", "", new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc) },
                { "shop", "Live", "Official Shop", "Apparel, limited physical items, stickers, and digital assets from Underground Vandals.", "", "", "", "", new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc) }
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "SitePageSettings");

    protected override void BuildTargetModel(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("UndergroundVandals.Api.Entities.SitePageSetting", entity =>
        {
            entity.Property<string>("PageKey").HasMaxLength(24).HasColumnType("character varying(24)");
            entity.Property<string>("Status").IsRequired().HasColumnType("text");
            entity.Property<string>("Title").IsRequired().HasColumnType("text");
            entity.Property<string>("Message").IsRequired().HasColumnType("text");
            entity.Property<string>("AnnouncementTitle").IsRequired().HasColumnType("text");
            entity.Property<string>("AnnouncementMessage").IsRequired().HasColumnType("text");
            entity.Property<string>("CtaLabel").IsRequired().HasColumnType("text");
            entity.Property<string>("CtaUrl").IsRequired().HasColumnType("text");
            entity.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
            entity.HasKey("PageKey");
            entity.ToTable("SitePageSettings");
        });
    }
}
