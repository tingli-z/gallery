Shader "Dreamcore/VolumetricBeam"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 0.86, 0.62, 0.35)
        _Softness ("Softness", Range(0.2, 8)) = 2.4
        _FadePower ("Length Fade", Range(0.2, 6)) = 1.4
        _Intensity ("Visible Intensity", Range(0.2, 4)) = 1.0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Softness;
                half _FadePower;
                half _Intensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half sideFade = saturate(1.0 - abs(input.uv.x * 2.0 - 1.0));
                sideFade = pow(sideFade, _Softness);
                half lengthFade = pow(saturate(1.0 - input.uv.y), _FadePower);
                half alpha = _BaseColor.a * sideFade * lengthFade;
                return half4(_BaseColor.rgb * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
}
