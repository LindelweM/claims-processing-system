using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Claims.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "Claims",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                // Every claim lodged before channels were recorded came through the web form.
                defaultValue: "WebForm");

            migrationBuilder.AddColumn<string>(
                name: "ChannelReference",
                table: "Claims",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Channel_ChannelReference",
                table: "Claims",
                columns: new[] { "Channel", "ChannelReference" },
                unique: true,
                filter: "[ChannelReference] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Claims_Channel_ChannelReference",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ChannelReference",
                table: "Claims");
        }
    }
}
