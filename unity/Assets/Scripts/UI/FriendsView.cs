using System.Collections;
using System.Collections.Generic;
using QuixoUnity.Auth;
using QuixoUnity.Core;
using QuixoUnity.Gameplay;
using QuixoUnity.Online;
using QuixoUnity.Social;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuixoUnity.UI
{
    public sealed class FriendsView : MonoBehaviour
    {
        private const string GameplaySceneName = "GameplayScene";
        private const float AutoRefreshSeconds = 5f;

        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private Button addButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private RectTransform requestsContainer;
        [SerializeField] private RectTransform invitesContainer;
        [SerializeField] private RectTransform friendsContainer;
        [SerializeField] private FriendService friendService;
        [SerializeField] private OnlineMatchService onlineMatchService;
        [SerializeField] private OnlinePresenceService onlinePresenceService;

        private bool _busy;
        private Coroutine _refreshRoutine;
        private FriendSummary _lastSummary;
        private Dictionary<string, bool> _lastPresence = new();

        private void Awake()
        {
            ResolveReferences();
            BindButtons();
            ApplyTheme();
        }

        private void OnEnable()
        {
            Refresh();
            if (_refreshRoutine == null)
            {
                _refreshRoutine = StartCoroutine(AutoRefreshRoutine());
            }
        }

        private void OnDisable()
        {
            if (_refreshRoutine != null)
            {
                StopCoroutine(_refreshRoutine);
                _refreshRoutine = null;
            }
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

            string raw = Read(usernameInput);
            if (string.IsNullOrWhiteSpace(raw))
            {
                SetStatus("Entrez un pseudo.");
                return;
            }

            SetBusy(true);
            SetStatus("Envoi de la demande...");
            friendService.SendFriendRequestByUsername(raw, result =>
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

            if (!SessionManager.IsOnline)
            {
                ClearList(requestsContainer);
                ClearList(invitesContainer);
                ClearList(friendsContainer);
                CreateInfoRow(requestsContainer, "Mode hors ligne.");
                CreateInfoRow(invitesContainer, "Mode hors ligne.");
                CreateInfoRow(friendsContainer, "Mode hors ligne.");
                SetStatus("Mode hors ligne : amis indisponibles.");
                return;
            }

            if (_busy)
            {
                return;
            }

            ClearList(requestsContainer);
            ClearList(invitesContainer);
            ClearList(friendsContainer);

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

                _lastSummary = result.Summary;
                LoadPresenceAndInvites(result.Summary);
            });
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void ApplyTheme()
        {
            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            if (statusLabel != null)
            {
                statusLabel.color = palette.UiMuted;
            }

            foreach (var label in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (VisualThemeCatalog.IsButtonLabel(label))
                {
                    continue;
                }

                bool muted = label.name.Contains("Subtitle") || label.name.Contains("Status");
                label.color = muted ? palette.UiMuted : palette.UiText;
            }

            ApplyButton(addButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(refreshButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(closeButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
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
                CreateInfoRow(requestsContainer, "Aucune demande d'ami recue.");
            }
            else
            {
                foreach (var request in summary.Requests)
                {
                    CreateFriendRequestRow(request);
                }
            }

            if (summary.Friends.Count == 0)
            {
                CreateInfoRow(friendsContainer, "Aucun ami accepte pour le moment.");
            }
            else
            {
                foreach (var friend in summary.Friends)
                {
                    CreateFriendRow(friend);
                }
            }
        }

        private void LoadPresenceAndInvites(FriendSummary summary)
        {
            RenderSummary(summary);
            if (onlinePresenceService == null || onlineMatchService == null)
            {
                ClearList(invitesContainer);
                CreateInfoRow(invitesContainer, "Service en ligne indisponible.");
                SetStatus("Amis a jour.");
                return;
            }

            onlinePresenceService.GetFriendsPresence(summary != null ? summary.Friends : null, presence =>
            {
                _lastPresence = presence ?? new Dictionary<string, bool>();
                RenderSummary(_lastSummary);
                onlineMatchService.LoadPendingInvites(invites =>
                {
                    RenderSummary(_lastSummary);
                    RenderMatchInvites(invites);
                    onlineMatchService.LoadAcceptedSentInvites(accepted =>
                    {
                        RenderAcceptedSentInvites(accepted);
                        SetStatus("Amis et invitations a jour.");
                    });
                });
            });
        }

        private void RenderMatchInvites(List<MatchInviteDto> invites)
        {
            ClearList(invitesContainer);
            if (invitesContainer == null)
            {
                return;
            }

            int rendered = 0;
            if (invites != null)
            {
                foreach (var invite in invites)
                {
                    if (invite == null || invite.status != "pending")
                    {
                        continue;
                    }

                    CreateMatchInviteRow(invite);
                    rendered++;
                }
            }

            if (rendered == 0)
            {
                CreateInfoRow(invitesContainer, "Aucune invitation de partie en attente.");
            }
        }

        private void RenderAcceptedSentInvites(List<MatchInviteDto> invites)
        {
            if (invitesContainer == null || invites == null)
            {
                return;
            }

            foreach (var invite in invites)
            {
                if (invite == null || string.IsNullOrWhiteSpace(invite.match_id) || invite.status != "accepted")
                {
                    continue;
                }

                var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
                var row = CreateRow(invitesContainer, palette);
                CreateRowDot(row.transform, palette, false, hidden: true);
                string cadence = TurnTimerSettings.DisplayName(invite.time_control_key, invite.initial_seconds, invite.increment_seconds);
                CreateRowLabel(row.transform, $"Invitation acceptee par {ResolveFriendName(invite.to_user_id)} - {cadence}", palette.UiText, palette, flex: true);
                var join = CreateRowButton(row.transform, "Rejoindre", palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
                join.interactable = !_busy;
                join.onClick.AddListener(() => JoinAcceptedInvite(invite));
            }
        }

        private void CreateFriendRequestRow(FriendListItem item)
        {
            if (requestsContainer == null || item == null)
            {
                return;
            }

            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            var row = CreateRow(requestsContainer, palette);
            CreateRowDot(row.transform, palette, false, hidden: true);
            string name = string.IsNullOrWhiteSpace(item.Username) ? "(inconnu)" : item.Username;
            CreateRowLabel(row.transform, $"Demande de {name}", palette.UiText, palette, flex: true);

            var accept = CreateRowButton(row.transform, "Accepter", palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            var reject = CreateRowButton(row.transform, "Refuser", palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            accept.onClick.AddListener(() => UpdateRequest(item.RequestId, true));
            reject.onClick.AddListener(() => UpdateRequest(item.RequestId, false));
        }

        private void CreateMatchInviteRow(MatchInviteDto invite)
        {
            if (invitesContainer == null || invite == null)
            {
                return;
            }

            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            var row = CreateRow(invitesContainer, palette);
            CreateRowDot(row.transform, palette, false, hidden: true);
            string fromName = ResolveFriendName(invite.from_user_id);
            string kind = string.IsNullOrWhiteSpace(invite.game_kind) ? "Quixo" : invite.game_kind;
            string cadence = TurnTimerSettings.DisplayName(invite.time_control_key, invite.initial_seconds, invite.increment_seconds);
            CreateRowLabel(row.transform, $"{fromName} t'invite a jouer {kind} - {cadence}", palette.UiText, palette, flex: true);

            var accept = CreateRowButton(row.transform, "Accepter", palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            var reject = CreateRowButton(row.transform, "Refuser", palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            accept.interactable = !_busy;
            reject.interactable = !_busy;
            accept.onClick.AddListener(() => AcceptMatchInvite(invite));
            reject.onClick.AddListener(() => RejectMatchInvite(invite));
        }

        private void CreateFriendRow(FriendListItem item)
        {
            if (friendsContainer == null || item == null)
            {
                return;
            }

            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            string name = string.IsNullOrWhiteSpace(item.DisplayName) ? item.Username : item.DisplayName;
            bool isOnline = _lastPresence.TryGetValue(item.UserId, out bool online) && online;
            var row = CreateRow(friendsContainer, palette);
            CreateRowDot(row.transform, palette, isOnline);
            string label = string.IsNullOrWhiteSpace(name) ? "(ami)" : name;
            CreateRowLabel(row.transform, label, palette.UiText, palette, flex: true);

            var quixo = CreateRowButton(row.transform, "Inviter Quixo", palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            var qomet = CreateRowButton(row.transform, "Inviter Qomet", palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            quixo.interactable = isOnline && !_busy;
            qomet.interactable = isOnline && !_busy;
            quixo.onClick.AddListener(() => SendGameInvite(item, GameKind.Quixo));
            qomet.onClick.AddListener(() => SendGameInvite(item, GameKind.Qomet));
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

        private void SendGameInvite(FriendListItem friend, GameKind kind)
        {
            if (_busy || friend == null)
            {
                return;
            }

            if (!SessionManager.IsOnline)
            {
                SetStatus("Connectez-vous pour inviter un ami.");
                return;
            }

            SetBusy(true);
            SetStatus($"Envoi invitation {OnlineSessionTransit.GameKindName(kind)} ({TurnTimerSettings.DisplayCurrent()})...");
            onlineMatchService.SendInvite(friend.UserId, kind, result =>
            {
                SetBusy(false);
                if (result != null && result.Success)
                {
                    SetStatus("Invitation envoyee.");
                }
                else
                {
                    SetStatus(result != null ? result.Message : "Impossible d'envoyer l'invitation.");
                }

                Refresh();
            });
        }

        private void AcceptMatchInvite(MatchInviteDto invite)
        {
            if (_busy || invite == null)
            {
                return;
            }

            SetBusy(true);
            SetStatus("Acceptation de l'invitation...");
            onlineMatchService.AcceptInvite(invite, result =>
            {
                SetBusy(false);
                if (result == null || !result.Success || result.Match == null)
                {
                    SetStatus(result != null ? result.Message : "Match impossible.");
                    Refresh();
                    return;
                }

                Debug.Log($"[Invite] Accepted invite {invite.id}, match={result.Match.id}, p1={result.Match.player1_id}, p2={result.Match.player2_id}, turn={result.Match.current_turn_id}");
                if (!OnlineSessionTransit.IsValidForLocalPlayer(result.Match, SessionManager.UserId))
                {
                    SetStatus("Vous ne faites pas partie de ce match.");
                    Refresh();
                    return;
                }

                OnlineSessionTransit.Start(result.Match, SessionManager.UserId, ResolveFriendName(invite.from_user_id));
                SceneTransit.SelectedGame = OnlineSessionTransit.SelectedGameKind;
                SceneTransit.SelectedTheme = VisualThemeCatalog.ActiveTheme;
                if (Application.CanStreamedLevelBeLoaded(GameplaySceneName))
                {
                    SceneManager.LoadScene(GameplaySceneName);
                    return;
                }

                SetStatus("GameplayScene introuvable. Regenerer les scenes.");
            });
        }

        private void RejectMatchInvite(MatchInviteDto invite)
        {
            if (_busy || invite == null)
            {
                return;
            }

            SetBusy(true);
            SetStatus("Refus de l'invitation...");
            onlineMatchService.RejectInvite(invite.id, result =>
            {
                SetBusy(false);
                SetStatus(result != null ? result.Message : "Refus impossible.");
                Refresh();
            });
        }

        private void JoinAcceptedInvite(MatchInviteDto invite)
        {
            if (_busy || invite == null || string.IsNullOrWhiteSpace(invite.match_id))
            {
                return;
            }

            SetBusy(true);
            SetStatus("Chargement du match...");
            Debug.Log($"[Invite] Join accepted match id={invite.match_id}");
            onlineMatchService.FetchMatch(invite.match_id, result =>
            {
                SetBusy(false);
                if (result == null || !result.Success || result.Match == null)
                {
                    SetStatus(result != null ? result.Message : "Match introuvable.");
                    Refresh();
                    return;
                }

                if (!string.Equals(result.Match.status, "active", System.StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus("Ce match n'est plus actif.");
                    Refresh();
                    return;
                }

                if (!OnlineSessionTransit.IsValidForLocalPlayer(result.Match, SessionManager.UserId))
                {
                    SetStatus("Vous ne faites pas partie de ce match.");
                    Refresh();
                    return;
                }

                Debug.Log($"[Invite] Join accepted match id={result.Match.id}, p1={result.Match.player1_id}, p2={result.Match.player2_id}, turn={result.Match.current_turn_id}");
                OnlineSessionTransit.Start(result.Match, SessionManager.UserId, ResolveFriendName(invite.to_user_id));
                SceneTransit.SelectedGame = OnlineSessionTransit.SelectedGameKind;
                SceneTransit.SelectedTheme = VisualThemeCatalog.ActiveTheme;
                if (Application.CanStreamedLevelBeLoaded(GameplaySceneName))
                {
                    SceneManager.LoadScene(GameplaySceneName);
                    return;
                }

                SetStatus("GameplayScene introuvable. Regenerer les scenes.");
            });
        }

        private GameObject CreateRow(Transform parent, GameplayPalette palette)
        {
            var row = new GameObject("FriendRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rect = row.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 60f);

            var bg = row.AddComponent<Image>();
            bg.color = new Color(palette.UiPanel.r, palette.UiPanel.g, palette.UiPanel.b, 0.40f);
            bg.raycastTarget = false;

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 10f;
            layout.padding = new RectOffset(14, 14, 6, 6);
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 60f;
            element.minHeight = 60f;
            element.flexibleHeight = 0f;
            element.flexibleWidth = 1f;
            return row;
        }

        private void CreateInfoRow(Transform parent, string label)
        {
            if (parent == null)
            {
                return;
            }

            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            var row = CreateRow(parent, palette);
            CreateRowLabel(row.transform, label, palette.UiMuted, palette, flex: true);
        }

        private void CreateRowDot(Transform parent, GameplayPalette palette, bool isOnline, bool hidden = false)
        {
            var dot = new GameObject("StatusDot", typeof(RectTransform));
            dot.transform.SetParent(parent, false);

            var image = dot.AddComponent<Image>();
            image.color = hidden
                ? new Color(0f, 0f, 0f, 0f)
                : isOnline ? new Color(0.26f, 0.86f, 0.42f, 1f) : new Color(0.92f, 0.30f, 0.24f, 1f);
            image.raycastTarget = false;

            if (!hidden)
            {
                var outline = dot.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.45f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            var element = dot.AddComponent<LayoutElement>();
            element.preferredWidth = 20f;
            element.preferredHeight = 20f;
            element.minWidth = 20f;
            element.minHeight = 20f;
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;
        }

        private TextMeshProUGUI CreateRowLabel(Transform parent, string text, Color color, GameplayPalette palette, bool flex)
        {
            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);

            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 17f;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            var element = labelObject.AddComponent<LayoutElement>();
            element.minWidth = flex ? 120f : 60f;
            element.preferredWidth = flex ? 220f : 100f;
            element.flexibleWidth = flex ? 1f : 0f;
            element.preferredHeight = 44f;
            element.minHeight = 44f;
            element.flexibleHeight = 0f;
            return label;
        }

        private Button CreateRowButton(Transform parent, string label, Color background, Color textColor, Color disabledColor)
        {
            var buttonObject = new GameObject(label + "Button", typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = true;

            // Petite ombre pour mieux distinguer les boutons du fond translucide.
            var shadow = buttonObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.32f);
            shadow.effectDistance = new Vector2(0f, -2f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ApplyButton(button, background, textColor, disabledColor);

            var element = buttonObject.AddComponent<LayoutElement>();
            element.preferredWidth = 130f;
            element.minWidth = 116f;
            element.preferredHeight = 44f;
            element.minHeight = 44f;
            element.flexibleHeight = 0f;
            element.flexibleWidth = 0f;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(buttonObject.transform, false);
            var tmp = textObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = VisualThemeCatalog.GetReadableTextColor(background);
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;

            var textRect = tmp.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4f, 0f);
            textRect.offsetMax = new Vector2(-4f, 0f);
            return button;
        }

        private void ResolveReferences()
        {
            friendService ??= FindObjectOfType<FriendService>();
            if (friendService == null)
            {
                friendService = gameObject.AddComponent<FriendService>();
            }

            onlineMatchService ??= FindObjectOfType<OnlineMatchService>();
            if (onlineMatchService == null)
            {
                onlineMatchService = gameObject.AddComponent<OnlineMatchService>();
            }

            onlinePresenceService ??= FindObjectOfType<OnlinePresenceService>();
            if (onlinePresenceService == null)
            {
                onlinePresenceService = gameObject.AddComponent<OnlinePresenceService>();
            }

            usernameInput ??= FindChild<TMP_InputField>("FriendUsernameInput");
            addButton ??= FindChild<Button>("AddFriendButton");
            refreshButton ??= FindChild<Button>("RefreshFriendsButton");
            closeButton ??= FindChild<Button>("CloseFriendsButton");
            statusLabel ??= FindChild<TextMeshProUGUI>("FriendsStatusLabel");
            requestsContainer ??= FindChild<RectTransform>("RequestsContainer");
            invitesContainer ??= FindChild<RectTransform>("InvitesContainer");
            friendsContainer ??= FindChild<RectTransform>("FriendsContainer");
        }

        private void BindButtons()
        {
            Bind(addButton, AddFriend);
            Bind(refreshButton, Refresh);
            Bind(closeButton, Close);
        }

        private IEnumerator AutoRefreshRoutine()
        {
            while (isActiveAndEnabled)
            {
                yield return new WaitForSeconds(AutoRefreshSeconds);
                if (!_busy && SessionManager.IsOnline && gameObject.activeInHierarchy)
                {
                    Refresh();
                }
            }
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

        private string ResolveFriendName(string userId)
        {
            if (_lastSummary == null || string.IsNullOrWhiteSpace(userId))
            {
                return "Un ami";
            }

            foreach (var friend in _lastSummary.Friends)
            {
                if (friend.UserId == userId)
                {
                    return string.IsNullOrWhiteSpace(friend.DisplayName) ? friend.Username : friend.DisplayName;
                }
            }

            foreach (var request in _lastSummary.Requests)
            {
                if (request.UserId == userId)
                {
                    return string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username : request.DisplayName;
                }
            }

            return "Un joueur";
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

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = button.interactable ? normalColor : disabledColor;
            }

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                Color background = button.interactable ? normalColor : disabledColor;
                var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
                label.color = VisualThemeCatalog.GetButtonTextColor(background, palette);
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
