using UnityEngine;
using UnityEngine.UI;

namespace ArenaSurvivor.Fx
{
    /// <summary>
    /// Cicla una lista fija de sprites a un framerate fijo mientras el objeto está activo —
    /// un flipbook manual para animaciones cortas que no ameritan un Animator entero (por
    /// ejemplo, los 2 frames que Unity separó de un GIF de Windows importado como sprites sueltos).
    /// </summary>
    public class SpriteFlipbook : MonoBehaviour
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float frameDuration = 0.15f;

        private float _timer;
        private int _frameIndex;

        private void OnEnable()
        {
            _timer = 0f;
            _frameIndex = 0;

            if (frames.Length > 0)
            {
                targetImage.sprite = frames[0];
            }
        }

        private void Update()
        {
            if (frames.Length < 2)
            {
                return;
            }

            _timer += Time.deltaTime;
            if (_timer < frameDuration)
            {
                return;
            }

            _timer -= frameDuration;
            _frameIndex = (_frameIndex + 1) % frames.Length;
            targetImage.sprite = frames[_frameIndex];
        }
    }
}
