using System.Net;
using System.Text.RegularExpressions;
using LawnDart.Samples.EShop.WebApp.Services;

namespace LawnDart.Samples.EShop.WebApp.Tests;

public class StorefrontSignInTests : IClassFixture<StorefrontApplication>
{
    private readonly StorefrontApplication _app;

    public StorefrontSignInTests(StorefrontApplication app) => _app = app;

    [Fact]
    public async Task Sign_in_sets_a_cookie_and_follows_a_local_return_url()
    {
        _app.Identity.Reset();
        using var client = _app.CreateNoRedirectClient();

        var response = await PostLoginAsync(client, "/cart", IdentityStub.UserName, IdentityStub.Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/cart", PathOf(response.Headers.Location));
        Assert.Contains(
            ShopperAuthentication.CookieName,
            string.Join(';', response.Headers.Select(header => header.Key + "=" + string.Join(',', header.Value))),
            StringComparison.Ordinal);

        var home = await client.GetAsync("/user/login");
        var html = await home.Content.ReadAsStringAsync();
        Assert.Contains("Signed in as Alice Smith.", html, StringComparison.Ordinal);
        AssertNoPassword(_app);
    }

    [Fact]
    public async Task Wrong_password_keeps_the_form_and_sets_no_cookie()
    {
        _app.Identity.Reset();
        using var client = _app.CreateNoRedirectClient();

        var response = await PostLoginAsync(client, "/cart", IdentityStub.UserName, "Wrong123$");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(ShopperSignInClient.InvalidCredentialsMessage, html, StringComparison.Ordinal);
        Assert.Contains("name=\"UserName\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Password\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Signed in as", html, StringComparison.Ordinal);
        AssertNoPassword(_app);

        var cart = await client.GetAsync("/cart");
        Assert.Equal(HttpStatusCode.Redirect, cart.StatusCode);
        Assert.StartsWith("/user/login", PathOf(cart.Headers.Location), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://evil.example/phish")]
    [InlineData("http://evil.example/phish")]
    [InlineData("//evil.example/phish")]
    [InlineData("/\\evil.example")]
    [InlineData("javascript:alert(1)")]
    public async Task Unsafe_return_url_goes_home(string returnUrl)
    {
        _app.Identity.Reset();
        using var client = _app.CreateNoRedirectClient();

        var response = await PostLoginAsync(client, returnUrl, IdentityStub.UserName, IdentityStub.Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = PathOf(response.Headers.Location);
        Assert.Equal("/", location);
        Assert.DoesNotContain("evil.example", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sign_out_calls_identity_and_clears_the_cookie()
    {
        _app.Identity.Reset();
        using var client = _app.CreateNoRedirectClient();
        var signedIn = await PostLoginAsync(client, "/", IdentityStub.UserName, IdentityStub.Password);
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        var page = await client.GetAsync("/user/login");
        var html = await page.Content.ReadAsStringAsync();
        var fields = HiddenFields(html);
        fields["_handler"] = "logout";
        using var content = new FormUrlEncodedContent(fields);
        var logout = await client.PostAsync("/user/logout", content);

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/", PathOf(logout.Headers.Location));
        Assert.Equal(IdentityStub.AliceId, _app.Identity.SignedOutAccount);

        var cart = await client.GetAsync("/cart");
        Assert.Equal(HttpStatusCode.Redirect, cart.StatusCode);
        Assert.StartsWith("/user/login", PathOf(cart.Headers.Location), StringComparison.OrdinalIgnoreCase);
        AssertNoPassword(_app);
    }

    [Theory]
    [InlineData("/cart")]
    [InlineData("/checkout")]
    [InlineData("/user/orders")]
    public async Task Signed_out_shopper_page_redirects_to_login(string path)
    {
        using var client = _app.CreateNoRedirectClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Uri.UnescapeDataString(PathOf(response.Headers.Location));
        Assert.StartsWith("/user/login", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(path, location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_without_antiforgery_token_sets_no_cookie()
    {
        _app.Identity.Reset();
        using var client = _app.CreateNoRedirectClient();

        var response = await PostLoginAsync(
            client,
            "/",
            IdentityStub.UserName,
            IdentityStub.Password,
            includeToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(ShopperAuthentication.CookieName, SetCookie(response), StringComparison.Ordinal);

        var cart = await client.GetAsync("/cart");
        Assert.Equal(HttpStatusCode.Redirect, cart.StatusCode);
        Assert.StartsWith("/user/login", PathOf(cart.Headers.Location), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_without_antiforgery_token_keeps_the_session()
    {
        _app.Identity.Reset();
        using var client = _app.CreateNoRedirectClient();
        var signedIn = await PostLoginAsync(client, "/", IdentityStub.UserName, IdentityStub.Password);
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "logout"
        });
        var logout = await client.PostAsync("/user/logout", content);
        var stillIn = await client.GetAsync("/user/login");
        var html = await stillIn.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, logout.StatusCode);
        Assert.Null(_app.Identity.SignedOutAccount);
        Assert.Contains("Signed in as Alice Smith.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_logout_does_not_sign_out()
    {
        _app.Identity.Reset();
        using var client = _app.CreateNoRedirectClient();
        var signedIn = await PostLoginAsync(client, "/", IdentityStub.UserName, IdentityStub.Password);
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        var logout = await client.GetAsync("/user/logout");
        var stillIn = await client.GetAsync("/user/login");
        var html = await stillIn.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/", PathOf(logout.Headers.Location));
        Assert.Null(_app.Identity.SignedOutAccount);
        Assert.Contains("Signed in as Alice Smith.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Auth_cookie_is_secure_httponly_and_lax()
    {
        _app.Identity.Reset();
        using var client = _app.CreateNoRedirectClient();

        var response = await PostLoginAsync(client, "/", IdentityStub.UserName, IdentityStub.Password);
        var cookie = AuthSetCookie(response);

        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signed_in_calls_forward_the_bearer_token()
    {
        _app.Identity.Reset();
        _app.BasketAuthorization.Clear();
        _app.OrderingAuthorization.Clear();
        _app.CatalogAuthorization.Clear();
        using var client = _app.CreateNoRedirectClient();

        var signedIn = await PostLoginAsync(client, "/cart", IdentityStub.UserName, IdentityStub.Password);
        var cart = await client.GetAsync("/cart");
        var orders = await client.GetAsync("/user/orders");
        var checkout = await client.GetAsync("/checkout");
        var home = await client.GetAsync("/");
        var cartHtml = await cart.Content.ReadAsStringAsync();
        var bearer = "Bearer " + IdentityStub.AccessToken;

        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cart.StatusCode);
        Assert.Equal(HttpStatusCode.OK, orders.StatusCode);
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains(bearer, _app.BasketAuthorization);
        Assert.Contains(bearer, _app.OrderingAuthorization);
        Assert.Contains(bearer, _app.Identity.AuthorizationHeaders);
        Assert.DoesNotContain(bearer, _app.CatalogAuthorization);
        Assert.DoesNotContain(IdentityStub.AccessToken, cartHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(IdentityStub.AccessToken, AuthSetCookie(signedIn), StringComparison.Ordinal);
        AssertNoSecret(_app, IdentityStub.AccessToken);
    }

    [Fact]
    public async Task Backend_unauthorized_sends_the_shopper_to_login()
    {
        _app.Identity.Reset();
        _app.Basket.Status = HttpStatusCode.Unauthorized;
        try
        {
            using var client = _app.CreateNoRedirectClient();
            var signedIn = await PostLoginAsync(client, "/cart", IdentityStub.UserName, IdentityStub.Password);
            var cart = await client.GetAsync("/cart");
            var html = await cart.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
            Assert.DoesNotContain("Basket is temporarily unavailable.", html, StringComparison.Ordinal);
            if (cart.StatusCode == HttpStatusCode.Redirect)
                Assert.StartsWith("/user/login", PathOf(cart.Headers.Location), StringComparison.OrdinalIgnoreCase);
            else
                Assert.Contains("/user/login", html, StringComparison.OrdinalIgnoreCase);

            var again = await client.GetAsync("/cart");
            Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
            Assert.StartsWith("/user/login", PathOf(again.Headers.Location), StringComparison.OrdinalIgnoreCase);
            AssertAuthCookieCleared(cart, again);
        }
        finally
        {
            _app.Basket.Status = HttpStatusCode.OK;
        }
    }

    [Fact]
    public async Task Expired_access_token_sends_the_shopper_to_login()
    {
        _app.Identity.Reset();
        _app.Identity.NextAccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        using var client = _app.CreateNoRedirectClient();

        var signedIn = await PostLoginAsync(client, "/cart", IdentityStub.UserName, IdentityStub.Password);
        var cart = await client.GetAsync("/cart");

        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, cart.StatusCode);
        Assert.StartsWith("/user/login", PathOf(cart.Headers.Location), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Catalog_stays_anonymous()
    {
        using var client = _app.CreateNoRedirectClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Shop all", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Signed in as", html, StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string returnUrl,
        string userName,
        string password) =>
        PostLoginAsync(client, returnUrl, userName, password, includeToken: true);

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string returnUrl,
        string userName,
        string password,
        bool includeToken)
    {
        var loginUrl = "/user/login?returnUrl=" + Uri.EscapeDataString(returnUrl);
        var page = await client.GetAsync(loginUrl);
        var html = await page.Content.ReadAsStringAsync();
        if (!html.Contains("name=\"UserName\"", StringComparison.Ordinal))
            Assert.Fail(html.Length > 2500 ? html[..2500] : html);

        var fields = HiddenFields(html);
        if (!includeToken)
            fields.Remove("__RequestVerificationToken");

        fields["UserName"] = userName;
        fields["Password"] = password;
        using var content = new FormUrlEncodedContent(fields);
        return await client.PostAsync(loginUrl, content);
    }

    private static Dictionary<string, string> HiddenFields(string html)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase))
        {
            var tag = match.Value;
            if (!tag.Contains("hidden", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = Attribute(tag, "name");
            if (string.IsNullOrEmpty(name))
                continue;

            fields[name] = Attribute(tag, "value") ?? "";
        }

        return fields;
    }

    private static string? Attribute(string tag, string name)
    {
        var match = Regex.Match(
            tag,
            name + "\\s*=\\s*(\"[^\"]*\"|'[^']*')",
            RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;

        var value = match.Groups[1].Value;
        return value.Substring(1, value.Length - 2);
    }

    private static string SetCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            return "";

        return string.Join('\n', values);
    }

    private static void AssertAuthCookieCleared(params HttpResponseMessage[] responses)
    {
        var cleared = responses.Any(response =>
            SetCookie(response)
                .Split('\n')
                .Any(value => value.StartsWith(ShopperAuthentication.CookieName + "=", StringComparison.Ordinal)
                    && (value.Contains("expires=", StringComparison.OrdinalIgnoreCase)
                        || value.Contains("max-age=0", StringComparison.OrdinalIgnoreCase))));
        Assert.True(cleared);
    }

    private static string AuthSetCookie(HttpResponseMessage response)
    {
        var cookie = SetCookie(response)
            .Split('\n')
            .FirstOrDefault(value => value.StartsWith(
                ShopperAuthentication.CookieName + "=",
                StringComparison.Ordinal));
        Assert.False(string.IsNullOrEmpty(cookie));
        return cookie;
    }

    private static string PathOf(Uri? location)
    {
        if (location is null)
            return "";

        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    private static void AssertNoPassword(StorefrontApplication app) =>
        AssertNoSecret(app, IdentityStub.Password);

    private static void AssertNoSecret(StorefrontApplication app, string secret)
    {
        lock (app.Logs.Lines)
        {
            Assert.DoesNotContain(
                app.Logs.Lines,
                line => line.Contains(secret, StringComparison.Ordinal)
                    || line.Contains("Wrong123$", StringComparison.Ordinal));
        }
    }
}
