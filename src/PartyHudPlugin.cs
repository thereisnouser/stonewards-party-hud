using System;
using System.Collections.Generic;
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
        private const float PanelWidth = 326f;
        private const float HeaderHeight = 32f;
        private const float RowHeight = 55f;
        private const float FooterPadding = 6f;

        private readonly List<PlayerEntry> _players = new List<PlayerEntry>(4);
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _scale;
        private ConfigEntry<int> _leftOffset;
        private ConfigEntry<int> _bottomOffset;
        private float _nextRefresh;
        private float _nextWarning;
        private GUIStyle _headingStyle;
        private GUIStyle _countStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _classStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _healthStyle;
        private GUIStyle _statusStyle;

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
        }

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true,
                "Show the party status HUD when network players are present.");
            _scale = Config.Bind("Appearance", "Scale", 1.0f,
                "HUD size multiplier (clamped to 0.65 - 1.8).");
            _leftOffset = Config.Bind("Appearance", "LeftOffset", 20,
                "Distance from the left screen edge, in unscaled pixels.");
            _bottomOffset = Config.Bind("Appearance", "BottomOffset", 74,
                "Distance from the bottom screen edge, in unscaled pixels.");
            Logger.LogInfo("Party HUD v" + PluginVersion + " initialized.");
        }

        private void Update()
        {
            if (!_enabled.Value)
                return;

            if (Time.unscaledTime < _nextRefresh)
                return;

            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            RefreshRoster();
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

        private void OnGUI()
        {
            if (!_enabled.Value || _players.Count == 0 || Event.current.type != EventType.Repaint)
                return;

            EnsureStyles();

            float scale = Mathf.Clamp(_scale.Value, 0.65f, 1.8f);
            float screenWidth = Screen.width / scale;
            float screenHeight = Screen.height / scale;
            float height = HeaderHeight + _players.Count * RowHeight + FooterPadding;
            float x = Mathf.Clamp(_leftOffset.Value, 0f, Mathf.Max(0f, screenWidth - PanelWidth - 8f));
            float y = Mathf.Clamp(screenHeight - _bottomOffset.Value - height,
                8f, Mathf.Max(8f, screenHeight - height));

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
            Fill(new Rect(x, y, PanelWidth, height), new Color(0.045f, 0.085f, 0.13f, 0.91f));
            Fill(new Rect(x, y, 3f, height), new Color(0.34f, 0.66f, 0.78f, 0.95f));
            Fill(new Rect(x + 3f, y + HeaderHeight - 1f, PanelWidth - 3f, 1f),
                new Color(0.35f, 0.49f, 0.58f, 0.4f));

            GUI.Label(new Rect(x + 13f, y + 4f, 190f, 24f), "PARTY STATUS", _headingStyle);
            GUI.Label(new Rect(x + 215f, y + 5f, 98f, 22f),
                _players.Count + " PLAYER" + (_players.Count == 1 ? "" : "S"), _countStyle);

            for (int i = 0; i < _players.Count; i++)
                DrawPlayer(x, y + HeaderHeight + i * RowHeight, _players[i], i);
        }

        private void DrawPlayer(float x, float rowY, PlayerEntry player, int index)
        {
            if (player.IsDead)
                Fill(new Rect(x + 3f, rowY, PanelWidth - 3f, RowHeight),
                    new Color(0.48f, 0.09f, 0.14f, 0.26f));
            else if (index % 2 == 1)
                Fill(new Rect(x + 3f, rowY, PanelWidth - 3f, RowHeight),
                    new Color(0.27f, 0.42f, 0.52f, 0.09f));

            Fill(new Rect(x + 11f, rowY + 9f, 32f, 33f),
                player.IsDead ? new Color(0.39f, 0.12f, 0.17f, 0.95f) : player.Accent);
            GUI.Label(new Rect(x + 11f, rowY + 13f, 32f, 25f), player.Badge, _badgeStyle);

            GUI.Label(new Rect(x + 51f, rowY + 4f, 165f, 23f), player.Name, _nameStyle);
            GUI.Label(new Rect(x + 214f, rowY + 4f, 99f, 23f), player.ClassName, _classStyle);

            const float barWidth = 182f;
            Fill(new Rect(x + 51f, rowY + 28f, barWidth, 8f),
                new Color(0.22f, 0.30f, 0.36f, 1f));
            if (player.HasStats && player.MaxHealth > 0f && !player.IsDead)
            {
                float fraction = Mathf.Clamp01(player.Health / player.MaxHealth);
                float healthBar = barWidth * fraction;
                Color color = fraction > 0.65f
                    ? new Color(0.33f, 0.79f, 0.53f, 1f)
                    : fraction > 0.3f
                        ? new Color(0.96f, 0.72f, 0.31f, 1f)
                        : new Color(0.92f, 0.28f, 0.32f, 1f);
                if (healthBar > 0f)
                    Fill(new Rect(x + 51f, rowY + 28f, healthBar, 8f), color);
            }

            string hp = player.HasStats && player.MaxHealth > 0f
                ? Mathf.CeilToInt(Mathf.Max(0f, player.Health)) + " / " + Mathf.CeilToInt(player.MaxHealth)
                : "-- / --";
            GUI.Label(new Rect(x + 236f, rowY + 21f, 77f, 24f), hp, _healthStyle);
            _statusStyle.normal.textColor = player.IsDead
                ? new Color(1f, 0.43f, 0.48f, 1f)
                : new Color(0.58f, 0.74f, 0.82f, 1f);
            GUI.Label(new Rect(x + 51f, rowY + 37f, 235f, 17f),
                player.IsDead ? "DOWN - REVIVE" : "ALIVE", _statusStyle);
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

            _headingStyle = MakeStyle(13, Color.white, TextAnchor.MiddleLeft, true);
            _countStyle = MakeStyle(11, new Color(0.62f, 0.76f, 0.84f), TextAnchor.MiddleRight);
            _nameStyle = MakeStyle(13, Color.white, TextAnchor.MiddleLeft, true);
            _classStyle = MakeStyle(11, new Color(0.67f, 0.80f, 0.89f), TextAnchor.MiddleRight);
            _badgeStyle = MakeStyle(12, Color.white, TextAnchor.MiddleCenter, true);
            _healthStyle = MakeStyle(11, Color.white, TextAnchor.MiddleRight);
            _statusStyle = MakeStyle(10, Color.white, TextAnchor.MiddleLeft, true);
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
