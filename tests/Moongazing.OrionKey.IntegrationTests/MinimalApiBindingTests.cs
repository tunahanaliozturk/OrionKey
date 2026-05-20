using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Moongazing.OrionKey.IntegrationTests;

public class MinimalApiBindingTests
{
    [Fact]
    public async Task Route_ShouldBindOrderId_ViaIParsable()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.MapGet("/orders/{id}", (OrderId id) => Results.Ok(id.ToString()));
        await app.StartAsync();

        var client = app.GetTestClient();
        var id = OrderId.New();
        var response = await client.GetAsync($"/orders/{id}");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(id.ToString(), body);

        await app.StopAsync();
    }
}
