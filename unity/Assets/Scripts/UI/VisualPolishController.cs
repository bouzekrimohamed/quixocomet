using System.Collections;
using UnityEngine;

namespace QuixoUnity.UI
{
    public sealed class VisualPolishController : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera = null!;
        [SerializeField] private Light keyLight = null!;
        [SerializeField] private AudioSource uiAudio = null!;
        [SerializeField] private AudioClip clickClip = null!;
        [SerializeField] private AudioClip winClip = null!;
        [SerializeField] private float idleCameraBob = 0.08f;
        [SerializeField] private float bobSpeed = 0.55f;

        private Vector3 _cameraOrigin;

        private void Awake()
        {
            if (mainCamera != null)
            {
                _cameraOrigin = mainCamera.transform.position;
            }
        }

        private void Update()
        {
            if (mainCamera != null)
            {
                float y = Mathf.Sin(Time.time * bobSpeed) * idleCameraBob;
                mainCamera.transform.position = _cameraOrigin + new Vector3(0f, y, 0f);
            }

            if (keyLight != null)
            {
                keyLight.intensity = 1.1f + Mathf.Sin(Time.time * 0.3f) * 0.08f;
            }
        }

        public void PlayClick()
        {
            PlayClip(clickClip);
        }

        public void PlayWin()
        {
            PlayClip(winClip);
            if (mainCamera != null)
            {
                StartCoroutine(ShakeCamera(0.35f, 0.08f));
            }
        }

        private void PlayClip(AudioClip clip)
        {
            if (uiAudio != null && clip != null)
            {
                uiAudio.PlayOneShot(clip);
            }
        }

        private IEnumerator ShakeCamera(float duration, float strength)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float x = Random.Range(-strength, strength);
                float y = Random.Range(-strength, strength);
                mainCamera.transform.position = _cameraOrigin + new Vector3(x, y, 0f);
                yield return null;
            }

            mainCamera.transform.position = _cameraOrigin;
        }
    }
}
