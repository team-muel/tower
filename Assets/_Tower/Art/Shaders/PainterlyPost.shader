Shader "Tower/PainterlyPost"
{
    Properties
    {
        _Saturation ("Saturation", Range(0,2)) = 0.72
        _Contrast ("Contrast", Range(0.5,1.5)) = 0.92
        _Lift ("Shadow Lift", Range(0,0.25)) = 0.05
        _ShadowTint ("Shadow Tint", Color) = (0.40,0.48,0.60,1)
        _HighlightTint ("Highlight Tint", Color) = (1.0,0.93,0.80,1)
        _SplitAmount ("Split Tone Amount", Range(0,1)) = 0.35
        _GrainAmount ("Canvas Grain", Range(0,0.5)) = 0.08
        _FogColor ("Aerial Fog Color", Color) = (0.60,0.64,0.66,1)
        _FogDensity ("Aerial Fog Density", Range(0,1)) = 0.30
        _Vignette ("Vignette", Range(0,2)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            Name "PainterlyPost"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _Saturation;
            float _Contrast;
            float _Lift;
            float _SplitAmount;
            float _GrainAmount;
            float _FogDensity;
            float _Vignette;
            float4 _ShadowTint;
            float4 _HighlightTint;
            float4 _FogColor;

            float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            half4 Frag (Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                float l = Luma(col);
                col = lerp(l.xxx, col, _Saturation);
                col = (col - 0.5) * _Contrast + 0.5;
                col = col * (1.0 - _Lift) + _Lift;

                float3 tint = lerp(_ShadowTint.rgb, _HighlightTint.rgb, smoothstep(0.15, 0.85, saturate(l)));
                col = lerp(col, col * tint * 1.4, _SplitAmount);

                float depth = SampleSceneDepth(uv);
                float lin = Linear01Depth(depth, _ZBufferParams);
                col = lerp(col, _FogColor.rgb, saturate(lin * _FogDensity));

                float g = Hash(floor(uv * _ScreenSize.xy * 0.5));
                col *= 1.0 - _GrainAmount * (g - 0.5);

                float2 d = uv - 0.5;
                float vig = 1.0 - saturate(dot(d, d) * _Vignette);
                col *= vig;

                return half4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
