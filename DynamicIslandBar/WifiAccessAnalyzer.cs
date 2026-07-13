namespace DynamicIslandBar
{
    public enum WifiAccessIssue
    {
        None,
        LocationPermissionRequired,
        ElevatedAccessRequired
    }

    public sealed class WifiNetworkSnapshot
    {
        public required List<WifiNetwork> Networks { get; init; }
        public required WifiAccessIssue AccessIssue { get; init; }
        public string? CurrentSsid { get; init; }
    }

    public static class WifiAccessAnalyzer
    {
        public static WifiAccessIssue DetectIssue(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return WifiAccessIssue.None;
            }

            if (output.Contains("location permission", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("定位", StringComparison.OrdinalIgnoreCase))
            {
                return WifiAccessIssue.LocationPermissionRequired;
            }

            if (output.Contains("requires elevation", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("管理员", StringComparison.OrdinalIgnoreCase))
            {
                return WifiAccessIssue.ElevatedAccessRequired;
            }

            return WifiAccessIssue.None;
        }
    }
}
