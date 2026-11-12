using System;
using System.Globalization;

namespace ASI.Basecode.WebApp.Formatting
{
    /// <summary>Formats UTC database timestamps for display in Asia/Manila.</summary>
    public static class ManilaTimeFormatter
    {
        private static readonly TimeZoneInfo Manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

        /// <summary>Formats a UTC timestamp, treating unspecified SQL datetime2 values as UTC.</summary>
        /// <exception cref="ArgumentException">The timestamp is marked as local time.</exception>
        public static string Format(DateTime value)
        {
            if (value.Kind == DateTimeKind.Local)
                throw new ArgumentException("Use a UTC or unspecified database timestamp, or provide a DateTimeOffset.", nameof(value));

            return Format(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));
        }

        public static string Format(DateTime? value) => value.HasValue ? Format(value.Value) : string.Empty;

        public static string Format(DateTimeOffset value) =>
            TimeZoneInfo.ConvertTime(value, Manila).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " Asia/Manila";

        public static string Format(DateTimeOffset? value) => value.HasValue ? Format(value.Value) : string.Empty;

        /// <summary>
        /// Converts an Asia/Manila wall-clock value entered in a form control to the UTC value stored in SQL.
        /// A value already marked as UTC is returned unchanged.
        /// </summary>
        public static DateTime ToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
                return value;

            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), Manila);
        }

        /// <summary>
        /// Converts a UTC database timestamp to the Asia/Manila wall-clock value a form control edits.
        /// </summary>
        /// <exception cref="ArgumentException">The timestamp is marked as local time.</exception>
        public static DateTime ToLocal(DateTime value)
        {
            if (value.Kind == DateTimeKind.Local)
                throw new ArgumentException("Use a UTC or unspecified database timestamp.", nameof(value));

            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), Manila);
        }
    }
}
