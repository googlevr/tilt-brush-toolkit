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

Shader "Brush/Special/AdditiveCutout" {
Properties {
  _MainTex ("Texture", 2D) = "white" {}
  _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5

}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend SrcAlpha One
  AlphaTest Greater .01
  ColorMask RGB
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile __ TBT_LINEAR_TARGET
      #include "UnityCG.cginc"
      #include "../../../Shaders/Include/Brush.cginc"

      sampler2D _MainTex;
      uniform float _Cutoff;
      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float3 normal : NORMAL;
        float2 texcoord : TEXCOORD0;
      };

      struct v2f {
        float4 vertex : SV_POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
      };

      float4 _MainTex_ST;

      v2f vert (appdata_t v)
      {

        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        o.color = TbVertToNative(v.color);
        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {
         half4 c = tex2D(_MainTex, i.texcoord );

        // Cutoff the alpha value based on the incoming vertex alpha
        i.color.a = (i.color.a * c.a < _Cutoff) ? 0 : 1;

        return i.color * float4(c.rgb,1);
      }
      ENDCG
    }
  }
}
}
