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

Shader "Brush/DiffuseOpaque" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
}

SubShader {
	
Cull Back

CGPROGRAM
#pragma surface surf Lambert vertex:vert addshadow 
#pragma multi_compile __ TBT_LINEAR_TARGET
#include "../../../Shaders/Include/Brush.cginc"

fixed4 _Color;

struct Input { 
	float4 color : COLOR;
};

void vert(inout appdata_full v) {
	v.color = TbVertToNative(v.color);
}

void surf (Input IN, inout SurfaceOutput o) {
	o.Albedo = _Color * IN.color.rgb;   
}
ENDCG
}

Fallback "Diffuse"
}
