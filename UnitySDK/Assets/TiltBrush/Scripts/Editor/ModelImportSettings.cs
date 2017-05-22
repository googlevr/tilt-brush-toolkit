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
using UnityEditor;
using UnityEngine;

namespace TiltBrushToolkit {

public class ModelImportSettings : AssetPostprocessor {
  readonly Version kToolkitVersion            = new Version { major=10 };
  readonly Version kRequiredFbxExportVersion  = new Version { major=10 };

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

  // Try to find a Tilt Brush material using the imported models's material name
  Material OnAssignMaterialModel(Material material, Renderer renderer) {
    // Ignore models that aren't Tilt Brush - generated FBXs
    if (! IsSupportedTiltBrushFbx(assetPath)) {
      return null;
    }

    // UVs come as four float2s so go through them and pack them back into two float4s
    if (renderer.GetComponent<MeshFilter>() != null) {
      var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
      CollapseUvs(mesh);
    }

    if (!string.IsNullOrEmpty (EditorUtils.TiltBrushDirectory) ) {
      // Find the material through a filtered search, check if it's inside the Tilt Brush SDK folder
      var materials = AssetDatabase.FindAssets ("t:material", new string[]{ EditorUtils.TiltBrushDirectory });
      foreach (var m in materials) {
        var path = AssetDatabase.GUIDToAssetPath (m);
        if (path.Contains(EditorUtils.TiltBrushDirectory) && Path.GetFileNameWithoutExtension(path) == material.name)
          return AssetDatabase.LoadAssetAtPath<Material> (path);
      }
      // If material isn't found, search for it in a hard coded path
      var manualPathAsset = AssetDatabase.LoadAssetAtPath<Material> (string.Format ("{0}/Assets/Brushes/Basic/{1}/{1}.mat", EditorUtils.TiltBrushDirectory, material.name));
      if (manualPathAsset != null)
        return manualPathAsset;
      Debug.LogWarningFormat(
        AssetDatabase.LoadAssetAtPath<GameObject>(assetPath),
        "Couldn't find the Tilt Brush material '{0}' for model '{1}'. Reimport the Tilt Brush Unity SDK to ensure it's unmodified and import the model again.",
        material.name, System.IO.Path.GetFileName(assetPath));
      return null;
    }
    Debug.LogWarningFormat(
      AssetDatabase.LoadAssetAtPath<GameObject>(assetPath),
      "Couldn't find the TiltBrush folder when importing '{0}'. Reimport the Tilt Brush Unity SDK to ensure it's unmodified and import the model again.",
      System.IO.Path.GetFileName(assetPath));
    return null;
  }

  /// Returns true if the path refers to an fbx that can be processed
  /// by this version of the toolkit
  bool IsSupportedTiltBrushFbx(string path) {
    var info = FbxUtils.GetTiltBrushFbxInfo(path);
    if (info.tiltBrushVersion == null) {
      return false;
    }

    if (info.tiltBrushVersion < kRequiredFbxExportVersion) {
      Debug.LogWarningFormat(
          "{0} was exported with an older version of Tilt Brush ({1}) that is not supported by this version of the Toolkit. For best results, re-export it with a newer Tilt Brush version ({2}).",
          assetPath, info.tiltBrushVersion, kRequiredFbxExportVersion);
      return false;
    }

    if (info.requiredToolkitVersion != null &&
        kToolkitVersion < info.requiredToolkitVersion) {
      Debug.LogWarningFormat(
          "{0} was exported with an newer version of Tilt Brush that is not supported by this version of the Toolkit ({1}). For best results, upgrade your Toolkit to a newer version ({2}) or downgrade your Tilt Brush.",
          assetPath, kToolkitVersion, info.requiredToolkitVersion);
      return false;
    }

    return true;
  }
}

}  // namespace TiltBrushToolkit
