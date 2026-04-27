using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace StockExchangeSimulator.Services
{
    public static class EnvService
    {
        private static readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public static void Load(string filePath)
        {
            _values.Clear();

            if (!File.Exists(filePath))
                return;

            var lines = File.ReadAllLines(filePath);

            foreach (var rawLine in lines)
            {
                var line = rawLine?.Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("#"))
                    continue;

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                    continue;

                string key = line[..separatorIndex].Trim();
                string value = line[(separatorIndex + 1)..].Trim();

                _values[key] = value;
            }
        }

        public static string Get(string key, string defaultValue = "")
        {
            return _values.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            return int.TryParse(Get(key), out int value) ? value : defaultValue;
        }

        public static bool GetBool(string key, bool defaultValue = false)
        {
            return bool.TryParse(Get(key), out bool value) ? value : defaultValue;
        }

        public static decimal GetDecimal(string key, decimal defaultValue = 0m)
        {
            string raw = Get(key);

            if (string.IsNullOrWhiteSpace(raw))
                return defaultValue;

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal invariantValue))
                return invariantValue;

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal localValue))
                return localValue;

            return defaultValue;
        }
    }
}