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

namespace TiltBrushToolkit {

public class BrushDescriptor : ScriptableObject {
  [Serializable]
  public enum Semantic {
    Unspecified,
    Position,
    Vector,
    XyIsUvZIsDistance,
    UnitlessVector,
    XyIsUv,
  }

  public Material Material { get { return m_Material; } }
  public SerializableGuid m_Guid;
  [Tooltip("A human readable name that cannot change, but is not guaranteed to be unique.")]
  public string m_DurableName;
  public Material m_Material;
  public bool m_IsParticle;

  public int m_uv0Size;
  public Semantic m_uv0Semantic;
  public int m_uv1Size;
  public Semantic m_uv1Semantic;
  public bool m_bUseNormals;
  public Semantic m_normalSemantic;
  public bool m_bFbxExportNormalAsTexcoord1;
}

}
