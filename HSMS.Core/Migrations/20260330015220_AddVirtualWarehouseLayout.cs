using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSMS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualWarehouseLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StoragePositionId",
                table: "InventoryRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StoragePositions",
                columns: table => new
                {
                    StoragePositionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PositionCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RackCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ShelfLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    MapPercentX = table.Column<int>(type: "INTEGER", nullable: false),
                    MapPercentY = table.Column<int>(type: "INTEGER", nullable: false),
                    AisleLabel = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoragePositions", x => x.StoragePositionId);
                    table.ForeignKey(
                        name: "FK_StoragePositions_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseZones",
                columns: table => new
                {
                    WarehouseZoneId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    RectX = table.Column<int>(type: "INTEGER", nullable: false),
                    RectY = table.Column<int>(type: "INTEGER", nullable: false),
                    RectW = table.Column<int>(type: "INTEGER", nullable: false),
                    RectH = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseZones", x => x.WarehouseZoneId);
                    table.ForeignKey(
                        name: "FK_WarehouseZones_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRecords_StoragePositionId",
                table: "InventoryRecords",
                column: "StoragePositionId");

            migrationBuilder.CreateIndex(
                name: "IX_StoragePositions_WarehouseId_PositionCode",
                table: "StoragePositions",
                columns: new[] { "WarehouseId", "PositionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseZones_WarehouseId_SortOrder",
                table: "WarehouseZones",
                columns: new[] { "WarehouseId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryRecords_StoragePositions_StoragePositionId",
                table: "InventoryRecords",
                column: "StoragePositionId",
                principalTable: "StoragePositions",
                principalColumn: "StoragePositionId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryRecords_StoragePositions_StoragePositionId",
                table: "InventoryRecords");

            migrationBuilder.DropTable(
                name: "StoragePositions");

            migrationBuilder.DropTable(
                name: "WarehouseZones");

            migrationBuilder.DropIndex(
                name: "IX_InventoryRecords_StoragePositionId",
                table: "InventoryRecords");

            migrationBuilder.DropColumn(
                name: "StoragePositionId",
                table: "InventoryRecords");
        }
    }
}
