// URP Lit with one addition: the vertex colour multiplies the albedo.
//
// The project ships no authored assets, and this is the one deliberate exception -
// a .shader is hand-written source in the repository, like the .cs files beside it,
// not a binary anyone had to open an art tool to make. It exists because baked
// ambient occlusion has nowhere else to go: URP's own Lit shader ignores vertex
// colours entirely, so the shades the mesher computes would be carried all the way
// to the GPU and then thrown away.
//
// Nothing depends on it. MaterialLibrary asks for it by name and falls back to stock
// URP Lit when it is missing, so the worst case is terrain without AO rather than
// terrain that does not render - which matters, because a shader that fails to
// compile is exactly how this project shipped a magenta world once already.
Shader "MadVoxel/VoxelLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Colour", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.1
        _Metallic("Metallic", Range(0,1)) = 0.0

        // Lets the effect be dialled back without remeshing the world, which matters
        // when the only way to judge the strength is to stand in a trench and look.
        _AoStrength("Ambient Occlusion Strength", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
                half _AoStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  colour     : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half4  colour     : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.colour = input.colour;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // The whole reason this shader exists. Lerped from white rather than
                // multiplied outright so the strength slider means something.
                half ao = lerp(1.0h, input.colour.r, _AoStrength);
                albedo.rgb *= ao;

                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = NormalizeNormalPerPixel(input.normalWS);
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI = SampleSH(lighting.normalWS) * ao;

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.occlusion = ao;
                surface.alpha = 1.0h;

                return UniversalFragmentPBR(lighting, surface);
            }
            ENDHLSL
        }

        // Shadows and depth come straight from URP's own passes. Terrain that casts no
        // shadow would undo more than the occlusion adds.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    Fallback "Universal Render Pipeline/Lit"
}
