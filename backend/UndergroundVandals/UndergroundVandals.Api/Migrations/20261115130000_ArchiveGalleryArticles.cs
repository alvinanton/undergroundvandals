using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;
using System.Collections.Generic;
using UndergroundVandals.Api.Data;

#nullable disable

namespace UndergroundVandals.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261115130000_ArchiveGalleryArticles")]
public partial class ArchiveGalleryArticles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsArchived",
            table: "GalleryArticles",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "IsArchived", table: "GalleryArticles");

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("UndergroundVandals.Api.Entities.GalleryArticle", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<string>("Category").IsRequired().HasColumnType("text");
            b.Property<string>("ContentMarkdown").IsRequired().HasColumnType("text");
            b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("EventDate").HasColumnType("timestamp with time zone");
            b.Property<string>("ExternalUrl").HasColumnType("text");
            b.PrimitiveCollection<List<string>>("GalleryImageUrls").IsRequired().HasColumnType("text[]");
            b.Property<string>("HeroImageUrl").IsRequired().HasColumnType("text");
            b.Property<bool>("IsArchived").HasColumnType("boolean");
            b.Property<bool>("IsFeatured").HasColumnType("boolean");
            b.Property<bool>("IsPublished").HasColumnType("boolean");
            b.Property<string>("Location").HasColumnType("text");
            b.Property<DateTime>("PublishedAt").HasColumnType("timestamp with time zone");
            b.Property<int>("ReadingTimeMinutes").HasColumnType("integer");
            b.Property<string>("ShopCollectionSlug").HasColumnType("text");
            b.Property<string>("ShopCollectionTitle").HasColumnType("text");
            b.Property<string>("Slug").IsRequired().HasColumnType("text");
            b.Property<string>("SubjectName").HasColumnType("text");
            b.Property<string>("Subtitle").IsRequired().HasColumnType("text");
            b.PrimitiveCollection<List<string>>("Tags").IsRequired().HasColumnType("text[]");
            b.Property<string>("Title").IsRequired().HasColumnType("text");
            b.Property<DateTime?>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("VideoSource").IsRequired().HasColumnType("text");
            b.Property<string>("VideoUrl").HasColumnType("text");
            b.HasKey("Id");
            b.HasIndex("Category");
            b.HasIndex("PublishedAt");
            b.HasIndex("Slug").IsUnique();
            b.ToTable("GalleryArticles");
        });
    }
}
