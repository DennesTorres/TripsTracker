using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripsTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParentCommentIdFkAndIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PlaceComments_ParentCommentId",
                table: "PlaceComments",
                column: "ParentCommentId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaceComments_PlaceComments_ParentCommentId",
                table: "PlaceComments",
                column: "ParentCommentId",
                principalTable: "PlaceComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlaceComments_PlaceComments_ParentCommentId",
                table: "PlaceComments");

            migrationBuilder.DropIndex(
                name: "IX_PlaceComments_ParentCommentId",
                table: "PlaceComments");
        }
    }
}
