using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace MediaPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Videos",
                type: "tsvector",
                nullable: false)
                .Annotation("Npgsql:TsVectorConfig", "french")
                .Annotation("Npgsql:TsVectorProperties", new[] { "Title", "Description" });

            migrationBuilder.CreateIndex(
                name: "IX_Videos_SearchVector",
                table: "Videos",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Videos_SearchVector",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Videos");
        }
    }
}
