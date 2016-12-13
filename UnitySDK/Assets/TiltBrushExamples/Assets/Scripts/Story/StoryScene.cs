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
using System.Collections;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TiltBrushToolkit {
  
#if UNITY_EDITOR
[CanEditMultipleObjects()]
[CustomEditor(typeof(StoryScene))]
public class StorySceneEditor : Editor {

  void OnEnable() {
    var t = target as StoryScene;
    // Check if this is the only point, set as the starting point
    var allscenes = Resources.FindObjectsOfTypeAll<StoryScene>(); // Find all, including inactive
    if (allscenes.Count() == 1)
      t.m_FirstScene = true;
  }

  public override void OnInspectorGUI() {
    var t = target as StoryScene;
    bool wasFirstScene = t.m_FirstScene;

    base.OnInspectorGUI ();

    var allscenes = Resources.FindObjectsOfTypeAll<StoryScene> ();
    foreach(var s in allscenes) { 
      if (s == t) continue;
      if (t.m_FirstScene && t.m_FirstScene != wasFirstScene && s.m_FirstScene) {
        s.m_FirstScene = false;
        Debug.Log("Replacing " + s.name + " with " + t.name + " as the first scene");
      }
    }
  }
}
#endif

  /// <summary>
  /// Container for objects that represent a scene. Gets activated/deactivated when teleported to.
  /// </summary>
  [AddComponentMenu("Tilt Brush/Story/Story Scene")]
  public class StoryScene : MonoBehaviour {

    [Tooltip("Show this scene first when the game starts?")]
    [SerializeField] internal bool m_FirstScene = false;

    void Awake() {
      StoryManager.m_Instance.Initialize();
    }

  }
}