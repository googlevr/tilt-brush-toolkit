// Copyright 2019 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.


using Object = UnityEngine.Object;
#if UNITY_2017_1_OR_NEWER
using System;
using System.IO;

using UnityEngine;
using UnityEditor;

#if UNITY_2020_1_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace TiltBrushToolkit {

[ScriptedImporter(kVersion, "glb1", kImportQueueOffset)]
public class Glb1Importer : ScriptedImporter {
  const int kVersion = 1;
  // ImportGltf needs to reference meshes and textures, so the glb import
  // must come after them. We're assuming that Unity built-in asset types
  // import at queue offset = 0.
  const int kImportQueueOffset = 1;

  private static readonly GltfImportOptions kOptions = new GltfImportOptions {
      rescalingMode = GltfImportOptions.RescalingMode.CONVERT,
      scaleFactor = 1,
      recenter = false,
  };

  public override void OnImportAsset(AssetImportContext ctx) {
    IUriLoader loader = new BufferedStreamLoader(
        ctx.assetPath, Path.GetDirectoryName(ctx.assetPath));

    ImportGltf.GltfImportResult result = ImportGltf.Import(
        ctx.assetPath, loader, null, kOptions);

    // The "identifier" param passed here is supposed to be:
    // - Unique to this asset
    // - Deterministic (if possible)
    foreach (var obj in result.textures) {
      if (! AssetDatabase.Contains(obj)) {
        ctx.AddObjectToAsset("Texture/" + obj.name, obj);
      }
    }
    foreach (var obj in result.materials) {
      ctx.AddObjectToAsset("Material/" + obj.name, obj);
    }
    foreach (var obj in result.meshes) {
      ctx.AddObjectToAsset("Mesh/" + obj.name, obj);
    }
    string objectName = Path.GetFileNameWithoutExtension(ctx.assetPath);
    result.root.name = objectName;
    ctx.AddObjectToAsset("ROOT", result.root);
    ctx.SetMainObject(result.root);
  }
}

[CustomEditor(typeof(Glb1Importer))]
public class Glb1ImporterEditor : ScriptedImporterEditor {
  // SerializedProperty m_UniformScaleProp;

  public override void OnEnable() {
    base.OnEnable();
    // m_UniformScaleProp = serializedObject.FindProperty("m_UniformScale");
  }

  public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height) {
    return base.RenderStaticPreview(assetPath, subAssets, width, height);
  }

  protected override bool useAssetDrawPreview {
    get { return base.useAssetDrawPreview; }
  }

  public override void OnInspectorGUI() {
    base.OnInspectorGUI();
    // serializedObject.Update();
    // EditorGUILayout.PropertyField(m_UniformScaleProp);
    // EditorGUILayout.PropertyField(m_UniformScaleProp);
    // serializedObject.ApplyModifiedProperties();
    // ApplyRevertGUI();
  }
}

}  // namespace TiltBrushToolkit

#endif
