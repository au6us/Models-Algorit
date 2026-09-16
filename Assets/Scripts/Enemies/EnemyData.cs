using UnityEngine;

namespace ArenaSurvivor.Enemies
{
    /// <summary>
    /// Configuración de un "tipo" de enemigo (swarm, tank, etc.). Agregar un tipo nuevo es
    /// crear un asset de este ScriptableObject, no escribir una clase nueva — así el
    /// EnemyFactory no necesita cambiar cuando el juego crece (Open/Closed).
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Arena Survivor/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [SerializeField] private float maxHealth = 20f;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float contactDamage = 10f;
        [SerializeField] private int experienceReward = 5;
        [SerializeField] private Color color = Color.red;
        [SerializeField] private float scale = 1f;

        [Header("Elite / Jefe")]
        [Tooltip("Enemigo elite o jefe: al morir suelta un cofre y, mientras esté vivo, un " +
                 "indicador en el borde de pantalla apunta hacia él si está fuera de cámara.")]
        [SerializeField] private bool isSpecial;

        [Tooltip("Además de 'especial', es un jefe: muestra su propia barra de vida en el HUD.")]
        [SerializeField] private bool isBoss;

        [SerializeField] private string displayName = "Jefe";

        public float MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public float ContactDamage => contactDamage;
        public int ExperienceReward => experienceReward;
        public Color Color => color;
        public float Scale => scale;
        public bool IsSpecial => isSpecial;
        public bool IsBoss => isBoss;
        public string DisplayName => displayName;
    }
}
