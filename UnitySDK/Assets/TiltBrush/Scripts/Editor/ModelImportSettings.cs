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
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TiltBrushToolkit {

public class ModelImportSettings : AssetPostprocessor {

  const int kNodeHeaderSize = 13;
  const string kFbxTiltBrushPath = "FBXHeaderExtension/CreationTimeStamp/SceneInfo/MetaData/Properties70";

  // Try to find a Tilt Brush material using the imported models's material name
  Material OnAssignMaterialModel(Material material, Renderer renderer) {
    // Ignore models that aren't Tilt Brush - generated FBXs
    if (!IsTiltBrushFbx(assetPath))
      return null;

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

  // Simple and limited fbx reading code - we only need enough to read the 'Tilt Brush' property.
  // FBX format ref found here: https://code.blender.org/2013/08/fbx-binary-file-format-specification/
  bool IsTiltBrushFbx(string path) {
    if (!path.ToLowerInvariant().EndsWith(".fbx")) {
      return false;
    }

    if (!IsTiltBrushAsciiFbx(path)) {
      return IsTiltBrushBinaryFbx(path);
    }
    return true;
  }

  bool IsTiltBrushAsciiFbx(string path) {
    using (var file = new FileStream(path, FileMode.Open, FileAccess.Read)) {
      using (var reader = new StreamReader(file)) {
        if (!reader.ReadLine().StartsWith(";")) {
          return false;  // Ascii FBX files (certainly the ones from TB) always start with a comment.
        }

        // Path in the FBX file we expect to find the Tilt Brush property:
        Stack<string> expectedNames = new Stack<string>(kFbxTiltBrushPath.Split('/').Reverse());

        while (!reader.EndOfStream) {
          string line = reader.ReadLine().Trim();
          if (line.StartsWith(";") || string.IsNullOrEmpty(line)) {
            continue;
          }
          if (line.EndsWith("{")) {
            string sectionName = line.Split(':')[0];
            if (expectedNames.Count > 0 && sectionName == expectedNames.Peek()) {
              expectedNames.Pop();
            }
          } else if (line.StartsWith("}") && expectedNames.Count == 0) {
            return false;
          } else if (expectedNames.Count == 0) {
            var parts = line.Split(',').Select(x => x.Trim(' ', '"'));
            if (parts.Contains("Tilt Brush")) {
              return true;
            }
          }
        }
      }
    }
    return false;
  }

  bool IsTiltBrushBinaryFbx(string path) {
    using (var file = new FileStream(path, FileMode.Open, FileAccess.Read)) {
      using (var reader = new BinaryReader(file)) {
        // Check header
        string firstTwenty = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(20));
        if ((firstTwenty != "Kaydara FBX Binary  ")
            || (reader.ReadByte() != 0x00)
            || (reader.ReadByte() != 0x1a)
            || (reader.ReadByte() != 0x00)) {
          return false;
        }
        reader.ReadUInt32(); // Version - unneeded

        Stack<uint> endOffsets = new Stack<uint>();
        // Path in the FBX file we expect to find the Tilt Brush property:
        Stack<string> expectedNames = new Stack<string>(kFbxTiltBrushPath.Split('/').Reverse());

        while (file.Position < (file.Length - kNodeHeaderSize)) {
          // Read Node header
          endOffsets.Push(reader.ReadUInt32());
          uint numProperties = reader.ReadUInt32();
          uint propertyListLen = reader.ReadUInt32();
          int nameLen = reader.ReadByte(); // read the name length
          string name = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(nameLen));

          // Blank headers are used at the ends of nodes that have had sub-nodes; skip.
          if (endOffsets.Peek() == 0) {
            endOffsets.Pop();
            continue;
          }

          long propertyStart = file.Position;
          if (expectedNames.Count == 0) {
            // We're looking for a node that contains a string property containing 'Tilt Brush'.
            for (int i = 0; i < numProperties; ++i) {
              char type = reader.ReadChar();
              if (type == 'S') {
                uint length = reader.ReadUInt32();
                byte[] bytes = reader.ReadBytes((int)length);
                string value = System.Text.Encoding.ASCII.GetString(bytes);
                if (value == "Tilt Brush") {
                  return true;
                }
              } else {
                break; // the node we want only has string properties.
              }
            }
          } else if (name == expectedNames.Peek()) {
              expectedNames.Pop();
            }

          file.Seek(propertyListLen + propertyStart, SeekOrigin.Begin);

          long pos = file.Position;
          uint endPos = endOffsets.Peek();
          if (pos == endPos) {
            endOffsets.Pop();
          }
        }
      }
    }
    return false;
  }

}

}  // namespace TiltBrushToolkit