using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TelegramAggregator.Common.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameContentBase64ToByteaContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TelegramChannelId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PerceptualHash = table.Column<string>(type: "text", nullable: true),
                    MimeType = table.Column<string>(type: "text", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: true),
                    TelegramFileId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_images", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "summaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WindowStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WindowEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SummaryText = table.Column<string>(type: "text", nullable: false),
                    Headline = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IncludedPostIds = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_summaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "posts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TelegramMessageId = table.Column<long>(type: "bigint", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    NormalizedTextHash = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IngestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsSummarized = table.Column<bool>(type: "boolean", nullable: false),
                    RawJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_posts_channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "post_images",
                columns: table => new
                {
                    PostId = table.Column<long>(type: "bigint", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_images", x => new { x.PostId, x.ImageId });
                    table.ForeignKey(
                        name: "FK_post_images_images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_post_images_posts_PostId",
                        column: x => x.PostId,
                        principalTable: "posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_channels_TelegramChannelId",
                table: "channels",
                column: "TelegramChannelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_images_ChecksumSha256",
                table: "images",
                column: "ChecksumSha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_images_PerceptualHash",
                table: "images",
                column: "PerceptualHash");

            migrationBuilder.CreateIndex(
                name: "IX_images_UsedAt",
                table: "images",
                column: "UsedAt");

            migrationBuilder.CreateIndex(
                name: "IX_post_images_ImageId",
                table: "post_images",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_posts_ChannelId",
                table: "posts",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_posts_Fingerprint",
                table: "posts",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_posts_IngestedAt",
                table: "posts",
                column: "IngestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_posts_IsSummarized",
                table: "posts",
                column: "IsSummarized");

            migrationBuilder.CreateIndex(
                name: "IX_posts_TelegramMessageId",
                table: "posts",
                column: "TelegramMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_summaries_PublishedAt",
                table: "summaries",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_summaries_WindowStart",
                table: "summaries",
                column: "WindowStart");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "post_images");

            migrationBuilder.DropTable(
                name: "summaries");

            migrationBuilder.DropTable(
                name: "images");

            migrationBuilder.DropTable(
                name: "posts");

            migrationBuilder.DropTable(
                name: "channels");
        }
    }
}
