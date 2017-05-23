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
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Reflection;

namespace TiltBrushToolkit {

[InitializeOnLoad]
public class GammaSettings : EditorWindow {

  static ColorSpace m_LastColorSpace;

  static GammaSettings() {
    EditorApplication.update += OnUpdate;

    SetKeywords();
    m_LastColorSpace = PlayerSettings.colorSpace;
  }

  static void OnUpdate() {
    if (m_LastColorSpace != PlayerSettings.colorSpace) {
      SetKeywords();
      m_LastColorSpace = PlayerSettings.colorSpace;
    }
    
  }

  static void SetKeywords() {
    bool linear = PlayerSettings.colorSpace == ColorSpace.Linear;
    if (linear) {
      Shader.EnableKeyword("TBT_LINEAR_TARGET");
    } else {
      Shader.DisableKeyword("TBT_LINEAR_TARGET");
    }
  }

}
}