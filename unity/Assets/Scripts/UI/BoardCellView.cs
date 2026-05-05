using System;
using QuixoUnity.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuixoUnity.UI
{
    public sealed class BoardCellView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private MeshRenderer tileRenderer = null!;
        [SerializeField] private TextMeshPro markText = null!;
        [SerializeField] private Image selectionRing = null!;
        [SerializeField] private Color emptyColor = new(0.85f, 0.83f, 0.78f);
        [SerializeField] private Color player1Color = new(0.25f, 0.35f, 0.95f);
        [SerializeField] private Color player2Color = new(0.95f, 0.45f, 0.3f);

        private int _row;
        private int _col;
        private Action<int, int> _onClick = null!;
        private Vector3 _baseScale;

        public void Initialize(int row, int col, Action<int, int> onClick)
        {
            _row = row;
            _col = col;
            _onClick = onClick;
            _baseScale = transform.localScale;
        }

        public void SetState(PlayerMark mark, bool selected)
        {
            string text = mark switch
            {
                PlayerMark.Player1 => "X",
                PlayerMark.Player2 => "O",
                _ => string.Empty,
            };
            markText.text = text;

            var color = mark switch
            {
                PlayerMark.Player1 => player1Color,
                PlayerMark.Player2 => player2Color,
                _ => emptyColor,
            };

            tileRenderer.material.color = color;
            selectionRing.enabled = selected;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onClick?.Invoke(_row, _col);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.localScale = _baseScale * 1.06f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = _baseScale;
        }
    }
}
