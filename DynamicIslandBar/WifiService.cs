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
        public bool HasSavedProfile { get; set; }
    }

    public enum WifiConnectionResult
    {
        Connected,
        ProfileMissing,
        Failed,
        TimedOut
    }

    public static class WifiService
    {
        private const int CommandTimeoutMilliseconds = 5000;
        private const string CurrentWifiProfileCommand =
            "-NoProfile -Command \"Get-NetConnectionProfile | Where-Object { $_.InterfaceAlias -match 'Wi-?Fi|WLAN|无线' } | Select-Object -First 1 -ExpandProperty Name\"";

        private sealed record CommandResult(string Output, int ExitCode, bool TimedOut)
        {
            public bool Succeeded => !TimedOut && ExitCode == 0;
        }

        public static string? GetCurrentSsid()
        {
            var currentSsid = WifiTextParser.ParseCurrentSsid(RunNetsh("wlan show interfaces").Output);
            if (!string.IsNullOrWhiteSpace(currentSsid))
            {
                return currentSsid;
            }

            return GetCurrentProfileName();
        }

        public static bool IsWifiEnabled()
        {
            var result = RunNetsh("wlan show interfaces");
            return result.Succeeded
                && !result.Output.Contains("There are no interfaces", StringComparison.OrdinalIgnoreCase)
                && !result.Output.Contains("没有接口", StringComparison.OrdinalIgnoreCase);
        }

        public static List<WifiNetwork> ScanNetworks()
        {
            var output = RunNetsh("wlan show networks mode=bssid").Output;
            return ParseNetworks(output, GetCurrentSsid());
        }

        public static WifiNetworkSnapshot GetNetworkSnapshot()
        {
            var currentSsid = GetCurrentSsid();
            var scanOutput = RunNetsh("wlan show networks mode=bssid").Output;
            var accessIssue = WifiAccessAnalyzer.DetectIssue(scanOutput);
            var scannedNetworks = ParseNetworks(scanOutput, currentSsid);
            var savedProfiles = GetSavedProfiles();

            return new WifiNetworkSnapshot
            {
                Networks = WifiNetworkListBuilder.Build(scannedNetworks, savedProfiles, currentSsid),
                AccessIssue = accessIssue,
                CurrentSsid = currentSsid
            };
        }

        public static List<string> GetSavedProfiles()
        {
            return WifiTextParser.ParseSavedProfiles(RunNetsh("wlan show profiles").Output);
        }

        public static List<WifiNetwork> GetNetworks()
        {
            return GetNetworkSnapshot().Networks;
        }

        public static async Task<WifiConnectionResult> ConnectAsync(string ssid)
        {
            if (!GetSavedProfiles().Contains(ssid, StringComparer.OrdinalIgnoreCase))
            {
                return WifiConnectionResult.ProfileMissing;
            }

            var result = await Task.Run(() => RunNetsh($"wlan connect name=\"{ssid}\" ssid=\"{ssid}\""))
                .ConfigureAwait(false);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Output))
            {
                return WifiConnectionResult.Failed;
            }

            for (var attempt = 0; attempt < 12; attempt++)
            {
                await Task.Delay(350).ConfigureAwait(false);
                if (string.Equals(GetCurrentSsid(), ssid, StringComparison.OrdinalIgnoreCase))
                {
                    return WifiConnectionResult.Connected;
                }
            }

            return WifiConnectionResult.TimedOut;
        }

        public static void Disconnect()
        {
            RunNetsh("wlan disconnect");
        }

        public static async Task<bool> DisconnectAsync()
        {
            await Task.Run(Disconnect).ConfigureAwait(false);
            for (var attempt = 0; attempt < 10; attempt++)
            {
                await Task.Delay(250).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(GetCurrentSsid()))
                {
                    return true;
                }
            }

            return false;
        }

        private static CommandResult RunNetsh(string args) => RunCommand("netsh", args);

        private static CommandResult RunCommand(string fileName, string arguments)
        {
            try
            {
                var processInfo = new ProcessStartInfo(fileName, arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(processInfo);
                if (process is null)
                {
                    return new CommandResult(string.Empty, -1, false);
                }

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(CommandTimeoutMilliseconds))
                {
                    TryTerminateProcess(process);
                    AppDiagnostics.Error($"CommandTimeout-{fileName}", new TimeoutException(arguments));
                    return new CommandResult(string.Empty, -1, true);
                }

                Task.WaitAll(outputTask, errorTask);
                var output = outputTask.Result;
                var error = errorTask.Result;
                if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
                {
                    AppDiagnostics.Error(
                        $"CommandFailed-{fileName}",
                        new InvalidOperationException($"Exit code {process.ExitCode}: {error.Trim()}"));
                }

                return new CommandResult(output, process.ExitCode, false);
            }
            catch (Exception ex)
            {
                AppDiagnostics.Error($"Command-{fileName}", ex);
                return new CommandResult(string.Empty, -1, false);
            }
        }

        private static void TryTerminateProcess(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(1000);
            }
            catch (Exception ex)
            {
                AppDiagnostics.Error("TerminateCommandProcess", ex);
            }
        }

        private static string? GetCurrentProfileName()
        {
            var result = RunCommand("powershell", CurrentWifiProfileCommand);
            return result.Succeeded
                ? WifiTextParser.ParseCurrentProfileName(result.Output)
                : null;
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
