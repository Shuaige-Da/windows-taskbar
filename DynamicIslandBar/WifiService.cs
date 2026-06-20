using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace DynamicIslandBar
{
    public class WifiNetwork
    {
        public string Ssid { get; set; } = "";
        public string SignalStrength { get; set; } = "";
        public bool IsSecured { get; set; }
        public bool IsConnected { get; set; }
    }

    public static class WifiService
    {
        public static string? GetCurrentSsid()
        {
            try
            {
                var output = RunNetsh("wlan show interfaces");
                var currentSsid = WifiTextParser.ParseCurrentSsid(output);
                if (!string.IsNullOrWhiteSpace(currentSsid))
                {
                    return currentSsid;
                }
            }
            catch
            {
            }

            return GetCurrentProfileName();
        }

        public static bool IsWifiEnabled()
        {
            try
            {
                var output = RunNetsh("wlan show interfaces");
                return !output.Contains("There are no interfaces", StringComparison.OrdinalIgnoreCase) &&
                       !output.Contains("没有接口", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static List<WifiNetwork> ScanNetworks()
        {
            try
            {
                var output = RunNetsh("wlan show networks mode=bssid");
                var currentSsid = GetCurrentSsid();
                return ParseNetworks(output, currentSsid);
            }
            catch
            {
                return [];
            }
        }

        public static WifiNetworkSnapshot GetNetworkSnapshot()
        {
            var currentSsid = GetCurrentSsid();
            var scanOutput = RunNetsh("wlan show networks mode=bssid");
            var accessIssue = WifiAccessAnalyzer.DetectIssue(scanOutput);
            var scannedNetworks = ParseNetworks(scanOutput, currentSsid);
            var savedProfiles = GetSavedProfiles();

            return new WifiNetworkSnapshot
            {
                Networks = WifiNetworkListBuilder.Build(scannedNetworks, savedProfiles, currentSsid),
                AccessIssue = accessIssue
            };
        }

        public static List<string> GetSavedProfiles()
        {
            try
            {
                var output = RunNetsh("wlan show profiles");
                return WifiTextParser.ParseSavedProfiles(output);
            }
            catch
            {
                return [];
            }
        }

        public static List<WifiNetwork> GetNetworks()
        {
            return GetNetworkSnapshot().Networks;
        }

        public static void Connect(string ssid)
        {
            try
            {
                RunNetsh($"wlan connect name=\"{ssid}\"");
            }
            catch
            {
            }
        }

        public static void Disconnect()
        {
            try
            {
                RunNetsh("wlan disconnect");
            }
            catch
            {
            }
        }

        private static string RunNetsh(string args)
        {
            try
            {
                var process = new ProcessStartInfo("netsh", args)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var proc = Process.Start(process);
                if (proc == null)
                {
                    return string.Empty;
                }

                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000);
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string? GetCurrentProfileName()
        {
            try
            {
                var process = new ProcessStartInfo("powershell", "-NoProfile -Command \"Get-NetConnectionProfile | Select-Object -First 1 -ExpandProperty Name\"")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var proc = Process.Start(process);
                if (proc == null)
                {
                    return null;
                }

                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000);
                return WifiTextParser.ParseCurrentProfileName(output);
            }
            catch
            {
                return null;
            }
        }

        private static List<WifiNetwork> ParseNetworks(string output, string? currentSsid)
        {
            var networks = new List<WifiNetwork>();
            var blocks = output.Split(["\nSSID "], StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in blocks)
            {
                if (!block.Contains(':'))
                {
                    continue;
                }

                var network = new WifiNetwork();

                var ssidMatch = Regex.Match(block, @"^(\d+)\s*:\s*(.+)", RegexOptions.Multiline);
                if (ssidMatch.Success)
                {
                    network.Ssid = ssidMatch.Groups[2].Value.Trim();
                }
                else
                {
                    var firstLine = block.TrimStart().Split('\n')[0];
                    var colonIndex = firstLine.IndexOf(':');
                    if (colonIndex >= 0)
                    {
                        network.Ssid = firstLine[(colonIndex + 1)..].Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(network.Ssid))
                {
                    continue;
                }

                var signalMatch = Regex.Match(block, @"Signal\s*:\s*(\d+)%", RegexOptions.IgnoreCase);
                if (signalMatch.Success)
                {
                    network.SignalStrength = signalMatch.Groups[1].Value + "%";
                }

                var authMatch = Regex.Match(block, @"Authentication\s*:\s*(.+)", RegexOptions.IgnoreCase);
                if (authMatch.Success)
                {
                    var auth = authMatch.Groups[1].Value.Trim();
                    network.IsSecured = !auth.Contains("Open", StringComparison.OrdinalIgnoreCase) &&
                                        !auth.Contains("开放", StringComparison.OrdinalIgnoreCase);
                }

                network.IsConnected = string.Equals(network.Ssid, currentSsid, StringComparison.OrdinalIgnoreCase);
                networks.Add(network);
            }

            return networks;
        }
    }
}
