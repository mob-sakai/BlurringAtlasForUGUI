Shader "UI/Hidden/UI-Effect-BlurringAtlas"
{
	Properties
	{
		[PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_ColorMask ("Color Mask", Float) = 15

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
	}



	SubShader
	{
		Tags
		{ 
			"Queue"="Transparent" 
			"IgnoreProjector"="True" 
			"RenderType"="Transparent" 
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
		}
		
		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp] 
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}

		Cull Off
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask [_ColorMask]

		Pass
		{
			Name "Blurring"

		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#if !defined(SHADER_API_D3D11_9X) && !defined(SHADER_API_D3D9)
			#pragma target 2.0
			#else
			#pragma target 3.0
			#endif

			#pragma multi_compile __ UNITY_UI_ALPHACLIP
			
			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color	: COLOR;
				float2 texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID

				float2 uvMask : TEXCOORD1;
			};

			struct v2f
			{
				float4 vertex   : SV_POSITION;
				fixed4 color	: COLOR;
				float3 texcoord  : TEXCOORD0;
				float4 worldPosition : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO

				half4 uvMask : TEXCOORD2;
			};
			
			fixed4 _Color;
			fixed4 _TextureSampleAdd;
			float4 _ClipRect;
			sampler2D _MainTex;
			float4 _MainTex_TexelSize;

			// Unpack float to low-precision [0-1] half2. 
			half2 UnpackToVec2(float value)
			{
				const int PACKER_STEP = 4096;
				const int PRECISION = PACKER_STEP - 1;
				half2 result;

				result.x = (value % PACKER_STEP) / PRECISION;
				value = floor(value / PACKER_STEP);

				result.y = (value % PACKER_STEP) / PRECISION;
				return result;
			}

			// Sample texture with blurring.
			fixed4 Tex2DBlurring (sampler2D tex, half2 texcood, half2 blur, half4 mask)
			{
				const int KERNEL_SIZE = 7;
				float4 o = 0;
				float sum = 0;
				const fixed4 clear = fixed4(0.5,0.5,0.5,0);
				for(int x = -KERNEL_SIZE/2; x <= KERNEL_SIZE/2; x++)
				{
					for(int y = -KERNEL_SIZE/2; y <= KERNEL_SIZE/2; y++)
					{
						half weight = (4 - abs(x)) * (4 - abs(y));
						half2 uv = texcood + blur * half2(x,y);
						sum += weight;
						fixed masked = min(mask.x <= uv.x, uv.x <= mask.z) * min(mask.y <= uv.y, uv.y <= mask.w);
						o += lerp(clear, tex2D(tex, uv), masked) * weight;
					}
				}
				return o / sum;
			}

			v2f vert(appdata_t IN)
			{
				v2f OUT;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

				OUT.worldPosition = IN.vertex;
				OUT.vertex = UnityObjectToClipPos(IN.vertex);

				OUT.texcoord.xy = UnpackToVec2(IN.texcoord.x);
				OUT.texcoord.xy = OUT.texcoord *2 - 0.5;
				OUT.texcoord.z = IN.texcoord.y;
				
				OUT.color = IN.color * _Color;

				OUT.uvMask.xy = UnpackToVec2(IN.uvMask.x);
				OUT.uvMask.zw = UnpackToVec2(IN.uvMask.y);
				return OUT;
			}

			fixed4 frag(v2f IN) : SV_Target
			{
				half2 uv = IN.texcoord.xy;
				half4 uvMask = IN.uvMask;
				half blurring = IN.texcoord.z;
				half4 color = (Tex2DBlurring(_MainTex, uv, blurring * _MainTex_TexelSize.xy * 2, uvMask) + _TextureSampleAdd) * IN.color;

				color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);

				return color;
			}
		ENDCG
		}
	}
}