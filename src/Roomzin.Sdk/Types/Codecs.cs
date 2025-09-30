using System;
using System.Collections.Generic;
using System.Linq;

namespace Roomzin.Sdk.Types
{
    /// <summary>
    /// Codecs data structure (matches Go/Java)
    /// </summary>
    public class Codecs
    {
        public List<string> RateFeatures { get; set; }

        public Codecs(List<string> rateFeatures)
        {
            RateFeatures = rateFeatures ?? new List<string>();
        }
    }

    /// <summary>
    /// Static validation functions that work with Codecs instances
    /// </summary>
    public static class CodecsValidation
    {
        /// <summary>
        /// Validates rate featurelation policies against codecs
        /// </summary>
        public static (bool isValid, string errorMessage) ValidateRateFeatures(Codecs? codecs, IEnumerable<string> input)
        {
            if (codecs == null)
                return (false, "Codecs not initialized");

            if (input == null)
                return (true, string.Empty);

            var invalid = input.Where(rate => !codecs.RateFeatures.Contains(rate)).ToList();

            if (invalid.Count > 0)
                return (false, "Invalid rate features: " + string.Join(", ", invalid));

            return (true, string.Empty);
        }
    }
}