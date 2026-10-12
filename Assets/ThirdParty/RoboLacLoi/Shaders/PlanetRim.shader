Shader "RoboLacLoi/Planet Rim" {
	Properties {
		_RimColor ("Rim Color", Color) = (0.25, 0.62, 0.8, 0.45)
		_RimPower ("Rim Power", Range(1, 6)) = 3
	}
	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Cull Back
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			fixed4 _RimColor;
			float _RimPower;
			struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
			struct v2f { float4 pos : SV_POSITION; float3 viewDir : TEXCOORD0; float3 worldNormal : TEXCOORD1; };
			v2f vert(appdata v) {
				v2f o;
				float4 world = mul(unity_ObjectToWorld, v.vertex);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.viewDir = _WorldSpaceCameraPos.xyz - world.xyz;
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
				return o;
			}
			fixed4 frag(v2f i) : SV_Target {
				float facing = saturate(dot(normalize(i.viewDir), normalize(i.worldNormal)));
				float rim = pow(1.0 - facing, _RimPower);
				return fixed4(_RimColor.rgb, _RimColor.a * rim);
			}
			ENDCG
		}
	}
}
