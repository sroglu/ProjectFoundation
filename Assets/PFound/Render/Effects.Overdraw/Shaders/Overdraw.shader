Shader "Hidden/Render/Overdraw"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Overdraw.hlsl"
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        // Pass 0: Accumulator — additive blend constant contribution per fragment.
        Pass
        {
            Name "OverdrawAccumulator"
            Cull Off ZWrite Off ZTest Always
            Blend One One
            BlendOp Add

            HLSLPROGRAM
            #pragma vertex   vertAcc
            #pragma fragment fragAcc

            float _OverdrawContribution;

            struct AppdataAcc
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsAcc
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsAcc vertAcc(AppdataAcc v)
            {
                VaryingsAcc o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                return o;
            }

            half4 fragAcc(VaryingsAcc input) : SV_Target
            {
                return half4(_OverdrawContribution, 0, 0, 1);
            }
            ENDHLSL
        }

        // Pass 1: Composite — sample accumulator R channel and map to threshold color.
        Pass
        {
            Name "OverdrawComposite"
            Cull Off ZWrite Off ZTest Always

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment FragComposite

            // sampler_PointClamp is a global sampler pre-declared by URP Core.hlsl —
            // re-declaring it here causes a "redefinition" error on Metal (URP 17 / Unity 6.3+).
            TEXTURE2D(_OverdrawAccumulator);

            half4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float overdraw = SAMPLE_TEXTURE2D(_OverdrawAccumulator, sampler_PointClamp, input.texcoord).r;
                return MRender_OverdrawSampleRamp(overdraw);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
