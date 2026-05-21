using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalEventIdFkAndIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PointEvents_OriginalEventId",
                table: "PointEvents",
                column: "OriginalEventId");

            migrationBuilder.AddForeignKey(
                name: "FK_PointEvents_PointEvents_OriginalEventId",
                table: "PointEvents",
                column: "OriginalEventId",
                principalTable: "PointEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PointEvents_PointEvents_OriginalEventId",
                table: "PointEvents");

            migrationBuilder.DropIndex(
                name: "IX_PointEvents_OriginalEventId",
                table: "PointEvents");
        }
    }
}
