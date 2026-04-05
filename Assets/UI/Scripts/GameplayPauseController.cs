using DungeonGenerator;
using UnityEngine;
using UnityEngine.UI;

namespace CleanupCrawler.UI
{
    /// <summary>
    /// Gameplay pause: toggles with Escape (or an alternate key), shows control hints and a Resume action.
    /// <para><b>No Canvas required:</b> if <see cref="panelRoot"/> is left empty, the overlay uses <c>OnGUI</c> like <see cref="DungeonGenerator.PlayerHealthBarHud"/>.</para>
    /// <para><b>Optional uGUI:</b> assign a <see cref="CanvasGroup"/> + Text + Button if you prefer Canvas-based UI.</para>
    /// </summary>
    public sealed class GameplayPauseController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
        [SerializeField] private KeyCode alternatePauseKey = KeyCode.P;

        [Header("UI (optional — leave empty for OnGUI overlay)")]
        [SerializeField, Tooltip("If set, uses uGUI instead of OnGUI. Leave empty to match IMGUI HUDs (health bar, etc.).")]
        private CanvasGroup panelRoot;
        [SerializeField] private Text controlsBodyText;
        [SerializeField] private Button resumeButton;

        [Header("OnGUI overlay")]
        [SerializeField, Tooltip("Optional. Uses default IMGUI font when unset.")]
        private Font onGuiFont;
        [SerializeField, Min(8)] private int onGuiFontSize = 15;
        [SerializeField] private Color onGuiTitleColor = Color.white;
        [SerializeField] private Color onGuiBodyColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        [SerializeField] private float onGuiButtonHeight = 44f;
        [SerializeField] private float onGuiButtonMaxWidth = 280f;
        [SerializeField] private Color onGuiButtonBackgroundColor = new Color(0.22f, 0.45f, 0.85f, 1f);
        [SerializeField] private Color onGuiButtonHoverColor = new Color(0.30f, 0.52f, 0.92f, 1f);
        [SerializeField] private Color onGuiButtonTextColor = Color.white;

        [Header("Copy")]
        [TextArea(12, 28)]
        [SerializeField] private string controlsText =
            "Movement — W A S D or arrow keys (Up/Down).\n" +
            "Turn — Q and E (or Left / Right arrows).\n\n" +
            "Abilities — Space evokes the front orb in your queue.\n\n" +
            "Inventory — I to open; drag items on the grid; R rotates while dragging.\n\n" +
            "Pause — Esc or P.\n\n" +
            "Resume to continue.";

        private bool _panelVisible;
        private GUIStyle _wrappedLabelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _flatButtonStyle;
        private Font _stylesBuiltForFont;
        private int _stylesBuiltForTitleSize;
        private int _stylesBuiltForBodySize;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = GetComponent<CanvasGroup>();
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(Resume);
            }

            ApplyPanelVisible(false, false);
            if (controlsBodyText != null && !string.IsNullOrEmpty(controlsText))
            {
                controlsBodyText.text = controlsText;
            }

            if (controlsBodyText != null && onGuiFont != null)
            {
                controlsBodyText.font = onGuiFont;
            }
        }

        private void OnDestroy()
        {
            if (GameplayPause.IsPaused)
            {
                GameplayPause.SetPaused(false);
            }
        }

        private void Update()
        {
            if (GameplayDeathController.IsDeathScreenActive)
            {
                return;
            }

            if (GetPauseToggleDown())
            {
                TogglePause();
            }
        }

        private bool GetPauseToggleDown()
        {
            if (Input.GetKeyDown(pauseKey))
            {
                return true;
            }

            return alternatePauseKey != pauseKey && Input.GetKeyDown(alternatePauseKey);
        }

        private void TogglePause()
        {
            ApplyPanelVisible(!_panelVisible, true);
        }

        public void Resume()
        {
            ApplyPanelVisible(false, true);
        }

        private void OnGUI()
        {
            if (!_panelVisible || panelRoot != null)
            {
                return;
            }

            var prevDepth = GUI.depth;
            GUI.depth = 1000;

            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            var dimColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(dim, Texture2D.whiteTexture);
            GUI.color = dimColor;

            float boxW = Mathf.Min(560f, Screen.width - 48f);
            float boxH = Mathf.Min(420f, Screen.height - 48f);
            var box = new Rect((Screen.width - boxW) * 0.5f, (Screen.height - boxH) * 0.5f, boxW, boxH);

            EnsureOnGuiStyles();

            GUILayout.BeginArea(box);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(boxW));
            GUILayout.Label("Paused", _titleStyle);
            GUILayout.Space(12f);
            GUILayout.Label(controlsText, _wrappedLabelStyle);
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
            if (GUI.Button(continueRect, "Continue", _flatButtonStyle))
            {
                Resume();
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

        private void EnsureOnGuiStyles()
        {
            var font = onGuiFont != null ? onGuiFont : GUI.skin.font;
            int titleSize = onGuiFontSize + 6;
            bool rebuild = _titleStyle == null || _wrappedLabelStyle == null || _flatButtonStyle == null
                           || _stylesBuiltForFont != font || _stylesBuiltForTitleSize != titleSize
                           || _stylesBuiltForBodySize != onGuiFontSize;

            if (rebuild)
            {
                _stylesBuiltForFont = font;
                _stylesBuiltForTitleSize = titleSize;
                _stylesBuiltForBodySize = onGuiFontSize;

                var flatTex = Texture2D.whiteTexture;

                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = titleSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };

                _wrappedLabelStyle = new GUIStyle(GUI.skin.label)
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
            _wrappedLabelStyle.normal.textColor = onGuiBodyColor;
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
            _wrappedLabelStyle = null;
            _flatButtonStyle = null;
        }
#endif

        private void ApplyPanelVisible(bool visible, bool drivePauseState)
        {
            _panelVisible = visible;

            if (panelRoot != null)
            {
                panelRoot.alpha = visible ? 1f : 0f;
                panelRoot.interactable = visible;
                panelRoot.blocksRaycasts = visible;
            }

            if (drivePauseState)
            {
                GameplayPause.SetPaused(visible);
            }
        }
    }
}
