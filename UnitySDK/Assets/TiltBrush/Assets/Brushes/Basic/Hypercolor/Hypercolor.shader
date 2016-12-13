// Copyright 2016 Google Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

Shader "Brush/Special/Hypercolor" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
	_Shininess ("Shininess", Range (0.01, 1)) = 0.078125
	_MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
	_BumpMap ("Normalmap", 2D) = "bump" {}
	_Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
}
    SubShader {
		Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
		LOD 100
      
		CGPROGRAM
		#pragma target 3.0
		#pragma surface surf StandardSpecular vertex:vert alphatest:_Cutoff addshadow
		#pragma multi_compile __ AUDIO_REACTIVE 
		#include "../../../Shaders/Brush.cginc"

		struct Input {
			float2 uv_MainTex;
			float2 uv_BumpMap;
			float4 color : Color;
			float3 worldPos;
		};
      
		sampler2D _MainTex;
		sampler2D _BumpMap;
		fixed4 _Color;
		half _Shininess;
      
		void vert (inout appdata_full v) {

			float t = 0.0;
			// Grab stroke width from tangent.w and renormalize
			float strokeWidth = abs(v.tangent.w) * 1.2;
			v.tangent.w /= strokeWidth;

#ifdef AUDIO_REACTIVE
			t = _BeatOutputAccum.z * 5;
			float waveIntensity = _BeatOutput.z * .1 * strokeWidth;
			v.vertex.xyz += (pow(1 - (sin(t + v.texcoord.x * 5 + v.texcoord.y * 10) + 1), 2)
								* cross(v.tangent.xyz, v.normal.xyz)
								* waveIntensity)
							;
#endif
		}
	
		void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
			fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
			
			float scroll = _Time.z;
#ifdef AUDIO_REACTIVE
			float3 localPos = mul(xf_I_CS, float4(IN.worldPos, 1.0)).xyz;
			float t = length(localPos) * .5;
			scroll =  _BeatOutputAccum.y*30;
			float angle = atan2(localPos.x, localPos.y);
			float waveform = tex2D(_WaveFormTex, float2(angle * 6,0)).g*2;

			//t += waveform;
			tex.rgb =  float3(1,0,0) * (sin(tex.r*2 + scroll*0.5 - t) + 1);
			tex.rgb += float3(0,1,0) * (sin(tex.r*3 + scroll*1 - t) + 1);
			tex.rgb += float3(0,0,1) * (sin(tex.r*4 + scroll*0.25 - t) + 1);
			//tex.rgb += tex.rgb * _BeatOutput.y;
#else
			tex.rgb =  float3(1,0,0) * (sin(tex.r * 2 + scroll*0.5 - IN.uv_MainTex.x) + 1) * 2;
			tex.rgb += float3(0,1,0) * (sin(tex.r * 3.3 + scroll*1 - IN.uv_MainTex.x) + 1) * 2;
			tex.rgb += float3(0,0,1) * (sin(tex.r * 4.66 + scroll*0.25 - IN.uv_MainTex.x) + 1) * 2;
#endif

			o.Albedo = tex.rgb * _Color.rgb * IN.color.rgb;   
			o.Smoothness = _Shininess;
			o.Specular = _SpecColor * tex.rgb;
			o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
			o.Alpha = tex.a * IN.color.a;
#ifdef AUDIO_REACTIVE
			o.Emission = o.Albedo;
			o.Albedo = .2;
			o.Specular *= .5;
#endif
			
		}
		ENDCG
    }

	FallBack "Transparent/Cutout/VertexLit"
}
				
				
				
