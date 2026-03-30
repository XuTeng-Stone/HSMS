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
    public async Task PostStockTransfer_WhenSatelliteToCentral_ReturnsBadRequest()
    {
        var body = new
        {
            sourceWarehouseId = SatelliteId,
            destinationWarehouseId = CentralId,
            lines = new[] { new { itemId = ItemAmoxicillin, quantity = 1 } }
        };
        var res = await _client.PostAsJsonAsync("/api/stock-transfers", body, JsonOpts);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PostStockTransfer_ThenComplete_AdjustsSatelliteOnHand()
    {
        var satBefore = await GetSatelliteOnHand(ItemAmoxicillin);
        var create = new
        {
            sourceWarehouseId = CentralId,
            destinationWarehouseId = SatelliteId,
            lines = new[] { new { itemId = ItemAmoxicillin, quantity = 3 } }
        };
        var post = await _client.PostAsJsonAsync("/api/stock-transfers", create, JsonOpts);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var orderId = created.GetProperty("stockTransferOrderId").GetGuid();
        var complete = await _client.PostAsync($"/api/stock-transfers/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var satAfter = await GetSatelliteOnHand(ItemAmoxicillin);
        Assert.Equal(satBefore + 3, satAfter);
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
}
