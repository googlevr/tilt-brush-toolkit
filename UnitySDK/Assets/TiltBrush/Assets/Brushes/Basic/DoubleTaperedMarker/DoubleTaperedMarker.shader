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

Shader "Brush/Special/DoubleTaperedMarker" {
Properties {
}

Category {
	Cull Off Lighting Off 
	
	SubShader {
		Tags{ "DisableBatching" = "True" }
		Pass { 
		 
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_particles

			#include "UnityCG.cginc"
			#include "../../../Shaders/Brush.cginc"

			sampler2D _MainTex;
			
			struct appdata_t {
				float4 vertex : POSITION;
				fixed4 color : COLOR; 
				float2 texcoord0 : TEXCOORD0;
				float3 texcoord1 : TEXCOORD1; //per vert offset vector
			};

			struct v2f {
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};
			
			v2f vert (appdata_t v)
			{

				//
				// XXX - THIS SHADER SHOULD BE DELETED AFTER WE TAPERING IS DONE IN THE GEOMETRY GENERATION
				//

				v2f o; 
				float envelope = sin(v.texcoord0.x * 3.14159);
				float widthMultiplier = 1 - envelope;
				v.vertex.xyz += -v.texcoord1 * widthMultiplier * 0.1; // TODO: Use raw secondary coordinates once supported
				o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				o.color = v.color;
				o.texcoord = v.texcoord0;
				return o; 
			}
		
			fixed4 frag (v2f i) : COLOR
			{
				
				return float4(i.color.rgb, 1);
			}

			ENDCG 
		}
	}	
}
}
