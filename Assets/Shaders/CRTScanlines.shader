// Post-proceso de pantalla completa "monitor CRT viejo": curva los bordes como un tubo,
// pinta scanlines horizontales, agrega un barrido que sube/baja, aberración cromática
// radial, vineteado y un parpadeo de brillo opcional.
//
// No anda como shader de un sprite/material común: lo corre CrtPostProcess.cs vía
// Graphics.Blit(cameraRT, processedRT, material) — no un Renderer Feature. Main Camera
// renderiza a su propio RenderTexture (targetTexture) en vez de a la pantalla, con el Canvas
// del juego en Screen Space - Camera apuntando a esa misma cámara, así que _BlitTexture ya
// trae el frame entero — mundo Y UI juntos. Ver CrtPostProcess.cs para por qué NO es un
// Full Screen Pass Renderer Feature: en el Renderer2D de este proyecto ningún injection
// point da lectura Y escritura correctas del color a la vez.
//
// _BlitTexture es TEXTURE2D plano, no TEXTURE2D_X: el macro "_X" (de Blit.hlsl/TextureXR.hlsl)
// espera un Texture2DArray para renderizado estéreo (XR/VR) — este proyecto es solo desktop,
// sin XR, y _cameraRT es un RenderTexture plano (Texture2D). Usar TEXTURE2D_X ahí sample-aba
// un recurso del tipo equivocado y volvía un gris plano uniforme en vez del contenido real
// (confirmado a mano: con TEXTURE2D_X se veía un gris sólido pese a que _cameraRT, mostrado
// directo sin pasar por el shader, tenía el juego real; con TEXTURE2D plano + un Vert()
// propio, sin ninguna dependencia de Blit.hlsl, el mismo _BlitTexture se lee perfecto). Por
// eso este shader define su propio Vert() de triángulo completo en vez de incluir Blit.hlsl.
Shader "ArenaSurvivor/CRT Scanlines"
{
    Properties
    {
        [Header(Curvatura)]
        _CurvatureAmount ("Curvatura del tubo", Range(0, 0.5)) = 0.08

        [Header(Scanlines)]
        _ScanlineDensity ("Densidad de lineas", Range(80, 1080)) = 320
        _ScanlineIntensity ("Intensidad de lineas", Range(0, 1)) = 0.35

        [Header(Barrido vertical)]
        _ScanRollSpeed ("Velocidad del barrido", Float) = 0.12
        _ScanRollIntensity ("Intensidad del barrido", Range(0, 1)) = 0.05
        _ScanRollWidth ("Ancho del barrido", Range(0.001, 0.3)) = 0.06

        [Header(Vineteado)]
        _VignetteRadius ("Radio", Range(0, 1.5)) = 0.85
        _VignetteSmoothness ("Suavidad", Range(0.01, 1)) = 0.45
        _VignetteIntensity ("Intensidad", Range(0, 1)) = 0.55

        [Header(Aberracion cromatica)]
        _AberrationAmount ("Cantidad", Range(0, 0.02)) = 0.0022

        [Header(Parpadeo)]
        _FlickerSpeed ("Velocidad", Float) = 6
        _FlickerIntensity ("Intensidad", Range(0, 0.2)) = 0

        [Header(Color final)]
        _Tint ("Tinte", Color) = (1, 1, 1, 1)
        _Brightness ("Brillo", Range(0.5, 1.5)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "CRTScanlines"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Core.hlsl trae _Time, sampler_LinearClamp y el resto de variables/macros
            // estándar de URP en el orden correcto (incluirlas sueltas a mano rompe: esperan
            // defines de archivos previos en su propia cadena de includes).
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // TEXTURE2D plano — ver comentario grande arriba del todo sobre por qué NO
            // TEXTURE2D_X acá.
            TEXTURE2D(_BlitTexture);

            // Tiempo real (Time.unscaledTime), lo setea CrtPostProcess.cs a mano cada frame.
            // NO usamos el _Time built-in de Unity para el barrido/parpadeo a propósito: ese
            // _Time sigue a Time.time, que se congela con Time.timeScale = 0 (juego en
            // pausa) — y el pedido es que el shader siga "vivo" aunque el gameplay esté
            // pausado.
            uniform float _CrtUnscaledTime;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
            };

            // Triángulo de pantalla completa de 3 vértices (el mismo truco que usa
            // Blit.hlsl): más barato que un quad de 2 triángulos y sin costuras en la
            // diagonal. positionCS en [-1,1], texcoord en [0,1] con Y ya invertida para
            // que coincida con cómo Unity arma sus RenderTexture.
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.texcoord = float2(uv.x, 1.0 - uv.y);
                return output;
            }

            CBUFFER_START(UnityPerMaterial)
                float _CurvatureAmount;
                float _ScanlineDensity;
                float _ScanlineIntensity;
                float _ScanRollSpeed;
                float _ScanRollIntensity;
                float _ScanRollWidth;
                float _VignetteRadius;
                float _VignetteSmoothness;
                float _VignetteIntensity;
                float _AberrationAmount;
                float _FlickerSpeed;
                float _FlickerIntensity;
                float4 _Tint;
                float _Brightness;
            CBUFFER_END

            // Empuja las esquinas hacia adentro como un tubo de CRT real. Un uv que queda
            // fuera de [0,1] después de esto ya NO se corta con un "return" a un color
            // inventado — eso es lo que tapaba el juego entero como si fuera opaco. Ahora
            // se resuelve con edgeMask en Frag(): atenúa el pixel REAL hacia negro cerca del
            // borde curvo, en vez de reemplazarlo por un color sólido que no viene de la
            // pantalla.
            float2 CurveUV(float2 uv, float amount)
            {
                uv = uv * 2.0 - 1.0;
                float2 offset = uv.yx * amount;
                uv += uv * offset * offset;
                return uv * 0.5 + 0.5;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 distortedUV = CurveUV(input.texcoord, _CurvatureAmount);

                // Máscara suave del borde del tubo: 1 adentro, se apaga a 0 justo en el
                // límite de la curvatura. Se multiplica al final sobre el color YA leído
                // de la pantalla — nunca reemplaza el pixel por un valor inventado.
                float2 edgeFade = smoothstep(0.0, 0.01, distortedUV) * smoothstep(0.0, 0.01, 1.0 - distortedUV);
                float edgeMask = edgeFade.x * edgeFade.y;

                // saturate() para no mandarle al sampler un uv absurdamente fuera de rango
                // (sampler_LinearClamp ya clampea, esto solo evita que la aberración
                // cromática "estire" texels raros justo en el borde clampeado).
                float2 uv = saturate(distortedUV);

                // El color sale de acá: _BlitTexture es el frame ya renderizado por la
                // cámara (mundo + UI, ver comentario arriba del todo), muestreado con
                // sampler_LinearClamp — nunca _MainTex.
                //
                // Aberración cromática radial: cada canal mira un poquito más cerca/lejos
                // del centro — más notorio en los bordes, como una lente barata.
                float2 centered = uv - 0.5;
                float2 dir = centered * _AberrationAmount;

                half r = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv - dir, 0).r;
                half g = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).g;
                half b = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv + dir, 0).b;
                half4 color = half4(r, g, b, 1);

                // Scanlines: franjas horizontales oscuras. La densidad es independiente de
                // la resolución real de pantalla, para que se vea igual en 1080p que en 1440p.
                float scan = sin(uv.y * _ScanlineDensity * 3.14159265 * 2.0) * 0.5 + 0.5;
                color.rgb *= 1.0 - _ScanlineIntensity * (1.0 - scan);

                // Barrido: una franja más clara que baja lentamente en loop, como un
                // monitor viejo perdiendo el sincronismo vertical. _CrtUnscaledTime, no
                // _Time.y — sigue corriendo aunque el juego esté en pausa.
                float rollY = frac(_CrtUnscaledTime * _ScanRollSpeed);
                float rollDist = abs(uv.y - rollY);
                float rollBand = smoothstep(_ScanRollWidth, 0.0, rollDist);
                color.rgb += rollBand * _ScanRollIntensity;

                // Vineteado: oscurece hacia los bordes/esquinas del tubo.
                float dist = length(centered);
                float vig = smoothstep(_VignetteRadius, _VignetteRadius - _VignetteSmoothness, dist);
                color.rgb *= lerp(1.0, vig, _VignetteIntensity);

                // Parpadeo sutil de brillo — apagado por defecto (_FlickerIntensity = 0),
                // lo activa quien haga el pase de arte si quiere ese "tubo que no regula
                // bien el voltaje". _CrtUnscaledTime otra vez, por lo mismo que el barrido.
                float flicker = 1.0 + sin(_CrtUnscaledTime * _FlickerSpeed) * _FlickerIntensity;
                color.rgb *= flicker * _Brightness;
                color.rgb *= _Tint.rgb;

                // Última multiplicación, no un reemplazo: el pixel real (ya con scanlines,
                // vineteado, etc.) se apaga suave hacia negro solo si cayó fuera del tubo
                // curvo. En el 99% de la pantalla edgeMask=1 y esto no hace nada.
                color.rgb *= edgeMask;

                return color;
            }
            ENDHLSL
        }
    }
}
