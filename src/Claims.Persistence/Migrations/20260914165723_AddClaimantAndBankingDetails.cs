using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Claims.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimantAndBankingDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountHolder",
                table: "Claims",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "Claims",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Claims",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BranchCode",
                table: "Claims",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClaimantFirstName",
                table: "Claims",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClaimantIdNumber",
                table: "Claims",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClaimantLastName",
                table: "Claims",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountHolder",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "BranchCode",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ClaimantFirstName",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ClaimantIdNumber",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ClaimantLastName",
                table: "Claims");
        }
    }
}
