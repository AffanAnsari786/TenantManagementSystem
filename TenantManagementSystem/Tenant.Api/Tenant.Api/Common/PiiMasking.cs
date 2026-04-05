namespace Tenant.Api.Common;

/// <summary>
/// Centralised masking helpers for personally identifiable information.
/// All read paths that surface PII to the client or to PDFs must use these
/// helpers — never reimplement masking inline.
/// </summary>
public static class PiiMasking
{
    /// <summary>
    /// Masks an Aadhaar number for display. Accepts raw digits (12) or the
    /// spaced/hyphenated variants. Returns <c>null</c> when the input is
    /// null/blank and a best-effort masked form when the length is off
    /// (rather than throwing — callers should not assume well-formed input).
    /// Format: first two digits + masked middle + last two digits, e.g.
    /// <c>12** **** **90</c>.
    /// </summary>
    public static string? MaskAadhaar(string? aadhaar)
    {
        if (string.IsNullOrWhiteSpace(aadhaar)) return null;

        var clean = aadhaar.Replace(" ", string.Empty).Replace("-", string.Empty);

        // Standard Indian Aadhaar is exactly 12 digits.
        if (clean.Length == 12)
        {
            return $"{clean.Substring(0, 2)}** **** **{clean.Substring(10, 2)}";
        }

        // Defensive fallback for malformed values already in the DB.
        if (clean.Length <= 4)
        {
            return new string('*', clean.Length);
        }

        var firstTwo = clean.Substring(0, 2);
        var lastTwo = clean.Substring(clean.Length - 2);
        var middleMaskLength = clean.Length - 4;
        return $"{firstTwo}{new string('*', middleMaskLength)}{lastTwo}";
    }

    /// <summary>
    /// Same as <see cref="MaskAadhaar"/>, but returns a display-friendly
    /// default ("Not Provided") instead of <c>null</c>. Use in UI/PDF surfaces.
    /// </summary>
    public static string MaskAadhaarForDisplay(string? aadhaar)
        => MaskAadhaar(aadhaar) ?? "Not Provided";

    /// <summary>
    /// Strips spaces and hyphens from an Aadhaar before persisting, so the DB
    /// stores a single canonical form.
    /// </summary>
    public static string? NormaliseAadhaar(string? aadhaar)
    {
        if (string.IsNullOrWhiteSpace(aadhaar)) return null;
        return aadhaar.Replace(" ", string.Empty).Replace("-", string.Empty);
    }
}
