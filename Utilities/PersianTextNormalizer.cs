namespace OfficeAutomation.Utilities
{
    /// <summary>
    /// Normalizes Persian/Arabic text so substring matching in selectors is robust
    /// against the visually-identical Unicode variants used across the Tax Portal
    /// ("درخواست فایل خروجی", "تایید", "خرید داخلی", "موفق", ...). Mirrors the proven
    /// _norm_text helper in the reference Python exporter (scripts/tax_exporter.py).
    /// </summary>
    public static class PersianTextNormalizer
    {
        /// <summary>
        /// Canonical, reusable normalization of Persian/Arabic glyph variants:
        /// Arabic yeh (ي U+064A) -> Persian yeh (ی U+06CC), Arabic kaf (ك U+0643) ->
        /// Persian kaf (ک U+06A9), Arabic/Persian heh variants -> plain heh (ه U+0647),
        /// and removal of zero-width joiners/non-joiners (U+200C/U+200D/U+200B) and
        /// tatweel (ـ U+0640). Also folds Persian/Arabic-Indic digits to ASCII.
        /// </summary>
        public static string NormalizeText(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var normalized = input
                .Replace('ي', 'ی')      // Arabic yeh -> Farsi yeh
                .Replace('ك', 'ک')      // Arabic kaf -> Farsi kaf
                .Replace("ه\u200C", "ه") // ARABIC HEH + ZWNJ -> plain heh
                .Replace("ه\u200D", "ه") // ARABIC HEH + ZWJ -> plain heh
                .Replace('ۀ', 'ه')   // HEH WITH YEH ABOVE -> plain heh
                .Replace('أ', 'ا')   // ALEF WITH HAMZA ABOVE -> ALEF
                .Replace('إ', 'ا')   // ALEF WITH HAMZA BELOW -> ALEF
                .Replace('ؤ', 'و')   // WAW WITH HAMZA ABOVE -> WAW
                .Replace('ة', 'ه')   // TEH MARBUTA -> HEH
                .Replace('آ', 'ا')   // ALEF WITH MADDA -> ALEF (canonical ا for matching)
                .Replace("\u200C", string.Empty) // ZWNJ
                .Replace("\u200D", string.Empty) // ZWJ
                .Replace("\u200B", string.Empty) // ZWSP
                .Replace("\u0640", string.Empty) // Tatweel
                .Replace('۰', '0').Replace('٠', '0')
                .Replace('۱', '1').Replace('١', '1')
                .Replace('۲', '2').Replace('٢', '2')
                .Replace('۳', '3').Replace('٣', '3')
                .Replace('۴', '4').Replace('٤', '4')
                .Replace('۵', '5').Replace('٥', '5')
                .Replace('۶', '6').Replace('٦', '6')
                .Replace('۷', '7').Replace('٧', '7')
                .Replace('۸', '8').Replace('٨', '8')
                .Replace('۹', '9').Replace('٩', '9')
                .Trim();

            return normalized;
        }

        /// <summary>
        /// Backwards-compatible overload used by existing callers. Delegates to
        /// <see cref="NormalizeText(string)"/> and trims when requested.
        /// </summary>
        public static string Normalize(string? input, bool trim = true)
        {
            var normalized = NormalizeText(input ?? string.Empty);
            return trim ? normalized : (input ?? string.Empty);
        }
    }
}
