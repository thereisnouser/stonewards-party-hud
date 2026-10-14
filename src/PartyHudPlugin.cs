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
        public const string PluginVersion = "0.2.1";

        private const float RefreshSeconds = 0.2f;
        private const float PanelWidth = 278f;
        private const float HeaderHeight = 49f;
        private const float RowHeight = 67f;
        private const float CompanionRowHeight = 38f;
        private const float WallRowHeight = 66f;
        private const float FooterPadding = 8f;
        private const float BottomHudSafeZone = 132f;
        private const float NativeRosterHeaderSafeZone = 130f;
        private const float NativeRosterPlayerRowSafeZone = 42f;
        // Match the approved compact on-screen proportions (~296px wide
        // at 1920x1080 with the default Scale and ReadabilityScale).
        // Keep fixed-size layout across resolutions; shrink only to fit.
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

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true,
                "Show the party status HUD when network players are present.");
            _scale = Config.Bind("Appearance", "Scale", 1.0f,
                "HUD size multiplier (clamped to 0.65 - 1.8).");
            _readabilityScale = Config.Bind("Appearance", "ReadabilityScale", 1.12f,
                "Extra panel enlargement (1.0 - 1.4), also applied to existing configurations.");
            _autoScaleToResolution = Config.Bind("Appearance", "AutoScaleToResolution", true,
                "Keep the same HUD size at every resolution. Shrink only if needed to fit the screen and native UI safe zones. Disable to fit only the screen edges.");
            _showCompanionHealth = Config.Bind("Appearance", "ShowCompanionHealth", true,
                "Show a Hunter's companion health directly below the Hunter's entry.");
            _showWallHealth = Config.Bind("General", "ShowWallHealth", true,
                "Show combined defensive wall durability beneath the party roster.");
            _showHostPing = Config.Bind("Appearance", "ShowHostPing", true,
                "On the hosting player's screen, show each remote player's real Mirror connection RTT in ms. Other clients never display unverified peer ping.");
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
            if (!_enabled.Value)
            {
                RemoveAllOutlines();
                return;
            }

            if (Time.unscaledTime < _nextRefresh)
                return;

            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            RefreshRoster();
            RefreshWallHealth();
            UpdateTeammateOutlines();
        }

        private void OnDisable()
        {
            RemoveAllOutlines();
        }

        private void OnDestroy()
        {
            RemoveAllOutlines();
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

                    var renderers = root.GetComponentsInChildren<Renderer>(true);
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
                            outline.enabled = true;
                        }
                        catch
                        {
                            Destroy(outline);
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
                    if (!entry.Component.enabled)
                        entry.Component.enabled = true;

                    if (Time.unscaledTime >= entry.NextMaterialCheck)
                    {
                        entry.NextMaterialCheck = Time.unscaledTime + 2f;
                        if (IsOutlineMaterialMissing(entry))
                        {
                            entry.Component.enabled = false;
                            entry.Component.enabled = true;
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
            if (!_enabled.Value || _players.Count == 0 ||
                !Mirror.NetworkClient.active ||
                Event.current == null || Event.current.type != EventType.Repaint ||
                IsBlockingGameMenuOpen())
                return;

            EnsureStyles();
            DrawTeammatePlayerNames();

            float height = HeaderHeight + _players.Count * RowHeight + FooterPadding;
            if (_hasWall)
                height += WallRowHeight;
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].Companion != null)
                    height += CompanionRowHeight;

            // Resolution-independent by default. Use a fixed pixel scale on
            // 1080p/1440p/4K and smaller windows alike, unless the complete
            // panel actually needs more room than is available.
            float scale = Mathf.Clamp(_scale.Value *
                Mathf.Clamp(_readabilityScale.Value, 1f, 1.4f) *
                PanelSizeBoost, 0.55f, 1.8f);

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
            // A separate caption row prevents DOWN and party count from
            // overlapping the centered title on the narrower panel.
            GUI.Label(new Rect(x + 20f, y + 2f, PanelWidth - 40f, 29f),
                "PARTY STATUS", _headingStyle);

            int downed = 0;
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].IsDead)
                    downed++;

            if (downed > 0)
                GUI.Label(new Rect(x + 14f, y + 28f, 116f, 18f),
                    downed + " DOWN", _alertStyle);
            GUI.Label(new Rect(x + PanelWidth - 80f, y + 28f, 66f, 18f),
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
                _alertStyle, _wallStatusStyle, _pingStyle
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
