using System.Text.RegularExpressions;

namespace DynamicIslandBar
{
    public static class WifiTextParser
    {
        private static readonly Regex SavedProfileRegex = new(
            @"^\s*(?:All User Profile|所有用户配置文件)\s*:\s*(.+?)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static List<string> ParseSavedProfiles(string output)
        {
            var profiles = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in SavedProfileRegex.Matches(output))
            {
                var profile = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(profile) && seen.Add(profile))
                {
                    profiles.Add(profile);
                }
            }

            return profiles;
        }

        public static string? ParseCurrentSsid(string output)
        {
            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.Contains("BSSID", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!line.Contains("SSID", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0 || colonIndex == line.Length - 1)
                {
                    continue;
                }

                return line[(colonIndex + 1)..].Trim();
            }

            return null;
        }

        public static string? ParseCurrentProfileName(string output)
        {
            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line;
                }
            }

            return null;
        }
    }

    public static class WifiNetworkListBuilder
    {
        public static List<WifiNetwork> Build(
            IEnumerable<WifiNetwork> scannedNetworks,
            IEnumerable<string> savedProfiles,
            string? currentSsid)
        {
            var merged = new Dictionary<string, WifiNetwork>(StringComparer.OrdinalIgnoreCase);

            foreach (var network in scannedNetworks)
            {
                if (string.IsNullOrWhiteSpace(network.Ssid))
                {
                    continue;
                }

                merged[network.Ssid] = new WifiNetwork
                {
                    Ssid = network.Ssid.Trim(),
                    SignalStrength = string.IsNullOrWhiteSpace(network.SignalStrength) ? "可用" : network.SignalStrength,
                    IsSecured = network.IsSecured,
                    IsConnected = string.Equals(network.Ssid, currentSsid, StringComparison.OrdinalIgnoreCase)
                };
            }

            foreach (var profile in savedProfiles)
            {
                if (string.IsNullOrWhiteSpace(profile) || merged.ContainsKey(profile))
                {
                    continue;
                }

                merged[profile] = new WifiNetwork
                {
                    Ssid = profile.Trim(),
                    SignalStrength = "已保存",
                    IsSecured = true,
                    IsConnected = string.Equals(profile, currentSsid, StringComparison.OrdinalIgnoreCase)
                };
            }

            if (!string.IsNullOrWhiteSpace(currentSsid) && !merged.ContainsKey(currentSsid))
            {
                merged[currentSsid] = new WifiNetwork
                {
                    Ssid = currentSsid,
                    SignalStrength = "已连接",
                    IsSecured = true,
                    IsConnected = true
                };
            }

            return merged.Values
                .OrderByDescending(network => network.IsConnected)
                .ThenByDescending(network => ParseSignal(network.SignalStrength))
                .ThenBy(network => network.Ssid, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static int ParseSignal(string signalStrength)
        {
            var normalized = signalStrength.Replace("%", string.Empty).Trim();
            return int.TryParse(normalized, out var value) ? value : 0;
        }
    }
}
