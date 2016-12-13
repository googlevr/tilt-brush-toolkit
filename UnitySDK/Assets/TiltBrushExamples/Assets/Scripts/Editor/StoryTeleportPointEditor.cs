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

using UnityEditor;
using UnityEngine;
using System.Linq;

namespace TiltBrushToolkit {

[CanEditMultipleObjects()]
[CustomEditor(typeof(StoryTeleportPoint))]
public class StoryTeleportPointEditor : Editor {

  SerializedProperty m_IconTexture;
  SerializedProperty m_IconMaterial;
  SerializedProperty m_TeleportToAllPoints;
  SerializedProperty m_Points;
  SerializedProperty m_FloorOffset;
  SerializedProperty m_TeleportOnStart;
  SerializedProperty m_MoveToSight;

  void OnEnable() {
    var t = target as StoryTeleportPoint;

    m_IconTexture = serializedObject.FindProperty("m_IconTexture");
    m_IconMaterial = serializedObject.FindProperty("m_IconMaterial");
    m_TeleportToAllPoints = serializedObject.FindProperty("m_TeleportToAllPoints");
    m_Points = serializedObject.FindProperty("m_Points");
    m_FloorOffset = serializedObject.FindProperty("m_FloorOffset");
    m_TeleportOnStart = serializedObject.FindProperty("m_TeleportOnStart");
    m_MoveToSight = serializedObject.FindProperty("m_MoveToSight");

    // Check if this is the only point, set as the starting point
    var allpoints = Resources.FindObjectsOfTypeAll<StoryTeleportPoint>(); // Find all, including inactive
    if (allpoints.Count() == 1)
      t.m_TeleportOnStart = true;
  }


  public override void OnInspectorGUI() {
    var t = target as StoryTeleportPoint;
    bool previousStartingValue = t.m_TeleportOnStart; 

    var gs_header = new GUIStyle(GUI.skin.label);
    gs_header.fontStyle = FontStyle.Bold;

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Where to teleport from here:", gs_header);

    EditorGUILayout.PropertyField(m_TeleportToAllPoints);
    GUI.enabled = !t.m_TeleportToAllPoints;
    EditorGUILayout.PropertyField(m_Points, true);
    GUI.enabled = true;

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Settings:", gs_header);

    EditorGUILayout.PropertyField(m_TeleportOnStart);
    EditorGUILayout.PropertyField(m_MoveToSight);
    EditorGUILayout.PropertyField(m_FloorOffset);
    EditorGUILayout.PropertyField(m_IconTexture);
    if (m_IconTexture.objectReferenceValue != null) {
      EditorGUILayout.PropertyField(m_IconMaterial);
      if (m_IconMaterial.objectReferenceValue == null)
        EditorGUILayout.HelpBox("Choose a material to render the icon with.", MessageType.Error);
    }

    serializedObject.ApplyModifiedProperties();

    var allpoints = Resources.FindObjectsOfTypeAll<StoryTeleportPoint>(); // Find all, including inactive
    bool orphan = true;
    foreach(var p in allpoints) {
      if (p == t) continue;
      if (p.m_TeleportToAllPoints || (p.m_Points != null && p.m_Points.Contains(t)))
        orphan = false;
      if (t.m_TeleportOnStart && t.m_TeleportOnStart != previousStartingValue && p.m_TeleportOnStart) {
        p.m_TeleportOnStart = false;
        Debug.Log("Replacing " + p.name + " with " + t.name + " as the starting teleport point");
      }
    }
    if (!t.m_TeleportToAllPoints && (t.m_Points == null || t.m_Points.Length == 0))
      EditorGUILayout.HelpBox("Add points to teleport from here.", MessageType.Info);
    if (orphan)
      EditorGUILayout.HelpBox("There's no way to teleport here! Make another point that leads here.", MessageType.Warning);
      
    EditorGUILayout.Space();

  }
}

}  // namespace TiltBrushToolkit
