using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Bills;

public sealed class BillPersistenceTests
{
    [Fact]
    public async Task QueryOrdersPaginatesAndAggregatesWholeFilteredSet()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await BillScenario.StartAsync(token);
        using var older = await scenario.CreateAsync("Antiga", "40.00", "2026-10-10", token);
        using var today = await scenario.CreateAsync("Hoje", "30.00", "2026-11-10", token);
        using var future = await scenario.CreateAsync("Futura", "20.00", "2026-11-11", token);

        using var page = await scenario.Client.GetAsync("/api/bills?page=2&pageSize=1", token);
        var json = JsonDocument.Parse(await page.Content.ReadAsStringAsync(token)).RootElement;
        Assert.Equal(3, json.GetProperty("totalCount").GetInt64());
        Assert.Equal("90.00", json.GetProperty("totals").GetProperty("openAmount").GetString());
        Assert.Equal("Hoje", json.GetProperty("items")[0].GetProperty("description").GetString());

        using var canceled = await scenario.PutAsync(future.Headers.Location + "/status", new { status = "canceled" },
            future.Headers.ETag!.ToString(), token);
        Assert.Equal(HttpStatusCode.OK, canceled.StatusCode);
        using var canceledList = await scenario.Client.GetAsync("/api/bills?status=canceled", token);
        var canceledJson = JsonDocument.Parse(await canceledList.Content.ReadAsStringAsync(token)).RootElement;
        Assert.Equal("0.00", canceledJson.GetProperty("totals").GetProperty("openAmount").GetString());

        await using var context = scenario.Database.CreateContext();
        Assert.Equal(4, await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM [BillAudits]").SingleAsync(token));
    }
}
