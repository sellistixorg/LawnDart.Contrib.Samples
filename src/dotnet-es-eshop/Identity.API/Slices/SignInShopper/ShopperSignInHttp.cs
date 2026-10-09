using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace LawnDart.Samples.EShop.Identity.API.Slices.SignInShopper;

/// <summary>
/// Writes the account id, display name, access token, and token expiry on a successful sign-in response.
/// </summary>
public static class ShopperSignInHttp
{
    /// <summary>
    /// POST /api/identity/sign-in-shopper.
    /// </summary>
    public const string Path = "/api/identity/sign-in-shopper";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// True when the request is the sign-in command.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <returns>True for POST <see cref="Path"/>.</returns>
    public static bool IsSignIn(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return HttpMethods.IsPost(request.Method)
            && request.Path.Equals(Path, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Runs the rest of the pipeline. A 202 with a receipt becomes JSON. Any other status is copied through.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <param name="receipt">Account id, display name, and access token set by the handler. The password is not on it. This method does not log the token.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>A task that completes when the response is written.</returns>
    /// <remarks>
    /// This method does not read the request body and does not log the password.
    /// </remarks>
    public static async Task InvokeAsync(
        HttpContext context,
        SignInShopperReceipt receipt,
        RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(next);

        if (!IsSignIn(context.Request))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = original;
        }

        if (context.Response.StatusCode == StatusCodes.Status202Accepted && receipt.Result is not null)
        {
            context.Response.ContentType = "application/json";
            context.Response.Headers.ContentLength = null;
            await JsonSerializer.SerializeAsync(original, receipt.Result, Json, context.RequestAborted)
                .ConfigureAwait(false);
            return;
        }

        if (buffer.Length == 0)
            return;

        buffer.Position = 0;
        context.Response.ContentLength = buffer.Length;
        await buffer.CopyToAsync(original, context.RequestAborted).ConfigureAwait(false);
    }
}
