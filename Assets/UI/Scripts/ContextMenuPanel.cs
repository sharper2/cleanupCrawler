using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CleanupCrawler.UI
{
    /// <summary>
    /// Typical Unity pattern: a <see cref="Canvas"/> (Screen Space - Overlay) contains this panel.
    /// Use an <see cref="Image"/> for a background texture, <see cref="Text"/> with your font, and <see cref="Button"/> for actions.
    /// While open, disable gameplay behaviours (e.g. <c>DungeonGridPlayerController</c>) so input does not move the player.
    /// </summary>
    public sealed class ContextMenuPanel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private CanvasGroup panelRoot;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Text primaryButtonLabel;

        [Header("Gameplay (optional)")]
        [Tooltip("Behaviours disabled while the menu is visible (movement, combat, inventory input, etc.).")]
        [SerializeField] private Behaviour[] disableWhileOpen;

        [Header("Behaviour")]
        [SerializeField] private bool hideOnPrimaryClick = true;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = GetComponent<CanvasGroup>();
            }

            SetVisible(false, false);
        }

        /// <summary>
        /// Shows the panel, applies texture and copy, wires the primary button, and disables listed behaviours.
        /// </summary>
        public void Show(
            Sprite backgroundSprite,
            string title,
            string body,
            string primaryLabel,
            UnityAction onPrimaryClicked)
        {
            if (backgroundImage != null)
            {
                backgroundImage.sprite = backgroundSprite;
                backgroundImage.enabled = backgroundSprite != null;
            }

            if (titleText != null)
            {
                titleText.text = title ?? string.Empty;
            }

            if (bodyText != null)
            {
                bodyText.text = body ?? string.Empty;
            }

            if (primaryButtonLabel != null)
            {
                primaryButtonLabel.text = primaryLabel ?? string.Empty;
            }

            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveAllListeners();
                if (onPrimaryClicked != null)
                {
                    primaryButton.onClick.AddListener(onPrimaryClicked);
                }

                primaryButton.onClick.AddListener(OnPrimaryClickedInternal);
            }

            SetGameplayBlocked(true);
            SetVisible(true, true);
            IsOpen = true;
        }

        public void Hide()
        {
            if (!IsOpen)
            {
                return;
            }

            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveAllListeners();
            }

            SetGameplayBlocked(false);
            SetVisible(false, false);
            IsOpen = false;
        }

        private void OnPrimaryClickedInternal()
        {
            if (hideOnPrimaryClick)
            {
                Hide();
            }
        }

        private void SetVisible(bool visible, bool interactable)
        {
            if (panelRoot != null)
            {
                panelRoot.alpha = visible ? 1f : 0f;
                panelRoot.interactable = interactable;
                panelRoot.blocksRaycasts = interactable;
                panelRoot.gameObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }

        private void SetGameplayBlocked(bool blocked)
        {
            if (disableWhileOpen == null)
            {
                return;
            }

            for (var i = 0; i < disableWhileOpen.Length; i++)
            {
                var b = disableWhileOpen[i];
                if (b != null)
                {
                    b.enabled = !blocked;
                }
            }
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                SetGameplayBlocked(false);
            }
        }
    }
}
