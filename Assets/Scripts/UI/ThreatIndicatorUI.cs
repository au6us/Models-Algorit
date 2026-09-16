using UnityEngine;
using UnityEngine.UI;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.UI
{
    /// <summary>
    /// Flechita parpadeante en el borde de pantalla que apunta hacia los enemigos "especiales"
    /// (elite/jefe) que estén fuera de cámara. No conoce a Enemy más que por su lista pública
    /// (EnemyRegistry) y su flag IsSpecial — no hace falta ningún evento nuevo para esto.
    /// </summary>
    public class ThreatIndicatorUI : MonoBehaviour
    {
        [SerializeField] private RectTransform[] arrowPool;
        [SerializeField] private float edgeMargin = 60f;
        [SerializeField] private float blinkSpeed = 6f;

        private Image[] _arrowImages;
        private Camera _camera;

        private void Awake()
        {
            _camera = Camera.main;
            _arrowImages = new Image[arrowPool.Length];
            for (var i = 0; i < arrowPool.Length; i++)
            {
                _arrowImages[i] = arrowPool[i].GetComponent<Image>();
                arrowPool[i].gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_camera == null)
            {
                return;
            }

            var arrowIndex = 0;
            var enemies = EnemyRegistry.ActiveEnemies;

            for (var i = 0; i < enemies.Count && arrowIndex < arrowPool.Length; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsSpecial)
                {
                    continue;
                }

                if (TryGetEdgePosition(enemy.transform.position, out var edgePosition, out var angle))
                {
                    var arrow = arrowPool[arrowIndex];
                    arrow.gameObject.SetActive(true);
                    arrow.position = edgePosition;
                    arrow.rotation = Quaternion.Euler(0f, 0f, angle);

                    var alpha = Mathf.PingPong(Time.unscaledTime * blinkSpeed, 1f);
                    var color = _arrowImages[arrowIndex].color;
                    color.a = alpha;
                    _arrowImages[arrowIndex].color = color;

                    arrowIndex++;
                }
            }

            for (var i = arrowIndex; i < arrowPool.Length; i++)
            {
                arrowPool[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Si el punto está dentro de cámara, no hay nada que mostrar. Si no, calcula dónde
        /// tocaría el borde de pantalla la línea desde el centro hasta ese punto.
        /// </summary>
        private bool TryGetEdgePosition(Vector3 worldPosition, out Vector2 edgePosition, out float angle)
        {
            var viewportPoint = _camera.WorldToViewportPoint(worldPosition);
            var isBehind = viewportPoint.z < 0f;
            var isOnScreen = !isBehind && viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
                              viewportPoint.y >= 0f && viewportPoint.y <= 1f;

            if (isOnScreen)
            {
                edgePosition = Vector2.zero;
                angle = 0f;
                return false;
            }

            var direction = new Vector2(viewportPoint.x - 0.5f, viewportPoint.y - 0.5f);
            if (isBehind)
            {
                direction = -direction;
            }

            direction.Normalize();

            var screenCenter = new Vector2(Screen.width, Screen.height) * 0.5f;
            var screenHalf = screenCenter - Vector2.one * edgeMargin;
            var scaleX = direction.x != 0f ? screenHalf.x / Mathf.Abs(direction.x) : Mathf.Infinity;
            var scaleY = direction.y != 0f ? screenHalf.y / Mathf.Abs(direction.y) : Mathf.Infinity;
            var scale = Mathf.Min(scaleX, scaleY);

            edgePosition = screenCenter + direction * scale;
            // El sprite del enemigo (triángulo) apunta "hacia arriba" por defecto — se le resta
            // 90° para que el atan2 (que mide desde la derecha) lo alinee con la dirección real.
            angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            return true;
        }
    }
}
