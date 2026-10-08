namespace Chloe.SonnetDB
{
    internal static class Utils
    {
        public static void EnsureSupportedMappingType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(DateTimeOffset))
                throw new NotSupportedException("SonnetDB's general ADO.NET reader returns DateTime values for DATETIME columns, which are incompatible with Chloe's DateTimeOffset value converter. See https://github.com/IoTSharp/SonnetDB/issues/251.");
            if (type == typeof(TimeSpan))
                throw new NotSupportedException("SonnetDB TIME represents a time of day and cannot preserve the full CLR TimeSpan duration range. See https://github.com/IoTSharp/SonnetDB/issues/252.");
        }

        public static void EnsureSupportedValue(object value)
        {
            if (value is DateTime dateTime && dateTime.Ticks % TimeSpan.TicksPerMillisecond != 0)
                throw new NotSupportedException("SonnetDB DATETIME currently stores millisecond precision and cannot preserve submillisecond ticks. See https://github.com/IoTSharp/SonnetDB/issues/236.");
        }

        public static object NormalizeParameterValue(object value)
        {
            if (value is DateTime dateTime && dateTime.Kind == DateTimeKind.Local)
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);

            return value;
        }

        public static string QuoteName(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("name");

            return string.Concat(UtilConstants.LeftQuoteChar, name.Replace("\"", "\"\""), UtilConstants.RightQuoteChar);
        }
    }
}
