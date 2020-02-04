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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEngine;

namespace TiltBrushToolkit {

public class ModelImportSettings : AssetPostprocessor {
  private readonly Version kToolkitVersion = TbtSettings.TbtVersion;
  readonly Version kRequiredFbxExportVersion  = new Version { major=10 };

  public static bool sm_forceOldMeshNamingConvention = false;

  private FbxUtils.FbxInfo m_Info;

  private bool IsTiltBrush { get { return m_Info.tiltBrushVersion != null; } }

  // Similar to our ListExtensions.SetCount() but only handles resizing upwards.
  // New values are default-initialized.
  static void ListSetCount<T>(List<T> lst, int newCount) where T : struct {
    int delta = newCount - lst.Count;
    Debug.Assert(delta >= 0);
    if (delta > 0) {
      lst.Capacity = newCount;
      for (int i = 0; i < delta; ++i) {
        lst.Add(default(T));
      }
    }
  }

  // UVs come as four float2s so go through them and pack them back into two float4s
  static void CollapseUvs(Mesh mesh) {
    var finalUVs = new List<List<Vector4>>();
    for (int iUnityChannel = 0; iUnityChannel < 2; iUnityChannel++) {
      var sourceUVs = new List<Vector2>();
      var targetUVs = new List<Vector4>();
      int iFbxChannel = 2 * iUnityChannel;
      mesh.GetUVs(iFbxChannel, targetUVs);  // aka "uv0" in comments below
      mesh.GetUVs(iFbxChannel + 1, sourceUVs);  // aka "uv1" in comments below
      // In Unity, texcoord array lengths are always either 0 or vertexCount.
      // We aren't guaranteed that the fbx's texcoord arrays come in matched pairs, so we
      // have to deal with 4 cases: only uv0 empty, only uv1 empty, neither empty, both empty.
      // Recent Tilt Brush won't generate .fbx with "gaps" in the UV channels, so the
      // "only uv0 empty" case is only seen with old/legacy fbx exports.
      if (targetUVs.Count <= sourceUVs.Count) {
        // cases: both empty, neither empty, only uv0 empty
        int count = sourceUVs.Count;

        // Handles the uv0-empty case; only needed for legacy reasons.
        ListSetCount(targetUVs, count);

        // Pack source.xy into target.zw
        for (int i = 0; i < count; i++) {
          var v4 = targetUVs[i];
          v4.z = sourceUVs[i].x;
          v4.w = sourceUVs[i].y;
          targetUVs[i] = v4;
        }
      } else {
        // case: only uv1 empty.
        // Nothing to do.
        Debug.Assert(sourceUVs.Count == 0);
        targetUVs = null;  // Use null to indicate "unchanged"
      }
      finalUVs.Add(targetUVs);
    }

    for (int i = 0; i < finalUVs.Count; i++) {
      if (finalUVs[i] != null) {
        mesh.SetUVs(i, finalUVs[i]);
      }
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
#if UNITY_2019_1_OR_NEWER
      if (importer != null && importer.optimizeMeshVertices) {
        // Not a warning, just a friendly notification.
        Debug.LogFormat("{0}: Tilt Brush Toolkit requires importer.optimizeMeshVertices = false; disabling.", assetPath);
        importer.optimizeMeshVertices = false;
      }
#else
      if (importer != null && importer.optimizeMesh) {
        // Not a warning, just a friendly notification.
        Debug.LogFormat("{0}: Tilt Brush Toolkit requires optimizeMesh=false; disabling.", assetPath);
        importer.optimizeMesh = false;
      }
#endif
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

  // Returns a Material which is also an asset.
  // The resulting asset will be somehere "nearby" relatedAssetPath.
  Material GetOrCreateAsset(Material material, string relatedAssetPath) {
    // You can't add assets to a .fbx unless you're Unity.
    // So instead, put it in a loose .mat near the fbx.
    // This also allows the user to customize the material if they want.
    string objAssetPath = Path.Combine(
        Path.GetDirectoryName(relatedAssetPath), $"{material.name}.mat");

    // If it already exists on disk, it came either from a previous import, or (more likely)
    // from a previous OnAssignMaterialModel for a material of the same name.
    // We can't tell the difference, but our desired behavior in both cases is the same.
    Material existing = AssetDatabase.LoadAssetAtPath<Material>(objAssetPath);
    if (existing != null) {
      return existing;
    } else {
      AssetDatabase.CreateAsset(material, objAssetPath);
      return material;
    }
  }

  // Sets the first texture-valued property on mtl, based on the properties required by its shader.
  void SetFirstShaderTexture(Material material, Texture texture) {
    var shader = material.shader;
    for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); ++i) {
      if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv) {
        material.SetTexture(ShaderUtil.GetPropertyName(shader, i), texture);
        return;
      }
    }
  }

  // Returns true if this is a BrushDescriptor meant to be cloned and customized rather
  // than used directly, to support things like gltf PbrMetallicRoughness or
  // double-sided/non-culling materials in fbx
  bool IsTemplateDescriptor(BrushDescriptor desc) {
    return desc.name.StartsWith("Pbr");  // hacky but works for now
  }

  // Try to find a Tilt Brush material using the imported models's material name.
  Material OnAssignMaterialModel(Material material, Renderer renderer) {
    // This gets called once for each (Renderer, Material) pair.
    // However, Unity passes a fresh Material every time, even if two FbxNodes use the
    // same FbxMaterial. Therefore we can't distinguish between "two unique materials with
    // the same name" and "one material being used multiple times".
    // Therefore we have to rely on the exporter using distinct names.

    // Ignore models that aren't Tilt Brush - generated FBXs
    if (! IsTiltBrush) {
      return null;
    }
    // For now, don't be strict about versions, because we can do an okay job on TB7 fbx files

    BrushDescriptor desc = GetDescriptorForStroke(material.name);

    if (desc != null) {
      if (IsTemplateDescriptor(desc)) {
        // Replace shader with our own so we get culling and so on.

        // First 32 characters are the guid and underscore
        material.name = material.name.Substring(33);
        material.shader = desc.m_Material.shader;
        // Our shaders don't use "_MainTex", so we need to find the correct property name.
        SetFirstShaderTexture(material, material.mainTexture);
        // If we return null, Unity will ignore our material mutations.
        // If we return the material, Unity complains it's not an asset instead of making it one
        // and embedding it in the fbx.
        // So create one explicitly.
        return GetOrCreateAsset(material, this.assetPath);
      }

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
#if UNITY_2019_1_OR_NEWER
          if (importer != null && importer.optimizeMeshVertices) {
            // Should never get here any more.
            LogWarningWithContext("Disabling optimizeMesh and reimporting. Tilt Brush particle meshes must have importer.optimizeMeshVertices = false.");
            importer.optimizeMeshVertices = false;
            importer.SaveAndReimport();
          }
#else
          if (importer != null && importer.optimizeMesh) {
            // Should never get here any more.
            LogWarningWithContext("Disabling optimizeMesh and reimporting. Tilt Brush particle meshes must have optimizeMesh=false.");
            importer.optimizeMesh = false;
            importer.SaveAndReimport();
          }
#endif

          ParticleMesh.FilterMesh(mesh, s => LogWarningWithContext(s, renderer));
        }
      }
      return desc.m_Material;
    } else {
      return null;
    }
  }

  // Convert from fbx coordinate conventions to Unity coordinate conventions
  static void InPlaceUnityFromFbx(List<Vector3> vs) {
    var length = vs.Count;
    for (int i = 0; i < length; ++i) {
      var val = vs[i];
      val.x = -val.x;
      vs[i] = val;
    }
  }

  static void InPlaceUnityFromFbx(List<Vector4> vs) {
    var length = vs.Count;
    for (int i = 0; i < length; ++i) {
      var val = vs[i];
      val.x = -val.x;
      vs[i] = val;
    }
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
        InPlaceUnityFromFbx(data);
        mesh.SetUVs(uvSet, data);
      } else if (size == 4) {
        var data = new List<Vector4>();
        mesh.GetUVs(uvSet, data);
        InPlaceUnityFromFbx(data);
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
      Dictionary<Material, BrushDescriptor> lookup = TbtSettings.BrushManifest.AllBrushes
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
        return TbtSettings.BrushManifest.BrushesByGuid[brushGuid];
      } catch (KeyNotFoundException) {
        LogWarningWithContext(string.Format(
            "Unexpected: Couldn't find Tilt Brush material for guid {0}.",
            brushGuid));
        return null;
      }
    }

    // Older versions use material.name == brush.m_DurableName
    {
      var descs = TbtSettings.BrushManifest.BrushesByName[oldMaterialName].ToArray();
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
