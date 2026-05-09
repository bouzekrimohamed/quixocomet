using QuixoUnity.Auth;
using QuixoUnity.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuixoUnity.UI
{
    public sealed class FriendsView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private Button addButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private RectTransform requestsContainer;
        [SerializeField] private RectTransform friendsContainer;
        [SerializeField] private FriendService friendService;

        private bool _busy;

        private void Awake()
        {
            ResolveReferences();
            BindButtons();
            ApplyTheme();
        }

        private void OnEnable()
        {
            Refresh();
        }

        public void AddFriend()
        {
            if (_busy)
            {
                return;
            }

            if (!SessionManager.IsOnline)
            {
                SetStatus("Connectez-vous pour ajouter des amis.");
                return;
            }

            SetBusy(true);
            SetStatus("Envoi de la demande...");
            friendService.SendFriendRequestByUsername(Read(usernameInput), result =>
            {
                SetBusy(false);
                SetStatus(result != null ? result.Message : "Operation impossible.");
                if (result != null && result.Success)
                {
                    if (usernameInput != null)
                    {
                        usernameInput.text = string.Empty;
                    }

                    Refresh();
                }
            });
        }

        public void Refresh()
        {
            ResolveReferences();
            ClearList(requestsContainer);
            ClearList(friendsContainer);

            if (!SessionManager.IsOnline)
            {
                SetStatus("Mode hors ligne : amis indisponibles.");
                return;
            }

            if (_busy)
            {
                return;
            }

            SetBusy(true);
            SetStatus("Chargement des amis...");
            friendService.LoadSummary(result =>
            {
                SetBusy(false);
                if (result == null || !result.Success)
                {
                    SetStatus(result != null ? result.Message : "Chargement impossible.");
                    return;
                }

                RenderSummary(result.Summary);
                SetStatus("Amis a jour.");
            });
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void ApplyTheme()
        {
            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            ApplyButton(addButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(refreshButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(closeButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            if (statusLabel != null)
            {
                statusLabel.color = palette.UiMuted;
            }
        }

        private void RenderSummary(FriendSummary summary)
        {
            ClearList(requestsContainer);
            ClearList(friendsContainer);

            if (summary == null)
            {
                return;
            }

            if (summary.Requests.Count == 0)
            {
                CreateTextRow(requestsContainer, "Aucune demande recue.");
            }
            else
            {
                foreach (var request in summary.Requests)
                {
                    CreateRequestRow(request);
                }
            }

            if (summary.Friends.Count == 0)
            {
                CreateTextRow(friendsContainer, "Aucun ami accepte pour le moment.");
            }
            else
            {
                foreach (var friend in summary.Friends)
                {
                    string name = string.IsNullOrWhiteSpace(friend.DisplayName) ? friend.Username : friend.DisplayName;
                    CreateTextRow(friendsContainer, name);
                }
            }
        }

        private void CreateRequestRow(FriendListItem item)
        {
            if (requestsContainer == null || item == null)
            {
                return;
            }

            var row = CreateRow(requestsContainer, $"Demande de {item.Username}");
            var accept = CreateSmallButton(row.transform, "Accepter", VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme).UiButton);
            var reject = CreateSmallButton(row.transform, "Refuser", VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme).UiButtonSecondary);

            accept.onClick.AddListener(() => UpdateRequest(item.RequestId, true));
            reject.onClick.AddListener(() => UpdateRequest(item.RequestId, false));
        }

        private void UpdateRequest(string requestId, bool accept)
        {
            if (_busy)
            {
                return;
            }

            SetBusy(true);
            SetStatus(accept ? "Acceptation..." : "Refus...");
            var callback = new System.Action<SocialOperationResult>(result =>
            {
                SetBusy(false);
                SetStatus(result != null ? result.Message : "Operation impossible.");
                Refresh();
            });

            if (accept)
            {
                friendService.AcceptRequest(requestId, callback);
            }
            else
            {
                friendService.RejectRequest(requestId, callback);
            }
        }

        private GameObject CreateRow(Transform parent, string label)
        {
            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            var row = new GameObject("FriendRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rect = row.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 38f);

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(row.transform, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18f;
            text.color = palette.UiText;
            text.enableWordWrapping = false;
            var layoutElement = textObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 250f;
            layoutElement.preferredHeight = 34f;
            return row;
        }

        private void CreateTextRow(Transform parent, string label)
        {
            if (parent != null)
            {
                CreateRow(parent, label);
            }
        }

        private Button CreateSmallButton(Transform parent, string label, Color color)
        {
            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            var buttonObject = new GameObject(label + "Button", typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = color;
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ApplyButton(button, color, palette.UiText, palette.UiButtonDisabled);
            var layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 92f;
            layout.preferredHeight = 34f;

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(buttonObject.transform, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 15f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = palette.UiText;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return button;
        }

        private void ResolveReferences()
        {
            friendService ??= FindObjectOfType<FriendService>();
            if (friendService == null)
            {
                friendService = gameObject.AddComponent<FriendService>();
            }

            usernameInput ??= FindChild<TMP_InputField>("FriendUsernameInput");
            addButton ??= FindChild<Button>("AddFriendButton");
            refreshButton ??= FindChild<Button>("RefreshFriendsButton");
            closeButton ??= FindChild<Button>("CloseFriendsButton");
            statusLabel ??= FindChild<TextMeshProUGUI>("FriendsStatusLabel");
            requestsContainer ??= FindChild<RectTransform>("RequestsContainer");
            friendsContainer ??= FindChild<RectTransform>("FriendsContainer");
        }

        private void BindButtons()
        {
            Bind(addButton, AddFriend);
            Bind(refreshButton, Refresh);
            Bind(closeButton, Close);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            SetInteractable(addButton, !busy);
            SetInteractable(refreshButton, !busy);
            SetInteractable(closeButton, true);
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        private static void ClearList(Transform container)
        {
            if (container == null)
            {
                return;
            }

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static string Read(TMP_InputField input)
        {
            return input != null ? input.text.Trim() : string.Empty;
        }

        private static void ApplyButton(Button button, Color normalColor, Color textColor, Color disabledColor)
        {
            if (button == null)
            {
                return;
            }

            var colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
            colors.disabledColor = disabledColor;
            button.colors = colors;

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.color = textColor;
            }
        }

        private T FindChild<T>(string childName) where T : Component
        {
            var components = GetComponentsInChildren<T>(true);
            foreach (var component in components)
            {
                if (component.name == childName)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
