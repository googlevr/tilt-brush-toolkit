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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrushToolkit {

// Helper class for fixing damage done to particle meshes by Unity
// import-time vertex optimization and mesh splitting.
//
// Particle quads are two triangles whose indices look like
// [n+0 n+1 n+3] [n+0 n+3 n+2].  The particle shader additionally
// expects n to be a multiple of 4, because a vert's location in the
// quad is implicit in SV_VertexId rather than explicit in the data.
//
// We expect to see that pattern, but also expect that sometimes n will
// not be a multiple of 4 -- this can happen if Unity splits a mesh
// into multiple parts, or if Unity performs (unavoidable)
// unused-vertex optimization on import.
//
// This class therefore helps to classify input data, and to build
// up known-good output data.
internal class ParticleMesh {
  // Classifies a sequence of 6 indices
  internal enum QuadType {
    Degenerate,         // [n n n] [n n n]
    FullParticle,       // [n n+1 n+3] [n n+3 n+2]
    LatterHalfParticle, // [n n+3 n+2] ...
    Unknown,            // everything else
  };

  internal delegate void WarningCallback(string msg);

  // Static API

  // Rewrites data in mesh to ensure that it only contains valid particle quads.
  internal static void FilterMesh(Mesh mesh, WarningCallback callback) {
    ParticleMesh src = ParticleMesh.FromMesh(mesh);
    ParticleMesh dst = new ParticleMesh();

    // ClassifyQuad wants at least 6 verts to examine
    int limit = src.VertexCount - 5;
    int iiVert = 0;
    while (iiVert < limit) {
      switch (src.ClassifyQuad(iiVert, callback)) {
      case ParticleMesh.QuadType.FullParticle:
        dst.AppendQuad(src, iiVert);
        iiVert += 6;
        break;
      case ParticleMesh.QuadType.Degenerate:
        iiVert += 6;
        break;
      case ParticleMesh.QuadType.LatterHalfParticle:
        iiVert += 3;
        break;
      case ParticleMesh.QuadType.Unknown:
      default:
        iiVert += 1;
        break;
      }
    }
    dst.CopyToMesh(mesh);
  }

  private static ParticleMesh FromMesh(Mesh mesh) {
    var ret = new ParticleMesh {
      m_sourceMesh = mesh,
      m_vertices = mesh.vertices.ToList(),
      m_normals = mesh.normals.ToList(),
      m_colors = mesh.colors32.ToList(),
      m_tangents = mesh.tangents.ToList(),
      m_triangles = mesh.triangles.ToList()
    };
    mesh.GetUVs(0, ret.m_uv0);
    mesh.GetUVs(1, ret.m_uv1);
    return ret;
  }

  // Instance API

  Mesh m_sourceMesh = null;
  List<Vector3> m_vertices = new List<Vector3>();
  List<Vector3> m_normals = new List<Vector3>();
  List<Vector4> m_uv0 = new List<Vector4>();
  List<Vector4> m_uv1 = new List<Vector4>();
  List<Color32> m_colors = new List<Color32>();
  List<Vector4> m_tangents = new List<Vector4>();
  List<int> m_triangles = new List<int>();
  bool m_bNoisy = true;
  int? m_lastMod;

  internal int VertexCount { get { return m_vertices.Count; } }

  // iiVert is an index to an index to a vert (an index into m_triangles)
  // iiVert must be at least 6 verts from the end of the mesh.
  internal QuadType ClassifyQuad(int iiVert, WarningCallback callback) {
    if (iiVert + 6 > m_triangles.Count) {
      Debug.Assert(false, "Invalid ClassifyQuad {0}", m_sourceMesh);
      return QuadType.Unknown;
    }

    int i = iiVert;
    int v = m_triangles[i];
    if (m_triangles[i  ] == v   &&
        m_triangles[i+1] == v+1 &&
        m_triangles[i+2] == v+3 &&
        m_triangles[i+3] == v+0 &&
        m_triangles[i+4] == v+3 &&
        m_triangles[i+5] == v+2) {
      if (m_lastMod == null || (v % 4) != m_lastMod.Value) {
        // if (m_lastMod != null) {
        //   Debug.LogFormat(
        //       "At {0}: changing mod to {1}\n{2} {3} {4} {5} {6} {7}",
        //       iiVert, v % 4,
        //       m_triangles[i  ], m_triangles[i+1], m_triangles[i+2],
        //       m_triangles[i+3], m_triangles[i+4], m_triangles[i+5]);
        // }
        m_lastMod = (v % 4);
      }
      return QuadType.FullParticle;
    } else if (m_triangles[i  ] == v &&
               m_triangles[i+1] == v &&
               m_triangles[i+2] == v &&
               m_triangles[i+3] == v &&
               m_triangles[i+4] == v &&
               m_triangles[i+5] == v) {
      // Seems to be produced by either TB or FBX export
      // Debug.LogFormat("At {0}: degenerate quad", iiVert);
      return QuadType.Degenerate;
    } else if (m_triangles[i  ] == v   &&
               m_triangles[i+1] == v+3 &&
               m_triangles[i+2] == v+2) {
      // Will be produced by Unity splitting the particle mesh up
      // Debug.LogFormat("At {0}: half-particle", iiVert);
      return QuadType.LatterHalfParticle;
    } else {
      if (m_bNoisy) {
        m_bNoisy = false;
        if (callback != null) {
          callback(string.Format(
            "Found unexpected index sequence @ {0}: {1} {2} {3} {4} {5} {6}",
            iiVert,
            m_triangles[i  ], m_triangles[i+1], m_triangles[i+2],
            m_triangles[i+3], m_triangles[i+4], m_triangles[i+5]));
        }
      }
      return QuadType.Unknown;
    }
  }

  internal void AppendQuad(ParticleMesh rhs, int iiVert) {
    int rv0 = rhs.m_triangles[iiVert];
    int indexOffset = m_vertices.Count - rv0;
    bool hasNormals = rhs.m_normals != null && rhs.m_normals.Count > 0;
    bool hasTangents = rhs.m_tangents != null && rhs.m_tangents.Count > 0;
    // Assume IsValidParticle, and therefore [v0, v0+6) should be copied
    for (int i = 0; i < 6; ++i) {
      m_vertices.Add (rhs.m_vertices [rv0 + i]);
      if (hasNormals) { m_normals.Add  (rhs.m_normals  [rv0 + i]); }
      m_uv0.Add      (rhs.m_uv0      [rv0 + i]);
      m_uv1.Add      (rhs.m_uv1      [rv0 + i]);
      m_colors.Add   (rhs.m_colors   [rv0 + i]);
      if (hasTangents) { m_tangents.Add(rhs.m_tangents[rv0 + i]); }
      m_triangles.Add(rhs.m_triangles[iiVert + i] + indexOffset);
    }
  }

  // Copy data back to a Unity mesh
  internal void CopyToMesh(Mesh mesh) {
    mesh.Clear();
    mesh.vertices = m_vertices.ToArray();
    mesh.normals = m_normals.ToArray();
    mesh.SetUVs(0, m_uv0);
    mesh.SetUVs(1, m_uv1);
    mesh.colors32 = m_colors.ToArray();
    mesh.tangents = m_tangents.ToArray();
    mesh.triangles = m_triangles.ToArray();
  }
}

}
