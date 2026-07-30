Shader "Hidden/Render/Outline"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Outline.hlsl"
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Outline"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragOutline

            half4 _EdgeColor;
            float _Strength;
            float _Thickness;

            half4 FragOutline(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                float edge = MRender_RobertsCrossDepthEdge(input.texcoord, _BlitTexture_TexelSize.xy, _Thickness);
                float w = saturate(edge * _Strength * _EdgeColor.a);
                return half4(lerp(src, _EdgeColor.rgb, w), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
