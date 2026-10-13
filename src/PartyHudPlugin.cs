using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace StonewardsPartyHUD
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class PartyHudPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "community.stonewards.partyhud";
        public const string PluginName = "Stonewards Party HUD";
        public const string PluginVersion = "0.1.0";

        private const float RefreshSeconds = 0.2f;
        private const float PanelWidth = 358f;
        private const float HeaderHeight = 40f;
        private const float RowHeight = 67f;
        private const float CompanionRowHeight = 38f;
        private const float FooterPadding = 8f;
        private const float BottomHudSafeZone = 132f;

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
        private static readonly FieldInfo CharacterVisualsParentField =
            typeof(FirstPersonController).GetField("characterVisualsParent",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _scale;
        private ConfigEntry<bool> _showCompanionHealth;
        private ConfigEntry<bool> _showTeammateOutlines;
        private ConfigEntry<bool> _outlinesThroughWalls;
        private ConfigEntry<float> _outlineWidth;
        private ConfigEntry<int> _leftOffset;
        private ConfigEntry<int> _topOffset;
        private ConfigEntry<int> _bottomOffset;
        private ConfigEntry<PanelAnchor> _anchor;
        private float _nextRefresh;
        private float _nextWarning;
        private GUIStyle _headingStyle;
        private GUIStyle _countStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _classStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _healthStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _companionStyle;
        private GUIStyle _alertStyle;

        private sealed class PlayerEntry
        {
            public string Name;
            public string ClassName;
            public string Badge;
            public Color Accent;
            public float Health;
            public float MaxHealth;
            public bool HasStats;
            public bool IsDead;
            public CompanionEntry Companion;
        }

        private sealed class OutlineEntry
        {
            public GameObject VisualRoot;
            public QuickOutline Component;
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
            _showCompanionHealth = Config.Bind("Appearance", "ShowCompanionHealth", true,
                "Show a Hunter's companion health directly below the Hunter's entry.");
            _showTeammateOutlines = Config.Bind("TeammateOutlines", "Enabled", true,
                "Outline remote teammates in the world (never the local player).");
            _outlinesThroughWalls = Config.Bind("TeammateOutlines", "ThroughWalls", true,
                "Show teammate silhouettes even when they are behind obstacles.");
            _outlineWidth = Config.Bind("TeammateOutlines", "Width", 3.0f,
                "Outline thickness; clamped to 0.5 - 6.0.");
            _anchor = Config.Bind("Appearance", "Anchor", PanelAnchor.TopLeft,
                "Panel location: TopLeft (recommended) or BottomLeft.");
            _leftOffset = Config.Bind("Appearance", "LeftOffset", 20,
                "Distance from the left edge, in unscaled pixels.");
            _topOffset = Config.Bind("Appearance", "TopOffset", 105,
                "Distance from the top when Anchor is TopLeft; clears native upper-left text.");
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
                        Accent = ClassColor(playerClass)
                    };

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

                    OutlineEntry entry;
                    if (!_teamOutlines.TryGetValue(player, out entry))
                    {
                        // Never overwrite an outline that belongs to the game or another mod.
                        if (root.GetComponent<QuickOutline>() != null)
                            continue;

                        var renderers = root.GetComponentsInChildren<Renderer>(true);
                        if (renderers.Length == 0)
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

                        entry = new OutlineEntry { VisualRoot = root, Component = outline };
                        _teamOutlines.Add(player, entry);
                        Logger.LogInfo("Initialized outline for teammate " +
                            player.NetworkplayerName + " (" + renderers.Length + " renderers).");
                    }

                    if (entry.Component == null)
                        continue;

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

        private void OnGUI()
        {
            if (!_enabled.Value || _players.Count == 0 ||
                Event.current == null || Event.current.type != EventType.Repaint)
                return;

            EnsureStyles();

            float height = HeaderHeight + _players.Count * RowHeight + FooterPadding;
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].Companion != null)
                    height += CompanionRowHeight;

            // Fit even when the player uses a large scale or a small display.
            float scale = Mathf.Clamp(_scale.Value, 0.65f, 1.8f);
            scale = Mathf.Min(scale, (Screen.width - 16f) / PanelWidth,
                (Screen.height - 16f) / height);
            if (scale <= 0f)
                return;

            float screenWidth = Screen.width / scale;
            float screenHeight = Screen.height / scale;
            float x = Mathf.Clamp(_leftOffset.Value, 8f,
                Mathf.Max(8f, screenWidth - PanelWidth - 8f));
            float requestedY = _anchor.Value == PanelAnchor.TopLeft
                ? _topOffset.Value
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
            Fill(new Rect(x, y, PanelWidth, height), new Color(0.035f, 0.073f, 0.11f, 0.94f));
            Fill(new Rect(x, y, PanelWidth, 2f), new Color(0.27f, 0.67f, 0.77f));
            Fill(new Rect(x, y + height - 1f, PanelWidth, 1f),
                new Color(0.34f, 0.56f, 0.64f, 0.38f));
            Fill(new Rect(x, y + HeaderHeight - 1f, PanelWidth, 1f),
                new Color(0.36f, 0.52f, 0.62f, 0.45f));

            GUI.Label(new Rect(x + 15f, y + 7f, 171f, 27f), "PARTY STATUS", _headingStyle);

            int downed = 0;
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].IsDead)
                    downed++;

            if (downed > 0)
                GUI.Label(new Rect(x + 165f, y + 11f, 83f, 20f),
                    downed + " DOWN", _alertStyle);
            GUI.Label(new Rect(x + 252f, y + 11f, 91f, 20f),
                _players.Count + " PLAYER" + (_players.Count == 1 ? "" : "S"), _countStyle);

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
        }

        private void DrawPlayer(float x, float rowY, PlayerEntry player, int index)
        {
            if (player.IsDead)
                Fill(new Rect(x + 3f, rowY, PanelWidth - 3f, RowHeight),
                    new Color(0.51f, 0.10f, 0.15f, 0.28f));
            else if (index % 2 == 1)
                Fill(new Rect(x + 3f, rowY, PanelWidth - 3f, RowHeight),
                    new Color(0.24f, 0.39f, 0.46f, 0.14f));

            Fill(new Rect(x + 5f, rowY + 8f, 3f, 51f),
                player.IsDead ? new Color(1f, 0.34f, 0.39f) : player.Accent);
            Fill(new Rect(x + 14f, rowY + 11f, 38f, 39f),
                player.IsDead ? new Color(0.49f, 0.17f, 0.21f) : player.Accent);
            GUI.Label(new Rect(x + 14f, rowY + 16f, 38f, 28f),
                player.Badge, _badgeStyle);

            GUI.Label(new Rect(x + 62f, rowY + 6f, 196f, 25f),
                player.Name, _nameStyle);

            string hp = player.HasStats && player.MaxHealth > 0f
                ? Mathf.CeilToInt(Mathf.Max(0f, player.Health)) + " / " +
                    Mathf.CeilToInt(player.MaxHealth)
                : "-- / --";
            GUI.Label(new Rect(x + 263f, rowY + 8f, 81f, 22f),
                hp, _healthStyle);

            GUI.Label(new Rect(x + 62f, rowY + 30f, 115f, 17f),
                player.ClassName, _classStyle);
            _statusStyle.normal.textColor = player.IsDead
                ? new Color(1f, 0.39f, 0.44f)
                : new Color(0.62f, 0.79f, 0.81f);
            GUI.Label(new Rect(x + 180f, rowY + 30f, 164f, 17f),
                player.IsDead ? "DOWN - REVIVE" : "ALIVE", _statusStyle);

            const float barWidth = 282f;
            Fill(new Rect(x + 62f, rowY + 53f, barWidth, 8f),
                new Color(0.22f, 0.30f, 0.37f, 1f));
            if (player.HasStats && player.MaxHealth > 0f && !player.IsDead)
            {
                float fraction = Mathf.Clamp01(player.Health / player.MaxHealth);
                Color color = fraction > 0.65f
                    ? new Color(0.31f, 0.80f, 0.52f)
                    : fraction > 0.3f
                        ? new Color(0.97f, 0.72f, 0.30f)
                        : new Color(0.95f, 0.32f, 0.36f);
                if (fraction > 0f)
                    Fill(new Rect(x + 62f, rowY + 53f, barWidth * fraction, 8f), color);
            }

            Fill(new Rect(x + 14f, rowY + RowHeight - 1f, PanelWidth - 28f, 1f),
                new Color(0.34f, 0.50f, 0.56f, 0.14f));
        }

        private void DrawCompanion(float x, float rowY, CompanionEntry companion)
        {
            bool down = companion.IsKnockedOut;
            Fill(new Rect(x + 52f, rowY, PanelWidth - 60f, CompanionRowHeight),
                down ? new Color(0.53f, 0.12f, 0.16f, 0.30f)
                     : new Color(0.17f, 0.34f, 0.29f, 0.30f));
            Fill(new Rect(x + 59f, rowY + 4f, 2f, CompanionRowHeight - 9f),
                down ? new Color(1f, 0.37f, 0.43f) : new Color(0.44f, 0.76f, 0.52f));

            _companionStyle.normal.textColor = down
                ? new Color(1f, 0.48f, 0.51f)
                : new Color(0.76f, 0.88f, 0.78f);
            GUI.Label(new Rect(x + 69f, rowY + 2f, 195f, 21f),
                down ? "BEAR - KNOCKED OUT" : "BEAR", _companionStyle);

            string hp = companion.MaxHealth > 0f
                ? Mathf.CeilToInt(Mathf.Max(0f, companion.Health)) + " / " +
                    Mathf.CeilToInt(companion.MaxHealth)
                : "-- / --";
            GUI.Label(new Rect(x + 263f, rowY + 2f, 81f, 22f),
                hp, _healthStyle);

            const float barWidth = 275f;
            Fill(new Rect(x + 69f, rowY + 28f, barWidth, 6f),
                new Color(0.22f, 0.30f, 0.37f, 1f));
            if (!down && companion.MaxHealth > 0f)
            {
                float fraction = Mathf.Clamp01(companion.Health / companion.MaxHealth);
                Color barColor = fraction > 0.3f
                    ? new Color(0.43f, 0.75f, 0.52f)
                    : new Color(0.96f, 0.64f, 0.30f);
                if (fraction > 0f)
                    Fill(new Rect(x + 69f, rowY + 28f, barWidth * fraction, 6f), barColor);
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
            if (_headingStyle != null)
                return;

            _headingStyle = MakeStyle(15, Color.white, TextAnchor.MiddleLeft, true);
            _countStyle = MakeStyle(11, new Color(0.65f, 0.80f, 0.87f), TextAnchor.MiddleRight, true);
            _nameStyle = MakeStyle(15, Color.white, TextAnchor.MiddleLeft, true);
            _classStyle = MakeStyle(11, new Color(0.68f, 0.82f, 0.90f), TextAnchor.MiddleLeft, true);
            _badgeStyle = MakeStyle(13, Color.white, TextAnchor.MiddleCenter, true);
            _healthStyle = MakeStyle(12, Color.white, TextAnchor.MiddleRight, true);
            _statusStyle = MakeStyle(11, Color.white, TextAnchor.MiddleRight, true);
            _companionStyle = MakeStyle(12, Color.white, TextAnchor.MiddleLeft, true);
            _alertStyle = MakeStyle(11, new Color(1f, 0.40f, 0.44f), TextAnchor.MiddleRight, true);
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
