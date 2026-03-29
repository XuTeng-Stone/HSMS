using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSMS.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SpecificationText = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MinimumThreshold = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.ItemId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Department = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ContactNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SystemRole = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserType = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
                    AssignedWarehouseZone = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ActiveVehicleId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LicenseNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    WarehouseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsCentralHub = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.WarehouseId);
                });

            migrationBuilder.CreateTable(
                name: "Requisitions",
                columns: table => new
                {
                    RequisitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RequestedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DeliveryLocation = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RequisitionType = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
                    JustificationCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TargetDeliveryWindow = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requisitions", x => x.RequisitionId);
                    table.ForeignKey(
                        name: "FK_Requisitions_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryRecords",
                columns: table => new
                {
                    RecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatchLotNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "INTEGER", nullable: false),
                    LocationBin = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryRecords", x => x.RecordId);
                    table.ForeignKey(
                        name: "FK_InventoryRecords_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryRecords_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemWarehouseProfiles",
                columns: table => new
                {
                    ItemWarehouseProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SafetyStockCeiling = table.Column<int>(type: "INTEGER", nullable: false),
                    ReorderPoint = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemWarehouseProfiles", x => x.ItemWarehouseProfileId);
                    table.ForeignKey(
                        name: "FK_ItemWarehouseProfiles_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemWarehouseProfiles_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferOrders",
                columns: table => new
                {
                    StockTransferOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceWarehouseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DestinationWarehouseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferOrders", x => x.StockTransferOrderId);
                    table.ForeignKey(
                        name: "FK_StockTransferOrders_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockTransferOrders_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockTransferOrders_Warehouses_DestinationWarehouseId",
                        column: x => x.DestinationWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferOrders_Warehouses_SourceWarehouseId",
                        column: x => x.SourceWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryTasks",
                columns: table => new
                {
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequisitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedToId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DispatchTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeliveryStatus = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryTasks", x => x.TaskId);
                    table.ForeignKey(
                        name: "FK_DeliveryTasks_Requisitions_RequisitionId",
                        column: x => x.RequisitionId,
                        principalTable: "Requisitions",
                        principalColumn: "RequisitionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryTasks_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PickLists",
                columns: table => new
                {
                    PickListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequisitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GeneratedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreationTimestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PickStatus = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickLists", x => x.PickListId);
                    table.ForeignKey(
                        name: "FK_PickLists_Requisitions_RequisitionId",
                        column: x => x.RequisitionId,
                        principalTable: "Requisitions",
                        principalColumn: "RequisitionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickLists_Users_GeneratedById",
                        column: x => x.GeneratedById,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequisitionLineItems",
                columns: table => new
                {
                    LineItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequisitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    FulfilledQuantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequisitionLineItems", x => x.LineItemId);
                    table.ForeignKey(
                        name: "FK_RequisitionLineItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequisitionLineItems_Requisitions_RequisitionId",
                        column: x => x.RequisitionId,
                        principalTable: "Requisitions",
                        principalColumn: "RequisitionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferLines",
                columns: table => new
                {
                    StockTransferLineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StockTransferOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferLines", x => x.StockTransferLineId);
                    table.ForeignKey(
                        name: "FK_StockTransferLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferLines_StockTransferOrders_StockTransferOrderId",
                        column: x => x.StockTransferOrderId,
                        principalTable: "StockTransferOrders",
                        principalColumn: "StockTransferOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTasks_AssignedToId",
                table: "DeliveryTasks",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTasks_RequisitionId",
                table: "DeliveryTasks",
                column: "RequisitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRecords_ItemId",
                table: "InventoryRecords",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRecords_WarehouseId_ItemId",
                table: "InventoryRecords",
                columns: new[] { "WarehouseId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemWarehouseProfiles_ItemId_WarehouseId",
                table: "ItemWarehouseProfiles",
                columns: new[] { "ItemId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemWarehouseProfiles_WarehouseId",
                table: "ItemWarehouseProfiles",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_PickLists_GeneratedById",
                table: "PickLists",
                column: "GeneratedById");

            migrationBuilder.CreateIndex(
                name: "IX_PickLists_RequisitionId",
                table: "PickLists",
                column: "RequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitionLineItems_ItemId",
                table: "RequisitionLineItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitionLineItems_RequisitionId",
                table: "RequisitionLineItems",
                column: "RequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Requisitions_RequestDate",
                table: "Requisitions",
                column: "RequestDate");

            migrationBuilder.CreateIndex(
                name: "IX_Requisitions_RequestedById",
                table: "Requisitions",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_Requisitions_Status",
                table: "Requisitions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_ItemId",
                table: "StockTransferLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_StockTransferOrderId",
                table: "StockTransferLines",
                column: "StockTransferOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferOrders_CompletedByUserId",
                table: "StockTransferOrders",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferOrders_DestinationWarehouseId",
                table: "StockTransferOrders",
                column: "DestinationWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferOrders_RequestedByUserId",
                table: "StockTransferOrders",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferOrders_SourceWarehouseId",
                table: "StockTransferOrders",
                column: "SourceWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferOrders_Status",
                table: "StockTransferOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Code",
                table: "Warehouses",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryTasks");

            migrationBuilder.DropTable(
                name: "InventoryRecords");

            migrationBuilder.DropTable(
                name: "ItemWarehouseProfiles");

            migrationBuilder.DropTable(
                name: "PickLists");

            migrationBuilder.DropTable(
                name: "RequisitionLineItems");

            migrationBuilder.DropTable(
                name: "StockTransferLines");

            migrationBuilder.DropTable(
                name: "Requisitions");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "StockTransferOrders");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Warehouses");
        }
    }
}
