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

Shader "Brush/Special/Rainbow" {
Properties {
  _MainTex ("Particle Texture", 2D) = "white" {}
    _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One // SrcAlpha One
  BlendOp Add, Min
  ColorMask RGBA
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ TBT_LINEAR_TARGET

      #pragma target 3.0
      #include "UnityCG.cginc"
      #include "../../../Shaders/Include/Brush.cginc"

      sampler2D _MainTex;

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
      half _EmissionGain;

      v2f vert (appdata_t v)
      {
        v.color = TbVertToSrgb(v.color);

        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        o.color = v.color;
        return o;
      }

      float4 GetRainbowColor( half2 texcoord)
      {
        texcoord = saturate(texcoord);
        // Create parametric UV's
        half2 uvs = texcoord;
        float row_id = floor(uvs.y * 5);
        uvs.y *= 5;

        // Create parametric colors
        half4 tex = float4(0,0,0,1);
        half row_y = fmod(uvs.y,1);

        row_id = ceil(fmod(row_id + _Time.z,5)) - 1;

        tex.rgb = row_id == 0 ? float3(1,0,0) : tex.rgb;
        tex.rgb = row_id == 1 ? float3(.7,.3,0) : tex.rgb;
        tex.rgb = row_id == 2 ? float3(0,1,.0) : tex.rgb;
        tex.rgb = row_id == 3 ? float3(0,.2,1) : tex.rgb;
        tex.rgb = row_id == 4 ? float3(.4,0,1.2) : tex.rgb;

        // Make rainbow lines pulse
        tex.rgb *= pow( (sin(row_id * 1 + _Time.z)   + 1)/2,5);

        // Make rainbow lines thin
        tex.rgb *= saturate(pow(row_y * (1 - row_y) * 5, 50));

        return tex;
      }

      float4 GetAudioReactiveRainbowColor( half2 texcoord)
      {
        texcoord = saturate(texcoord);
        // Create parametric UV's
        half2 uvs = texcoord;
        float row_id = floor(uvs.y * 5);
        uvs.y *= 5;

        // Create parametric colors
        half4 tex = float4(0,0,0,1);
        half row_y = fmod(uvs.y,1);

        row_id = ceil(fmod(row_id + _BeatOutputAccum.x*3,5)) - 1;

        tex.rgb = row_id == 0 ? float3(1,0,0) : tex.rgb;
        tex.rgb = row_id == 1 ? float3(.7,.3,0) : tex.rgb;
        tex.rgb = row_id == 2 ? float3(0,1,.0) : tex.rgb;
        tex.rgb = row_id == 3 ? float3(0,.2,1) : tex.rgb;
        tex.rgb = row_id == 4 ? float3(.4,0,1.2) : tex.rgb;

        // Make rainbow lines pulse
        // tex.rgb *= pow( (sin(row_id * 1 + _BeatOutputAccum.x*10)   + 1)/2,5);

        // Make rainbow lines thin
        tex.rgb *= saturate(pow(row_y * (1 - row_y) * 5, 50));

        return tex;
      }

      float4 GetAudioReactiveColor( half2 texcoord)
      {
        texcoord = texcoord.yx;
        texcoord.y *= 2;

        // Create parametric UV's
        float quantizedMotion = ceil((_BeatOutputAccum.z*.1) / 10);
        float row_id = abs(texcoord.y * 12 + quantizedMotion);

        // Create parametric colors
        float4 tex = float4(0,0,0,1);
        float row_y = fmod(abs(row_id),1.0);

        row_id = ceil(fmod(row_id, 8));

        float bandlevels = tex2D(_FFTTex, float2(row_id/8,0) ).w;
        bandlevels = max(bandlevels, .1);
        tex.rgb = abs(texcoord.x - .5) < bandlevels * .5 ? float3(1,1,1) : tex.rgb;

        // Make rainbow lines pulse
        tex.rgb *= tex.rgb * .5 + tex.rgb * _BeatOutput.y;

        // Make rainbow lines thin
        tex.rgb *= saturate(20 - abs(row_y - .5)*50);
        return tex;
      }

      // Input color is srgb
      fixed4 frag (v2f i) : SV_Target
      {
        i.color.a = 1; //ignore incoming vert alpha
#ifdef AUDIO_REACTIVE
        float4 tex =  GetAudioReactiveRainbowColor(i.texcoord.xy);
        tex *= GetAudioReactiveColor(i.texcoord.xy);
        tex = i.color * tex * exp(_EmissionGain * 2.5f);
#else
        float4 tex =  GetRainbowColor(i.texcoord.xy);
        tex = i.color * tex * exp(_EmissionGain * 3.0f);
#endif
        float4 color = float4(tex.rgb * tex.a, 1.0);
        color = SrgbToNative(color);
        return color;
      }
      ENDCG
    }
  }
}
}
