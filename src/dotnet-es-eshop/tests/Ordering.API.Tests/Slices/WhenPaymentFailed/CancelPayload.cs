using System.Text.Json;
using System.Text.RegularExpressions;

namespace LawnDart.Samples.EShop.Ordering.API.Tests.Slices.WhenPaymentFailed;

static class CancelPayload
{
    // A timestamp or an identifier can contain the same four digits. Those values are not the card.
    private static readonly Regex Clock = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void AssertOmitsCardLastFour(string json)
    {
        using var document = JsonDocument.Parse(json);
        AssertOmitsCardLastFour(document.RootElement);
    }

    private static void AssertOmitsCardLastFour(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    AssertOmitsCardLastFour(property.Value);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    AssertOmitsCardLastFour(item);
                break;
            case JsonValueKind.String:
                var text = element.GetString() ?? "";
                if (Clock.IsMatch(text)
                    || Guid.TryParseExact(text, "D", out _)
                    || Guid.TryParseExact(text, "N", out _))
                    return;
                Assert.DoesNotContain("4444", text, StringComparison.Ordinal);
                break;
        }
    }
}
