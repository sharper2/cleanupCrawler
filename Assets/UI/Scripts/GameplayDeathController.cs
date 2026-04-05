using CleanupCrawler.Levels;
using DungeonGenerator;
using UnityEngine;

namespace CleanupCrawler.UI
{
    /// <summary>
    /// Player death: freezes gameplay, shows a simple OnGUI overlay with Retry (rebuilds the same level with a new layout).
    /// Add to the player. Ensure <see cref="HealthComponent"/> has <c>destroyOnDeath</c> disabled so the player object survives.
    /// </summary>
    public sealed class GameplayDeathController : MonoBehaviour
    {
        public static bool IsDeathScreenActive { get; private set; }

        [Header("References")]
        [SerializeField] private HealthComponent playerHealth;
        [SerializeField] private AbilityQueueComponent abilityQueue;
        [SerializeField] private LevelController levelController;

        [Header("OnGUI overlay")]
        [SerializeField, Tooltip("Optional. Uses default IMGUI font when unset.")]
        private Font onGuiFont;
        [SerializeField, Min(8)] private int onGuiFontSize = 18;
        [SerializeField] private Color onGuiTitleColor = new Color(1f, 0.45f, 0.45f, 1f);
        [SerializeField] private Color onGuiBodyColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] private float onGuiButtonHeight = 44f;
        [SerializeField] private float onGuiButtonMaxWidth = 280f;
        [SerializeField] private Color onGuiButtonBackgroundColor = new Color(0.22f, 0.45f, 0.85f, 1f);
        [SerializeField] private Color onGuiButtonHoverColor = new Color(0.30f, 0.52f, 0.92f, 1f);
        [SerializeField] private Color onGuiButtonTextColor = Color.white;

        [Header("Copy")]
        [TextArea(4, 10)]
        [SerializeField] private string deathMessage = "You died.\n\nRetry to generate this level again with a new layout.";

        private bool _showDeathScreen;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _flatButtonStyle;
        private Font _stylesBuiltForFont;
        private int _stylesBuiltForSize;

        private void Awake()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponent<HealthComponent>()
                               ?? GetComponentInChildren<HealthComponent>(true)
                               ?? GetComponentInParent<HealthComponent>();
            }

            if (levelController == null)
            {
                levelController = FindFirstObjectByType<LevelController>();
            }
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died += OnPlayerDied;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= OnPlayerDied;
            }
        }

        private void OnDestroy()
        {
            if (IsDeathScreenActive && _showDeathScreen)
            {
                IsDeathScreenActive = false;
                GameplayPause.SetPaused(false);
            }
        }

        private void OnPlayerDied()
        {
            abilityQueue?.ClearQueue();

            _showDeathScreen = true;
            IsDeathScreenActive = true;
            GameplayPause.SetPaused(true);
        }

        /// <summary>Rebuilds the current level (same profile) and restores health.</summary>
        public void Retry()
        {
            if (!_showDeathScreen)
            {
                return;
            }

            _showDeathScreen = false;
            IsDeathScreenActive = false;

            if (levelController != null)
            {
                levelController.BuildLevel();
            }

            if (playerHealth != null)
            {
                playerHealth.SetCurrentHealth(playerHealth.MaxHealth);
            }

            GameplayPause.SetPaused(false);
        }

        private void OnGUI()
        {
            if (!_showDeathScreen)
            {
                return;
            }

            var prevDepth = GUI.depth;
            GUI.depth = 1100;

            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            var dimColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(dim, Texture2D.whiteTexture);
            GUI.color = dimColor;

            float boxW = Mathf.Min(520f, Screen.width - 48f);
            float boxH = Mathf.Min(320f, Screen.height - 48f);
            var box = new Rect((Screen.width - boxW) * 0.5f, (Screen.height - boxH) * 0.5f, boxW, boxH);

            EnsureStyles();

            GUILayout.BeginArea(box);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(boxW));
            GUILayout.Label("Defeated", _titleStyle);
            GUILayout.Space(12f);
            GUILayout.Label(deathMessage, _bodyStyle);
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var btnW = Mathf.Min(onGuiButtonMaxWidth, boxW - 32f);
            var oldBg = GUI.backgroundColor;
            var oldGuiColor = GUI.color;
            Rect continueRect = GUILayoutUtility.GetRect(btnW, onGuiButtonHeight, _flatButtonStyle);
            bool hover = (Event.current.type == EventType.Repaint || Event.current.type == EventType.MouseMove)
                         && continueRect.Contains(Event.current.mousePosition);
            GUI.backgroundColor = hover ? onGuiButtonHoverColor : onGuiButtonBackgroundColor;
            GUI.color = Color.white;
            if (GUI.Button(continueRect, "Retry", _flatButtonStyle))
            {
                Retry();
            }

            GUI.backgroundColor = oldBg;
            GUI.color = oldGuiColor;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUI.depth = prevDepth;
        }

        private void EnsureStyles()
        {
            var font = onGuiFont != null ? onGuiFont : GUI.skin.font;
            if (_titleStyle == null || _bodyStyle == null || _flatButtonStyle == null
                || _stylesBuiltForFont != font || _stylesBuiltForSize != onGuiFontSize)
            {
                _stylesBuiltForFont = font;
                _stylesBuiltForSize = onGuiFontSize;
                var flatTex = Texture2D.whiteTexture;

                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = onGuiFontSize + 8,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };

                _bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = onGuiFontSize,
                    alignment = TextAnchor.UpperCenter,
                    wordWrap = true
                };

                _flatButtonStyle = new GUIStyle
                {
                    font = font,
                    fontSize = onGuiFontSize + 1,
                    alignment = TextAnchor.MiddleCenter,
                    border = new RectOffset(0, 0, 0, 0),
                    padding = new RectOffset(16, 16, 12, 12),
                    margin = new RectOffset(0, 0, 0, 0),
                    clipping = TextClipping.Clip
                };
                _flatButtonStyle.normal.background = flatTex;
                _flatButtonStyle.hover.background = flatTex;
                _flatButtonStyle.active.background = flatTex;
                _flatButtonStyle.focused.background = flatTex;
            }

            _titleStyle.normal.textColor = onGuiTitleColor;
            _bodyStyle.normal.textColor = onGuiBodyColor;
            _flatButtonStyle.fontSize = onGuiFontSize + 1;
            _flatButtonStyle.normal.textColor = onGuiButtonTextColor;
            _flatButtonStyle.hover.textColor = onGuiButtonTextColor;
            _flatButtonStyle.active.textColor = onGuiButtonTextColor;
            _flatButtonStyle.focused.textColor = onGuiButtonTextColor;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _titleStyle = null;
            _bodyStyle = null;
            _flatButtonStyle = null;
        }
#endif
    }
}
