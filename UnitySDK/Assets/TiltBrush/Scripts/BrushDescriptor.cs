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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TiltBrushToolkit {

public class BrushDescriptor : ScriptableObject {
  // static API

#if UNITY_EDITOR
  private static bool sm_Initialized;
  private static List<BrushDescriptor> sm_Descriptors = null;
  private static Dictionary<Guid, BrushDescriptor> sm_ByGuid = null;
  private static ILookup<string, BrushDescriptor> sm_ByName = null;

  private static void Init() {
    if (sm_Initialized) { return; }
    sm_Initialized = true;
    sm_Descriptors = AssetDatabase.FindAssets("t:BrushDescriptor")
        .Select(g => AssetDatabase.GUIDToAssetPath(g))
        .Select(p => AssetDatabase.LoadAssetAtPath<BrushDescriptor>(p))
        .ToList();
    sm_ByGuid = sm_Descriptors.ToDictionary(desc => (Guid)desc.m_Guid);
    sm_ByName = sm_Descriptors.ToLookup(desc => desc.m_DurableName);
  }

  public static IEnumerable<BrushDescriptor> All {
    get { Init(); return sm_Descriptors; }
  }

  public static Dictionary<Guid, BrushDescriptor> ByGuid {
    get { Init(); return sm_ByGuid; }
  }

  public static ILookup<string, BrushDescriptor> ByName {
    get { Init(); return sm_ByName; }
  }
#endif

  // instance API

  public SerializableGuid m_Guid;
  [Tooltip("A human readable name that cannot change, but is not guaranteed to be unique.")]
  public string m_DurableName;
  public Material m_Material;
  public bool m_IsParticle;
}

}
