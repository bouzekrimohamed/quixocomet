using System.Collections;
using QuixoUnity.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuixoUnity.UI
{
    public sealed class SplashController : MonoBehaviour
    {
        private const string AuthSceneName = "AuthScene";
        private const string MenuSceneName = "MenuScene";

        [SerializeField] private CanvasGroup contentGroup;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Transform starRoot;
        [SerializeField] private TextMeshProUGUI poweredLabel;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private float duration = 2f;

        private bool _loading;

        private void Awake()
        {
            ResolveReferences();
            ApplyTheme();
        }

        private void Start()
        {
            StartCoroutine(PlayIntro());
        }

        private IEnumerator PlayIntro()
        {
            if (contentGroup != null)
            {
                contentGroup.alpha = 0f;
            }

            if (contentRoot != null)
            {
                contentRoot.localScale = Vector3.one * 0.94f;
            }

            float elapsed = 0f;
            float safeDuration = Mathf.Max(duration, 0.2f);
            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.45f));

                if (contentGroup != null)
                {
                    contentGroup.alpha = fade;
                }

                if (contentRoot != null)
                {
                    float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.035f;
                    contentRoot.localScale = Vector3.one * Mathf.Lerp(0.94f, pulse, fade);
                }

                if (starRoot != null)
                {
                    starRoot.Rotate(Vector3.forward, Time.deltaTime * 2.8f, Space.Self);
                }

                yield return null;
            }

            LoadNextScene();
        }

        private void LoadNextScene()
        {
            if (_loading)
            {
                return;
            }

            _loading = true;
            string target = SessionManager.HasSession ? MenuSceneName : AuthSceneName;
            if (Application.CanStreamedLevelBeLoaded(target))
            {
                SceneManager.LoadScene(target);
                return;
            }

            Debug.LogError($"Scene '{target}' introuvable. Lancez Tools > Quixo > Create/Repair Scenes.", this);
        }

        private void ResolveReferences()
        {
            contentGroup ??= GetComponentInChildren<CanvasGroup>(true);
            if (contentRoot == null && contentGroup != null)
            {
                contentRoot = contentGroup.transform as RectTransform;
            }

            poweredLabel ??= FindText("PoweredLabel");
            nameLabel ??= FindText("NameLabel");
            starRoot ??= transform.Find("Canvas/StarLayer");
        }

        private void ApplyTheme()
        {
            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);

            if (poweredLabel != null)
            {
                poweredLabel.color = palette.UiMuted;
            }

            if (nameLabel != null)
            {
                nameLabel.color = palette.UiText;
            }
        }

        private TextMeshProUGUI FindText(string childName)
        {
            var labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var label in labels)
            {
                if (label.name == childName)
                {
                    return label;
                }
            }

            return null;
        }
    }
}
