Shader "Hidden/Render/Blur"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Blur.hlsl"
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "BlurDownsampleH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownsampleH
            half4 FragDownsampleH(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return MRender_GaussianFilter5TapH(TEXTURE2D_ARGS(_BlitTexture, sampler_LinearClamp), input.texcoord, _BlitTexture_TexelSize.xy);
            }
            ENDHLSL
        }

        Pass
        {
            Name "BlurV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragV
            half4 FragV(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return MRender_GaussianFilter5TapV(TEXTURE2D_ARGS(_BlitTexture, sampler_LinearClamp), input.texcoord, _BlitTexture_TexelSize.xy);
            }
            ENDHLSL
        }

        Pass
        {
            Name "BlurComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            TEXTURE2D(_BlurResult); SAMPLER(sampler_BlurResult);
            float _BlurStrength;

            half4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 src  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                half3 blur = SAMPLE_TEXTURE2D(_BlurResult, sampler_BlurResult, input.texcoord).rgb;
                return half4(lerp(src, blur, _BlurStrength), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
