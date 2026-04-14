using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HSMS.Api.Tests;

public sealed class ApiIntegrationTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly Guid CentralId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid SatelliteId = Guid.Parse("11111111-1111-1111-1111-111111111102");
    private static readonly Guid ItemAmoxicillin = Guid.Parse("22222222-2222-2222-2222-222222222201");
    private static readonly Guid ItemTitaniumPlate = Guid.Parse("22222222-2222-2222-2222-222222222206");
    private static readonly Guid MedicalUser = Guid.Parse("33333333-3333-3333-3333-333333333301");
    private static readonly Guid InventoryManagerUser = Guid.Parse("33333333-3333-3333-3333-333333333303");
    private static readonly Guid LogisticsUser = Guid.Parse("33333333-3333-3333-3333-333333333304");

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task GetWarehouses_ReturnsSeededCentralAndSatellite()
    {
        var res = await _client.GetAsync("/api/warehouses");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(JsonValueKind.Array, list.ValueKind);
        var codes = new List<string>();
        foreach (var row in list.EnumerateArray())
            codes.Add(row.GetProperty("code").GetString()!);
        Assert.Contains("WH_CENTRAL", codes);
        Assert.Contains("WH_SATELLITE", codes);
    }

    [Fact]
    public async Task GetItems_ReturnsCatalog()
    {
        var res = await _client.GetAsync("/api/items");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(JsonValueKind.Array, list.ValueKind);
        Assert.True(list.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetInventoryLevels_IncludesReplenishmentFlags()
    {
        var res = await _client.GetAsync("/api/inventory/levels");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(JsonValueKind.Array, list.ValueKind);
        var seenFlag = false;
        foreach (var row in list.EnumerateArray())
        {
            Assert.True(row.TryGetProperty("needsReplenishment", out _));
            if (row.GetProperty("needsReplenishment").GetBoolean())
                seenFlag = true;
        }
        Assert.True(seenFlag);
    }

    [Fact]
    public async Task GetVirtualLayout_ForCentral_ReturnsZonesAndPositions()
    {
        var res = await _client.GetAsync($"/api/warehouses/{CentralId}/virtual-layout");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.True(doc.GetProperty("zones").GetArrayLength() >= 1);
        Assert.True(doc.GetProperty("positions").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetVirtualLayout_ForUnknownWarehouse_ReturnsNotFound()
    {
        var res = await _client.GetAsync($"/api/warehouses/{Guid.Empty}/virtual-layout");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetPlacementMap_ForSeededItem_ReturnsWarehouseMaps()
    {
        var res = await _client.GetAsync($"/api/inventory/items/{ItemAmoxicillin}/placement-map");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(ItemAmoxicillin, doc.GetProperty("itemId").GetGuid());
        Assert.True(doc.GetProperty("warehouseMaps").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetPlacementMap_ForUnknownItem_ReturnsNotFound()
    {
        var res = await _client.GetAsync("/api/inventory/items/00000000-0000-0000-0000-000000000001/placement-map");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task PostStockTransfer_WithNoLines_ReturnsBadRequest()
    {
        var body = new
        {
            sourceWarehouseId = CentralId,
            destinationWarehouseId = SatelliteId,
            lines = Array.Empty<object>()
        };
        var res = await _client.PostAsJsonAsync("/api/stock-transfers", body, JsonOpts);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PostStockTransfer_WhenSatelliteToCentral_IsAllowedForInterDepartmentTransfer()
    {
        var body = new
        {
            sourceWarehouseId = SatelliteId,
            destinationWarehouseId = CentralId,
            lines = new[] { new { itemId = ItemAmoxicillin, quantity = 1 } },
            requestedByUserId = InventoryManagerUser,
            priority = "urgent",
            justification = "ER temporary reallocation"
        };
        var res = await _client.PostAsJsonAsync("/api/stock-transfers", body, JsonOpts);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task PostStockTransfer_ThenComplete_AdjustsSatelliteOnHand()
    {
        var satBefore = await GetSatelliteOnHand(ItemAmoxicillin);
        var create = new
        {
            sourceWarehouseId = CentralId,
            destinationWarehouseId = SatelliteId,
            lines = new[] { new { itemId = ItemAmoxicillin, quantity = 3 } },
            requestedByUserId = InventoryManagerUser
        };
        var post = await _client.PostAsJsonAsync("/api/stock-transfers", create, JsonOpts);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var orderId = created.GetProperty("stockTransferOrderId").GetGuid();
        var complete = await _client.PostAsJsonAsync($"/api/stock-transfers/{orderId}/complete", new { completedByUserId = InventoryManagerUser }, JsonOpts);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var satAfter = await GetSatelliteOnHand(ItemAmoxicillin);
        Assert.Equal(satBefore + 3, satAfter);
    }

    [Fact]
    public async Task ApproveHighValueEmergencyRequisition_RequiresApprovalNote()
    {
        var create = await _client.PostAsJsonAsync("/api/requisitions/emergency", new
        {
            requestedById = MedicalUser,
            deliveryLocation = "OR-02",
            justificationCode = "CRITICAL_IMPLANT",
            lines = new[] { new { itemId = ItemTitaniumPlate, quantity = 1 } }
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createBody = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var reqId = createBody.GetProperty("requisitionId").GetGuid();

        var noNote = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/approve", new
        {
            approvedByUserId = InventoryManagerUser
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);

        var withNote = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/approve", new
        {
            approvedByUserId = InventoryManagerUser,
            approvalNote = "Implant verified against surgery plan."
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.OK, withNote.StatusCode);
    }

    [Fact]
    public async Task StandardRequisition_FullFlow_ReachesCompleted()
    {
        var create = await _client.PostAsJsonAsync("/api/requisitions/standard", new
        {
            requestedById = MedicalUser,
            deliveryLocation = "ER-Bed-05",
            targetDeliveryWindow = "within-30-min",
            lines = new[] { new { itemId = ItemAmoxicillin, quantity = 2 } }
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createBody = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var reqId = createBody.GetProperty("requisitionId").GetGuid();

        var approve = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/approve", new
        {
            approvedByUserId = InventoryManagerUser,
            approvalNote = "Within policy limits."
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var pick = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/pick-and-pack", new
        {
            pickedByUserId = InventoryManagerUser
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);

        var delivery = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/delivery-task", new
        {
            assignedToId = LogisticsUser
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.Created, delivery.StatusCode);
        var taskBody = await delivery.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var taskId = taskBody.GetProperty("taskId").GetGuid();

        var accept = await _client.PostAsync($"/api/delivery-tasks/{taskId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var arrive = await _client.PostAsync($"/api/delivery-tasks/{taskId}/arrive", null);
        Assert.Equal(HttpStatusCode.OK, arrive.StatusCode);

        var confirm = await _client.PostAsync($"/api/requisitions/{reqId}/confirm-receipt", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var confirmed = await confirm.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal("Completed", confirmed.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DashboardAndReports_ReturnSuccess()
    {
        var kpi = await _client.GetAsync("/api/dashboard/kpis");
        Assert.Equal(HttpStatusCode.OK, kpi.StatusCode);

        var lowStock = await _client.GetAsync("/api/reports/low-stock-risk");
        Assert.Equal(HttpStatusCode.OK, lowStock.StatusCode);

        var dept = await _client.GetAsync("/api/reports/consumption-by-department?days=30");
        Assert.Equal(HttpStatusCode.OK, dept.StatusCode);
    }

    [Fact]
    public async Task ApproveRequisition_WithMedicalStaffRole_ReturnsBadRequest()
    {
        var create = await _client.PostAsJsonAsync("/api/requisitions/standard", new
        {
            requestedById = MedicalUser,
            deliveryLocation = "ER-Bed-03",
            targetDeliveryWindow = "routine",
            lines = new[] { new { itemId = ItemAmoxicillin, quantity = 1 } }
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createBody = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var reqId = createBody.GetProperty("requisitionId").GetGuid();

        var approve = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/approve", new
        {
            approvedByUserId = MedicalUser,
            approvalNote = "should fail"
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.BadRequest, approve.StatusCode);
    }

    [Fact]
    public async Task CreateDeliveryTask_WithNonLogisticsUser_ReturnsBadRequest()
    {
        var reqId = await CreateAndApproveStandardRequisition();
        var pick = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/pick-and-pack", new
        {
            pickedByUserId = InventoryManagerUser
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);

        var delivery = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/delivery-task", new
        {
            assignedToId = InventoryManagerUser
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.BadRequest, delivery.StatusCode);
    }

    [Fact]
    public async Task ConfirmReceipt_BeforeArrive_ReturnsBadRequest()
    {
        var reqId = await CreateAndApproveStandardRequisition();
        var pick = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/pick-and-pack", new
        {
            pickedByUserId = InventoryManagerUser
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);

        var delivery = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/delivery-task", new
        {
            assignedToId = LogisticsUser
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.Created, delivery.StatusCode);
        var taskBody = await delivery.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var taskId = taskBody.GetProperty("taskId").GetGuid();

        var accept = await _client.PostAsync($"/api/delivery-tasks/{taskId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var confirm = await _client.PostAsync($"/api/requisitions/{reqId}/confirm-receipt", null);
        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
    }

    [Fact]
    public async Task Notifications_AfterCompletion_ContainsCompletedStage()
    {
        var create = await _client.PostAsJsonAsync("/api/requisitions/standard", new
        {
            requestedById = MedicalUser,
            deliveryLocation = "OR-01",
            targetDeliveryWindow = "urgent",
            lines = new[] { new { itemId = ItemAmoxicillin, quantity = 1 } }
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createBody = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var reqId = createBody.GetProperty("requisitionId").GetGuid();

        var approve = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/approve", new
        {
            approvedByUserId = InventoryManagerUser,
            approvalNote = "approved"
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var pick = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/pick-and-pack", new
        {
            pickedByUserId = InventoryManagerUser
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);

        var delivery = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/delivery-task", new
        {
            assignedToId = LogisticsUser
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.Created, delivery.StatusCode);
        var taskBody = await delivery.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var taskId = taskBody.GetProperty("taskId").GetGuid();

        var accept = await _client.PostAsync($"/api/delivery-tasks/{taskId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var arrive = await _client.PostAsync($"/api/delivery-tasks/{taskId}/arrive", null);
        Assert.Equal(HttpStatusCode.OK, arrive.StatusCode);
        var confirm = await _client.PostAsync($"/api/requisitions/{reqId}/confirm-receipt", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var feedRes = await _client.GetAsync($"/api/requisitions/{reqId}/notifications");
        Assert.Equal(HttpStatusCode.OK, feedRes.StatusCode);
        var feed = await feedRes.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(JsonValueKind.Array, feed.ValueKind);
        Assert.Contains(feed.EnumerateArray(), e => e.GetProperty("stage").GetString() == "completed");
    }

    private async Task<int> GetSatelliteOnHand(Guid itemId)
    {
        var res = await _client.GetAsync($"/api/inventory/items/{itemId}?warehouseId={SatelliteId}");
        res.EnsureSuccessStatusCode();
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var groups = doc.GetProperty("byWarehouse");
        foreach (var g in groups.EnumerateArray())
        {
            if (g.GetProperty("warehouseId").GetGuid() == SatelliteId)
                return g.GetProperty("totalOnHand").GetInt32();
        }
        return 0;
    }

    private async Task<Guid> CreateAndApproveStandardRequisition()
    {
        var create = await _client.PostAsJsonAsync("/api/requisitions/standard", new
        {
            requestedById = MedicalUser,
            deliveryLocation = "ER-Bed-08",
            targetDeliveryWindow = "routine",
            lines = new[] { new { itemId = ItemAmoxicillin, quantity = 1 } }
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createBody = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var reqId = createBody.GetProperty("requisitionId").GetGuid();

        var approve = await _client.PostAsJsonAsync($"/api/requisitions/{reqId}/approve", new
        {
            approvedByUserId = InventoryManagerUser,
            approvalNote = "approved for testing"
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        return reqId;
    }
}
