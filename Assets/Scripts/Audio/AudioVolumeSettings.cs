using UnityEngine;

namespace ArenaSurvivor.Audio
{
    /// <summary>
    /// Todos los volúmenes del juego en un solo lugar: un master general y uno por
    /// sonido/música. MusicPlayer y SfxPlayer no guardan sus propios volúmenes — leen
    /// estos, así que calibrar el audio es venir a este único componente en vez de andar
    /// buscando cada AudioSource por separado.
    /// </summary>
    public class AudioVolumeSettings : MonoBehaviour
    {
        [Range(0f, 1f)] public float masterVolume = 1f;

        [Header("Música")]
        [Range(0f, 1f)] public float musicVolume = 0.35f;

        [Header("Efectos")]
        [Range(0f, 1f)] public float shootVolume = 1f;
        [Range(0f, 1f)] public float impactVolume = 1f;
        [Range(0f, 1f)] public float enemyDeathVolume = 1f;
        [Range(0f, 1f)] public float playerHurtVolume = 1f;
        [Range(0f, 1f)] public float xpVolume = 1f;
        [Range(0f, 1f)] public float levelUpVolume = 1f;
        [Range(0f, 1f)] public float clickVolume = 1f;
        [Range(0f, 1f)] public float bossDefeatedVolume = 1f;
    }
}
