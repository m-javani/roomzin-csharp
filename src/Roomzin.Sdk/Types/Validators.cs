using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Roomzin.Sdk.Types
{
    /// <summary>
    /// Validation methods for request payloads
    /// </summary>
    public static class Validators
    {
        private static readonly Regex DateRegex = new Regex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

        /// <summary>
        /// Validates a date string in YYYY-MM-DD format
        /// </summary>
        public static (bool isValid, string errorMessage) ValidateDate(string date)
        {
            var errors = new List<string>();

            // Check format YYYY-MM-DD
            if (!DateRegex.IsMatch(date))
            {
                errors.Add($"invalid date format: {date}, expected YYYY-MM-DD");
            }
            else
            {
                // Parse to ensure valid date
                if (!DateTime.TryParse(date, out var parsedDate))
                {
                    errors.Add($"invalid date: {date}");
                }
                else
                {
                    // Check if date is in the past
                    var today = DateTime.Today;
                    if (parsedDate < today)
                    {
                        errors.Add($"date {date} is in the past");
                    }

                    // Check if date is beyond 365 days
                    var oneYearFromNow = today.AddYears(1);
                    if (parsedDate > oneYearFromNow)
                    {
                        errors.Add($"date {date} is beyond 365 days from today");
                    }
                }
            }

            if (errors.Count > 0)
                return (false, string.Join("; ", errors));

            return (true, string.Empty);
        }

        /// <summary>
        /// Validates multiple date strings
        /// </summary>
        public static (bool isValid, string errorMessage) ValidateDates(IEnumerable<string> dates)
        {
            if (dates == null)
                return (true, string.Empty);

            var errors = dates
                .Select(date => ValidateDate(date))
                .Where(result => !result.isValid)
                .Select(result => result.errorMessage)
                .ToList();

            if (errors.Count > 0)
                return (false, "Date errors: " + string.Join("; ", errors));

            return (true, string.Empty);
        }
    }
}