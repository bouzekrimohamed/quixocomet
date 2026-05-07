using QuixoUnity.Core;
using QuixoUnity.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuixoUnity.UI
{
    public sealed class MenuController : MonoBehaviour
    {
        private const string GameplaySceneName = "GameplayScene";

        [SerializeField] private GameKind nextGame = GameKind.Quixo;
        private bool _loadingGameplay;

        public void StartQuixo()
        {
            StartGame(GameKind.Quixo);
        }

        public void StartQomet()
        {
            StartGame(GameKind.Qomet);
        }

        public void Quit()
        {
            Application.Quit();
        }

        private void StartGame(GameKind kind)
        {
            if (_loadingGameplay)
            {
                return;
            }

            nextGame = kind;
            SceneTransit.SelectedGame = nextGame;

            if (Application.CanStreamedLevelBeLoaded(GameplaySceneName))
            {
                _loadingGameplay = true;
                SceneManager.LoadScene(GameplaySceneName);
                return;
            }

            Debug.LogError($"Scene '{GameplaySceneName}' introuvable. Ajoutez-la aux Build Settings.", this);
        }
    }

    public static class SceneTransit
    {
        public static GameKind SelectedGame = GameKind.Quixo;
    }
}
