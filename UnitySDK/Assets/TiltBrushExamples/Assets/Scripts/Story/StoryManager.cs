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
using System.Collections.Generic;

namespace TiltBrushToolkit {
  [AddComponentMenu("")]
  public class StoryManager : MonoBehaviour {

    public enum TransitionType {
      Instant,
      Fade
    }

    public enum TargetType {
      Scene,
      TeleportPoint
    }

    [System.Serializable]
    public struct Transition {
      public TargetType m_TargetType;
      public StoryScene m_TargetScene;
      public StoryTeleportPoint m_TargetPoint;
      public float m_Time;
      public Color m_FadeColor;

      public Transition(StoryScene Scene, float Time, Color? FadeColor) {
        m_TargetType = TargetType.Scene;
        m_TargetScene = Scene;
        m_TargetPoint = null;
        m_Time = Time;
        m_FadeColor = FadeColor ?? Color.black;
      }
      public Transition(StoryTeleportPoint Point, float Time, Color? FadeColor) {
        m_TargetType = TargetType.TeleportPoint;
        m_TargetScene = null;
        m_TargetPoint = Point;
        m_Time = Time;
        m_FadeColor = FadeColor ?? Color.black;
      }
    }

    public System.Action OnTransition;

    private bool m_TransitionActive = false;
    private Transition m_CurrentTransition;

    private StoryScene _currentScene;
    public StoryScene currentScene {
      get {
        return _currentScene;
      }
      set {
        if (_currentScene != null)
          _currentScene.gameObject.SetActive(false);
        _currentScene = value;
        _currentScene.gameObject.SetActive(true);
      }
    }

    private static StoryManager _instance;
    public static StoryManager m_Instance {
      get {
        if (_instance == null) {
          var go = new GameObject("(Story Manager)");
          _instance = go.AddComponent<StoryManager>();
        }
        return _instance;
      }
    }

    void Start() {
      // Make sure there's only one active scene when we start playing
      foreach (var s in FindObjectsOfType<StoryScene>()) {
        if (currentScene == null) {
          currentScene = s;
          s.gameObject.SetActive(true);
          continue;
        }
        s.gameObject.SetActive(false);
      }

#if TILTBRUSH_STEAMVRPRESENT
      if (VRInput.Instance.IsSteamVRPresent) {
        var vrcam = FindObjectOfType<SteamVR_Camera>();
        if (vrcam.GetComponent<SteamVR_Fade>() == null)
          vrcam.gameObject.AddComponent<SteamVR_Fade>();
      }
#endif
    }

    void OnDestroy() {
      _instance = null;
    }

    public void Initialize() {
      var scenes = Resources.FindObjectsOfTypeAll<StoryScene>();
      bool firstSceneSettingPresent = false;
      foreach (var s in scenes) {
        s.gameObject.SetActive(s.m_FirstScene);
        if (s.m_FirstScene)
          firstSceneSettingPresent = true;
      }
      // If no scene has the "First Scene" setting enabled, activate the first one
      if (!firstSceneSettingPresent && scenes.Length > 0)
        scenes[0].gameObject.SetActive(true);
    }

    public void TransitionTo(Transition Transition, TransitionType Type = TransitionType.Instant, float FadeTime = 1, Color? FadeColor = null) {

      if (m_TransitionActive)
        return;
      m_CurrentTransition = Transition;
      if (Type == TransitionType.Instant) {
        PerformChange();
      } else if (Type == TransitionType.Fade) {
        StartCoroutine(TransitionSequence());
      }
    }
    public void TransitionTo(StoryScene Scene, TransitionType Type = TransitionType.Instant, float FadeTime = 1, Color? FadeColor = null) {
      TransitionTo(new Transition(Scene, FadeTime, FadeColor), Type, FadeTime, FadeColor);
    }
    public void TransitionTo(StoryTeleportPoint Point, TransitionType Type = TransitionType.Instant, float FadeTime = 1, Color? FadeColor = null) {
      TransitionTo(new Transition(Point, FadeTime, FadeColor), Type, FadeTime, FadeColor);
    }

    IEnumerator TransitionSequence() {
      m_TransitionActive = true;

#if TILTBRUSH_STEAMVRPRESENT
      float FadeTime = m_CurrentTransition.m_Time * 0.5f;
      Color FadeColor = m_CurrentTransition.m_FadeColor;
      FadeColor.a = 1;

      // Fade to 
      if (VRInput.Instance.IsSteamVRPresent) {
        SteamVR_Fade.View(FadeColor, FadeTime);
        yield return new WaitForSeconds(FadeTime);
      }
#endif

      PerformChange();

#if TILTBRUSH_STEAMVRPRESENT
      // Fade from 
      if (VRInput.Instance.IsSteamVRPresent) {
        SteamVR_Fade.View(new Color(FadeColor.r, FadeColor.g, FadeColor.b, 0), FadeTime);
        yield return new WaitForSeconds(FadeTime);
      }
#endif
      m_TransitionActive = false;
      yield return 0;
    }

    void PerformChange() {
      if (m_CurrentTransition.m_TargetType == TargetType.Scene) {
        currentScene = m_CurrentTransition.m_TargetScene;
      } else if (m_CurrentTransition.m_TargetType == TargetType.TeleportPoint) {
        m_CurrentTransition.m_TargetPoint.TeleportHere();
      }
      if (OnTransition != null)
        OnTransition();
    }


  }
}