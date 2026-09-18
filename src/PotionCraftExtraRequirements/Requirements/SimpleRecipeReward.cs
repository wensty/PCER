using System;
using System.Globalization;

namespace PotionCraftExtraRequirements;

internal static class SimpleRecipeReward
{
    private const string RawMaximumPrefix = "PCER_SimpleRecipeRaw:";

    public static string EncodeRawMaximum(float rawMaximum)
        => RawMaximumPrefix + rawMaximum.ToString("R", CultureInfo.InvariantCulture);

    public static bool TryReadRawMaximum(string encoded, out float rawMaximum)
    {
        rawMaximum = 0;
        return encoded != null && encoded.StartsWith(RawMaximumPrefix, StringComparison.Ordinal)
            && float.TryParse(encoded.Substring(RawMaximumPrefix.Length), NumberStyles.Float,
                CultureInfo.InvariantCulture, out rawMaximum)
            && !float.IsNaN(rawMaximum) && !float.IsInfinity(rawMaximum);
    }

    public static bool IsEfficient(int actual, int displayedMaximum, float rawMaximum)
    {
        return TryGetEfficiencyThreshold(displayedMaximum, rawMaximum, out int threshold)
            && actual > 0 && actual <= threshold;
    }

    public static bool TryGetEfficiencyThreshold(int displayedMaximum, float rawMaximum, out int threshold)
    {
        threshold = 0;
        if (displayedMaximum < 1 || rawMaximum <= 0f
            || float.IsNaN(rawMaximum) || float.IsInfinity(rawMaximum))
            return false;
        threshold = (int)Math.Min(displayedMaximum, Math.Floor(rawMaximum * 0.6f));
        return threshold >= 1;
    }
}
