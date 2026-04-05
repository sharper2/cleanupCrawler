using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CleanupCrawler.UI
{
    /// <summary>
    /// Title flow: main menu → credits, story, or how-to-play → back.
    /// <para><b>Hierarchy:</b> Canvas + EventSystem. Either stack panels in one place and enable
    /// <see cref="hideNonFocusedPanels"/> to show/hide them, or place panels in different screen areas and leave
    /// <see cref="hideNonFocusedPanels"/> off: sub-panels are shown (active + visible) on load and when a menu item is chosen; siblings are not hidden. Navigation still updates internal state (e.g. for Escape).</para>
    /// </summary>
    public sealed class TitleScreenController : MonoBehaviour
    {
        [Header("Screens")]
        [Tooltip("On: one panel at a time using SetActive (no CanvasGroup required). Off: all panel roots stay active; script brings the focused panel to front — use when panels are in different screen areas.")]
        [SerializeField] private bool hideNonFocusedPanels;

        [Tooltip("When hideNonFocusedPanels is on: must be siblings under Canvas — NOT nested (CanvasGroup on a parent hides children).")]
        [SerializeField] private GameObject titleScreenRoot;
        [SerializeField] private GameObject creditsScreenRoot;
        [SerializeField] private GameObject tutorialScreenRoot;
        [SerializeField] private GameObject storyScreenRoot;

        [Header("Victory (after final level)")]
        [Tooltip("Optional root for your victory UI. If set, it is shown and the main menu is hidden when returning from a completed run.")]
        [SerializeField] private GameObject victoryPanelRoot;
        [Tooltip("Invoked once when the title scene loads after clearing the last level (same moment as showing Victory Panel Root). Use for custom wiring if you do not use the panel field.")]
        [SerializeField] private UnityEvent onVictoryFromRun;

        [Header("Optional text (assign body fields; copy can come from text areas below)")]
        [SerializeField] private Text creditsBodyText;
        [SerializeField] private Text tutorialBodyText;
        [SerializeField] private Text storyBodyText;

        [Header("Credits copy")]
        [TextArea(6, 24)]
        [SerializeField] private string creditsText =
            "Cleanup Crawler\n\n" +
            "Design & programming — Sam Harper\n\n" +
            "Thank you for playing!";

        [Header("Tutorial / controls")]
        [TextArea(10, 32)]
        [SerializeField] private string tutorialText =
            "Movement — W A S D or arrow keys (Up/Down). Turn — Q and E (or Left/Right arrows).\n\n" +
            "Abilities — pick up orbs on the floor. New orbs join the left of the queue; the right orb is next to evoke.\n" +
            "Press Space to evoke the front orb.\n\n" +
            "Inventory — manage gear from the inventory UI when available.\n\n" +
            "Goal — explore, survive, and clear objectives to reach the exit.";

        [Header("Story")]
        [TextArea(8, 28)]
        [SerializeField] private string storyText =
            "The depths await.\n\n" +
            "Edit this text in the Title Screen Controller, or bind Story Body Text to a Text component and write your intro here.";

        [Header("Navigation")]
        [SerializeField] private string gameplaySceneName = "generatorTestScene";
        [SerializeField] private bool loadGameplayAdditive;
        [SerializeField] private bool quitOnExitButton = true;

        [Header("Input")]
        [SerializeField] private bool backKeyReturnsToTitle = true;
        [SerializeField] private KeyCode backKey = KeyCode.Escape;

        private TitleScreenState _state = TitleScreenState.Title;
        private bool _showingVictoryFromRun;

        private enum TitleScreenState
        {
            Title,
            Credits,
            Tutorial,
            Story
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateScreenHierarchy();
        }
#endif

        private void Awake()
        {
            ValidateScreenHierarchy();
            ApplyStaticTextsIfNeeded();
            if (!hideNonFocusedPanels)
            {
                EnsurePanelRootShown(titleScreenRoot);
                EnsurePanelRootShown(creditsScreenRoot);
                EnsurePanelRootShown(tutorialScreenRoot);
                EnsurePanelRootShown(storyScreenRoot);
            }

            if (TitleFlowPendingVictory.TryConsumePending())
            {
                PresentVictoryFromRun();
            }
            else
            {
                ShowTitle();
            }
        }

        /// <summary>
        /// When panels are siblings under the same Canvas, draw order follows hierarchy (later = on top).
        /// Full-screen panels above the menu steal clicks; bring the focused screen to front without hiding siblings.
        /// </summary>
        private void BringScreenToFront(GameObject root)
        {
            if (hideNonFocusedPanels || root == null)
            {
                return;
            }

            root.transform.SetAsLastSibling();
        }

        private void Update()
        {
            if (backKeyReturnsToTitle && Input.GetKeyDown(backKey) && _showingVictoryFromRun)
            {
                DismissVictoryToTitle();
                return;
            }

            if (!backKeyReturnsToTitle)
            {
                return;
            }

            if (!Input.GetKeyDown(backKey))
            {
                return;
            }

            if (_state == TitleScreenState.Title)
            {
                return;
            }

            ShowTitle();
        }

        /// <summary>Hook to the Play / Start Game button.</summary>
        public void OnPlayClicked()
        {
            if (string.IsNullOrEmpty(gameplaySceneName))
            {
                Debug.LogWarning("[TitleScreenController] Gameplay scene name is empty.");
                return;
            }

            var mode = loadGameplayAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            SceneManager.LoadScene(gameplaySceneName, mode);
        }

        /// <summary>Hook to the Credits button.</summary>
        public void OnCreditsClicked()
        {
            _state = TitleScreenState.Credits;
            SetScreenVisible(tutorialScreenRoot, false);
            SetScreenVisible(storyScreenRoot, false);
            SetScreenVisible(creditsScreenRoot, true);
            SetScreenVisible(titleScreenRoot, false);
            BringScreenToFront(creditsScreenRoot);
        }

        /// <summary>Hook to the How to Play / Tutorial button.</summary>
        public void OnTutorialClicked()
        {
            _state = TitleScreenState.Tutorial;
            SetScreenVisible(creditsScreenRoot, false);
            SetScreenVisible(storyScreenRoot, false);
            SetScreenVisible(tutorialScreenRoot, true);
            SetScreenVisible(titleScreenRoot, false);
            BringScreenToFront(tutorialScreenRoot);
        }

        /// <summary>Hook to the Story button.</summary>
        public void OnStoryClicked()
        {
            _state = TitleScreenState.Story;
            SetScreenVisible(creditsScreenRoot, false);
            SetScreenVisible(tutorialScreenRoot, false);
            SetScreenVisible(storyScreenRoot, true);
            SetScreenVisible(titleScreenRoot, false);
            BringScreenToFront(storyScreenRoot);
        }

        /// <summary>Hook to Back buttons on sub-screens.</summary>
        public void ShowTitle()
        {
            _showingVictoryFromRun = false;
            if (victoryPanelRoot != null)
            {
                if (hideNonFocusedPanels)
                {
                    SetScreenVisible(victoryPanelRoot, false);
                }
                else
                {
                    victoryPanelRoot.SetActive(false);
                }
            }

            _state = TitleScreenState.Title;
            ApplyStaticTextsIfNeeded();
            SetScreenVisible(creditsScreenRoot, false);
            SetScreenVisible(tutorialScreenRoot, false);
            SetScreenVisible(storyScreenRoot, false);
            SetScreenVisible(titleScreenRoot, true);
            BringScreenToFront(titleScreenRoot);
        }

        /// <summary>Hook to your victory panel Continue / Back to title button.</summary>
        public void DismissVictoryToTitle()
        {
            if (!_showingVictoryFromRun)
            {
                return;
            }

            ShowTitle();
        }

        private void PresentVictoryFromRun()
        {
            _showingVictoryFromRun = true;
            _state = TitleScreenState.Title;
            ApplyStaticTextsIfNeeded();
            SetScreenVisible(creditsScreenRoot, false);
            SetScreenVisible(tutorialScreenRoot, false);
            SetScreenVisible(storyScreenRoot, false);
            if (titleScreenRoot != null)
            {
                if (hideNonFocusedPanels)
                {
                    SetScreenVisible(titleScreenRoot, false);
                }
                else
                {
                    titleScreenRoot.SetActive(false);
                }
            }

            onVictoryFromRun?.Invoke();

            if (victoryPanelRoot != null)
            {
                if (hideNonFocusedPanels)
                {
                    SetScreenVisible(victoryPanelRoot, true);
                }
                else
                {
                    victoryPanelRoot.SetActive(true);
                    EnsurePanelRootShown(victoryPanelRoot);
                }

                BringScreenToFront(victoryPanelRoot);
            }
        }

        /// <summary>Hook to Quit (standalone builds).</summary>
        public void OnQuitClicked()
        {
            if (!quitOnExitButton)
            {
                return;
            }

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ApplyStaticTextsIfNeeded()
        {
            if (creditsBodyText != null && !string.IsNullOrEmpty(creditsText))
            {
                creditsBodyText.text = creditsText;
            }

            if (tutorialBodyText != null && !string.IsNullOrEmpty(tutorialText))
            {
                tutorialBodyText.text = tutorialText;
            }

            if (storyBodyText != null && !string.IsNullOrEmpty(storyText))
            {
                storyBodyText.text = storyText;
            }
        }

        private void ValidateScreenHierarchy()
        {
            if (!hideNonFocusedPanels)
            {
                return;
            }

            if (IsNestedInside(titleScreenRoot, creditsScreenRoot)
                || IsNestedInside(titleScreenRoot, tutorialScreenRoot)
                || IsNestedInside(titleScreenRoot, storyScreenRoot))
            {
                Debug.LogWarning(
                    "[TitleScreenController] A sub-screen (Credits, Tutorial, or Story) is nested under the Title panel. " +
                    "When Title uses a CanvasGroup, its alpha hides all children — sub-screens will never appear. " +
                    "Reparent sub-screens to be siblings of Title (direct children of the same Canvas).",
                    this);
            }

            if (AreMutuallyNested(creditsScreenRoot, tutorialScreenRoot)
                || AreMutuallyNested(creditsScreenRoot, storyScreenRoot)
                || AreMutuallyNested(tutorialScreenRoot, storyScreenRoot))
            {
                Debug.LogWarning(
                    "[TitleScreenController] Credits, Tutorial, and Story panels should not be nested inside each other.",
                    this);
            }
        }

        private static bool AreMutuallyNested(GameObject a, GameObject b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return b.transform.IsChildOf(a.transform) || a.transform.IsChildOf(b.transform);
        }

        private static bool IsNestedInside(GameObject ancestor, GameObject descendant)
        {
            if (ancestor == null || descendant == null)
            {
                return false;
            }

            return descendant.transform.IsChildOf(ancestor.transform) && descendant != ancestor;
        }

        private void SetScreenVisible(GameObject root, bool visible)
        {
            if (root == null)
            {
                return;
            }

            if (!hideNonFocusedPanels)
            {
                // Spread layout: never hide siblings; only "show" targets (active + alpha 1) when asked.
                if (visible)
                {
                    EnsurePanelRootShown(root);
                }

                return;
            }

            var cg = root.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = visible ? 1f : 0f;
                cg.interactable = visible;
                cg.blocksRaycasts = visible;
                root.SetActive(true);
            }
            else
            {
                root.SetActive(visible);
            }
        }

        /// <summary>
        /// Activates the root and makes an attached CanvasGroup fully visible and interactive.
        /// Used when panels are laid out in different areas (hideNonFocusedPanels off) or when showing a target after click.
        /// </summary>
        private static void EnsurePanelRootShown(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            root.SetActive(true);
            var cg = root.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }
    }

    /// <summary>
    /// Set when the final level exit loads the title scene; consumed once by <see cref="TitleScreenController"/>.
    /// </summary>
    public static class TitleFlowPendingVictory
    {
        public static bool Pending { get; private set; }

        public static void MarkPending()
        {
            Pending = true;
        }

        /// <summary>Returns true if a run completion was pending and clears the flag.</summary>
        public static bool TryConsumePending()
        {
            if (!Pending)
            {
                return false;
            }

            Pending = false;
            return true;
        }
    }
}
