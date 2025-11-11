using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device_Recording_List
{
    public static class TimeSpanExtensions
    {
        /// <summary>
        /// Converts a number of seconds into a human-readable string.
        /// If seconds is 3600 or more, returns a decimal hour value.
        /// If seconds is 86400 or more, returns a decimal day value.
        /// Otherwise, returns minutes or seconds as appropriate.
        /// </summary>
        public static string ToHumanReadableTime(this long seconds)
        {
            if (seconds >= 86400) // 1 day or more
            {
                double days = seconds / 86400.0;
                return $"{days:F2} days";
            }
            else if (seconds >= 3600) // 1 hour or more
            {
                double hours = seconds / 3600.0;
                return $"{hours:F2} hours";
            }
            else if (seconds >= 60)
            {
                double minutes = seconds / 60.0;
                // For minutes, using whole numbers is acceptable.
                return $"{Math.Round(minutes)} minutes";
            }
            else
            {
                return $"{seconds} seconds";
            }
        }
    }
}
