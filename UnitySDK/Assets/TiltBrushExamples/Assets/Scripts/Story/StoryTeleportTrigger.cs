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

namespace TiltBrushToolkit {
  
#if UNITY_EDITOR
using UnityEditor;

[CanEditMultipleObjects()]
[CustomEditor(typeof(StoryTeleportTrigger))]
public class StoryArrowEditor : Editor {
    SerializedProperty m_TriggerType;
    SerializedProperty m_TargetType;
    SerializedProperty m_TransitionType;
    SerializedProperty m_TransitionColor;
    SerializedProperty m_TransitionTime;
    SerializedProperty m_TargetScene;
    SerializedProperty m_TargetPoint;
    public void OnEnable() {
      m_TriggerType = serializedObject.FindProperty("m_TriggerType");
      m_TargetType = serializedObject.FindProperty("m_TargetType");
      m_TransitionType = serializedObject.FindProperty("m_TransitionType");
      m_TransitionColor = serializedObject.FindProperty("m_TransitionColor");
      m_TransitionTime = serializedObject.FindProperty("m_TransitionTime");
      m_TargetScene = serializedObject.FindProperty("m_TargetScene");
      m_TargetPoint = serializedObject.FindProperty("m_TargetPoint");
    }
    public override void OnInspectorGUI() {
      var t = target as StoryTeleportTrigger;
      string triggertext = "";
      string targettext = "";
      if (t.m_TriggerType == BaseTrigger.TriggerType.SteamVRControllerEnter) triggertext = "touches me with a controller";
      else if (t.m_TriggerType == BaseTrigger.TriggerType.SteamVRControllerInteract) triggertext = "presses the trigger over me";
      else if (t.m_TriggerType == BaseTrigger.TriggerType.SteamVRHeadEnter) triggertext = "puts their head in me";
      if (t.m_TargetType == StoryManager.TargetType.Scene) targettext = "a scene";
      else if (t.m_TargetType == StoryManager.TargetType.TeleportPoint) targettext = "a teleport point";
      EditorGUILayout.HelpBox(string.Format("When: \n   The player {0}\nDo this:\n   Teleport them into {1}", triggertext, targettext), MessageType.Info);
      
      EditorGUILayout.PropertyField(m_TriggerType);
      EditorGUILayout.PropertyField(m_TransitionType);
      EditorGUILayout.PropertyField(m_TargetType);
      EditorGUI.indentLevel++;
      if (t.m_TargetType == StoryManager.TargetType.Scene) {
        EditorGUILayout.PropertyField(m_TargetScene);
        if (t.m_TargetScene == null)
          EditorGUILayout.HelpBox("Select a scene to transition to", MessageType.Error);
      } else if (t.m_TargetType == StoryManager.TargetType.TeleportPoint) {
        EditorGUILayout.PropertyField(m_TargetPoint);
        if (t.m_TargetPoint == null)
          EditorGUILayout.HelpBox("Select a vantage point to transition to", MessageType.Error);
      }
      EditorGUI.indentLevel--;
      EditorGUILayout.PropertyField(m_TransitionTime);
      EditorGUILayout.PropertyField(m_TransitionColor);

      serializedObject.ApplyModifiedProperties();
    }
}
#endif

  /// <summary>
  /// A trigger that calls the StoryManager to fade into a different scene or position
  /// </summary>
  /// 
  [RequireComponent(typeof(Collider))]
  [AddComponentMenu("Tilt Brush/Story/Story Teleport Trigger")]
  public class StoryTeleportTrigger : BaseTrigger {
    
    [Tooltip("What are we teleporting to?")]
    public StoryManager.TargetType m_TargetType = StoryManager.TargetType.Scene;
    [Tooltip("What kind of transition?")]
    public StoryManager.TransitionType m_TransitionType = StoryManager.TransitionType.Fade;
    [Tooltip("What color will the screen turn into during the transition?")]
    public Color m_TransitionColor = Color.black;
    [Tooltip("Time it takes for the transition to happen (in seconds)")]
    public float m_TransitionTime = 1;
    [Tooltip("The scene we're teleporting to")]
    public StoryScene m_TargetScene;
    [Tooltip("The point we're teleporting to")]
    public StoryTeleportPoint m_TargetPoint;
    
    public override void OnInteract() {
      if (m_TargetType == StoryManager.TargetType.Scene)
        StoryManager.m_Instance.TransitionTo(m_TargetScene, m_TransitionType, m_TransitionTime, m_TransitionColor);
      else if (m_TargetType == StoryManager.TargetType.TeleportPoint)
        StoryManager.m_Instance.TransitionTo(m_TargetPoint, m_TransitionType, m_TransitionTime, m_TransitionColor);
    }
  }
}