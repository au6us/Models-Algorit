using UnityEngine;
using UnityEngine.UI;

namespace ArenaSurvivor.Fx
{
    /// <summary>
    /// Aplica el filtro CRT a TODO el frame (mundo + UI) sin depender de Renderer Features:
    /// Main Camera renderiza a su propio RenderTexture (targetTexture) en vez de a la
    /// pantalla — con el Canvas del juego en Screen Space - Camera apuntando a esa misma
    /// cámara (ver GameBootstrap.BuildCrtScanlineEffect), así que la UI ya viene mezclada
    /// adentro. Cada frame, un Graphics.Blit con el material CRT pasa esa textura a una
    /// segunda, que un RawImage de pantalla completa muestra — el único paso de todo esto
    /// que toca la pantalla real.
    ///
    /// Por qué no un Renderer Feature (probado a mano, los tres injection points posibles):
    /// en el Renderer2D de este proyecto (Unity 6 / URP 17.0.4), "Before/After Rendering
    /// Post Processing" hacen que fetchColorBuffer copie un buffer vacío (gris plano,
    /// confirmado con un shader de passthrough puro sin ninguna matemática propia) — un
    /// límite real de cómo Renderer2D arma sus recursos de RenderGraph alrededor del
    /// post-proceso. "Before Rendering Transparents" sí lee el color real, pero lo que ese
    /// pase dibuja se termina tapando con el redibujado normal de sprites/UI que viene
    /// después (confirmado con un shader que devuelve rojo sólido: nunca se vio rojo en
    /// pantalla). Ningún injection point da lectura Y escritura correctas a la vez.
    ///
    /// Graphics.Blit corre AFUERA de todo RenderGraph — nada de lo anterior aplica acá. Pero
    /// classic Graphics.Blit(src, dst, material) por sí solo NO alcanza con un shader que lee
    /// _BlitTexture: Graphics.Blit clásico solo bindea el source como _MainTex (una
    /// convención vieja, previa a URP) — nunca toca _BlitTexture. Como el shader nunca lee
    /// _MainTex (a propósito, para que la fuente sea siempre la real y no un color
    /// inventado), _BlitTexture quedaba con el ÚLTIMO valor global que haya dejado
    /// CUALQUIER OTRO Blitter interno de URP ese mismo frame (el propio post-proceso de la
    /// cámara, por ejemplo) — nunca _cameraRT. Fix: setear _BlitTexture (global) a mano con
    /// el _cameraRT correcto antes de cada Blit.
    ///
    /// Segunda causa, más de fondo, del mismo síntoma ("pantalla plana con el patrón de
    /// scanlines/vineteado pero sin contenido real"): el shader usaba TEXTURE2D_X (pensado
    /// para Texture2DArray, renderizado estéreo/XR) para leer _BlitTexture, pero _cameraRT es
    /// un RenderTexture plano (Texture2D) — el mismatch de tipo de recurso hacía que el
    /// muestreo devolviera un gris uniforme en vez del contenido real, sin ningún error de
    /// compilación ni de consola que lo delatara. El shader ahora usa TEXTURE2D plano (ver
    /// CRTScanlines.shader) — no hace falta nada especial de este lado para eso.
    ///
    /// Costo real de este enfoque: un frame de latencia (Update() corre antes de que la
    /// cámara renderice ESTE frame, así que se blittea el frame anterior) — imperceptible
    /// para un post-proceso visual como este.
    ///
    /// OnEnable/OnDisable/Update de este componente NO corren en el Editor fuera de Play
    /// (no tiene [ExecuteAlways], a propósito: no hace falta gastar RenderTexture ni Blit
    /// mientras se edita la escena). Por eso el RawImage se deja DESACTIVADO por defecto
    /// (ver GameBootstrap.BuildCrtScanlineEffect) y este componente lo prende/apaga él
    /// mismo en OnEnable/OnDisable — si quedara siempre activo con textura null, un
    /// RawImage sin textura se dibuja blanco sólido y tapa toda la Game view en el Editor.
    /// </summary>
    public class CrtPostProcess : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RawImage displayImage;

        private RenderTexture _cameraRT;
        private RenderTexture _processedRT;
        private int _cachedWidth;
        private int _cachedHeight;

        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int CrtUnscaledTimeId = Shader.PropertyToID("_CrtUnscaledTime");

        private void OnEnable()
        {
            AllocateIfNeeded();

            if (displayImage != null)
            {
                displayImage.enabled = true;
            }
        }

        private void OnDisable()
        {
            ReleaseTargets();

            if (displayImage != null)
            {
                displayImage.texture = null;
                displayImage.enabled = false;
            }
        }

        private void AllocateIfNeeded()
        {
            var width = Screen.width;
            var height = Screen.height;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (_cameraRT != null && width == _cachedWidth && height == _cachedHeight)
            {
                return;
            }

            ReleaseTargets();
            _cachedWidth = width;
            _cachedHeight = height;

            // El constructor viejo (w, h, depthBits, format) no rellena el depthStencilFormat
            // que pide el render graph de Unity 6 para un targetTexture de cámara — con el
            // descriptor sí queda bien seteado.
            var cameraDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.Default, 24);
            _cameraRT = new RenderTexture(cameraDesc) { filterMode = FilterMode.Bilinear };
            _cameraRT.Create();

            _processedRT = new RenderTexture(width, height, 0, RenderTextureFormat.Default) { filterMode = FilterMode.Bilinear };
            _processedRT.Create();

            if (worldCamera != null)
            {
                worldCamera.targetTexture = _cameraRT;
            }

            if (displayImage != null)
            {
                displayImage.texture = _processedRT;
            }
        }

        private void ReleaseTargets()
        {
            if (worldCamera != null)
            {
                worldCamera.targetTexture = null;
            }

            if (_cameraRT != null)
            {
                _cameraRT.Release();
                _cameraRT = null;
            }

            if (_processedRT != null)
            {
                _processedRT.Release();
                _processedRT = null;
            }

            _cachedWidth = 0;
            _cachedHeight = 0;
        }

        private void Update()
        {
            if (material == null || worldCamera == null || displayImage == null)
            {
                return;
            }

            AllocateIfNeeded();

            if (_cameraRT == null || _processedRT == null)
            {
                return;
            }

            if (displayImage.texture != _processedRT)
            {
                displayImage.texture = _processedRT;
            }

            // Graphics.Blit clásico solo bindea _MainTex — este shader nunca lo lee, así que
            // sin esto _BlitTexture quedaría con el último valor global que haya dejado
            // cualquier otro Blitter interno de URP ese mismo frame, no _cameraRT.
            Shader.SetGlobalTexture(BlitTextureId, _cameraRT);

            // Update() sigue corriendo igual con Time.timeScale = 0 (juego en pausa: ESC,
            // panel de nivel/buffs, cofre, game over) — pero _Time.y del shader SÍ se
            // congelaría si lo usáramos, porque sigue a Time.time. Time.unscaledTime no se
            // entera de la pausa, así que el barrido/parpadeo del CRT queda "vivo" siempre.
            Shader.SetGlobalFloat(CrtUnscaledTimeId, Time.unscaledTime);
            Graphics.Blit(_cameraRT, _processedRT, material);
        }
    }
}
