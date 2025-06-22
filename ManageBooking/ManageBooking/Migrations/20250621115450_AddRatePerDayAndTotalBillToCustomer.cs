using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManageBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddRatePerDayAndTotalBillToCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price",
                table: "Customers");

            migrationBuilder.AddColumn<decimal>(
                name: "RatePerDay",
                table: "Customers",
                type: "decimal(18, 2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalBill",
                table: "Customers",
                type: "decimal(18, 2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RatePerDay",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TotalBill",
                table: "Customers");

            migrationBuilder.AddColumn<double>(
                name: "Price",
                table: "Customers",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
