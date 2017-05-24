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

// -*- c -*-
// Utilities for implementing 'genius' particles.
//
// These functions help with orienting the particle quads to face the camera, and also
// the animation of the particles out from the origin.
//
// In general, a particle's quad is made camera facing in the following way:
//
// * v.corner is valid, but the quad is arbitrarily-oriented
// * v.center is the center of the quad
// * v.vid (vertex id) is used to work out which corner of the quad the vertex is.
// * The size of the quad is inferred from (v.corner - v.center).length
// * One of the _OrientParticle variants is called
// * This is done so a "naive" geometry export still contains usable vert data
//
// Some particles also 'spread' from an origin. The spreading works as follows:
//
// * The particle vertices are stored in their final, 'resting' position.
// * v.origin is the birth position of the particle
// * The time of particle spawn is passed through in a spare texcoord channel.
// * The particle position is an exponential decay from v.origin to v.center
//
// Also note that there is a special case for shrinking the particles, as this is needed for the
// preview brush. In the case of the preview brush, the time is sent through in negative,
// and this is used as a signal to shrink the particle over time.

static const float kRecipSquareRootOfTwo = 0.70710678;

// Value of the preview lifetime *must* be greater than zero.
uniform float _GeniusParticlePreviewLifetime;

#define ParticleVertexWithSpread_t ParticleVertex_t

struct ParticleVertex_t {
  uint vid : SV_VertexID;
  float4 corner : POSITION;     // pos: corner of randomly-oriented quad
  float3 center : TEXCOORD1;
  fixed4 color : COLOR;
  float4 texcoord : TEXCOORD0;  // xy: texcoord   z: rotation   w: birth time
};

// Rotates the corner of a square quad centered at the origin
//   up, rt   - quad's basis axes; unit-length
//   center   - quad center
//   halfSize - ie, distance from center to edge
//   corner   - a number in [0, 3]; specifies which corner to rotate
//   rotation - in radians; rotates counterclockwise in the plane of the quad
//
// quad-space coordinate system
//
//           ^ y axis/up
//           |
//       2 . . . 3
//       .   |   .         x axis/right
//       . - o <--origin   --->
//       .   |   .
//       0 . . . 1
//
float3 _RotatedQuadCorner(float3 up, float3 rt, float3 center,
                          float halfSize, int corner, float rotation) {
  // The corner's position in the (2D) quad coordinate system
  float2 pos = halfSize * float2(
      float(corner == 1 || corner == 3) * 2 - 1,  // +1 for 1 and 3,  -1 for 0 and 2
      float(corner == 2 || corner == 3) * 2 - 1   // +1 for 2 and 3,  -1 for 0 and 1
  );

  // Perform rotation in quad coordinate system
  float c = cos(rotation);
  float s = sin(rotation);
  float2x2 mRotation = float2x2(c, -s,  s, c);
  float2 rotatedPos = mul(mRotation, pos);

  // Change-of-basis to 3D coordinate system.
  // gles requires square arrays, so do it homogeneous-style.
  return mul(float3(rotatedPos, 1), float3x3(rt, up,  center));
}

// Returns the position of a camera-oriented quad corner.
// The _WS variant returns a worldspace position.
//
//   center   - object-space center of quad
//   halfSize - distance from center to an edge
//   corner   - a number in [0,3] in CCW order as seen from front, bottom-left is 0
//   rotation - in radians
//
float3 _OrientParticle(float3 center, float halfSize, int corner, float rotation)
{
  float3 up, rt; {
    float4x4 cameraToObject = mul(unity_WorldToObject, unity_CameraToWorld);
    float3 upIsh = mul(cameraToObject, float3(0, 1, 0));
    float3 objSpaceCameraPos = mul(cameraToObject, float4(0, 0, 0, 1));
    float3 fwd = (center - objSpaceCameraPos);
    rt = normalize(cross(upIsh, fwd));
    // TODO(timaidley): Temporarily revert to previous behaviour; see b/62067322
    up = upIsh;  // normalize(cross(fwd, rt));
  }

  return _RotatedQuadCorner(up, rt, center, halfSize, corner, rotation);
}

float3 _OrientParticle_WS(float3 center_OS, float halfSize_OS, int corner, float rotation)
{
  float3 center_WS = mul(unity_ObjectToWorld, float4(center_OS, 1));
  float3 up_WS, rt_WS; {
    // Trying to write this without using unity_CameraToWorld because some renderers
    // don't keep around the inverse view matrix. upIsh_WS won't be unit-length, but that's fine.
    float3 upIsh_WS = UNITY_MATRIX_V[1].xyz;
    float3 cameraPos_WS = _WorldSpaceCameraPos;
    float3 fwd_WS = (center_WS - cameraPos_WS);
    rt_WS = normalize(cross(upIsh_WS, fwd_WS));
    // TODO(timaidley): Temporarily revert to previous behaviour; see b/62067322
    up_WS = upIsh_WS;  // normalize(cross(fwd_WS, rt_WS));
  }

  float halfSize_WS = halfSize_OS * length(unity_ObjectToWorld[0].xyz);
  return _RotatedQuadCorner(up_WS, rt_WS, center_WS, halfSize_WS, corner, rotation);
}

// Sign bit of time is used to determine if this is a preview brush or not.
// Unpack that into a positive time value, and a size adjustment.
float _ParticleUnpackTime(inout float time) {
  float sizeAdjust;
  if (time < 0) {
    time = -time;
    float life01 = clamp((_Time.y - time) / _GeniusParticlePreviewLifetime, 0, 1);
    sizeAdjust = 1 - (life01 * life01);
  } else {
    sizeAdjust = 1;
  }
  return sizeAdjust;
}

// Adjusts a quad vertex to make the quad camera-facing, and scales the particle
// if the particle is in "preview mode".
//
// The "AndSpread" versions additionally cause the quad to spread out from
// an origin position.
// The "_WS" versions return the result in worldspace instead of objectspace.
//
//   vertexId   - Which corner of the quad (0 lower-left, increasing CCW)
//   corner     - Object-space position of this corner; only used to compute particle size
//   center     - Object-space position of the center after fully-born
//   rotation   - in radians
//   birthTime  - Particle birth time; sign bit indicates preview-ness
//   origin     - Object-space position of center at birth
//   spreadRate - How fast quad moves from origin to center. Units of periods-per-second,
//                where one period is about 63% (ie, a decay to 1/e)

#define OrientParticleAndSpread(a,b,c,d,e,f,g) OrientParticle(a,b,c,d,e)

#define OrientParticleAndSpread_WS(a,b,c,d,e,f,g) OrientParticle_WS(a,b,c,d,e)

float4 OrientParticle(
    uint vertexId, float3 corner, float3 center, float rotation, float birthTime) {
  float sizeAdjust = _ParticleUnpackTime(/* inout */ birthTime);
  float halfSize = length(corner - center) * kRecipSquareRootOfTwo * sizeAdjust;
  float3 newCorner = _OrientParticle(center, halfSize, vertexId & 3, rotation);
  return float4(newCorner.xyz, 1);
}

float4 OrientParticle_WS(
    uint vertexId, float3 corner, float3 center, float rotation, float birthTime) {
  float sizeAdjust = _ParticleUnpackTime(/* inout */ birthTime);
  float halfSize = length(corner - center) * kRecipSquareRootOfTwo * sizeAdjust;
  float3 newCorner_WS = _OrientParticle_WS(center, halfSize, vertexId & 3, rotation);
  return float4(newCorner_WS.xyz, 1);
}
