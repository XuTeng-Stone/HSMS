using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSMS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryRackAndMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MapPercentX",
                table: "InventoryRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MapPercentY",
                table: "InventoryRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RackCode",
                table: "InventoryRecords",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ShelfLevel",
                table: "InventoryRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MapPercentX",
                table: "InventoryRecords");

            migrationBuilder.DropColumn(
                name: "MapPercentY",
                table: "InventoryRecords");

            migrationBuilder.DropColumn(
                name: "RackCode",
                table: "InventoryRecords");

            migrationBuilder.DropColumn(
                name: "ShelfLevel",
                table: "InventoryRecords");
        }
    }
}
