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

using System;

using UnityEngine;

namespace TiltBrushToolkit {

public class TbtSettings : ScriptableObject {
  [Serializable]
  public struct PbrMaterialInfo {
    public Material material;
  }
  const string kAssetName = "TiltBrushToolkitSettings";

  private static TbtSettings sm_Instance;

  public static TbtSettings Instance {
    get {
      if (sm_Instance == null) {
        sm_Instance = Resources.Load<TbtSettings>(kAssetName);
        if (sm_Instance == null) {
          throw new InvalidOperationException("Cannot find " + kAssetName + ".asset");
        }
      }
      return sm_Instance;
    }
  }

  public static Version TbtVersion {
    get { return new Version { major = 23, minor = 0 }; }
  }

  public static BrushManifest BrushManifest {
    get { return Instance.m_BrushManifest; }
  }

  [SerializeField] private BrushManifest m_BrushManifest = null;

  public PbrMaterialInfo m_PbrOpaqueSingleSided;
  public PbrMaterialInfo m_PbrOpaqueDoubleSided;
  public PbrMaterialInfo m_PbrBlendSingleSided;
  public PbrMaterialInfo m_PbrBlendDoubleSided;

  /// <returns>null if not found</returns>
  public bool TryGetBrush(Guid guid, out BrushDescriptor desc) {
    return m_BrushManifest.BrushesByGuid.TryGetValue(guid, out desc);
  }
}

}
