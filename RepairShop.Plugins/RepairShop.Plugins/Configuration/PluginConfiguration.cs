using System;
using System.Collections.Generic;
using System.Globalization;

namespace RepairShop.Plugins.Configuration
{
    /// <summary>
    /// Parses semicolon-delimited, non-secret step configuration.
    /// Example: completedStatusValue=100000002
    /// </summary>
    internal sealed class PluginConfiguration
    {
        private const string CompletedStatusValueKey = "completedStatusValue";

        internal PluginConfiguration(string unsecureConfiguration)
        {
            IReadOnlyDictionary<string, string> values = Parse(unsecureConfiguration);

            string configuredValue;
            int completedStatusValue;
            if (values.TryGetValue(CompletedStatusValueKey, out configuredValue) &&
                int.TryParse(
                    configuredValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out completedStatusValue))
            {
                CompletedStatusValue = completedStatusValue;
            }
        }

        internal int? CompletedStatusValue { get; }

        private static IReadOnlyDictionary<string, string> Parse(string configuration)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(configuration))
            {
                return values;
            }

            foreach (string item in configuration.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int separatorIndex = item.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex == item.Length - 1)
                {
                    continue;
                }

                string key = item.Substring(0, separatorIndex).Trim();
                string value = item.Substring(separatorIndex + 1).Trim();

                if (key.Length > 0 && value.Length > 0)
                {
                    values[key] = value;
                }
            }

            return values;
        }
    }
}
