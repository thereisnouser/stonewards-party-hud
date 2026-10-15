using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UIElements;

namespace StonewardsPartyHUD
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class PartyHudPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "community.stonewards.partyhud";
        public const string PluginName = "Stonewards Party HUD";
        public const string PluginVersion = "0.3.2";

        private const float RefreshSeconds = 0.2f;
        private const float PanelWidth = 278f;
        private const float HeaderHeight = 49f;
        private const float RowHeight = 67f;
        private const float CompanionRowHeight = 38f;
        private const float WallRowHeight = 66f;
        private const float FooterPadding = 8f;
        private const float BuildWindowWidth = 348f;
        private const float BuildWindowHeight = 434f;
        private const float BuildRingRowHeight = 42f;
        private const int BuildRingsPerPage = 6;
        private const float SettingsWidth = 500f;
        private const float SettingsHeight = 540f;
        private const float BottomHudSafeZone = 132f;
        private const float NativeRosterHeaderSafeZone = 130f;
        private const float NativeRosterPlayerRowSafeZone = 42f;
        private const float ReferenceScreenWidth = 1920f;
        private const float ReferenceScreenHeight = 1080f;
        // Match the approved compact on-screen proportions (~296px wide
        // at 1920x1080 with the default Scale and ReadabilityScale).
        // Scale proportionally to resolution, shrinking further only to fit.
        // Existing custom Scale / ReadabilityScale settings still apply.
        private const float PanelSizeBoost = 0.95f;

        // Native Stonewards UI colors sampled from Shop and Character Selection.
        private static readonly Color PanelFrame = new Color(0.72f, 0.64f, 0.48f);
        private static readonly Color PanelBackground = new Color(19f / 255f, 23f / 255f, 32f / 255f, 0.97f);
        private static readonly Color PanelHeader = new Color(76f / 255f, 46f / 255f, 48f / 255f);
        private static readonly Color PanelDivider = new Color(0.71f, 0.63f, 0.48f, 0.38f);
        private static readonly Color SlotBackground = new Color(0.25f, 0.32f, 0.38f);
        private static readonly Color BarTrack = new Color(0.16f, 0.18f, 0.20f, 1f);

        private enum PanelAnchor
        {
            TopLeft,
            BottomLeft
        }

        // Only components installed by this plugin are tracked and removed.
        private readonly List<PlayerEntry> _players = new List<PlayerEntry>(4);
        private readonly Dictionary<FirstPersonController, OutlineEntry> _teamOutlines =
            new Dictionary<FirstPersonController, OutlineEntry>();
        private readonly List<FirstPersonController> _expiredOutlines = new List<FirstPersonController>();
        // Read the host's existing server-side run data; never send network
        // messages or assume an ordinary client can inspect other builds.
        private readonly List<PlayerBuildEntry> _buildPlayers = new List<PlayerBuildEntry>();
        private bool _showBuilds;
        private float _nextBuildRefresh;
        private int _buildPlayerIndex;
        private int _buildPage;
        private bool _settingsOpen;
        private int _settingsTab; // 0: display, 1: layout, 2: hotkeys
        private int _capturingShortcut = -1;
        private string _settingsNotice = "";
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;
        // InventoryScreen does not inherit BaseMenu. Its UI Toolkit root
        // switches between DisplayStyle.Flex and None on opening/closing.
        private static readonly FieldInfo InventoryRootField =
            typeof(InventoryScreen).GetField("inventoryScreen",
                BindingFlags.Instance | BindingFlags.NonPublic);
        // Stonewards' LoadingScreen exposes its UI Toolkit element and static
        // instance in Assembly-CSharp. Use the actual window's visibility
        // instead of guessing based on scene names or temporary network state.
        private static readonly FieldInfo LoadingScreenInstanceField =
            typeof(LoadingScreen).GetField("Instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo LoadingScreenElementField =
            typeof(LoadingScreen).GetField("_loadingScreenElement",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo CharacterVisualsParentField =
            typeof(FirstPersonController).GetField("characterVisualsParent",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo OutlineMaskMaterialField =
            typeof(QuickOutline).GetField("outlineMaskMaterial",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo OutlineFillMaterialField =
            typeof(QuickOutline).GetField("outlineFillMaterial",
                BindingFlags.Instance | BindingFlags.NonPublic);
        // RTT exists per connection on the server in recent Mirror builds,
        // but use reflection for compatibility with game-bundled versions.
        private static readonly PropertyInfo HostConnectionRttProperty =
            typeof(Mirror.NetworkConnectionToClient).GetProperty("rtt",
                BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo HostConnectionRttField =
            typeof(Mirror.NetworkConnectionToClient).GetField("rtt",
                BindingFlags.Instance | BindingFlags.Public);
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _scale;
        private ConfigEntry<float> _readabilityScale;
        private ConfigEntry<bool> _autoScaleToResolution;
        private ConfigEntry<bool> _showCompanionHealth;
        private ConfigEntry<bool> _showWallHealth;
        private ConfigEntry<bool> _showHostPing;
        private ConfigEntry<bool> _enableBuildInspector;
        private ConfigEntry<KeyboardShortcut> _toggleSettingsShortcut;
        private ConfigEntry<KeyboardShortcut> _toggleBuildInspectorShortcut;
        private ConfigEntry<KeyboardShortcut> _nextBuildPlayerShortcut;
        private ConfigEntry<KeyboardShortcut> _nextBuildPageShortcut;
        private ConfigEntry<KeyboardShortcut> _previousBuildPageShortcut;
        private ConfigEntry<bool> _showTeammateOutlines;
        private ConfigEntry<bool> _showPlayerNames;
        private ConfigEntry<float> _playerNameVerticalOffset;
        private ConfigEntry<bool> _outlinesThroughWalls;
        private ConfigEntry<float> _outlineWidth;
        private ConfigEntry<int> _leftOffset;
        private ConfigEntry<int> _topOffset;
        private ConfigEntry<bool> _avoidNativeRoster;
        private ConfigEntry<int> _bottomOffset;
        private ConfigEntry<PanelAnchor> _anchor;
        private float _nextRefresh;
        private float _nextWallRefresh;
        private bool _hasWall;
        private float _wallHealth;
        private float _wallMaxHealth;
        private int _wallSections;
        private bool _wallBreached;
        private float _nextWarning;
        private float _nextPingDiagnostic;
        private float _nextOutlineShaderRetry;
        private GUIStyle _headingStyle;
        private GUIStyle _countStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _classStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _healthStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _companionStyle;
        private GUIStyle _alertStyle;
        private GUIStyle _worldNameStyle;
        private GUIStyle _wallStatusStyle;
        private GUIStyle _pingStyle;
        private GUIStyle _buildNameStyle;
        private GUIStyle _buildDetailStyle;
        private GUIStyle _buildHelpStyle;
        private GUIStyle _settingsButtonStyle;
        private Font _nativeUiFont;
        private float _nextNativeFontLookup;

        private sealed class PlayerEntry
        {
            public string Name;
            public string ClassName;
            public string Badge;
            public Sprite ClassIcon;
            public Color Accent;
            public float Health;
            public float MaxHealth;
            public bool HasStats;
            public bool IsDead;
            public bool ShowHostPing;
            public bool IsLocalHost;
            public bool HasPing;
            public int PingMs;
            public CompanionEntry Companion;
        }

        private sealed class OutlineEntry
        {
            public GameObject VisualRoot;
            public QuickOutline Component;
            public Renderer[] Renderers;
            public float NextMaterialCheck;
        }

        private sealed class CompanionEntry
        {
            public float Health;
            public float MaxHealth;
            public bool IsKnockedOut;
        }

        private sealed class PlayerBuildEntry
        {
            public FirstPersonController Player;
            public string Name;
            public bool HasRunData;
            public readonly List<BuildRingEntry> Rings = new List<BuildRingEntry>();
        }

        private sealed class BuildRingEntry
        {
            public string ID;
            public string Name;
            public Rarity Rarity;
            public Sprite Icon;
            public int Picks;
        }

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true,
                "Show the party status HUD when network players are present.");
            _scale = Config.Bind("Appearance", "Scale", 1.0f,
                "HUD size multiplier (clamped to 0.65 - 1.8).");
            _readabilityScale = Config.Bind("Appearance", "ReadabilityScale", 1.12f,
                "Extra panel enlargement (1.0 - 1.4), also applied to existing configurations.");
            _autoScaleToResolution = Config.Bind("Appearance", "AutoScaleToResolution", true,
                "Match the HUD's on-screen proportions across resolutions (reference 1920x1080). Shrink to fit native UI when necessary. Disable for fixed pixel sizing.");
            _showCompanionHealth = Config.Bind("Appearance", "ShowCompanionHealth", true,
                "Show a Hunter's companion health directly below the Hunter's entry.");
            _showWallHealth = Config.Bind("General", "ShowWallHealth", true,
                "Show combined defensive wall durability beneath the party roster.");
            _showHostPing = Config.Bind("Appearance", "ShowHostPing", true,
                "On the hosting player's screen, show each remote player's real Mirror connection RTT in ms. Other clients never display unverified peer ping.");
            _enableBuildInspector = Config.Bind("BuildInspector", "Enabled", true,
                "Allow the hosting player to inspect teammates' rings. Other players do not need the mod.");
            _toggleSettingsShortcut = Config.Bind("Hotkeys", "ToggleSettings",
                new KeyboardShortcut(KeyCode.F7),
                "Open or close Party HUD's built-in settings menu in-game.");
            _toggleBuildInspectorShortcut = Config.Bind("Hotkeys", "ToggleBuildInspector",
                new KeyboardShortcut(KeyCode.F8),
                "Open or close the host-only build inspector. Supports key combinations with Ctrl, Shift and Alt.");
            _nextBuildPlayerShortcut = Config.Bind("Hotkeys", "NextBuildPlayer",
                new KeyboardShortcut(KeyCode.F9),
                "Select the next teammate in the build inspector.");
            _nextBuildPageShortcut = Config.Bind("Hotkeys", "NextBuildPage",
                new KeyboardShortcut(KeyCode.PageDown),
                "Show the next page of rings in the build inspector.");
            _previousBuildPageShortcut = Config.Bind("Hotkeys", "PreviousBuildPage",
                new KeyboardShortcut(KeyCode.PageUp),
                "Show the previous page of rings in the build inspector.");
            _showTeammateOutlines = Config.Bind("TeammateOutlines", "Enabled", true,
                "Outline remote teammates in the world (never the local player).");
            _showPlayerNames = Config.Bind("TeammateOutlines", "ShowPlayerNames", true,
                "Show each remote teammate's nickname near their outlined character.");
            _playerNameVerticalOffset = Config.Bind("TeammateOutlines", "PlayerNameVerticalOffset", 14f,
                "Pixels below the projected top of the teammate model (0 - 80); positive values move name labels down.");
            _outlinesThroughWalls = Config.Bind("TeammateOutlines", "ThroughWalls", true,
                "Show teammate silhouettes even when they are behind obstacles.");
            _outlineWidth = Config.Bind("TeammateOutlines", "Width", 3.0f,
                "Outline thickness; clamped to 0.5 - 6.0.");
            _anchor = Config.Bind("Appearance", "Anchor", PanelAnchor.TopLeft,
                "Panel location: TopLeft (recommended) or BottomLeft.");
            _leftOffset = Config.Bind("Appearance", "LeftOffset", 20,
                "Distance from the left edge, in unscaled pixels.");
            _topOffset = Config.Bind("Appearance", "TopOffset", 105,
                "Minimum distance from the top when Anchor is TopLeft.");
            _avoidNativeRoster = Config.Bind("Appearance", "AvoidNativePlayerNames", true,
                "Keep Party HUD below Stonewards' native player names, including in larger lobbies.");
            _bottomOffset = Config.Bind("Appearance", "BottomOffset", 140,
                "Distance from the bottom when Anchor is BottomLeft. A safety zone protects native UI.");
            Logger.LogInfo("Party HUD v" + PluginVersion + " initialized.");
        }

        private void Update()
        {
            bool gameMenuOpen = IsBlockingGameMenuOpen();
            if (_settingsOpen && gameMenuOpen)
                CloseSettings(false);
            if (!gameMenuOpen && _capturingShortcut < 0 &&
                _toggleSettingsShortcut.Value.IsDown())
            {
                if (_settingsOpen)
                    CloseSettings();
                else
                    OpenSettings();
            }
            if (_settingsOpen)
            {
                // The game can lock the pointer again during Update.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (!_enabled.Value)
            {
                RemoveAllOutlines();
                CloseBuildInspector();
                return;
            }

            // The build inspector is strictly host-side. Normal clients never
            // receive a complete list of the other players' selected upgrades.
            bool canInspect = _enableBuildInspector.Value &&
                Mirror.NetworkServer.active && Mirror.NetworkClient.active;
            if (!canInspect)
                CloseBuildInspector();
            else if (!gameMenuOpen && !_settingsOpen)
            {
                if (_toggleBuildInspectorShortcut.Value.IsDown())
                {
                    _showBuilds = !_showBuilds;
                    _nextBuildRefresh = 0f;
                    if (!_showBuilds)
                        _buildPlayers.Clear();
                }
                else if (_showBuilds)
                {
                    // Else-if gives each keypress one action even when
                    // multiple commands are accidentally bound identically.
                    if (_nextBuildPlayerShortcut.Value.IsDown() &&
                        _buildPlayers.Count > 0)
                    {
                        _buildPlayerIndex = (_buildPlayerIndex + 1) % _buildPlayers.Count;
                        _buildPage = 0;
                    }
                    else if (_nextBuildPageShortcut.Value.IsDown())
                        _buildPage++;
                    else if (_previousBuildPageShortcut.Value.IsDown())
                        _buildPage = Math.Max(0, _buildPage - 1);
                }
            }

            if (Time.unscaledTime < _nextRefresh)
                return;

            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            RefreshRoster();
            RefreshWallHealth();
            UpdateTeammateOutlines();
            if (_showBuilds && Time.unscaledTime >= _nextBuildRefresh)
            {
                _nextBuildRefresh = Time.unscaledTime + 0.8f;
                RefreshBuildInspector();
            }
        }

        private void OnDisable()
        {
            CloseSettings();
            RemoveAllOutlines();
            CloseBuildInspector();
        }

        private void OnDestroy()
        {
            CloseSettings();
            RemoveAllOutlines();
            CloseBuildInspector();
        }

        private void OpenSettings()
        {
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _settingsOpen = true;
            _capturingShortcut = -1;
            _settingsNotice = "";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void CloseSettings(bool restoreCursor = true)
        {
            if (!_settingsOpen)
                return;
            _settingsOpen = false;
            _capturingShortcut = -1;
            // Let Stonewards manage the cursor when it opens its own menu.
            if (restoreCursor)
            {
                Cursor.lockState = _previousCursorLock;
                Cursor.visible = _previousCursorVisible;
            }
        }

        private void CloseBuildInspector()
        {
            _showBuilds = false;
            _buildPlayers.Clear();
            _buildPlayerIndex = 0;
            _buildPage = 0;
        }

        private void RefreshBuildInspector()
        {
            FirstPersonController selected = _buildPlayerIndex >= 0 &&
                _buildPlayerIndex < _buildPlayers.Count
                ? _buildPlayers[_buildPlayerIndex].Player : null;

            _buildPlayers.Clear();
            try
            {
                var roster = FirstPersonController.LocalPlayers;
                if (roster == null)
                    return;

                var manager = RogueLikeUpgradeManager.Instance;
                foreach (var player in roster)
                {
                    if (player == null || player.isLocalPlayer)
                        continue;

                    var build = new PlayerBuildEntry
                    {
                        Player = player,
                        Name = CleanName(player.NetworkplayerName)
                    };
                    try
                    {
                        // This object is maintained on the game's host for
                        // each player; it is NOT a replicated client roster.
                        RunPlayerData runData = player.RunPlayerData;
                        if (runData != null && runData.rogueUpgrades != null)
                        {
                            build.HasRunData = true;
                            foreach (RogueUpgradeData saved in runData.rogueUpgrades)
                            {
                                if (saved == null || string.IsNullOrWhiteSpace(saved.upgradeID))
                                    continue;

                                // Separate rarity variants, so we never claim
                                // a higher level from an unrelated rarity pick.
                                BuildRingEntry existing = null;
                                foreach (var ring in build.Rings)
                                    if (ring.ID == saved.upgradeID && ring.Rarity == saved.rarity)
                                    {
                                        existing = ring;
                                        break;
                                    }

                                if (existing != null)
                                {
                                    existing.Picks++;
                                    continue;
                                }

                                RogueLikeUpgradeSO definition = null;
                                try
                                {
                                    if (manager != null)
                                        definition = manager.GetUpgradeSOByID(saved.upgradeID);
                                }
                                catch
                                {
                                    // Missing definitions must not hide other
                                    // correctly recorded rings from the host.
                                }
                                string title = saved.upgradeID;
                                Sprite icon = null;
                                if (definition != null)
                                {
                                    try
                                    {
                                        var localized = definition.GetLocalizedTitle();
                                        if (!string.IsNullOrWhiteSpace(localized))
                                            title = localized;
                                        icon = definition.Icon;
                                    }
                                    catch
                                    {
                                        // Keep the precise upgrade ID visible
                                        // if localization/assets are unavailable.
                                    }
                                }
                                build.Rings.Add(new BuildRingEntry
                                {
                                    ID = saved.upgradeID,
                                    Name = title,
                                    Rarity = saved.rarity,
                                    Icon = icon,
                                    Picks = 1
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        build.HasRunData = false;
                        build.Rings.Clear();
                        if (Time.unscaledTime >= _nextWarning)
                        {
                            Logger.LogWarning("Could not read ring build for " +
                                build.Name + ": " + ex);
                            _nextWarning = Time.unscaledTime + 10f;
                        }
                    }
                    _buildPlayers.Add(build);
                }
            }
            catch (Exception ex)
            {
                _buildPlayers.Clear();
                if (Time.unscaledTime >= _nextWarning)
                {
                    Logger.LogWarning("Could not inspect host party builds: " + ex);
                    _nextWarning = Time.unscaledTime + 10f;
                }
            }

            _buildPlayerIndex = 0;
            if (selected != null)
                for (int i = 0; i < _buildPlayers.Count; i++)
                    if (_buildPlayers[i].Player == selected)
                    {
                        _buildPlayerIndex = i;
                        break;
                    }
        }

        // Read-only client-side state. No Harmony patches, network messages or game mutations.
        private void RefreshRoster()
        {
            _players.Clear();

            try
            {
                var roster = FirstPersonController.LocalPlayers;
                if (roster == null)
                    return;

                // Only a listen-server host has its peers' server-side RTT.
                // Do not infer or fabricate remote ping on ordinary clients.
                bool canShowHostPing = _showHostPing.Value &&
                    Mirror.NetworkServer.active && Mirror.NetworkClient.active;
                int remotePlayers = 0;
                int connectionsFound = 0;
                int pingsMeasured = 0;

                for (int i = 0; i < roster.Count; i++)
                {
                    var player = roster[i];
                    if (player == null)
                        continue;

                    var playerClass = player.NetworkplayerClass;
                    var entry = new PlayerEntry
                    {
                        Name = CleanName(player.NetworkplayerName),
                        ClassName = playerClass.ToString(),
                        Badge = ClassBadge(playerClass),
                        ClassIcon = FindClassIcon(player, playerClass),
                        Accent = ClassColor(playerClass),
                        ShowHostPing = canShowHostPing,
                        IsLocalHost = canShowHostPing && player.isLocalPlayer
                    };

                    if (canShowHostPing && !entry.IsLocalHost)
                    {
                        remotePlayers++;
                        var connection = player.connectionToClient;
                        if (connection != null)
                            connectionsFound++;
                        int ping;
                        if (TryReadHostPing(connection, out ping))
                        {
                            entry.HasPing = true;
                            entry.PingMs = ping;
                            pingsMeasured++;
                        }
                    }

                    var stats = player.PlayerStats;
                    if (stats != null)
                    {
                        entry.HasStats = true;
                        entry.Health = stats.GetCurrentHealth();
                        entry.MaxHealth = stats.NetworksyncMaxHealth;
                        entry.IsDead = stats.NetworkisDead;
                    }

                    if (_showCompanionHealth.Value && playerClass == PlayerClass.HUNTER)
                    {
                        // Mirror synchronizes the companion reference to all clients.
                        // If it arrives later than the companion spawn, try its owner link.
                        var companion = player.NetworksyncCompanion;
                        if (companion == null)
                        {
                            var known = Companion.LocalCompanions;
                            if (known != null)
                            {
                                for (int c = 0; c < known.Count; c++)
                                {
                                    var candidate = known[c];
                                    if (candidate != null && candidate.Networkowner == player)
                                    {
                                        companion = candidate;
                                        break;
                                    }
                                }
                            }
                        }

                        if (companion != null)
                        {
                            entry.Companion = new CompanionEntry
                            {
                                Health = companion.NetworkcurrentHealth,
                                MaxHealth = companion.NetworkcurrentMaxHealth,
                                IsKnockedOut = companion.NetworksyncIsKnockedOut
                            };
                        }
                    }

                    _players.Add(entry);
                }

                // An infrequent diagnostic helps distinguish missing
                // server-side connections, unsupported Mirror RTT and
                // RTT samples that simply have not arrived yet.
                if (_showHostPing.Value && canShowHostPing && remotePlayers > 0 &&
                    pingsMeasured == 0 && Time.unscaledTime >= _nextPingDiagnostic)
                {
                    _nextPingDiagnostic = Time.unscaledTime + 20f;
                    Logger.LogWarning("Host RTT unavailable: remotePlayers=" +
                        remotePlayers + ", serverConnections=" + connectionsFound +
                        ", rttMember=" +
                        (HostConnectionRttProperty != null || HostConnectionRttField != null) +
                        ". Only actual RTT samples are displayed.");
                }
            }
            catch (Exception ex)
            {
                _players.Clear();
                if (Time.unscaledTime >= _nextWarning)
                {
                    Logger.LogWarning("Could not read party state: " + ex);
                    _nextWarning = Time.unscaledTime + 10f;
                }
            }
        }

        private static bool TryReadHostPing(
            Mirror.NetworkConnectionToClient connection, out int pingMs)
        {
            pingMs = 0;
            if (connection == null ||
                (HostConnectionRttProperty == null && HostConnectionRttField == null))
                return false;

            try
            {
                object value = HostConnectionRttProperty != null
                    ? HostConnectionRttProperty.GetValue(connection, null)
                    : HostConnectionRttField.GetValue(connection);
                if (value == null)
                    return false;

                double rttSeconds = Convert.ToDouble(value);
                if (double.IsNaN(rttSeconds) || double.IsInfinity(rttSeconds) ||
                    rttSeconds <= 0d)
                    return false;

                pingMs = (int)Math.Max(1d,
                    Math.Round(Math.Min(rttSeconds * 1000d, 99999d)));
                return true;
            }
            catch
            {
                // An unsupported Mirror RTT API never breaks the party roster.
                return false;
            }
        }

        private void RefreshWallHealth()
        {
            if (Time.unscaledTime < _nextWallRefresh)
                return;

            _nextWallRefresh = Time.unscaledTime + 1f;
            _hasWall = false;
            _wallHealth = 0f;
            _wallMaxHealth = 0f;
            _wallSections = 0;
            _wallBreached = false;

            if (!_showWallHealth.Value)
                return;

            try
            {
                // WallStats exposes the defensive sections' Mirror SyncVars.
                // Aggregate active network sections instead of choosing an
                // arbitrary one that might not represent total durability.
                var walls = UnityEngine.Object.FindObjectsByType<WallStats>(FindObjectsSortMode.None);
                for (int i = 0; i < walls.Length; i++)
                {
                    var wall = walls[i];
                    if (wall == null || !wall.isActiveAndEnabled || !wall.isClient)
                        continue;

                    float maximum = wall.NetworkcurrentMaxHealth;
                    if (maximum <= 0f)
                        continue;

                    _wallHealth += Mathf.Clamp(wall.NetworkcurrentHealth, 0f, maximum);
                    _wallMaxHealth += maximum;
                    _wallSections++;
                    _wallBreached |= wall.NetworkisDestroyed;
                }

                _hasWall = _wallSections > 0 && _wallMaxHealth > 0f;
            }
            catch (Exception ex)
            {
                _hasWall = false;
                if (Time.unscaledTime >= _nextWarning)
                {
                    Logger.LogWarning("Could not read wall health: " + ex);
                    _nextWarning = Time.unscaledTime + 10f;
                }
            }
        }

        private static GameObject FindCharacterVisuals(FirstPersonController player)
        {
            if (CharacterVisualsParentField == null)
                return null;

            var visual = CharacterVisualsParentField.GetValue(player);
            var go = visual as GameObject;
            if (go != null)
                return go;

            var component = visual as Component;
            return component != null ? component.gameObject : null;
        }

        private static bool IsShaderUsable(Material material)
        {
            if (material == null || material.shader == null)
                return false;

            var shader = material.shader;
            // Unity's internal error shader paints entire meshes bright magenta.
            return shader.isSupported &&
                shader.name.IndexOf("InternalErrorShader",
                    StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static Renderer[] FindOutlineRenderers(GameObject root)
        {
            // QuickOutline appends two material passes to every renderer it is
            // given. VFX, particles, trails and other special renderers cannot
            // safely use these passes (see Unity issue UUM-73492).
            var supported = new List<Renderer>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) &&
                    !(renderer is SkinnedMeshRenderer))
                    continue;

                var materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                    continue;

                bool valid = true;
                foreach (var material in materials)
                    if (!IsShaderUsable(material))
                    {
                        valid = false;
                        break;
                    }

                if (valid)
                    supported.Add(renderer);
            }
            return supported.ToArray();
        }

        private static bool AreOutlineShadersUsable(QuickOutline outline)
        {
            if (outline == null || OutlineMaskMaterialField == null ||
                OutlineFillMaterialField == null)
                return false;

            var mask = OutlineMaskMaterialField.GetValue(outline) as Material;
            var fill = OutlineFillMaterialField.GetValue(outline) as Material;
            return IsShaderUsable(mask) && IsShaderUsable(fill);
        }

        private static bool SameRenderers(Renderer[] previous, Renderer[] current)
        {
            if (previous == null || current == null || previous.Length != current.Length)
                return false;

            for (int i = 0; i < current.Length; i++)
                if (previous[i] == null || previous[i] != current[i])
                    return false;

            return true;
        }

        private static bool HasOutlineMaterial(Renderer renderer, Material mask, Material fill)
        {
            // Another game system may overwrite sharedMaterials when replacing
            // equipment or visuals without destroying the character object.
            if (renderer == null || !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
                return true;

            var shared = renderer.sharedMaterials;
            bool foundMask = false;
            bool foundFill = false;
            foreach (var material in shared)
            {
                if (material == mask)
                    foundMask = true;
                if (material == fill)
                    foundFill = true;
            }
            return foundMask && foundFill;
        }

        private static bool IsOutlineMaterialMissing(OutlineEntry entry)
        {
            if (OutlineMaskMaterialField == null || OutlineFillMaterialField == null)
                return false;

            var mask = OutlineMaskMaterialField.GetValue(entry.Component) as Material;
            var fill = OutlineFillMaterialField.GetValue(entry.Component) as Material;
            if (mask == null || fill == null)
                return false;

            foreach (var renderer in entry.Renderers)
                if (!HasOutlineMaterial(renderer, mask, fill))
                    return true;

            return false;
        }

        private void UpdateTeammateOutlines()
        {
            if (!_showTeammateOutlines.Value)
            {
                RemoveAllOutlines();
                return;
            }

            try
            {
                var roster = FirstPersonController.LocalPlayers;
                if (roster == null || roster.Count == 0)
                {
                    RemoveAllOutlines();
                    return;
                }

                _expiredOutlines.Clear();
                foreach (var item in _teamOutlines)
                {
                    var player = item.Key;
                    var entry = item.Value;
                    if (player == null || !roster.Contains(player) || player.isLocalPlayer ||
                        entry.VisualRoot == null || entry.Component == null ||
                        FindCharacterVisuals(player) != entry.VisualRoot)
                    {
                        _expiredOutlines.Add(player);
                    }
                }
                foreach (var player in _expiredOutlines)
                    RemoveOutline(player);
                _expiredOutlines.Clear();

                for (int i = 0; i < roster.Count; i++)
                {
                    var player = roster[i];
                    if (player == null || player.isLocalPlayer)
                        continue;

                    var root = FindCharacterVisuals(player);
                    if (root == null || !root.activeInHierarchy)
                        continue;

                    var renderers = FindOutlineRenderers(root);
                    if (renderers.Length == 0)
                        continue;

                    OutlineEntry entry;
                    if (_teamOutlines.TryGetValue(player, out entry) &&
                        !SameRenderers(entry.Renderers, renderers))
                    {
                        // The scene/class/equipment swapped renderer objects.
                        // Remove our stale component; recreate on the next tick
                        // after Unity has processed Destroy().
                        RemoveOutline(player);
                        continue;
                    }

                    if (!_teamOutlines.TryGetValue(player, out entry))
                    {
                        // Unsupported game shaders are global to this client:
                        // avoid recreating and destroying them every refresh.
                        if (Time.unscaledTime < _nextOutlineShaderRetry)
                            continue;

                        // Never overwrite an outline that belongs to the game or another mod.
                        if (root.GetComponent<QuickOutline>() != null)
                            continue;

                        var outline = root.AddComponent<QuickOutline>();
                        if (outline == null)
                            continue;

                        // The game's QuickOutline does not initialize renderers in Awake().
                        // Its first OnEnable() happens before Init(), so it does no work.
                        // Explicitly initialize the meshes, then enable the material passes.
                        outline.enabled = false;
                        try
                        {
                            outline.OutlineMode = _outlinesThroughWalls.Value
                                ? QuickOutline.Mode.OutlineAll
                                : QuickOutline.Mode.OutlineVisible;
                            outline.OutlineColor = new Color(0.24f, 0.88f, 0.96f, 1f);
                            outline.OutlineWidth = Mathf.Clamp(_outlineWidth.Value, 0.5f, 6f);
                            outline.Init(renderers);

                            // Never attach QuickOutline passes that resolve to
                            // unsupported/error shaders: they can turn a whole
                            // teammate into a solid magenta silhouette.
                            if (!AreOutlineShadersUsable(outline))
                            {
                                _nextOutlineShaderRetry = Time.unscaledTime + 15f;
                                Logger.LogWarning("Skipped teammate outline: " +
                                    "QuickOutline mask/fill shader is missing or unsupported. " +
                                    "Retrying in 15 seconds.");
                                Destroy(outline);
                                continue;
                            }

                            outline.enabled = true;
                        }
                        catch
                        {
                            if (outline != null)
                            {
                                outline.enabled = false;
                                Destroy(outline);
                            }
                            throw;
                        }

                        entry = new OutlineEntry
                        {
                            VisualRoot = root,
                            Component = outline,
                            Renderers = renderers,
                            NextMaterialCheck = Time.unscaledTime + 2f
                        };
                        _teamOutlines.Add(player, entry);
                        Logger.LogInfo("Initialized outline for teammate " +
                            player.NetworkplayerName + " (" + renderers.Length + " renderers).");
                    }

                    if (entry.Component == null)
                        continue;

                    // Some game updates may disable our component or replace
                    // renderer materials without changing the renderer objects.
                    // Restore only our own materials, and at most once per 2 s.
                    if (!AreOutlineShadersUsable(entry.Component))
                    {
                        // Fail closed instead of painting teammate geometry
                        // with an invalid shader until the next scene load.
                        _nextOutlineShaderRetry = Time.unscaledTime + 15f;
                        RemoveOutline(player);
                        continue;
                    }

                    if (!entry.Component.enabled)
                        entry.Component.enabled = true;

                    if (Time.unscaledTime >= entry.NextMaterialCheck)
                    {
                        entry.NextMaterialCheck = Time.unscaledTime + 2f;
                        if (IsOutlineMaterialMissing(entry))
                        {
                            // Do not toggle an outline against a renderer whose
                            // materials were swapped in-game: retire it cleanly
                            // and rebuild with a fresh compatible mesh list.
                            RemoveOutline(player);
                            continue;
                        }
                    }

                    bool downed = player.PlayerStats != null && player.PlayerStats.NetworkisDead;
                    entry.Component.OutlineMode = _outlinesThroughWalls.Value
                        ? QuickOutline.Mode.OutlineAll
                        : QuickOutline.Mode.OutlineVisible;
                    entry.Component.OutlineColor = downed
                        ? new Color(1f, 0.34f, 0.36f, 1f)
                        : new Color(0.24f, 0.88f, 0.96f, 1f);
                    entry.Component.OutlineWidth = Mathf.Clamp(_outlineWidth.Value, 0.5f, 6f);
                }
            }
            catch (Exception ex)
            {
                if (Time.unscaledTime >= _nextWarning)
                {
                    Logger.LogWarning("Could not update teammate outlines: " + ex);
                    _nextWarning = Time.unscaledTime + 10f;
                }
            }
        }

        private void RemoveOutline(FirstPersonController player)
        {
            OutlineEntry entry;
            if (_teamOutlines.TryGetValue(player, out entry))
            {
                if (entry.Component != null)
                    Destroy(entry.Component);
                _teamOutlines.Remove(player);
            }
        }

        private void RemoveAllOutlines()
        {
            if (_teamOutlines.Count == 0)
                return;

            foreach (var entry in _teamOutlines.Values)
                if (entry.Component != null)
                    Destroy(entry.Component);
            _teamOutlines.Clear();
            _expiredOutlines.Clear();
        }

        private void DrawTeammatePlayerNames()
        {
            // Labels are drawn in screen space, so they remain legible through
            // terrain without modifying world materials or network state.
            if (!_showTeammateOutlines.Value || !_showPlayerNames.Value ||
                _teamOutlines.Count == 0)
                return;

            Camera camera = Camera.main;
            if (camera == null)
                return;

            Color previous = GUI.color;
            try
            {
                foreach (var pair in _teamOutlines)
                {
                    var player = pair.Key;
                    var outline = pair.Value;
                    if (player == null || player.isLocalPlayer || outline.Component == null ||
                        outline.VisualRoot == null || !outline.VisualRoot.activeInHierarchy ||
                        outline.Renderers == null)
                        continue;

                    // Renderer bounds follow the animated character. Avoid using
                    // a fixed height offset, which varies by character model.
                    bool found = false;
                    Bounds bounds = default(Bounds);
                    foreach (var renderer in outline.Renderers)
                    {
                        if (renderer == null || !renderer.enabled ||
                            !renderer.gameObject.activeInHierarchy)
                            continue;

                        if (!found)
                        {
                            bounds = renderer.bounds;
                            found = true;
                        }
                        else
                            bounds.Encapsulate(renderer.bounds);
                    }

                    if (!found)
                        continue;

                    Vector3 head = new Vector3(bounds.center.x,
                        bounds.max.y + 0.12f, bounds.center.z);
                    Vector3 projectedHead = camera.WorldToScreenPoint(head);
                    if (projectedHead.z <= 0f)
                        continue;

                    // The original game nameplate sits above the head. Keep our
                    // visible-through-walls nickname below it for teammate tracking.
                    string playerName = CleanName(player.NetworkplayerName);
                    const float labelHeight = 20f;
                    const float labelMaxWidth = 220f;
                    const float labelTextWidth = labelMaxWidth - 12f;
                    float measuredWidth = _worldNameStyle.CalcSize(
                        new GUIContent(playerName)).x;

                    // Avoid chopping off ordinary nicknames, including non-Latin names.
                    // Restrict exceptionally long names to prevent screen-wide labels.
                    if (measuredWidth > labelTextWidth)
                    {
                        string truncated = playerName;
                        while (truncated.Length > 1 &&
                            _worldNameStyle.CalcSize(
                                new GUIContent(truncated + "...")).x > labelTextWidth)
                            truncated = truncated.Substring(0, truncated.Length - 1);
                        playerName = truncated + "...";
                        measuredWidth = _worldNameStyle.CalcSize(
                            new GUIContent(playerName)).x;
                    }

                    float labelWidth = Mathf.Clamp(measuredWidth + 14f, 82f, labelMaxWidth);
                    float left = projectedHead.x - labelWidth * 0.5f;
                    float top = Screen.height - projectedHead.y +
                        Mathf.Clamp(_playerNameVerticalOffset.Value, 0f, 80f);

                    // No stuck labels when a teammate moves outside the view.
                    if (left < 4f || top < 4f ||
                        left + labelWidth > Screen.width - 4f ||
                        top + labelHeight > Screen.height - 4f)
                        continue;

                    bool downed = player.PlayerStats != null &&
                        player.PlayerStats.NetworkisDead;
                    Color accent = downed
                        ? new Color(1f, 0.35f, 0.40f, 1f)
                        : new Color(0.26f, 0.89f, 0.97f, 1f);

                    Fill(new Rect(left, top, labelWidth, labelHeight),
                        new Color(0.035f, 0.075f, 0.11f, 0.70f));
                    Fill(new Rect(left, top + labelHeight - 2f,
                        labelWidth, 2f), accent);

                    _worldNameStyle.normal.textColor = downed
                        ? new Color(1f, 0.78f, 0.79f, 1f)
                        : Color.white;
                    GUI.Label(new Rect(left + 3f, top + 1f,
                        labelWidth - 6f, labelHeight - 4f),
                        playerName, _worldNameStyle);
                }
            }
            catch (Exception ex)
            {
                if (Time.unscaledTime >= _nextWarning)
                {
                    Logger.LogWarning("Could not draw teammate player names: " + ex);
                    _nextWarning = Time.unscaledTime + 10f;
                }
            }
            finally
            {
                GUI.color = previous;
            }
        }

        private static bool IsLoadingScreenVisible()
        {
            if (LoadingScreenInstanceField == null ||
                LoadingScreenElementField == null)
                return false;

            try
            {
                var instance = LoadingScreenInstanceField.GetValue(null);
                if (instance == null)
                    return false;

                var element = LoadingScreenElementField.GetValue(instance) as VisualElement;
                if (element == null || element.panel == null)
                    return false;

                // A hidden parent can otherwise make a child appear visible
                // according to its own style. Inspect the complete UI chain.
                for (VisualElement node = element; node != null; node = node.parent)
                {
                    var style = node.resolvedStyle;
                    if (style.display == DisplayStyle.None ||
                        style.visibility == Visibility.Hidden ||
                        style.opacity <= 0.01f)
                        return false;
                }
                return true;
            }
            catch
            {
                // Loading-screen API differences must never crash the HUD.
                return false;
            }
        }

        private static bool IsBlockingGameMenuOpen()
        {
            if (IsLoadingScreenVisible())
                return true;

            // These menus derive from BaseMenu, which tracks visibility using
            // IsOpen. Component activation alone is not a reliable indicator.
            var shop = ShopMenu.Instance;
            if (shop != null && shop.IsOpen)
                return true;

            var selection = CharacterSelectionScreen.Instance;
            if (selection != null && selection.IsOpen)
                return true;

            var missions = GlobalMissionMenu.Instance;
            if (missions != null && missions.IsOpen)
                return true;

            // Check actual UI visibility, not Tab key presses or whether
            // the InventoryScreen component exists in the scene.
            var inventory = InventoryScreen.Instance;
            if (inventory != null && InventoryRootField != null)
            {
                var root = InventoryRootField.GetValue(inventory) as VisualElement;
                if (root != null && root.resolvedStyle.display != DisplayStyle.None)
                    return true;
            }

            // RogueLikeUpgradeMenu is the three-choice UPGRADE OFFERS screen,
            // including ring and other between-wave upgrade selections.
            var upgrades = RogueLikeUpgradeMenu.Instance;
            return upgrades != null && upgrades.IsOpen;
        }

        private void OnGUI()
        {
            if (Event.current == null || IsBlockingGameMenuOpen())
                return;

            // Real GUI controls need MouseDown/MouseUp/KeyDown/Layout events.
            // Process settings on all IMGUI event types; the passive HUD still
            // draws only on Repaint and never steals input.
            if (_settingsOpen)
            {
                EnsureStyles();
                DrawSettingsWindow();
            }

            if (!_enabled.Value || _players.Count == 0 ||
                !Mirror.NetworkClient.active ||
                Event.current.type != EventType.Repaint)
                return;

            EnsureStyles();
            DrawTeammatePlayerNames();

            float height = HeaderHeight + _players.Count * RowHeight + FooterPadding;
            if (_hasWall)
                height += WallRowHeight;
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].Companion != null)
                    height += CompanionRowHeight;

            // Match the same visual proportion of the screen rather than
            // forcing the same absolute pixel size on every resolution.
            // Clamp manual preferences BEFORE adapting to resolution so that
            // high-resolution screens can scale above the usual 1.8x limit.
            float baseScale = Mathf.Clamp(_scale.Value *
                Mathf.Clamp(_readabilityScale.Value, 1f, 1.4f) *
                PanelSizeBoost, 0.55f, 1.8f);
            float resolutionScale = _autoScaleToResolution.Value
                ? Mathf.Min(Screen.width / ReferenceScreenWidth,
                    Screen.height / ReferenceScreenHeight)
                : 1f;
            float scale = baseScale * resolutionScale;

            // The native player roster grows from the upper-left corner;
            // calculate its reserved space before deciding whether to shrink.
            float topOffset = _topOffset.Value;
            if (_avoidNativeRoster.Value)
                topOffset = Mathf.Max(topOffset,
                    NativeRosterHeaderSafeZone + _players.Count * NativeRosterPlayerRowSafeZone);

            // Always keep the window inside the display, including four
            // players, companion rows and an optional wall.
            scale = Mathf.Min(scale, (Screen.width - 16f) / PanelWidth,
                (Screen.height - 16f) / height);

            if (_autoScaleToResolution.Value)
            {
                // Extra fit for the reserved UI space, only when required.
                // False preserves manual sizing while still keeping the
                // panel itself inside the display.
                float reservedOffset = _anchor.Value == PanelAnchor.TopLeft
                    ? Mathf.Max(8f, topOffset)
                    : Mathf.Max(8f, Mathf.Max(_bottomOffset.Value, BottomHudSafeZone));
                scale = Mathf.Min(scale,
                    (Screen.height - 8f) / (height + reservedOffset));
            }
            if (scale <= 0f)
                return;

            float screenWidth = Screen.width / scale;
            float screenHeight = Screen.height / scale;
            float x = Mathf.Clamp(_leftOffset.Value, 8f,
                Mathf.Max(8f, screenWidth - PanelWidth - 8f));

            float requestedY = _anchor.Value == PanelAnchor.TopLeft
                ? topOffset
                : screenHeight - Mathf.Max(_bottomOffset.Value, BottomHudSafeZone) - height;
            float y = Mathf.Clamp(requestedY, 8f, Mathf.Max(8f, screenHeight - height - 8f));

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                DrawPanel(x, y, height);
                if (_showBuilds && Mirror.NetworkServer.active &&
                    Mirror.NetworkClient.active && _enableBuildInspector.Value)
                {
                    // Separate optional window, adjacent to the normal HUD.
                    // Keep it on-screen when the HUD is moved by the user.
                    float detailsX = Mathf.Clamp(x + PanelWidth + 12f, 8f,
                        Mathf.Max(8f, screenWidth - BuildWindowWidth - 8f));
                    float detailsY = Mathf.Clamp(y, 8f,
                        Mathf.Max(8f, screenHeight - BuildWindowHeight - 8f));
                    DrawBuildInspector(detailsX, detailsY);
                }
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
            }
        }

        private void DrawPanel(float x, float y, float height)
        {
            DrawStonewardsWindow(x, y, PanelWidth, height);
            // Keep the title and party counter vertically centered on the
            // same header line; DOWN stays in the secondary row when needed.
            GUI.Label(new Rect(x + 20f, y + 2f, PanelWidth - 40f, 29f),
                "PARTY STATUS", _headingStyle);

            int downed = 0;
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].IsDead)
                    downed++;

            if (downed > 0)
                GUI.Label(new Rect(x + 14f, y + 28f, 116f, 18f),
                    downed + " DOWN", _alertStyle);
            else if (_enableBuildInspector.Value &&
                Mirror.NetworkServer.active && Mirror.NetworkClient.active)
            {
                string hint = ShortcutLabel(_toggleBuildInspectorShortcut.Value) +
                    (_showBuilds ? " CLOSE" : " BUILDS");
                GUI.Label(new Rect(x + 14f, y + 28f, PanelWidth - 28f, 18f),
                    FitText(hint, _buildHelpStyle, PanelWidth - 28f),
                    _buildHelpStyle);
            }
            GUI.Label(new Rect(x + PanelWidth - 80f, y + 2f, 66f, 29f),
                _players.Count + " / 4", _countStyle);

            float rowY = y + HeaderHeight;
            for (int i = 0; i < _players.Count; i++)
            {
                DrawPlayer(x, rowY, _players[i], i);
                rowY += RowHeight;
                if (_players[i].Companion != null)
                {
                    DrawCompanion(x, rowY, _players[i].Companion);
                    rowY += CompanionRowHeight;
                }
            }
            if (_hasWall)
                DrawWallHealth(x, rowY);
        }

        private void DrawSettingsWindow()
        {
            float preferred = Mathf.Clamp(Mathf.Min(
                Screen.width / ReferenceScreenWidth,
                Screen.height / ReferenceScreenHeight), 0.8f, 1.5f);
            float scale = Mathf.Min(preferred,
                (Screen.width - 16f) / SettingsWidth,
                (Screen.height - 16f) / SettingsHeight);
            if (scale <= 0f)
                return;

            float sw = Screen.width / scale;
            float sh = Screen.height / scale;
            float x = (sw - SettingsWidth) * 0.5f;
            float y = (sh - SettingsHeight) * 0.5f;
            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;
            int depth = GUI.depth;

            try
            {
                // Make this a foreground interactive overlay without changing
                // the passive party HUD's existing repaint-only behavior.
                GUI.depth = -100;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                Fill(new Rect(0f, 0f, sw, sh),
                    new Color(0.015f, 0.02f, 0.03f, 0.68f));

                if (_capturingShortcut >= 0 &&
                    Event.current.type == EventType.KeyDown)
                    CaptureSettingsShortcut(Event.current);

                DrawStonewardsWindow(x, y, SettingsWidth, SettingsHeight);
                GUI.Label(new Rect(x + 62f, y + 4f, SettingsWidth - 124f, 33f),
                    "PARTY HUD SETTINGS", _headingStyle);
                if (SettingsButton(new Rect(x + SettingsWidth - 43f, y + 10f,
                    29f, 26f), "X"))
                {
                    CloseSettings();
                    return;
                }

                const float gap = 8f;
                const float tabWidth = (SettingsWidth - 32f - 2f * gap) / 3f;
                string[] tabs = { "DISPLAY", "LAYOUT", "HOTKEYS" };
                for (int i = 0; i < tabs.Length; i++)
                    if (SettingsButton(new Rect(x + 16f + i * (tabWidth + gap),
                        y + 58f, tabWidth, 34f), tabs[i], _settingsTab == i))
                    {
                        _settingsTab = i;
                        _settingsNotice = "";
                        _capturingShortcut = -1;
                    }

                switch (_settingsTab)
                {
                    case 0: DrawDisplaySettings(x, y); break;
                    case 1: DrawLayoutSettings(x, y); break;
                    default: DrawHotkeySettings(x, y); break;
                }

                Fill(new Rect(x + 14f, y + SettingsHeight - 39f,
                    SettingsWidth - 28f, 1f), PanelDivider);
                string footer = !string.IsNullOrEmpty(_settingsNotice)
                    ? _settingsNotice
                    : _capturingShortcut >= 0
                        ? "PRESS A KEY + MODIFIERS. ESC TO CANCEL."
                        : "CHANGES ARE SAVED TO YOUR BEPINEX CONFIG.";
                GUI.Label(new Rect(x + 20f, y + SettingsHeight - 32f,
                    SettingsWidth - 40f, 24f),
                    FitText(footer, _buildHelpStyle, SettingsWidth - 40f),
                    _buildHelpStyle);
            }
            finally
            {
                GUI.matrix = matrix;
                GUI.color = color;
                GUI.depth = depth;
            }
        }

        private bool SettingsButton(Rect area, string text, bool active = false)
        {
            Fill(area, active ? PanelHeader : SlotBackground);
            Fill(new Rect(area.x, area.y, area.width, 1f), PanelFrame);
            Fill(new Rect(area.x, area.y + area.height - 1f,
                area.width, 1f), PanelFrame);
            return GUI.Button(area, text, _settingsButtonStyle);
        }

        private void SettingsToggle(float x, float rowY, string title,
            ConfigEntry<bool> option)
        {
            GUI.Label(new Rect(x + 23f, rowY, 330f, 31f),
                title, _buildNameStyle);
            bool enabled = option.Value;
            if (SettingsButton(new Rect(x + SettingsWidth - 105f, rowY,
                82f, 30f), enabled ? "ON" : "OFF", enabled))
            {
                option.Value = !enabled;
                Config.Save();
                _settingsNotice = title.ToUpperInvariant() + " SAVED";
            }
            Fill(new Rect(x + 19f, rowY + 34f,
                SettingsWidth - 38f, 1f), PanelDivider);
        }

        private void DrawDisplaySettings(float x, float y)
        {
            GUI.Label(new Rect(x + 23f, y + 104f,
                SettingsWidth - 46f, 27f),
                "SHOW OR HIDE HUD FEATURES", _buildDetailStyle);
            SettingsToggle(x, y + 139f, "Party HUD", _enabled);
            SettingsToggle(x, y + 176f, "Teammate outlines", _showTeammateOutlines);
            SettingsToggle(x, y + 213f, "Teammate nicknames", _showPlayerNames);
            SettingsToggle(x, y + 250f, "Outlines through walls", _outlinesThroughWalls);
            SettingsToggle(x, y + 287f, "Host ping", _showHostPing);
            SettingsToggle(x, y + 324f, "Hunter companion health", _showCompanionHealth);
            SettingsToggle(x, y + 361f, "Wall health", _showWallHealth);
            SettingsToggle(x, y + 398f, "Host build inspector", _enableBuildInspector);
        }

        private void SettingsSlider(float x, float rowY, string title,
            float value, float min, float max, Action<float> setValue,
            string displayedValue)
        {
            GUI.Label(new Rect(x + 23f, rowY, 278f, 27f),
                title, _buildNameStyle);
            GUI.Label(new Rect(x + 370f, rowY, 100f, 27f),
                displayedValue, _healthStyle);
            float chosen = GUI.HorizontalSlider(
                new Rect(x + 28f, rowY + 29f, SettingsWidth - 56f, 20f),
                Mathf.Clamp(value, min, max), min, max);
            if (Mathf.Abs(chosen - value) > 0.001f)
            {
                setValue(chosen);
                _settingsNotice = title.ToUpperInvariant() + " SAVED";
            }
        }

        private void DrawLayoutSettings(float x, float y)
        {
            GUI.Label(new Rect(x + 23f, y + 104f,
                SettingsWidth - 46f, 25f),
                "CHANGES APPLY IMMEDIATELY", _buildDetailStyle);
            SettingsSlider(x, y + 139f, "HUD size",
                _scale.Value, 0.65f, 1.8f,
                value => _scale.Value = (float)Math.Round(value, 2),
                Mathf.RoundToInt(_scale.Value * 100f) + "%");
            SettingsSlider(x, y + 213f, "Horizontal offset",
                _leftOffset.Value, 8f, 460f,
                value => _leftOffset.Value = Mathf.RoundToInt(value),
                _leftOffset.Value + " px");
            ConfigEntry<int> verticalOffset = _anchor.Value == PanelAnchor.TopLeft
                ? _topOffset : _bottomOffset;
            SettingsSlider(x, y + 287f, "Vertical offset",
                verticalOffset.Value, 8f, 460f,
                value => verticalOffset.Value = Mathf.RoundToInt(value),
                verticalOffset.Value + " px");
            GUI.Label(new Rect(x + 23f, y + 362f, 165f, 27f),
                "Panel anchor", _buildNameStyle);
            if (SettingsButton(new Rect(x + 217f, y + 360f, 118f, 31f),
                "TOP LEFT", _anchor.Value == PanelAnchor.TopLeft))
            {
                _anchor.Value = PanelAnchor.TopLeft;
                Config.Save();
            }
            if (SettingsButton(new Rect(x + 345f, y + 360f, 128f, 31f),
                "BOTTOM LEFT", _anchor.Value == PanelAnchor.BottomLeft))
            {
                _anchor.Value = PanelAnchor.BottomLeft;
                Config.Save();
            }
            SettingsToggle(x, y + 406f, "Scale with resolution",
                _autoScaleToResolution);
            SettingsToggle(x, y + 448f, "Avoid native player roster",
                _avoidNativeRoster);
        }

        private void DrawHotkeySettings(float x, float y)
        {
            GUI.Label(new Rect(x + 23f, y + 103f, SettingsWidth - 46f, 29f),
                "CLICK A BINDING, THEN PRESS THE NEW KEY", _buildDetailStyle);
            string[] names =
            {
                "Open settings", "Open builds", "Next player",
                "Next ring page", "Previous ring page"
            };
            for (int i = 0; i < names.Length; i++)
            {
                float rowY = y + 145f + 53f * i;
                var option = SettingsShortcutEntry(i);
                string shown = _capturingShortcut == i
                    ? "PRESS KEY..."
                    : ShortcutLabel(option.Value);
                GUI.Label(new Rect(x + 23f, rowY + 2f, 195f, 30f),
                    names[i], _buildNameStyle);
                if (SettingsButton(new Rect(x + 220f, rowY, 255f, 33f),
                    FitText(shown, _settingsButtonStyle, 241f),
                    _capturingShortcut == i))
                {
                    _capturingShortcut = i;
                    _settingsNotice = "";
                }
            }
            GUI.Label(new Rect(x + 23f, y + 420f, SettingsWidth - 46f, 27f),
                "CTRL, SHIFT AND ALT COMBINATIONS ARE SUPPORTED.",
                _buildDetailStyle);
            if (SettingsButton(new Rect(x + 20f, y + 460f,
                SettingsWidth - 40f, 32f), "RESTORE DEFAULT HOTKEYS"))
                ResetSettingsShortcuts();
        }

        private ConfigEntry<KeyboardShortcut> SettingsShortcutEntry(int index)
        {
            switch (index)
            {
                case 0: return _toggleSettingsShortcut;
                case 1: return _toggleBuildInspectorShortcut;
                case 2: return _nextBuildPlayerShortcut;
                case 3: return _nextBuildPageShortcut;
                case 4: return _previousBuildPageShortcut;
                default: throw new ArgumentOutOfRangeException("index");
            }
        }

        private static bool ModifierKey(KeyCode key)
        {
            return key == KeyCode.LeftControl || key == KeyCode.RightControl ||
                key == KeyCode.LeftShift || key == KeyCode.RightShift ||
                key == KeyCode.LeftAlt || key == KeyCode.RightAlt;
        }

        private static void AddHeldModifier(List<KeyCode> modifiers,
            KeyCode left, KeyCode right, EventModifiers flag, Event e)
        {
            if (Input.GetKey(left))
                modifiers.Add(left);
            else if (Input.GetKey(right))
                modifiers.Add(right);
            else if ((e.modifiers & flag) != 0)
                modifiers.Add(left);
        }

        private void CaptureSettingsShortcut(Event e)
        {
            if (e.keyCode == KeyCode.None)
                return;
            if (e.keyCode == KeyCode.Escape)
            {
                _capturingShortcut = -1;
                _settingsNotice = "KEY CAPTURE CANCELLED";
                e.Use();
                return;
            }
            if (ModifierKey(e.keyCode))
                return;

            var held = new List<KeyCode>(3);
            AddHeldModifier(held, KeyCode.LeftControl, KeyCode.RightControl,
                EventModifiers.Control, e);
            AddHeldModifier(held, KeyCode.LeftShift, KeyCode.RightShift,
                EventModifiers.Shift, e);
            AddHeldModifier(held, KeyCode.LeftAlt, KeyCode.RightAlt,
                EventModifiers.Alt, e);
            var shortcut = new KeyboardShortcut(e.keyCode, held.ToArray());
            for (int i = 0; i < 5; i++)
            {
                if (i != _capturingShortcut &&
                    SettingsShortcutEntry(i).Value.Equals(shortcut))
                {
                    _settingsNotice = "KEY ALREADY USED. CHOOSE ANOTHER.";
                    e.Use();
                    return;
                }
            }

            SettingsShortcutEntry(_capturingShortcut).Value = shortcut;
            Config.Save();
            _settingsNotice = "HOTKEY SAVED: " + ShortcutLabel(shortcut);
            _capturingShortcut = -1;
            e.Use();
        }

        private void ResetSettingsShortcuts()
        {
            _toggleSettingsShortcut.Value = new KeyboardShortcut(KeyCode.F7);
            _toggleBuildInspectorShortcut.Value = new KeyboardShortcut(KeyCode.F8);
            _nextBuildPlayerShortcut.Value = new KeyboardShortcut(KeyCode.F9);
            _nextBuildPageShortcut.Value = new KeyboardShortcut(KeyCode.PageDown);
            _previousBuildPageShortcut.Value = new KeyboardShortcut(KeyCode.PageUp);
            Config.Save();
            _capturingShortcut = -1;
            _settingsNotice = "DEFAULT HOTKEYS RESTORED";
        }

        private void DrawBuildInspector(float x, float y)
        {
            DrawStonewardsWindow(x, y, BuildWindowWidth, BuildWindowHeight);
            GUI.Label(new Rect(x + 12f, y + 2f, BuildWindowWidth - 24f, 29f),
                "PLAYER BUILD", _headingStyle);

            if (_buildPlayers.Count == 0)
            {
                GUI.Label(new Rect(x + 18f, y + 70f,
                    BuildWindowWidth - 36f, 28f),
                    "NO TEAMMATES TO INSPECT", _buildNameStyle);
                GUI.Label(new Rect(x + 18f, y + 103f,
                    BuildWindowWidth - 36f, 30f),
                    "Only the hosting player can inspect party rings.",
                    _buildDetailStyle);
                GUI.Label(new Rect(x + 16f, y + BuildWindowHeight - 28f,
                    BuildWindowWidth - 32f, 18f),
                    FitText(ShortcutLabel(_toggleBuildInspectorShortcut.Value) +
                        " CLOSE", _buildHelpStyle, BuildWindowWidth - 32f),
                    _buildHelpStyle);
                return;
            }

            _buildPlayerIndex = Mathf.Clamp(_buildPlayerIndex, 0,
                _buildPlayers.Count - 1);
            var selected = _buildPlayers[_buildPlayerIndex];
            int totalPages = Mathf.Max(1,
                (selected.Rings.Count + BuildRingsPerPage - 1) / BuildRingsPerPage);
            _buildPage = Mathf.Clamp(_buildPage, 0, totalPages - 1);

            GUI.Label(new Rect(x + 18f, y + 53f, BuildWindowWidth - 105f, 26f),
                FitText(selected.Name, _buildNameStyle, BuildWindowWidth - 105f),
                _buildNameStyle);
            GUI.Label(new Rect(x + BuildWindowWidth - 85f, y + 55f, 70f, 22f),
                (_buildPlayerIndex + 1) + " / " + _buildPlayers.Count,
                _countStyle);

            string summary = !selected.HasRunData
                ? "BUILD DATA UNAVAILABLE"
                : selected.Rings.Count == 0 ? "NO RINGS RECORDED"
                    : selected.Rings.Count + " RING VARIANTS";
            GUI.Label(new Rect(x + 18f, y + 80f, BuildWindowWidth - 36f, 23f),
                summary, _buildDetailStyle);
            Fill(new Rect(x + 12f, y + 108f, BuildWindowWidth - 24f, 1f),
                PanelDivider);

            if (selected.HasRunData)
            {
                int start = _buildPage * BuildRingsPerPage;
                int end = Math.Min(start + BuildRingsPerPage, selected.Rings.Count);
                for (int i = start; i < end; i++)
                {
                    var ring = selected.Rings[i];
                    float rowY = y + 112f + (i - start) * BuildRingRowHeight;
                    if ((i - start) % 2 == 1)
                        Fill(new Rect(x + 11f, rowY, BuildWindowWidth - 22f,
                            BuildRingRowHeight),
                            new Color(0.30f, 0.33f, 0.39f, 0.14f));

                    Color rarityColor = GetRingRarityColor(ring.Rarity);
                    Fill(new Rect(x + 15f, rowY + 7f, 3f, 30f), rarityColor);
                    Fill(new Rect(x + 23f, rowY + 5f, 33f, 33f),
                        SlotBackground);
                    if (!DrawClassIcon(ring.Icon,
                        new Rect(x + 26f, rowY + 8f, 27f, 27f)))
                        Fill(new Rect(x + 31f, rowY + 13f, 17f, 17f),
                            rarityColor);

                    GUI.Label(new Rect(x + 64f, rowY + 2f,
                        BuildWindowWidth - 135f, 23f),
                        FitText(ring.Name, _buildNameStyle,
                            BuildWindowWidth - 135f), _buildNameStyle);
                    _buildDetailStyle.normal.textColor = rarityColor;
                    GUI.Label(new Rect(x + 64f, rowY + 23f,
                        BuildWindowWidth - 140f, 17f),
                        ring.Rarity.ToString(), _buildDetailStyle);
                    GUI.Label(new Rect(x + BuildWindowWidth - 63f, rowY + 8f,
                        48f, 25f), "x" + ring.Picks, _healthStyle);
                }
            }

            Fill(new Rect(x + 12f, y + BuildWindowHeight - 56f,
                BuildWindowWidth - 24f, 1f), PanelDivider);
            _buildHelpStyle.normal.textColor = new Color(0.92f, 0.83f, 0.70f);
            string playerHotkeys = ShortcutLabel(_toggleBuildInspectorShortcut.Value) +
                " CLOSE   " + ShortcutLabel(_nextBuildPlayerShortcut.Value) + " PLAYER";
            string pageHotkeys = ShortcutLabel(_previousBuildPageShortcut.Value) +
                " PREV   " + ShortcutLabel(_nextBuildPageShortcut.Value) +
                " NEXT   " + (_buildPage + 1) + "/" + totalPages;
            GUI.Label(new Rect(x + 15f, y + BuildWindowHeight - 51f,
                BuildWindowWidth - 29f, 18f),
                FitText(playerHotkeys, _buildHelpStyle, BuildWindowWidth - 29f),
                _buildHelpStyle);
            GUI.Label(new Rect(x + 15f, y + BuildWindowHeight - 28f,
                BuildWindowWidth - 29f, 18f),
                FitText(pageHotkeys, _buildHelpStyle, BuildWindowWidth - 29f),
                _buildHelpStyle);
        }

        private static Color GetRingRarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.UNCOMMON: return new Color(0.44f, 0.84f, 0.54f);
                case Rarity.RARE: return new Color(0.46f, 0.73f, 0.99f);
                case Rarity.EPIC: return new Color(0.76f, 0.53f, 0.96f);
                case Rarity.LEGENDARY: return new Color(1f, 0.77f, 0.37f);
                default: return new Color(0.85f, 0.84f, 0.82f);
            }
        }

        private static void DrawStonewardsWindow(float x, float y, float width, float height)
        {
            // Pixel-stepped corners as in the game's own windows.
            // All geometry is generated at runtime; no game assets distributed.
            Fill(new Rect(x + 7f, y, width - 14f, height), PanelBackground);
            Fill(new Rect(x, y + 7f, width, height - 14f), PanelBackground);
            Fill(new Rect(x + 7f, y + 2f, width - 14f, HeaderHeight - 2f), PanelHeader);
            Fill(new Rect(x + 2f, y + 7f, width - 4f, HeaderHeight - 7f), PanelHeader);

            const float edge = 2f;
            Fill(new Rect(x + 8f, y, width - 16f, edge), PanelFrame);
            Fill(new Rect(x + 5f, y + 2f, 3f, edge), PanelFrame);
            Fill(new Rect(x + 2f, y + 5f, 3f, edge), PanelFrame);
            Fill(new Rect(x, y + 7f, edge, height - 14f), PanelFrame);
            Fill(new Rect(x + width - edge, y + 7f, edge, height - 14f), PanelFrame);
            Fill(new Rect(x + width - 5f, y + 5f, 3f, edge), PanelFrame);
            Fill(new Rect(x + width - 8f, y + 2f, 3f, edge), PanelFrame);
            Fill(new Rect(x + 8f, y + height - edge, width - 16f, edge), PanelFrame);
            Fill(new Rect(x + 5f, y + height - 4f, 3f, edge), PanelFrame);
            Fill(new Rect(x + 2f, y + height - 7f, 3f, edge), PanelFrame);
            Fill(new Rect(x + width - 5f, y + height - 7f, 3f, edge), PanelFrame);
            Fill(new Rect(x + width - 8f, y + height - 4f, 3f, edge), PanelFrame);
            Fill(new Rect(x + 2f, y + HeaderHeight - 1f, width - 4f, 1f), PanelFrame);
        }

        private void DrawPlayer(float x, float rowY, PlayerEntry player, int index)
        {
            if (player.IsDead)
                Fill(new Rect(x + 3f, rowY, PanelWidth - 6f, RowHeight),
                    new Color(0.48f, 0.12f, 0.14f, 0.36f));
            else if (index % 2 == 1)
                Fill(new Rect(x + 3f, rowY, PanelWidth - 6f, RowHeight),
                    new Color(0.30f, 0.33f, 0.39f, 0.14f));

            Fill(new Rect(x + 6f, rowY + 8f, 3f, 51f),
                player.IsDead ? new Color(0.94f, 0.31f, 0.32f) : player.Accent);
            var iconSlot = new Rect(x + 14f, rowY + 11f, 38f, 39f);
            Fill(iconSlot, new Color(0.90f, 0.86f, 0.79f));
            Fill(new Rect(iconSlot.x + 2f, iconSlot.y + 2f,
                iconSlot.width - 4f, iconSlot.height - 4f), SlotBackground);
            if (!DrawClassIcon(player.ClassIcon, new Rect(iconSlot.x + 3f,
                iconSlot.y + 3f, iconSlot.width - 6f, iconSlot.height - 6f)))
            {
                Fill(iconSlot, player.IsDead
                    ? new Color(0.49f, 0.17f, 0.21f) : player.Accent);
                GUI.Label(new Rect(x + 14f, rowY + 16f, 38f, 28f),
                    player.Badge, _badgeStyle);
            }

            GUI.Label(new Rect(x + 62f, rowY + 6f, 119f, 25f),
                FitText(player.Name, _nameStyle, 119f), _nameStyle);

            string hp = player.HasStats && player.MaxHealth > 0f
                ? Mathf.CeilToInt(Mathf.Max(0f, player.Health)) + " / " +
                    Mathf.CeilToInt(player.MaxHealth)
                : "-- / --";
            GUI.Label(new Rect(x + 187f, rowY + 8f, 79f, 22f),
                hp, _healthStyle);

            GUI.Label(new Rect(x + 62f, rowY + 30f, 91f, 17f),
                FitText(player.ClassName, _classStyle, 91f), _classStyle);
            _statusStyle.normal.textColor = player.IsDead
                ? new Color(1f, 0.52f, 0.50f)
                : new Color(0.89f, 0.83f, 0.72f);
            if (player.ShowHostPing)
            {
                string pingLabel = player.IsLocalHost
                    ? "HOST"
                    : !player.HasPing ? "--"
                        : player.PingMs > 999 ? "999+ms" : player.PingMs + "ms";
                _pingStyle.normal.textColor = player.IsLocalHost
                    ? new Color(0.95f, 0.81f, 0.56f)
                    : !player.HasPing ? new Color(0.61f, 0.63f, 0.66f)
                        : player.PingMs < 100 ? new Color(0.47f, 0.87f, 0.56f)
                            : player.PingMs < 200 ? new Color(0.98f, 0.76f, 0.37f)
                                : new Color(0.98f, 0.42f, 0.42f);
                GUI.Label(new Rect(x + 156f, rowY + 30f, 58f, 17f),
                    pingLabel, _pingStyle);
                GUI.Label(new Rect(x + 216f, rowY + 30f, 50f, 17f),
                    player.IsDead ? "DOWN" : "ALIVE", _statusStyle);
            }
            else
            {
                GUI.Label(new Rect(x + 155f, rowY + 30f, 111f, 17f),
                    player.IsDead ? "DOWN - REVIVE" : "ALIVE", _statusStyle);
            }

            const float barWidth = PanelWidth - 82f;
            Fill(new Rect(x + 62f, rowY + 53f, barWidth, 8f), BarTrack);
            if (player.HasStats && player.MaxHealth > 0f && !player.IsDead)
            {
                float fraction = Mathf.Clamp01(player.Health / player.MaxHealth);
                // Familiar gameplay feedback takes priority over the warm skin.
                Color color = fraction > 0.65f
                    ? new Color(0.37f, 0.79f, 0.42f)
                    : fraction > 0.3f
                        ? new Color(0.97f, 0.72f, 0.30f)
                        : new Color(0.95f, 0.32f, 0.36f);
                if (fraction > 0f)
                    Fill(new Rect(x + 62f, rowY + 53f, barWidth * fraction, 8f), color);
            }

            Fill(new Rect(x + 12f, rowY + RowHeight - 1f, PanelWidth - 24f, 1f),
                PanelDivider);
        }

        private void DrawCompanion(float x, float rowY, CompanionEntry companion)
        {
            bool down = companion.IsKnockedOut;
            Fill(new Rect(x + 52f, rowY, PanelWidth - 60f, CompanionRowHeight),
                down ? new Color(0.53f, 0.12f, 0.16f, 0.30f)
                     : new Color(0.30f, 0.36f, 0.42f, 0.19f));
            Fill(new Rect(x + 59f, rowY + 4f, 2f, CompanionRowHeight - 9f),
                down ? new Color(1f, 0.37f, 0.43f) : new Color(0.44f, 0.76f, 0.52f));

            _companionStyle.normal.textColor = down
                ? new Color(1f, 0.48f, 0.51f)
                : new Color(0.87f, 0.79f, 0.64f);
            GUI.Label(new Rect(x + 69f, rowY + 2f, 108f, 21f),
                down ? "BEAR - DOWN" : "BEAR", _companionStyle);

            string hp = companion.MaxHealth > 0f
                ? Mathf.CeilToInt(Mathf.Max(0f, companion.Health)) + " / " +
                    Mathf.CeilToInt(companion.MaxHealth)
                : "-- / --";
            GUI.Label(new Rect(x + 185f, rowY + 2f, 81f, 22f),
                hp, _healthStyle);

            const float barWidth = PanelWidth - 91f;
            Fill(new Rect(x + 69f, rowY + 28f, barWidth, 6f), BarTrack);
            if (!down && companion.MaxHealth > 0f)
            {
                float fraction = Mathf.Clamp01(companion.Health / companion.MaxHealth);
                Color barColor = fraction > 0.3f
                    ? new Color(0.43f, 0.76f, 0.44f)
                    : new Color(0.96f, 0.64f, 0.30f);
                if (fraction > 0f)
                    Fill(new Rect(x + 69f, rowY + 28f, barWidth * fraction, 6f), barColor);
            }
        }

        private void DrawWallHealth(float x, float rowY)
        {
            // Group defensive wall sections below the team, never as a player.
            Fill(new Rect(x + 4f, rowY, PanelWidth - 8f, WallRowHeight),
                new Color(0.30f, 0.34f, 0.39f, 0.19f));
            Fill(new Rect(x + 12f, rowY, PanelWidth - 24f, 1f),
                PanelDivider);
            Fill(new Rect(x + 12f, rowY + 11f, 4f, WallRowHeight - 19f),
                _wallBreached ? new Color(1f, 0.34f, 0.38f)
                             : new Color(0.97f, 0.73f, 0.35f));

            string title = _wallSections > 1
                ? "WALL (" + _wallSections + ")" : "WALL";
            GUI.Label(new Rect(x + 23f, rowY + 5f, 128f, 26f),
                title, _nameStyle);

            string hp = Mathf.CeilToInt(Mathf.Max(0f, _wallHealth)) + " / " +
                Mathf.CeilToInt(_wallMaxHealth);
            GUI.Label(new Rect(x + 161f, rowY + 6f, 105f, 26f),
                hp, _healthStyle);

            _wallStatusStyle.normal.textColor = _wallBreached
                ? new Color(1f, 0.49f, 0.47f)
                : new Color(0.95f, 0.81f, 0.56f);
            GUI.Label(new Rect(x + 23f, rowY + 30f, PanelWidth - 46f, 17f),
                _wallBreached ? "BREACHED - REPAIR" : "DEFENSE", _wallStatusStyle);

            const float barWidth = PanelWidth - 47f;
            Fill(new Rect(x + 23f, rowY + 51f, barWidth, 9f), BarTrack);
            float fraction = Mathf.Clamp01(_wallHealth / _wallMaxHealth);
            if (fraction > 0f)
            {
                Color color = fraction > 0.65f
                    ? new Color(0.95f, 0.73f, 0.33f)
                    : fraction > 0.3f
                        ? new Color(0.95f, 0.53f, 0.26f)
                        : new Color(0.97f, 0.30f, 0.31f);
                Fill(new Rect(x + 23f, rowY + 51f, barWidth * fraction, 9f), color);
            }
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (_headingStyle == null)
            {
                _headingStyle = MakeStyle(19, new Color(0.98f, 0.79f, 0.51f),
                    TextAnchor.MiddleCenter, true);
                _countStyle = MakeStyle(12, new Color(0.92f, 0.85f, 0.75f),
                    TextAnchor.MiddleRight, true);
                _nameStyle = MakeStyle(16, Color.white, TextAnchor.MiddleLeft, true);
                _classStyle = MakeStyle(12, new Color(0.82f, 0.83f, 0.86f),
                    TextAnchor.MiddleLeft, true);
                _badgeStyle = MakeStyle(14, Color.white, TextAnchor.MiddleCenter, true);
                _healthStyle = MakeStyle(13, Color.white, TextAnchor.MiddleRight, true);
                _statusStyle = MakeStyle(12, Color.white, TextAnchor.MiddleRight, true);
                _companionStyle = MakeStyle(13, Color.white, TextAnchor.MiddleLeft, true);
                _alertStyle = MakeStyle(12, new Color(1f, 0.49f, 0.45f),
                    TextAnchor.MiddleLeft, true);
                _worldNameStyle = MakeStyle(12, Color.white, TextAnchor.MiddleCenter, true);
                _wallStatusStyle = MakeStyle(12, new Color(0.98f, 0.79f, 0.51f),
                    TextAnchor.MiddleLeft, true);
                _pingStyle = MakeStyle(12, Color.white, TextAnchor.MiddleLeft, true);
                _buildNameStyle = MakeStyle(16, Color.white,
                    TextAnchor.MiddleLeft, true);
                _buildDetailStyle = MakeStyle(12,
                    new Color(0.88f, 0.84f, 0.77f), TextAnchor.MiddleLeft, true);
                _buildHelpStyle = MakeStyle(11,
                    new Color(0.93f, 0.84f, 0.69f), TextAnchor.MiddleLeft, true);
                _settingsButtonStyle = MakeStyle(15,
                    new Color(0.98f, 0.90f, 0.76f), TextAnchor.MiddleCenter, true);
            }
            TryApplyNativeFont();
        }

        private void TryApplyNativeFont()
        {
            if (_nativeUiFont != null || Time.unscaledTime < _nextNativeFontLookup)
                return;
            // The UnityEngine.Font resources are present in Stonewards.
            _nextNativeFontLookup = Time.unscaledTime + 8f;
            Font preferred = null;
            Font fallback = null;
            foreach (var font in Resources.FindObjectsOfTypeAll<Font>())
            {
                if (font == null)
                    continue;
                if (font.name == "Jersey10-Regular")
                    preferred = font;
                else if (font.name == "Jersey10-Regular2")
                    fallback = font;
            }
            _nativeUiFont = preferred != null ? preferred : fallback;
            if (_nativeUiFont == null)
                return;
            GUIStyle[] styles =
            {
                _headingStyle, _countStyle, _nameStyle, _classStyle,
                _badgeStyle, _healthStyle, _statusStyle, _companionStyle,
                _alertStyle, _wallStatusStyle, _pingStyle,
                _buildNameStyle, _buildDetailStyle, _buildHelpStyle,
                _settingsButtonStyle
            };
            foreach (var style in styles)
            {
                style.font = _nativeUiFont;
                style.fontStyle = FontStyle.Normal;
            }
            _headingStyle.fontSize = 24;
            _countStyle.fontSize = 16;
            _nameStyle.fontSize = 20;
            _classStyle.fontSize = 15;
            _badgeStyle.fontSize = 17;
            _healthStyle.fontSize = 16;
            _statusStyle.fontSize = 15;
            _companionStyle.fontSize = 16;
            _alertStyle.fontSize = 15;
            _wallStatusStyle.fontSize = 16;
            _pingStyle.fontSize = 13;
            _buildNameStyle.fontSize = 19;
            _buildDetailStyle.fontSize = 15;
            _buildHelpStyle.fontSize = 12;
            _settingsButtonStyle.fontSize = 18;
            Logger.LogInfo("Party HUD is using Stonewards UI font: " + _nativeUiFont.name);
        }

        private static GUIStyle MakeStyle(int size, Color color, TextAnchor align, bool bold = false)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                alignment = align,
                clipping = TextClipping.Clip,
                richText = false
            };
            style.normal.textColor = color;
            return style;
        }

        private static string ShortcutLabel(KeyboardShortcut shortcut)
        {
            // Use the actual user-configured binding in every on-screen hint.
            // Keep labels compact without changing which physical key is used.
            if (shortcut.MainKey == KeyCode.None)
                return "UNBOUND";

            return shortcut.ToString().ToUpperInvariant()
                .Replace("LEFTCONTROL", "LCTRL")
                .Replace("RIGHTCONTROL", "RCTRL")
                .Replace("LEFTSHIFT", "LSHIFT")
                .Replace("RIGHTSHIFT", "RSHIFT")
                .Replace("LEFTALT", "LALT")
                .Replace("RIGHTALT", "RALT")
                .Replace("PAGEDOWN", "PGDN")
                .Replace("PAGEUP", "PGUP")
                .Replace(" + ", "+");
        }

        private static string CleanName(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "Connecting..." : name;
        }

        private static string FitText(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || style.CalcSize(new GUIContent(text)).x <= width)
                return text;

            const string suffix = "...";
            int length = text.Length;
            while (length > 0 &&
                style.CalcSize(new GUIContent(text.Substring(0, length) + suffix)).x > width)
                length--;

            return length > 0 ? text.Substring(0, length) + suffix : suffix;
        }

        private static Sprite FindClassIcon(FirstPersonController player, PlayerClass cls)
        {
            // The game's archetype database owns the original class sprites.
            // Nothing is copied or bundled with this plugin.
            try
            {
                var manager = PlayerArchetypeManager.Instance;
                if (manager != null)
                {
                    var archetype = manager.GetPlayerArchetype(cls);
                    if (archetype != null && archetype.Icon != null)
                        return archetype.Icon;
                }
            }
            catch
            {
                // The archetype manager might not yet be initialized during a scene load.
            }

            try
            {
                var archetype = player.PlayerArchetypeSO;
                if (archetype != null && archetype.PlayerClass == cls)
                    return archetype.Icon;
            }
            catch
            {
                // Retain the two-letter fallback until the class data is available.
            }

            return null;
        }

        private static bool DrawClassIcon(Sprite sprite, Rect area)
        {
            if (sprite == null)
                return false;

            try
            {
                var texture = sprite.texture;
                if (texture == null || texture.width == 0 || texture.height == 0)
                    return false;

                // A Sprite may occupy only a portion of a texture atlas. Use its
                // actual texture coordinates instead of drawing the entire atlas.
                Rect texRect = sprite.textureRect;
                Rect uv = new Rect(texRect.x / texture.width, texRect.y / texture.height,
                    texRect.width / texture.width, texRect.height / texture.height);

                Rect original = sprite.rect;
                if (original.width <= 0f || original.height <= 0f)
                    return false;

                float ratio = original.width / original.height;
                if (ratio > 1f)
                {
                    float h = area.width / ratio;
                    area.y += (area.height - h) * 0.5f;
                    area.height = h;
                }
                else
                {
                    float w = area.height * ratio;
                    area.x += (area.width - w) * 0.5f;
                    area.width = w;
                }

                GUI.DrawTextureWithTexCoords(area, texture, uv, true);
                return true;
            }
            catch (Exception)
            {
                // Some packed sprites do not expose usable texture coordinates.
                // A failed icon must never break drawing of the party panel.
                return false;
            }
        }

        private static string ClassBadge(PlayerClass cls)
        {
            switch (cls)
            {
                case PlayerClass.WARRIOR: return "WA";
                case PlayerClass.ARCHER: return "AR";
                case PlayerClass.MINER: return "MI";
                case PlayerClass.ROGUE: return "RO";
                case PlayerClass.BOMBERMAN: return "BO";
                case PlayerClass.HUNTER: return "HU";
                case PlayerClass.MAGE: return "MA";
                case PlayerClass.HEALER: return "HE";
                default: return "??";
            }
        }

        private static Color ClassColor(PlayerClass cls)
        {
            switch (cls)
            {
                case PlayerClass.WARRIOR: return new Color(0.70f, 0.40f, 0.23f);
                case PlayerClass.ARCHER: return new Color(0.28f, 0.53f, 0.32f);
                case PlayerClass.MINER: return new Color(0.64f, 0.49f, 0.20f);
                case PlayerClass.ROGUE: return new Color(0.48f, 0.35f, 0.62f);
                case PlayerClass.BOMBERMAN: return new Color(0.65f, 0.30f, 0.27f);
                case PlayerClass.HUNTER: return new Color(0.27f, 0.53f, 0.53f);
                case PlayerClass.MAGE: return new Color(0.24f, 0.43f, 0.68f);
                case PlayerClass.HEALER: return new Color(0.32f, 0.60f, 0.64f);
                default: return new Color(0.40f, 0.48f, 0.55f);
            }
        }
    }
}
