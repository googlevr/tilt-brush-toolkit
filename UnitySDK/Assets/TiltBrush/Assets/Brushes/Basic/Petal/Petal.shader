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

Shader "Brush/Special/Petal" {
  Properties{
    _SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
    _Shininess("Shininess", Range(0.01, 1)) = 0.3
    _MainTex("Base (RGB) TransGloss (A)", 2D) = "white" {}
  }
  SubShader{
    Tags {"IgnoreProjector" = "True" "RenderType" = "Opaque"}
    Cull Off

    CGPROGRAM
      #pragma target 3.0
      #pragma surface surf StandardSpecular vertex:vert addshadow
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ ODS_RENDER

      #include "../../../Shaders/Include/Brush.cginc"

      struct Input {
        float2 uv_MainTex;
        float4 color : Color;
        fixed vface : VFACE;
      };

      half _Shininess;

      void vert(inout appdata_full i) {
        i.color = TbVertToNative(i.color);
      }

      void surf(Input IN, inout SurfaceOutputStandardSpecular o) {
        // Fade from center outward (dark to light)
        float4 darker_color = IN.color;
        darker_color *= 0.6;
        float4 finalColor = lerp(IN.color, darker_color, 1- IN.uv_MainTex.x);

        float fAO = IN.vface == -1 ? .5 * IN.uv_MainTex.x : 1;
        o.Albedo = finalColor * fAO;
        o.Smoothness = _Shininess;
        o.Specular = _SpecColor * fAO;
        o.Alpha = 1;
      }
    ENDCG
  }
}
