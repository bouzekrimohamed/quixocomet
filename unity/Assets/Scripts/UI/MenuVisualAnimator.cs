using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuixoUnity.UI
{
    // Anime de maniere legere le MenuScene : drift sinusoidal des etoiles/tokens,
    // pulse alpha des ghosts X/O, scale doux du titre et des boutons online, et
    // entree fade+slide du panneau central. Ne change AUCUNE fonctionnalite : se
    // contente de bouger des transforms et des couleurs en runtime.
    public sealed class MenuVisualAnimator : MonoBehaviour
    {
        [Header("Drift")]
        [SerializeField] private float starDriftRadius = 8f;
        [SerializeField] private float starDriftSpeed = 0.45f;
        [SerializeField] private float tokenDriftRadius = 14f;
        [SerializeField] private float tokenDriftSpeed = 0.30f;
        [SerializeField] private float ghostDriftRadius = 18f;
        [SerializeField] private float ghostDriftSpeed = 0.18f;

        [Header("Alpha pulse")]
        [SerializeField] private float starPulseSpeed = 0.6f;
        [SerializeField] private float starPulseAmplitude = 0.25f;
        [SerializeField] private float ghostPulseSpeed = 0.35f;
        [SerializeField] private float ghostPulseAmplitude = 0.35f;

        [Header("Panel entrance")]
        [SerializeField] private float entranceDuration = 0.55f;
        [SerializeField] private float entranceSlide = 26f;

        [Header("Title pulse")]
        [SerializeField] private float titlePulseSpeed = 1.1f;
        [SerializeField] private float titlePulseAmplitude = 0.025f;

        [Header("Online button pulse")]
        [SerializeField] private float onlinePulseSpeed = 1.4f;
        [SerializeField] private float onlinePulseAmplitude = 0.04f;

        private struct DriftItem
        {
            public RectTransform Rect;
            public Vector2 Home;
            public float PhaseX;
            public float PhaseY;
            public Graphic Graphic;
            public float BaseAlpha;
            public float Radius;
            public float Speed;
            public float PulseSpeed;
            public float PulseAmplitude;
        }

        private readonly List<DriftItem> _stars = new();
        private readonly List<DriftItem> _tokens = new();
        private readonly List<DriftItem> _ghosts = new();
        private readonly List<DriftItem> _bands = new();

        private RectTransform _menuPanelRect;
        private CanvasGroup _menuPanelGroup;
        private Vector2 _menuPanelHome;
        private TextMeshProUGUI _titleLabel;
        private Vector3 _titleBaseScale = Vector3.one;
        private RectTransform _quixoOnlineRect;
        private RectTransform _qometOnlineRect;
        private Vector3 _quixoOnlineBaseScale = Vector3.one;
        private Vector3 _qometOnlineBaseScale = Vector3.one;

        private void Start()
        {
            CollectBackdrop();
            CollectPanelAndButtons();
            if (_menuPanelRect != null)
            {
                StartCoroutine(EntranceRoutine());
            }
        }

        private void Update()
        {
            float t = Time.time;
            DriftAll(_stars, t);
            DriftAll(_tokens, t);
            DriftAll(_ghosts, t);
            DriftAll(_bands, t);
            PulseTitle(t);
            PulseOnlineButton(_quixoOnlineRect, _quixoOnlineBaseScale, t, 0f);
            PulseOnlineButton(_qometOnlineRect, _qometOnlineBaseScale, t, Mathf.PI * 0.5f);
        }

        private void CollectBackdrop()
        {
            var allRects = GetComponentsInChildren<RectTransform>(true);
            foreach (var rect in allRects)
            {
                if (rect == null)
                {
                    continue;
                }

                string n = rect.name;
                if (string.IsNullOrEmpty(n))
                {
                    continue;
                }

                if (n.StartsWith("Star_"))
                {
                    AddDriftItem(_stars, rect, starDriftRadius, starDriftSpeed, starPulseSpeed, starPulseAmplitude);
                }
                else if (n.StartsWith("MenuToken_") || n.StartsWith("AuthToken_"))
                {
                    AddDriftItem(_tokens, rect, tokenDriftRadius, tokenDriftSpeed, 0f, 0f);
                }
                else if (n == "MenuGhostX" || n == "MenuGhostO" || n == "AuthGhostX" || n == "AuthGhostO")
                {
                    AddDriftItem(_ghosts, rect, ghostDriftRadius, ghostDriftSpeed, ghostPulseSpeed, ghostPulseAmplitude);
                }
                else if (n.StartsWith("MenuSoftBand") || n.StartsWith("AuthSoftBand"))
                {
                    AddDriftItem(_bands, rect, tokenDriftRadius * 0.6f, tokenDriftSpeed * 0.6f, 0f, 0f);
                }
            }
        }

        private void AddDriftItem(List<DriftItem> bucket, RectTransform rect, float radius, float speed, float pulseSpeed, float pulseAmplitude)
        {
            Graphic g = rect.GetComponent<Graphic>();
            float baseAlpha = g != null ? g.color.a : 1f;
            // Seed deterministique base sur le nom : le pattern reste identique entre runs
            // mais reste varie d'un objet a l'autre.
            int seed = rect.name.GetHashCode();
            float phaseX = ((seed & 0xFFFF) / 65535f) * Mathf.PI * 2f;
            float phaseY = (((seed >> 16) & 0xFFFF) / 65535f) * Mathf.PI * 2f;
            bucket.Add(new DriftItem
            {
                Rect = rect,
                Home = rect.anchoredPosition,
                PhaseX = phaseX,
                PhaseY = phaseY,
                Graphic = g,
                BaseAlpha = baseAlpha,
                Radius = radius,
                Speed = speed,
                PulseSpeed = pulseSpeed,
                PulseAmplitude = pulseAmplitude
            });
        }

        private static void DriftAll(List<DriftItem> items, float t)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Rect == null)
                {
                    continue;
                }

                float dx = Mathf.Sin(t * item.Speed + item.PhaseX) * item.Radius;
                float dy = Mathf.Cos(t * item.Speed * 0.85f + item.PhaseY) * item.Radius * 0.75f;
                item.Rect.anchoredPosition = item.Home + new Vector2(dx, dy);

                if (item.Graphic != null && item.PulseAmplitude > 0f)
                {
                    float pulse = (Mathf.Sin(t * item.PulseSpeed + item.PhaseX) + 1f) * 0.5f;
                    float alpha = Mathf.Clamp01(item.BaseAlpha + (pulse - 0.5f) * item.PulseAmplitude);
                    var c = item.Graphic.color;
                    if (!Mathf.Approximately(c.a, alpha))
                    {
                        c.a = alpha;
                        item.Graphic.color = c;
                    }
                }
            }
        }

        private void CollectPanelAndButtons()
        {
            var panel = FindRectByName("MenuPanel");
            if (panel != null)
            {
                _menuPanelRect = panel;
                _menuPanelHome = panel.anchoredPosition;
                _menuPanelGroup = panel.GetComponent<CanvasGroup>();
                if (_menuPanelGroup == null)
                {
                    _menuPanelGroup = panel.gameObject.AddComponent<CanvasGroup>();
                }
            }

            var title = FindByName<TextMeshProUGUI>("Title");
            if (title != null)
            {
                _titleLabel = title;
                _titleBaseScale = title.rectTransform.localScale;
            }

            var quixoBtn = FindByName<Button>("QuixoOnlineButton");
            if (quixoBtn != null)
            {
                _quixoOnlineRect = quixoBtn.transform as RectTransform;
                if (_quixoOnlineRect != null)
                {
                    _quixoOnlineBaseScale = _quixoOnlineRect.localScale;
                }
            }

            var qometBtn = FindByName<Button>("QometOnlineButton");
            if (qometBtn != null)
            {
                _qometOnlineRect = qometBtn.transform as RectTransform;
                if (_qometOnlineRect != null)
                {
                    _qometOnlineBaseScale = _qometOnlineRect.localScale;
                }
            }
        }

        private IEnumerator EntranceRoutine()
        {
            if (_menuPanelRect == null)
            {
                yield break;
            }

            float elapsed = 0f;
            Vector2 from = _menuPanelHome + new Vector2(0f, -entranceSlide);
            _menuPanelRect.anchoredPosition = from;
            if (_menuPanelGroup != null)
            {
                _menuPanelGroup.alpha = 0f;
            }

            while (elapsed < entranceDuration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / entranceDuration);
                float eased = 1f - Mathf.Pow(1f - p, 3f);
                _menuPanelRect.anchoredPosition = Vector2.Lerp(from, _menuPanelHome, eased);
                if (_menuPanelGroup != null)
                {
                    _menuPanelGroup.alpha = eased;
                }
                yield return null;
            }

            _menuPanelRect.anchoredPosition = _menuPanelHome;
            if (_menuPanelGroup != null)
            {
                _menuPanelGroup.alpha = 1f;
            }
        }

        private void PulseTitle(float t)
        {
            if (_titleLabel == null)
            {
                return;
            }

            float p = Mathf.Sin(t * titlePulseSpeed) * titlePulseAmplitude;
            _titleLabel.rectTransform.localScale = _titleBaseScale * (1f + p);
        }

        private void PulseOnlineButton(RectTransform rect, Vector3 baseScale, float t, float phaseOffset)
        {
            if (rect == null)
            {
                return;
            }

            float p = Mathf.Sin(t * onlinePulseSpeed + phaseOffset) * onlinePulseAmplitude;
            rect.localScale = baseScale * (1f + p);
        }

        private RectTransform FindRectByName(string name)
        {
            var rects = GetComponentsInChildren<RectTransform>(true);
            foreach (var rect in rects)
            {
                if (rect != null && rect.name == name)
                {
                    return rect;
                }
            }

            return null;
        }

        private T FindByName<T>(string name) where T : Component
        {
            var components = GetComponentsInChildren<T>(true);
            foreach (var c in components)
            {
                if (c != null && c.name == name)
                {
                    return c;
                }
            }

            return null;
        }
    }
}
