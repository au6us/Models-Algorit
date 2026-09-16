using System.Collections;
using UnityEngine;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Leveling;

namespace ArenaSurvivor.Audio
{
    /// <summary>
    /// Música de fondo en loop. Se "atenúa" (baja de volumen + un filtro pasa-bajos que la
    /// amortigua) mientras el panel de subir de nivel está abierto, y vuelve a la normalidad
    /// al elegir una mejora — sin necesidad de un Audio Mixer, solo tocando el AudioSource
    /// y el AudioLowPassFilter por código.
    ///
    /// El volumen "normal" no es un número propio: se lee de AudioSettings (un solo lugar
    /// para calibrar todo el audio del juego), y se sigue actualizando en vivo mientras suena
    /// por si se lo retoca desde el Inspector durante Play.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(AudioLowPassFilter))]
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioVolumeSettings settings;
        [SerializeField] private AudioClip musicClip;

        [Tooltip("Qué fracción del volumen normal queda mientras el panel de nivel está abierto.")]
        [SerializeField] [Range(0f, 1f)] private float duckedVolumeMultiplier = 0.35f;

        [SerializeField] private float normalLowPassCutoff = 22000f;
        [SerializeField] private float duckedLowPassCutoff = 700f;
        [SerializeField] private float transitionDuration = 0.3f;

        private AudioSource _source;
        private AudioLowPassFilter _lowPass;
        private Coroutine _routine;
        private bool _isDucked;

        private float NormalVolume => settings.masterVolume * settings.musicVolume;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _lowPass = GetComponent<AudioLowPassFilter>();

            _source.clip = musicClip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = NormalVolume;
            _lowPass.cutoffFrequency = normalLowPassCutoff;
        }

        private void Start()
        {
            if (musicClip != null)
            {
                _source.Play();
            }
        }

        private void OnEnable()
        {
            EventManager.Subscribe<ChoicePanelOpenedEvent>(OnPanelOpened);
            EventManager.Subscribe<UpgradeChosenEvent>(OnPanelClosed);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<ChoicePanelOpenedEvent>(OnPanelOpened);
            EventManager.Unsubscribe<UpgradeChosenEvent>(OnPanelClosed);
        }

        private void Update()
        {
            // Mientras no hay una transición en curso, el volumen sigue en vivo al slider
            // de AudioSettings — así calibrarlo desde el Inspector se escucha al toque,
            // sin tener que volver a golpear un enemigo o abrir el panel para verlo aplicado.
            if (_routine == null)
            {
                _source.volume = _isDucked ? NormalVolume * duckedVolumeMultiplier : NormalVolume;
            }
        }

        private void OnPanelOpened(ChoicePanelOpenedEvent gameEvent)
        {
            _isDucked = true;
            StartTransition(NormalVolume * duckedVolumeMultiplier, duckedLowPassCutoff);
        }

        private void OnPanelClosed(UpgradeChosenEvent gameEvent)
        {
            _isDucked = false;
            StartTransition(NormalVolume, normalLowPassCutoff);
        }

        private void StartTransition(float targetVolume, float targetCutoff)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(TransitionRoutine(targetVolume, targetCutoff));
        }

        private IEnumerator TransitionRoutine(float targetVolume, float targetCutoff)
        {
            var startVolume = _source.volume;
            var startCutoff = _lowPass.cutoffFrequency;
            var elapsed = 0f;

            // Tiempo real, no Time.deltaTime: el panel de nivel pone timeScale en 0 mientras
            // está abierto, y esta transición tiene que animarse igual durante esa pausa.
            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = elapsed / transitionDuration;
                _source.volume = Mathf.Lerp(startVolume, targetVolume, t);
                _lowPass.cutoffFrequency = Mathf.Lerp(startCutoff, targetCutoff, t);
                yield return null;
            }

            _source.volume = targetVolume;
            _lowPass.cutoffFrequency = targetCutoff;
            _routine = null;
        }
    }
}
