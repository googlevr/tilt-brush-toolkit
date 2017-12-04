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
using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TiltBrushToolkit {

public class ModelImportSettings : AssetPostprocessor {
  readonly Version kToolkitVersion            = new Version { major=14 };
  readonly Version kRequiredFbxExportVersion  = new Version { major=10 };

  public static bool sm_forceOldMeshNamingConvention = false;

  private FbxUtils.FbxInfo m_Info;

  private bool IsTiltBrush { get { return m_Info.tiltBrushVersion != null; } }

  // UVs come as four float2s so go through them and pack them back into two float4s
  static void CollapseUvs(Mesh mesh) {
    var finalUVs = new List<List<Vector4>>();
    for (int iUnityChannel = 0; iUnityChannel < 2; iUnityChannel++) {
      var sourceUVs = new List<Vector2>();
      var targetUVs = new List<Vector4>();
      int iFbxChannel = 2 * iUnityChannel;
      mesh.GetUVs(iFbxChannel, targetUVs);
      mesh.GetUVs(iFbxChannel + 1, sourceUVs);
      if (sourceUVs.Count > 0 || targetUVs.Count > 0) {
        for (int i = 0; i < sourceUVs.Count; i++) {
          if (i < targetUVs.Count) {
            // Repack xy into zw
            var v4 = targetUVs[i];
            v4.z = sourceUVs[i].x;
            v4.w = sourceUVs[i].y;
            targetUVs[i] = v4;
          } else {
            targetUVs.Add(new Vector4(0, 0, sourceUVs[i].x, sourceUVs[i].y));
          }
        }
      }
      finalUVs.Add(targetUVs);
    }
    for (int i = 0; i < finalUVs.Count; i++) {
      mesh.SetUVs(i, finalUVs[i]);
    }
    // Clear unused uv sets
    mesh.SetUVs(2, new List<Vector2>());
    mesh.SetUVs(3, new List<Vector2>());
  }

  static void ConvertSrgbToLinear(Mesh mesh) {
    Color32[] colors32 = mesh.colors32;
    for (int i = 0; i < colors32.Length; ++i) {
      colors32[i] = ((Color)colors32[i]).linear;
    }
    mesh.colors32 = colors32;
  }

  // Pulls some shenanigans so that a proper context will be shown in the editor.
  // null context means "the current asset".
  // Useful because the objects we get during import are too transient for LogFormat()
  // to use effectively.
  public void LogWarningWithContext(string msg, UnityEngine.Object desiredContext=null) {
    // If we call .SaveAndReimport, the context is only good for about a second or so.
    // But otherwise, this (surprisingly) seems to work fairly well.

    UnityEngine.Object actualContext = null;

    if (desiredContext != null) {
      foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath)) {
        if (obj.name == desiredContext.name && obj is GameObject) {
          actualContext = obj;
          break;
        }
      }
    }

    if (actualContext == null) {
      actualContext = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    }
    Debug.LogWarning(msg, actualContext);
  }

  void OnPreprocessModel() {
    m_Info = FbxUtils.GetTiltBrushFbxInfo(assetPath);
    if (! IsTiltBrush) {
      return;
    }

    if (m_Info.tiltBrushVersion >= new Version { major=10 }) {
      var importer = assetImporter as ModelImporter;
      if (importer != null && importer.optimizeMesh) {
        // Not a warning, just a friendly notification.
        Debug.LogFormat("{0}: Tilt Brush Toolkit requires optimizeMesh=false; disabling.", assetPath);
        importer.optimizeMesh = false;
      }
    }

    if (m_Info.tiltBrushVersion < kRequiredFbxExportVersion) {
      LogWarningWithContext(string.Format(
          "{0} was exported with an older version of Tilt Brush ({1}) that is not supported by this version of the Toolkit. For best results, re-export it with a newer Tilt Brush version ({2}).",
          assetPath, m_Info.tiltBrushVersion, kRequiredFbxExportVersion));
    } else if (m_Info.requiredToolkitVersion != null &&
        kToolkitVersion < m_Info.requiredToolkitVersion) {
      LogWarningWithContext(string.Format(
          "{0} was exported with an newer version of Tilt Brush that is not supported by this version of the Toolkit ({1}). For best results, upgrade your Toolkit to a newer version ({2}) or downgrade your Tilt Brush.",
          assetPath, kToolkitVersion, m_Info.requiredToolkitVersion));
    }
  }

  // Try to find a Tilt Brush material using the imported models's material name
  Material OnAssignMaterialModel(Material material, Renderer renderer) {
    // Ignore models that aren't Tilt Brush - generated FBXs
    if (! IsTiltBrush) {
      return null;
    }
    // For now, don't be strict about versions, because we can do an okay job on TB7 fbx files

    BrushDescriptor desc = GetDescriptorForStroke(material.name);

    if (desc != null) {
      // This is a stroke mesh and needs postprocessing.
      if (renderer.GetComponent<MeshFilter>() != null) {
        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
        CollapseUvs(mesh);
        if (m_Info.tiltBrushVersion < new Version { major = 10 }) {
          // Old versions of TB use linear lighting
          ConvertSrgbToLinear(mesh);
        }

        if (m_Info.tiltBrushVersion.Value.major >= 12) {
          // Pre-TB12, Tilt Brush did unit conversion but not coord system conversion
          // when exporting texcoords with Position semantics. TB12 fixes this, so now
          // texcoord Position data are in fbx coordinates rather than Unity coordinates.
          // However, this means we need to convert from fbx -> Unity coords on import,
          // since Unity only takes care of mesh.vertices, tangents, normals, binormals.
          PerformTexcoordCoordinateConversion(desc, mesh, 0);
          PerformTexcoordCoordinateConversion(desc, mesh, 1);
        }
        if (m_Info.tiltBrushVersion.Value.major < 14 &&
            desc.m_Guid == new Guid("e8ef32b1-baa8-460a-9c2c-9cf8506794f5")) {
          // Pre-TB14, Tilt Brush hadn't set the "Z is distance" semantic and texcoord0.z
          // ended up in decimeters
          FixupHypercolorTexcoord(desc, mesh);
        }

        if (desc.m_IsParticle) {
          // Would like to do this in OnPreprocessModel, but we don't yet
          // know whether it's a particle mesh.
          var importer = assetImporter as ModelImporter;
          if (importer != null && importer.optimizeMesh) {
            // Should never get here any more.
            LogWarningWithContext("Disabling optimizeMesh and reimporting. Tilt Brush particle meshes must have optimizeMesh=false.");
            importer.optimizeMesh = false;
            importer.SaveAndReimport();
          }

          ParticleMesh.FilterMesh(mesh, s => LogWarningWithContext(s, renderer));
        }
      }
      return desc.m_Material;
    } else {
      return null;
    }
  }

  // Convert from fbx coordinate conventions to Unity coordinate conventions
  static Vector3 UnityFromFbx(Vector3 v) {
    return new Vector3(-v.x, v.y, v.z);
  }
  static Vector4 UnityFromFbx(Vector4 v) {
    return new Vector4(-v.x, v.y, v.z, v.w);
  }

  static BrushDescriptor.Semantic GetUvsetSemantic(BrushDescriptor desc, int uvSet) {
    switch (uvSet) {
    case 0:
      return desc.m_uv0Semantic;
    case 1:
      // Unity's mesh importer destroys non-unit data in fbx.normals, so Tilt Brush
      // sometimes routes mesh.normals through fbx.texcoord1 instead.
      if (desc.m_bFbxExportNormalAsTexcoord1) {
        return desc.m_normalSemantic;
      } else {
        return desc.m_uv1Semantic;
      }
    default:
      throw new ArgumentException("uvSet");
    }
  }

  static int GetUvsetSize(BrushDescriptor desc, int uvSet) {
    switch (uvSet) {
    case 0: return desc.m_uv0Size;
    case 1:
      // Unity's mesh importer destroys non-unit data in fbx.normals, so Tilt Brush
      // sometimes routes mesh.normals through fbx.texcoord1 instead.
      if (desc.m_bFbxExportNormalAsTexcoord1) {
        return 3;
      } else {
        return desc.m_uv1Size;
      }
    default:
      throw new ArgumentException("uvSet");
    }
  }

  void FixupHypercolorTexcoord(BrushDescriptor desc, Mesh mesh) {
    int uvSet = 0;
    var semantic = GetUvsetSemantic(desc, uvSet);
    if (semantic != BrushDescriptor.Semantic.XyIsUvZIsDistance ||
        GetUvsetSize(desc, uvSet) != 3) {
      LogWarningWithContext("Not hypercolor?");
    }

    var data = new List<Vector3>();
    mesh.GetUVs(uvSet, data);
    for (int i = 0; i < data.Count; ++i) {
      Vector3 tmp = data[i];
      tmp.z *= .1f;
      data[i] = tmp;
    }
    mesh.SetUVs(uvSet, data);
  }

  void PerformTexcoordCoordinateConversion(BrushDescriptor desc, Mesh mesh, int uvSet) {
    var semantic = GetUvsetSemantic(desc, uvSet);
    if (semantic == BrushDescriptor.Semantic.Vector ||
        semantic == BrushDescriptor.Semantic.Position) {
      var size = GetUvsetSize(desc, uvSet);
      if (size == 3) {
        var data = new List<Vector3>();
        mesh.GetUVs(uvSet, data);
        for (int i = 0; i < data.Count; ++i) {
          data[i] = UnityFromFbx(data[i]);
        }
        mesh.SetUVs(uvSet, data);
      } else if (size == 4) {
        var data = new List<Vector4>();
        mesh.GetUVs(uvSet, data);
        for (int i = 0; i < data.Count; ++i) {
          data[i] = UnityFromFbx(data[i]);
        }
        mesh.SetUVs(uvSet, data);
      } else {
        LogWarningWithContext(string.Format(
            "Unexpected: Semantic {0} on texcoord of size {1}, guid {2}",
            semantic, size, desc.m_Guid));
      }
    }
  }

  void OnPostprocessModel(GameObject g) {
    // For backwards compatibility, if people have projects that use the old naming
    if (sm_forceOldMeshNamingConvention) {
      Dictionary<Material, BrushDescriptor> lookup = BrushManifest.Instance.AllBrushes
          .ToDictionary(desc => desc.m_Material);
      ChangeNamesRecursive(lookup, d => d.m_DurableName + "_geo", g.transform);
    }
  }

  delegate string NameCallback(BrushDescriptor desc);
  void ChangeNamesRecursive(
      Dictionary<Material, BrushDescriptor> lookup,
      NameCallback callback,
      Transform t) {
    var filter = t.GetComponent<MeshFilter>();
    var renderer = t.GetComponent<MeshRenderer>();
    if (filter != null && renderer != null) {
      var mesh = filter.sharedMesh;
      var material = renderer.sharedMaterial;
      if (mesh != null) {
        BrushDescriptor desc;
        if (lookup.TryGetValue(material, out desc)) {
          string oldName = callback(desc);
          mesh.name = oldName;
          filter.gameObject.name = oldName;
        }
      }
    }

    foreach (Transform child in t) {
      ChangeNamesRecursive(lookup, callback, child);
    }
  }

  /// Returns true if the path refers to an fbx that can be processed
  /// by this version of the toolkit
  bool IsSupportedTiltBrushFbx() {
    if (! IsTiltBrush) {
      return false;
    }

    if (m_Info.tiltBrushVersion < kRequiredFbxExportVersion) {
      return false;
    } else if (m_Info.requiredToolkitVersion != null &&
        kToolkitVersion < m_Info.requiredToolkitVersion) {
      return false;
    }

    return true;
  }

  BrushDescriptor GetDescriptorForStroke(string oldMaterialName) {
    // Newer versions of Tilt Brush embed the GUID in the material name
    Match match = Regex.Match(oldMaterialName, @"^([0-9a-fA-F]{32})");
    if (match.Success) {
      Guid brushGuid = new Guid(match.Groups[1].Value);
      try {
        return BrushManifest.Instance.BrushesByGuid[brushGuid];
      } catch (KeyNotFoundException) {
        LogWarningWithContext(string.Format(
            "Unexpected: Couldn't find Tilt Brush material for guid {0}.",
            brushGuid));
        return null;
      }
    }

    // Older versions use material.name == brush.m_DurableName
    {
      var descs = BrushManifest.Instance.BrushesByName[oldMaterialName].ToArray();
      if (descs.Length == 1) {
        return descs[0];
      } else if (descs.Length > 1) {
        LogWarningWithContext(string.Format(
              "Ambiguous brush name {0}; try exporting with the latest version of Tilt Brush. Brush may have the incorrect material.",
              oldMaterialName));
        return descs[0];
      } else {
        LogWarningWithContext(string.Format(
              "Unexpected: Couldn't find Tilt Brush material for name {0}",
              oldMaterialName));
        return null;
      }
    }
  }
}

}  // namespace TiltBrushToolkit
