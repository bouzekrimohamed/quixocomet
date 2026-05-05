using System.Collections.Generic;
using QuixoUnity.Core;
using QuixoUnity.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuixoUnity.UI
{
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI turnLabel = null!;
        [SerializeField] private TextMeshProUGUI infoLabel = null!;
        [SerializeField] private Button restartButton = null!;
        [SerializeField] private Button menuButton = null!;
        [SerializeField] private Button upButton = null!;
        [SerializeField] private Button downButton = null!;
        [SerializeField] private Button leftButton = null!;
        [SerializeField] private Button rightButton = null!;

        private GameFlowController _controller = null!;

        public void Bind(GameFlowController controller)
        {
            _controller = controller;
            restartButton.onClick.AddListener(controller.RestartGame);
            upButton.onClick.AddListener(() => controller.PlayDirection(MoveDirection.Up));
            downButton.onClick.AddListener(() => controller.PlayDirection(MoveDirection.Down));
            leftButton.onClick.AddListener(() => controller.PlayDirection(MoveDirection.Left));
            rightButton.onClick.AddListener(() => controller.PlayDirection(MoveDirection.Right));
            menuButton.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene"));
        }

        public void SetTurn(PlayerMark player)
        {
            turnLabel.text = $"Tour: {(player == PlayerMark.Player1 ? "Joueur 1 (X)" : "Joueur 2 (O)")}";
        }

        public void SetInfo(string message)
        {
            infoLabel.text = message;
        }

        public void SetDirections(IReadOnlyList<MoveDirection> allowed)
        {
            bool has = allowed != null;
            var set = has ? new HashSet<MoveDirection>(allowed) : new HashSet<MoveDirection>();
            upButton.interactable = set.Contains(MoveDirection.Up);
            downButton.interactable = set.Contains(MoveDirection.Down);
            leftButton.interactable = set.Contains(MoveDirection.Left);
            rightButton.interactable = set.Contains(MoveDirection.Right);
        }
    }
}
