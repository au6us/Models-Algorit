using UnityEngine;

namespace ArenaSurvivor.UI
{
    /// <summary>
    /// El truco de "ventana cerrada": Left/Right/Bottom/TitleBar (ver WindowFrameSides en
    /// GameBootstrap) NUNCA se mueven, escalan ni deforman por separado — son todos hijos de
    /// un mismo contenedor padre (windowContainer, que ocupa toda la pantalla), anclados cada
    /// uno a su borde correspondiente con su tamaño fijo de siempre. Lo único que este script
    /// mueve es el RECTÁNGULO DEL CONTENEDOR (offsetMin/offsetMax): al achicarlo desde un
    /// lado, TODOS los hijos (incluida la barra de título entera, con su texto y sus 3
    /// botones) bajan/se acomodan juntos solos, porque los Anchors nativos de Unity los
    /// arrastran con el contenedor — nada de esto se calcula ni se toca por código.
    ///
    /// El hueco que se revela entre la pantalla real y el contenedor encogido queda vacío —
    /// ahí se ve directo el Background Color de la cámara (negro), sin ninguna UI tapándolo.
    /// La cámara sigue completamente libre, esto no le toca nada.
    /// </summary>
    public class DynamicWindowFrame : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private RectTransform windowContainer;

        // Límites reales del mapa — GameBootstrap los llena una sola vez, en tiempo de
        // editor; [SerializeField] a propósito para que sobrevivan el guardado de la escena
        // (nadie vuelve a llamar a ConfigureBounds durante la partida real).
        [SerializeField, HideInInspector] private float minX;
        [SerializeField, HideInInspector] private float maxX;
        [SerializeField, HideInInspector] private float minY;
        [SerializeField, HideInInspector] private float maxY;
        [SerializeField, HideInInspector] private bool boundsReady;

        /// <summary>Lo llama GameBootstrap una vez, con los límites reales (Bounds) del mapa.</summary>
        public void ConfigureBounds(float worldMinX, float worldMaxX, float worldMinY, float worldMaxY)
        {
            minX = worldMinX;
            maxX = worldMaxX;
            minY = worldMinY;
            maxY = worldMaxY;
            boundsReady = true;
        }

        private void LateUpdate()
        {
            if (!boundsReady || windowContainer == null)
            {
                return;
            }

            var camera = targetCamera != null ? targetCamera : Camera.main;
            if (camera == null)
            {
                return;
            }

            var cameraPosition = camera.transform.position;

            var leftScreenX = camera.WorldToScreenPoint(new Vector3(minX, cameraPosition.y, 0f)).x;
            var rightScreenX = camera.WorldToScreenPoint(new Vector3(maxX, cameraPosition.y, 0f)).x;
            var bottomScreenY = camera.WorldToScreenPoint(new Vector3(cameraPosition.x, minY, 0f)).y;
            var topScreenY = camera.WorldToScreenPoint(new Vector3(cameraPosition.x, maxY, 0f)).y;

            // 0 mientras el límite del mapa de ese lado ni siquiera entró en cámara — ahí el
            // contenedor queda exactamente del tamaño de la pantalla, como si nada.
            var leftInset = Mathf.Clamp(leftScreenX, 0f, Screen.width);
            var rightInset = Mathf.Clamp(Screen.width - rightScreenX, 0f, Screen.width);
            var bottomInset = Mathf.Clamp(bottomScreenY, 0f, Screen.height);
            var topInset = Mathf.Clamp(Screen.height - topScreenY, 0f, Screen.height);

            windowContainer.offsetMin = new Vector2(leftInset, bottomInset);
            windowContainer.offsetMax = new Vector2(-rightInset, -topInset);
        }
    }
}
