namespace OfficeAutomation.Utilities
{
    public static class PersianTextNormalizer
    {
        public static string Normalize(string? input, bool trim = true)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var normalized = input
                .Replace('ي', 'ی')
                .Replace('ك', 'ک')
                .Replace('ؤ', 'و')
                .Replace('إ', 'ا')
                .Replace('أ', 'ا')
                .Replace('۰', '0')
                .Replace('۱', '1')
                .Replace('۲', '2')
                .Replace('۳', '3')
                .Replace('۴', '4')
                .Replace('۵', '5')
                .Replace('۶', '6')
                .Replace('۷', '7')
                .Replace('۸', '8')
                .Replace('۹', '9')
                .Replace('٠', '0')
                .Replace('١', '1')
                .Replace('٢', '2')
                .Replace('٣', '3')
                .Replace('٤', '4')
                .Replace('٥', '5')
                .Replace('٦', '6')
                .Replace('٧', '7')
                .Replace('٨', '8')
                .Replace('٩', '9');

            return trim ? normalized.Trim() : normalized;
        }
    }
}
