using System.Collections;
using QuixoUnity.Auth;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace QuixoUnity.UI
{
    public sealed class IntroVideoController : MonoBehaviour
    {
        private const string AuthSceneName = "AuthScene";
        private const string MenuSceneName = "MenuScene";

        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private float missingVideoDelay = 0.35f;

        private bool _loadingNext;

        private void Awake()
        {
            videoPlayer ??= GetComponent<VideoPlayer>();
        }

        private void Start()
        {
            if (videoPlayer == null || videoPlayer.clip == null)
            {
                StartCoroutine(SkipMissingVideo());
                return;
            }

            videoPlayer.loopPointReached += OnVideoFinished;
            videoPlayer.errorReceived += OnVideoError;
            videoPlayer.Play();
        }

        private void Update()
        {
            if (_loadingNext)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetMouseButtonDown(0))
            {
                LoadNextScene();
            }
        }

        private void OnDisable()
        {
            if (videoPlayer == null)
            {
                return;
            }

            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.errorReceived -= OnVideoError;
        }

        private IEnumerator SkipMissingVideo()
        {
            yield return new WaitForSeconds(missingVideoDelay);
            LoadNextScene();
        }

        private void OnVideoFinished(VideoPlayer source)
        {
            LoadNextScene();
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"Intro video unavailable: {message}", this);
            LoadNextScene();
        }

        private void LoadNextScene()
        {
            if (_loadingNext)
            {
                return;
            }

            _loadingNext = true;
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }

            string targetScene = SessionManager.HasSession ? MenuSceneName : AuthSceneName;
            if (Application.CanStreamedLevelBeLoaded(targetScene))
            {
                SceneManager.LoadScene(targetScene);
                return;
            }

            Debug.LogError($"Scene '{targetScene}' introuvable. Lancez Tools > Quixo > Create/Repair Scenes.", this);
        }
    }
}
