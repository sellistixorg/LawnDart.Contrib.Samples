namespace HarborStreet.BookSupport.SnippetDemo;

/// <summary>
/// Tiny type used only to demonstrate Quarto code includes in the manuscript.
/// The runnable samples live in the topic folders next to this one.
/// </summary>
public static class LastCopyPolicy
{
    /// <summary>
// start snippet last-copy-policy
    /// Scarce titles must never oversell. Exactly one concurrent checkout may
    /// reserve the last available unit; everyone else fails cleanly.
    /// </summary>
    public static bool CanReserve(int availableUnits, int requestedUnits)
    {
        if (requestedUnits <= 0)
            return false;

        return availableUnits >= requestedUnits;
    }

    public static string DescribeFailure(int availableUnits)
        => availableUnits <= 0
            ? "That signed copy is gone—another reader got there first."
            : "Not enough stock for this checkout.";
}
// end snippet last-copy-policy
