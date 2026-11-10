namespace FinanceManager.IntegrationTests.Fixtures;

public sealed class FoundationTests
{
    [Fact]
    public async Task HostStartsAgainstAnIsolatedSqlServerDatabase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var context = database.CreateContext();
        Assert.True(await context.Database.CanConnectAsync(cancellationToken));
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync("/api/unknown", cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
