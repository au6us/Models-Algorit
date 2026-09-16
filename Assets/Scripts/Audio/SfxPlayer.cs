using UnityEngine;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Fx;
using ArenaSurvivor.Leveling;
using ArenaSurvivor.Player;
using ArenaSurvivor.Progression;

namespace ArenaSurvivor.Audio
{
    /// <summary>
    /// Traduce eventos de juego en efectos de sonido puntuales. Un solo AudioSource con
    /// PlayOneShot alcanza: a diferencia de la música (que necesita loop propio), varios
    /// efectos cortos superpuestos no se cortan entre sí — no hace falta poolear AudioSources.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SfxPlayer : MonoBehaviour
    {
        [SerializeField] private AudioVolumeSettings settings;

        [SerializeField] private AudioClip shootClip;
        [SerializeField] private AudioClip impactClip;
        [SerializeField] private AudioClip enemyDeathClip;
        [SerializeField] private AudioClip playerHurtClip;
        [SerializeField] private AudioClip xpClip;
        [SerializeField] private AudioClip levelUpClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] private AudioClip bossDefeatedClip;

        [Tooltip("Intervalo mínimo entre dos Impact/EnemyDeath seguidos, para que una " +
                 "muerte múltiple no sature el oído con el mismo sonido pegado.")]
        [SerializeField] private float minIntervalForFrequentSfx = 0.05f;

        [Tooltip("Variación de tono al azar en cada sonido (±), para que no suene igual de repetitivo.")]
        [SerializeField] private float pitchVariation = 0.05f;

        private AudioSource _source;
        private float _lastKnownPlayerHealth = -1f;
        private float _lastImpactPlayTime = float.NegativeInfinity;
        private float _lastEnemyDeathPlayTime = float.NegativeInfinity;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            EventManager.Subscribe<WeaponFiredEvent>(OnWeaponFired);
            EventManager.Subscribe<WeaponImpactEvent>(OnWeaponImpact);
            EventManager.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventManager.Subscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
            EventManager.Subscribe<XpCollectedEvent>(OnXpCollected);
            EventManager.Subscribe<ChoicePanelOpenedEvent>(OnPanelOpened);
            EventManager.Subscribe<UpgradeChosenEvent>(OnUpgradeChosen);
            EventManager.Subscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<WeaponFiredEvent>(OnWeaponFired);
            EventManager.Unsubscribe<WeaponImpactEvent>(OnWeaponImpact);
            EventManager.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventManager.Unsubscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
            EventManager.Unsubscribe<XpCollectedEvent>(OnXpCollected);
            EventManager.Unsubscribe<ChoicePanelOpenedEvent>(OnPanelOpened);
            EventManager.Unsubscribe<UpgradeChosenEvent>(OnUpgradeChosen);
            EventManager.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void Start()
        {
            _lastKnownPlayerHealth = -1f;
        }

        private void OnWeaponFired(WeaponFiredEvent gameEvent) => Play(shootClip, settings.shootVolume);

        private void OnWeaponImpact(WeaponImpactEvent gameEvent)
        {
            if (Time.unscaledTime - _lastImpactPlayTime < minIntervalForFrequentSfx)
            {
                return;
            }

            _lastImpactPlayTime = Time.unscaledTime;
            Play(impactClip, settings.impactVolume);
        }

        private void OnEnemyKilled(EnemyKilledEvent gameEvent)
        {
            if (Time.unscaledTime - _lastEnemyDeathPlayTime < minIntervalForFrequentSfx)
            {
                return;
            }

            _lastEnemyDeathPlayTime = Time.unscaledTime;
            Play(enemyDeathClip, settings.enemyDeathVolume);
        }

        private void OnPlayerHealthChanged(PlayerHealthChangedEvent gameEvent)
        {
            if (_lastKnownPlayerHealth >= 0f && gameEvent.CurrentHealth < _lastKnownPlayerHealth)
            {
                Play(playerHurtClip, settings.playerHurtVolume);
            }

            _lastKnownPlayerHealth = gameEvent.CurrentHealth;
        }

        private void OnXpCollected(XpCollectedEvent gameEvent) => Play(xpClip, settings.xpVolume);

        private void OnPanelOpened(ChoicePanelOpenedEvent gameEvent) => Play(levelUpClip, settings.levelUpVolume);

        private void OnUpgradeChosen(UpgradeChosenEvent gameEvent) => Play(clickClip, settings.clickVolume);

        private void OnBossDefeated(BossDefeatedEvent gameEvent) => Play(bossDefeatedClip, settings.bossDefeatedVolume);

        private void Play(AudioClip clip, float clipVolume)
        {
            _source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            _source.PlayOneShot(clip, clipVolume * settings.masterVolume);
        }
    }
}
