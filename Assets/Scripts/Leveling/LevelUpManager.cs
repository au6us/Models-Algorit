using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Player;
using ArenaSurvivor.Progression;
using ArenaSurvivor.UI;
using ArenaSurvivor.Weapons;

namespace ArenaSurvivor.Leveling
{
    /// <summary>
    /// Muestra 3 opciones al azar entre: desbloquear un arma nueva, mejorar una que ya
    /// tenga, o subir un stat — tanto al subir de nivel como al abrir un cofre (mismo panel,
    /// mismo pool de mejoras, dos disparadores distintos). Pausa el juego mientras elige.
    /// Al confirmar, no aplica la mejora al toque: primero juega una "instalación" falsa
    /// (estilo Windows — instalando/actualizando, con su propia barra de progreso) y recién
    /// al terminar aplica el cambio real y reanuda. Las mejoras en sí viven en UpgradeChoice.
    /// </summary>
    public class LevelUpManager : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private LevelUpButtonUI[] buttons;
        [SerializeField] private WeaponBase[] weapons;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private Health playerHealth;

        [Header("Instalación falsa al elegir")]
        [SerializeField] private GameObject installOverlay;
        [SerializeField] private Text installTitleText;
        [SerializeField] private Image installBarFill;
        [SerializeField] private float installDuration = 0.7f;
        [SerializeField] private Color newWeaponColor = new Color(0.3f, 0.85f, 0.3f);
        [SerializeField] private Color upgradeColor = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color statBoostColor = new Color(1f, 0.7f, 0.2f);

        private readonly List<UpgradeChoice> _currentChoices = new List<UpgradeChoice>();
        private Coroutine _installRoutine;

        private void Awake()
        {
            panelRoot.SetActive(false);

            if (installOverlay != null)
            {
                installOverlay.SetActive(false);
            }

            for (var i = 0; i < buttons.Length; i++)
            {
                var index = i;
                buttons[i].Button.onClick.AddListener(() => OnChoicePicked(index));
            }
        }

        private void OnEnable()
        {
            EventManager.Subscribe<PlayerLeveledUpEvent>(OnLeveledUp);
            EventManager.Subscribe<ChestOpenedEvent>(OnChestOpened);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<PlayerLeveledUpEvent>(OnLeveledUp);
            EventManager.Unsubscribe<ChestOpenedEvent>(OnChestOpened);
        }

        private void OnLeveledUp(PlayerLeveledUpEvent gameEvent) => ShowPanel();

        private void OnChestOpened(ChestOpenedEvent gameEvent) => ShowPanel();

        private void ShowPanel()
        {
            var pool = BuildChoicePool();
            Shuffle(pool);

            _currentChoices.Clear();
            var count = Mathf.Min(buttons.Length, pool.Count);
            for (var i = 0; i < count; i++)
            {
                _currentChoices.Add(pool[i]);
            }

            for (var i = 0; i < buttons.Length; i++)
            {
                var hasChoice = i < _currentChoices.Count;
                buttons[i].gameObject.SetActive(hasChoice);
                if (hasChoice)
                {
                    buttons[i].SetTitle(_currentChoices[i].Title);
                }
            }

            panelRoot.SetActive(true);
            Time.timeScale = 0f;
            EventManager.Publish(new ChoicePanelOpenedEvent());
        }

        private void OnChoicePicked(int index)
        {
            if (index >= _currentChoices.Count)
            {
                return;
            }

            foreach (var button in buttons)
            {
                button.gameObject.SetActive(false);
            }

            if (installOverlay == null)
            {
                // Sin overlay cableado (escena vieja): aplicá directo, como antes.
                ApplyChoiceAndClose(_currentChoices[index]);
                return;
            }

            if (_installRoutine != null)
            {
                StopCoroutine(_installRoutine);
            }

            _installRoutine = StartCoroutine(PlayInstallThenApply(_currentChoices[index]));
        }

        private IEnumerator PlayInstallThenApply(UpgradeChoice choice)
        {
            installOverlay.SetActive(true);

            var color = choice.Kind switch
            {
                UpgradeChoiceKind.NewWeapon => newWeaponColor,
                UpgradeChoiceKind.WeaponUpgrade => upgradeColor,
                _ => statBoostColor,
            };

            installTitleText.text = choice.Kind switch
            {
                UpgradeChoiceKind.NewWeapon => $"Se encontró hardware nuevo\nInstalando {choice.Name}...",
                UpgradeChoiceKind.WeaponUpgrade => $"Actualizando {choice.Name}...",
                _ => $"Aplicando {choice.Name}...",
            };
            installTitleText.color = color;
            installBarFill.color = color;
            installBarFill.fillAmount = 0f;

            // WaitForSeconds normal no serviría acá: el panel abre con Time.timeScale = 0,
            // así que la barra tiene que avanzar con tiempo real, no con el de juego (frenado).
            var elapsed = 0f;
            while (elapsed < installDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                installBarFill.fillAmount = Mathf.Clamp01(elapsed / installDuration);
                yield return null;
            }

            installOverlay.SetActive(false);
            ApplyChoiceAndClose(choice);
            _installRoutine = null;
        }

        private void ApplyChoiceAndClose(UpgradeChoice choice)
        {
            choice.Apply();
            panelRoot.SetActive(false);
            Time.timeScale = 1f;
            EventManager.Publish(new UpgradeChosenEvent());
        }

        private List<UpgradeChoice> BuildChoicePool()
        {
            var pool = new List<UpgradeChoice>();

            foreach (var weapon in weapons)
            {
                if (!weapon.enabled)
                {
                    var weaponToUnlock = weapon;
                    var weaponName = weaponToUnlock.Data.WeaponName;
                    pool.Add(new UpgradeChoice($"Nueva arma: {weaponName}", weaponName, UpgradeChoiceKind.NewWeapon,
                        () => weaponToUnlock.enabled = true));
                }
                else if (!weapon.IsMaxLevel)
                {
                    var weaponToLevelUp = weapon;
                    var weaponName = weaponToLevelUp.Data.WeaponName;
                    pool.Add(new UpgradeChoice($"{weaponName} Nv.{weaponToLevelUp.CurrentLevel + 1}", weaponName,
                        UpgradeChoiceKind.WeaponUpgrade, () => weaponToLevelUp.LevelUp()));
                }
            }

            pool.Add(new UpgradeChoice("Velocidad +10%", "Velocidad +10%", UpgradeChoiceKind.StatBoost,
                () => playerStats.AddMoveSpeedPercent(0.1f)));
            pool.Add(new UpgradeChoice("Daño +10%", "Daño +10%", UpgradeChoiceKind.StatBoost,
                () => playerStats.AddDamagePercent(0.1f)));
            pool.Add(new UpgradeChoice("Vida máxima +20", "Vida máxima +20", UpgradeChoiceKind.StatBoost,
                () => playerHealth.ConfigureMaxHealth(playerHealth.MaxHealth + 20f)));

            return pool;
        }

        private static void Shuffle(IList<UpgradeChoice> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
