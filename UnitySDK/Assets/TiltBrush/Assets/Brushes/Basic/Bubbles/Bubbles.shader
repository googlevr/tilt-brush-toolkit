// Copyright 2017 Google Inc.
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

Shader "Brush/Particle/Bubbles" {
Properties {
	_MainTex ("Particle Texture", 2D) = "white" {}
	_ScrollRate("Scroll Rate", Float) = 1.0
	_ScrollJitterIntensity("Scroll Jitter Intensity", Float) = 1.0
	_ScrollJitterFrequency("Scroll Jitter Frequency", Float) = 1.0
	_SpreadRate ("Spread Rate", Range(0.3, 5)) = 1.539
}

Category {
	Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "DisableBatching"="True" }
	Blend One One
	AlphaTest Greater .01
	ColorMask RGB
	Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

	SubShader {
		Pass {

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_particles
			#pragma target 3.0
			#pragma multi_compile __ TBT_LINEAR_TARGET

			#include "UnityCG.cginc"
			#include "../../../Shaders/Include/Brush.cginc"
			#include "../../../Shaders/Include/Particles.cginc"
			#include "Assets/ThirdParty/Noise/Shaders/Noise.cginc"

			sampler2D _MainTex;
			fixed4 _TintColor;

			struct v2f {
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
			};

			float4 _MainTex_ST;
			float _ScrollRate;
			float _ScrollJitterIntensity;
			float _ScrollJitterFrequency;
			float3 _WorldSpaceRootCameraPosition;
			float _SpreadRate;

			float4 displace(float4 pos, float timeOffset) {
				float t = _Time.y*_ScrollRate + timeOffset;

				pos.x += sin(t + _Time.y + pos.z * _ScrollJitterFrequency) * _ScrollJitterIntensity;
				pos.z += cos(t + _Time.y + pos.x * _ScrollJitterFrequency) * _ScrollJitterIntensity;
				pos.y += cos(t * 1.2 + _Time.y + pos.x * _ScrollJitterFrequency) * _ScrollJitterIntensity;

				float time = _Time.x * 5;
				float d = 30;
				float freq = .1;
				float3 disp = float3(1,0,0) * curlX(pos.xyz * freq + time, d);
				disp += float3(0,1,0) * curlY(pos.xyz * freq +time, d);
				disp += float3(0,0,1) * curlZ(pos.xyz * freq + time, d);
				pos.xyz += disp * 10 * kDecimetersToWorldUnits;
				return pos;
			}

			v2f vert (ParticleVertexWithSpread_t v) {
				v2f o;
				v.color = TbVertToSrgb(v.color);
				float4 pos_WS = OrientParticleAndSpread_WS(
						v.vid, v.corner.xyz, v.center,
						v.texcoord.z /* rotation */, v.texcoord.w /* birthTime */,
						v.origin, _SpreadRate);

				// Do this in scene space to avoid swimming through parameter space.
				// With genius particles, we'd do this in modelspace or something similar.
				// TODO(pld): object/canvas space is more invariant than scene space,
				// so use OrientParticle rather than OrientParticle_WS
				{
					float4 pos_CS = mul(xf_I_CS, pos_WS);
					pos_CS = displace(pos_CS, v.color.a * 10);
					pos_WS = mul(xf_CS, pos_CS);
				}

				o.vertex = mul(UNITY_MATRIX_VP, pos_WS);
				o.worldPos = pos_WS;

				// Brighten up the bubbles
				o.color = v.color;
				o.color.a = 1;
				o.texcoord = TRANSFORM_TEX(v.texcoord.xy,_MainTex);

				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float4 tex = tex2D(_MainTex, i.texcoord);

				// RGB Channels of the texture are affected by color
				float3 basecolor = i.color * tex.rgb;

				// Alpha channel of the texture is not affected by color.  It is the fake "highlight" bubble effect.
				float3 highlightcolor = tex.a;

				float4 color = float4(basecolor + highlightcolor, 1);
				return SrgbToNative(color);
			}
			ENDCG
		}
	}
}
}
