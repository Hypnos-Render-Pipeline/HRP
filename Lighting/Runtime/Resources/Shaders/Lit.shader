Shader "HRP/Lit"
{
    Properties
    {
		_MainTex("Albedo", 2D) = "white" {}
		_Cutoff("Alpha Cutoff",Range(0,1)) = 0
		_Color("Color", Color) = (1,1,1,1)

		[Toggle]_AutoDesk("Auto desk", int) = 0

		_MetallicGlossMap("Metallic Smoothness", 2D) = "white" {}
		[Gamma] _Metallic("Metallic", Range(0,1)) = 0.0

		_MetallicMap("Metallic Map", 2D) = "white" {}
		_RoughnessMap("Roughness Map", 2D) = "gray" {}
		_Roughness("Roughness", Range(0,1)) = 0.5

		_Smoothness("Smoothness", Range(0,1)) = 0.5
		_GlossMapScale("SmoothnessMapScale", Range(0,1)) = 1.0

		_BumpMap("Normal Map", 2D) = "bump" {}
		_BumpScale("Scale", Range(-10,10)) = 1.0

		_EmissionMap("EmissionMap", 2D) = "white" {}
		[HDR]_EmissionColor("EmissionColor", Color) = (0,0,0)

		_Index("IOR", Range(1, 4)) = 1.45

		[Toggle]_Subsurface("SubSurface", int) = 0
		_Ld("Average Scatter Distance", Range(0, 1)) = 0.1
		_ScatterProfile("Scatter Profile", 2D) = "white" {}

		_ClearCoat("Clear Coat", Range(0,1)) = 0

		_Sheen("Sheen", Range(0,1)) = 0

		[HideInInspector] _Mode("__mode", Float) = 0.0
		[HideInInspector] _SrcBlend("__src", Float) = 1.0
		[HideInInspector] _DstBlend("__dst", Float) = 0.0
		[HideInInspector] _ZWrite("__zw", Float) = 1.0
    }

    // Rasterization Shader
    SubShader
    {
        Pass
        {
            ColorMask 0
            Tags { "LightMode" = "PreZ" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert (float4 vertex : POSITION) : SV_POSITION { return UnityObjectToClipPos(vertex); }
            void frag() {}
            ENDCG
        }
    }

    CustomEditor "LitEditor"
}
