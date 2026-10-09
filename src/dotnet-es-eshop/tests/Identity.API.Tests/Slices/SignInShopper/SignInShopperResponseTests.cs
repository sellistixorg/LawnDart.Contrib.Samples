using System.Text;
using System.Text.Json;
using LawnDart.Samples.EShop.Identity.API.Slices.SignInShopper;
using Microsoft.AspNetCore.Http;

namespace LawnDart.Samples.EShop.Identity.API.Tests.Slices.SignInShopper;

public class SignInShopperResponseTests
{
    private static readonly Guid AccountId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task Accepted_sign_in_writes_account_id_and_display_name()
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = ShopperSignInHttp.Path;
        context.Response.Body = body;
        var receipt = new SignInShopperReceipt();

        await ShopperSignInHttp.InvokeAsync(context, receipt, _ =>
        {
            receipt.Set(new ShopperSignInResponse(
                AccountId,
                "Alice Smith",
                "header.payload.sig",
                DateTimeOffset.Parse("2026-10-08T23:00:00Z")));
            context.Response.StatusCode = StatusCodes.Status202Accepted;
            return Task.CompletedTask;
        });

        Assert.Equal("application/json", context.Response.ContentType);
        using var json = JsonDocument.Parse(body.ToArray());
        Assert.Equal(AccountId, json.RootElement.GetProperty("accountId").GetGuid());
        Assert.Equal("Alice Smith", json.RootElement.GetProperty("displayName").GetString());
        Assert.Equal("header.payload.sig", json.RootElement.GetProperty("accessToken").GetString());
        Assert.Equal(
            DateTimeOffset.Parse("2026-10-08T23:00:00Z"),
            json.RootElement.GetProperty("accessTokenExpiresAt").GetDateTimeOffset());
        Assert.DoesNotContain("Pass123$", Encoding.UTF8.GetString(body.ToArray()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejected_sign_in_keeps_the_problem_body()
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = ShopperSignInHttp.Path;
        context.Response.Body = body;
        var receipt = new SignInShopperReceipt();
        var problem = """{"title":"INVALID_CREDENTIALS","detail":"User name or password is incorrect, or account is locked/disabled."}""";

        await ShopperSignInHttp.InvokeAsync(context, receipt, async http =>
        {
            http.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            var bytes = Encoding.UTF8.GetBytes(problem);
            await http.Response.Body.WriteAsync(bytes);
        });

        Assert.Equal(problem, Encoding.UTF8.GetString(body.ToArray()));
        Assert.Null(receipt.Result);
    }

    [Fact]
    public async Task Other_routes_are_not_rewritten()
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/health";
        context.Response.Body = body;

        await ShopperSignInHttp.InvokeAsync(context, new SignInShopperReceipt(), async http =>
        {
            http.Response.StatusCode = StatusCodes.Status200OK;
            await http.Response.WriteAsync("ok");
        });

        Assert.Equal("ok", Encoding.UTF8.GetString(body.ToArray()));
    }
}
