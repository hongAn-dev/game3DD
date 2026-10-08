// Acid/lava surface: unlit, two world-space noise layers scrolling against each other, foam colour and glow
// where they overlap. Opaque and single pass so it stays cheap on Android (spec §5).
Shader "RoboLacLoi/HazardFlow" {
	Properties {
		_Color ("Deep color", Color) = (0.36, 0.52, 0.06, 1)
		_FoamColor ("Foam color", Color) = (0.8, 0.95, 0.25, 1)
		_NoiseTex ("Noise (R)", 2D) = "gray" {}
		_Scale ("World UV scale", Float) = 0.08
		_Flow ("Flow speed (layer 1 xy, layer 2 zw)", Vector) = (0.02, 0.01, -0.015, 0.02)
		_Glow ("Glow", Range(0, 2)) = 0.5
	}
	SubShader {
		Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#include "UnityCG.cginc"

			sampler2D _NoiseTex;
			fixed4 _Color, _FoamColor;
			float _Scale, _Glow;
			float4 _Flow;

			struct v2f {
				float4 pos : SV_POSITION;
				float2 uv0 : TEXCOORD0;
				float2 uv1 : TEXCOORD1;
				UNITY_FOG_COORDS(2)
			};

			v2f vert(appdata_base v) {
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				float2 world = mul(unity_ObjectToWorld, v.vertex).xz * _Scale;
				o.uv0 = world + _Flow.xy * _Time.y;
				o.uv1 = world * 1.7 + _Flow.zw * _Time.y;
				UNITY_TRANSFER_FOG(o, o.pos);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target {
				float n = tex2D(_NoiseTex, i.uv0).r * tex2D(_NoiseTex, i.uv1).r * 2.0;
				fixed4 c = lerp(_Color, _FoamColor, saturate(n * n));
				c.rgb *= 1.0 + _Glow * n;
				UNITY_APPLY_FOG(i.fogCoord, c);
				return c;
			}
			ENDCG
		}
	}
	Fallback "Unlit/Color"
}
