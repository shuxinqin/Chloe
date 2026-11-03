namespace Chloe.SonnetDB
{
    internal static class Utils
    {
        public static void EnsureSupportedMappingType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
        }

        public static void EnsureSupportedValue(object value)
        {
        }

        public static object NormalizeParameterValue(object value)
        {
            // Chloe models CLR DateTime as a wall-clock value; DATETIME2 preserves its ticks.
            if (value is DateTime local && local.Kind == DateTimeKind.Local)
                return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return value;
        }

        public static string QuoteTable(string name, string schema) => string.IsNullOrEmpty(schema) ? QuoteName(name) : QuoteName(schema) + "." + QuoteName(name);

        public static string QuoteName(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("name");

            return string.Concat(UtilConstants.LeftQuoteChar, name.Replace("\"", "\"\""), UtilConstants.RightQuoteChar);
        }
    }
}
