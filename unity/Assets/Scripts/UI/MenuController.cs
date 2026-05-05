using QuixoUnity.Core;
using QuixoUnity.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuixoUnity.UI
{
    public sealed class MenuController : MonoBehaviour
    {
        [SerializeField] private GameKind nextGame = GameKind.Quixo;

        public void StartQuixo()
        {
            nextGame = GameKind.Quixo;
            SceneTransit.SelectedGame = nextGame;
            SceneManager.LoadScene("GameplayScene");
        }

        public void StartQomet()
        {
            nextGame = GameKind.Qomet;
            SceneTransit.SelectedGame = nextGame;
            SceneManager.LoadScene("GameplayScene");
        }

        public void Quit()
        {
            Application.Quit();
        }
    }

    public static class SceneTransit
    {
        public static GameKind SelectedGame = GameKind.Quixo;
    }
}
