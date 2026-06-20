using System.IO;
using System.Text.Json;

namespace DynamicIslandBar
{
    public enum AppPermission
    {
        WifiNearbyNetworks,
        WifiControl,
        AudioControl,
        RunningApps
    }

    public enum PermissionDecision
    {
        AllowCurrent,
        Deny,
        AllowAll
    }

    public sealed class PermissionState
    {
        public bool AllowAll { get; set; }
        public Dictionary<AppPermission, bool> Decisions { get; } = [];
    }

    public sealed class PermissionCheckResult
    {
        public required bool IsGranted { get; init; }
        public required bool ShouldPrompt { get; init; }
    }

    public static class PermissionDecisionEngine
    {
        public static PermissionCheckResult Check(PermissionState state, AppPermission permission)
        {
            if (state.AllowAll)
            {
                return new PermissionCheckResult
                {
                    IsGranted = true,
                    ShouldPrompt = false
                };
            }

            if (state.Decisions.TryGetValue(permission, out var granted))
            {
                return new PermissionCheckResult
                {
                    IsGranted = granted,
                    ShouldPrompt = false
                };
            }

            return new PermissionCheckResult
            {
                IsGranted = false,
                ShouldPrompt = true
            };
        }

        public static void ApplyDecision(PermissionState state, AppPermission permission, PermissionDecision decision)
        {
            switch (decision)
            {
                case PermissionDecision.AllowCurrent:
                    state.Decisions[permission] = true;
                    break;
                case PermissionDecision.Deny:
                    state.Decisions[permission] = false;
                    break;
                case PermissionDecision.AllowAll:
                    state.AllowAll = true;
                    state.Decisions.Clear();
                    break;
            }
        }
    }

    public static class PermissionService
    {
        private static readonly string PermissionFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DynamicIslandBar",
            "permissions.json");

        private static PermissionState _state = new();
        private static bool _initialized;

        public static void Initialize(bool defaultAllowAll)
        {
            if (_initialized)
            {
                return;
            }

            _state = LoadState(defaultAllowAll);
            _initialized = true;
        }

        public static PermissionCheckResult Check(AppPermission permission)
        {
            EnsureInitialized();
            return PermissionDecisionEngine.Check(_state, permission);
        }

        public static void ApplyDecision(AppPermission permission, PermissionDecision decision)
        {
            EnsureInitialized();
            PermissionDecisionEngine.ApplyDecision(_state, permission, decision);
            SaveState();
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize(defaultAllowAll: true);
            }
        }

        private static PermissionState LoadState(bool defaultAllowAll)
        {
            try
            {
                if (File.Exists(PermissionFilePath))
                {
                    var json = File.ReadAllText(PermissionFilePath);
                    var model = JsonSerializer.Deserialize<PermissionStoreModel>(json);
                    if (model != null)
                    {
                        return model.ToPermissionState();
                    }
                }
            }
            catch
            {
            }

            var state = new PermissionState { AllowAll = defaultAllowAll };
            SaveState(state);
            return state;
        }

        private static void SaveState()
        {
            SaveState(_state);
        }

        private static void SaveState(PermissionState state)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PermissionFilePath)!);
                var model = PermissionStoreModel.FromState(state);
                var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PermissionFilePath, json);
            }
            catch
            {
            }
        }

        private sealed class PermissionStoreModel
        {
            public bool AllowAll { get; set; }
            public Dictionary<string, bool> Decisions { get; set; } = [];

            public PermissionState ToPermissionState()
            {
                var state = new PermissionState { AllowAll = AllowAll };
                foreach (var pair in Decisions)
                {
                    if (Enum.TryParse<AppPermission>(pair.Key, out var permission))
                    {
                        state.Decisions[permission] = pair.Value;
                    }
                }

                return state;
            }

            public static PermissionStoreModel FromState(PermissionState state)
            {
                return new PermissionStoreModel
                {
                    AllowAll = state.AllowAll,
                    Decisions = state.Decisions.ToDictionary(
                        pair => pair.Key.ToString(),
                        pair => pair.Value)
                };
            }
        }
    }
}
