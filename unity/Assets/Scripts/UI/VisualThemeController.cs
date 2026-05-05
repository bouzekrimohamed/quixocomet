using UnityEngine;

namespace QuixoUnity.UI
{
    public sealed class VisualThemeController : MonoBehaviour
    {
        [SerializeField] private Material boardMaterial = null!;
        [SerializeField] private Color lightBoardColor = new(0.78f, 0.72f, 0.6f);
        [SerializeField] private Color darkBoardColor = new(0.2f, 0.23f, 0.28f);
        [SerializeField] private float animationIntensity = 1f;

        private bool _darkTheme;

        public float AnimationIntensity => animationIntensity;

        public void ToggleTheme()
        {
            _darkTheme = !_darkTheme;
            ApplyTheme();
        }

        public void SetAnimationIntensity(float value)
        {
            animationIntensity = Mathf.Clamp(value, 0f, 1.5f);
        }

        private void Start()
        {
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            if (boardMaterial == null)
            {
                return;
            }

            boardMaterial.color = _darkTheme ? darkBoardColor : lightBoardColor;
        }
    }
}
