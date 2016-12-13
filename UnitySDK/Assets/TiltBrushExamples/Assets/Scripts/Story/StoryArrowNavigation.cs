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

namespace TiltBrushToolkit {
  /// <summary>
  /// Adds arrows around the player to navigate to the teleport points in the scene
  /// </summary>
  /// 
  [AddComponentMenu("Tilt Brush/Story/Story Arrow Navigation")]
  public class StoryArrowNavigation : MonoBehaviour {
     
    public GameObject m_TeleportArrowPrefab;
    public float m_DistanceFromPlayer = 0.52f;
    public float m_Height = -0.4f;

    private StoryTeleportPoint[]    m_TeleportPoints;
    private StoryTeleportTrigger[]  m_Arrows;
    private Transform               m_MainCamera;
    private Transform               m_ArrowsParent;
    private StoryTeleportPoint      m_CurrentPoint;

    IEnumerator Start() {
      while (StoryManager.m_Instance == null)
        yield return 0;

      if (Camera.main != null)
        m_MainCamera = Camera.main.transform;

      if (m_TeleportArrowPrefab == null)
        Debug.LogError ("Add an arrow prefab", this);

      m_TeleportPoints = Resources.FindObjectsOfTypeAll<StoryTeleportPoint>();

      if (m_TeleportPoints.Length > 0) {
        m_ArrowsParent = new GameObject("Teleport Arrows").transform;
        if (m_MainCamera != null)
          m_ArrowsParent.SetParent(m_MainCamera.parent);
        m_ArrowsParent.localPosition = new Vector3(0, 0.8f, 0);

        m_Arrows = new StoryTeleportTrigger[m_TeleportPoints.Length];
        for (int i = 0; i < m_TeleportPoints.Length; i++) {
          var p = m_TeleportPoints [i];
          var arrow = Instantiate(m_TeleportArrowPrefab);
          arrow.name = "Arrow to " + p.name;
          arrow.transform.SetParent(m_ArrowsParent);
          var trigger = arrow.GetComponent<StoryTeleportTrigger>();
          if (trigger == null) {
            if (arrow.GetComponent<Collider> () == null) 
              Debug.LogWarning ("The arrow prefab needs a collider. Adding a default one.", this);
            trigger = arrow.AddComponent<StoryTeleportTrigger> ();
            trigger.m_TargetType = StoryManager.TargetType.TeleportPoint;
          }
          m_Arrows[i] = trigger;
          trigger.m_TargetPoint = p;
        }
      }
    }
  	
    void OnEnable() {
      StoryTeleportPoint.OnTeleported += OnTeleported;
    }
    void OnDisable() {
      StoryTeleportPoint.OnTeleported -= OnTeleported;
    }

    void OnDestroy() {
      Destroy (m_ArrowsParent);
    }

    void Update() {
      if (m_TeleportPoints.Length > 0) {
        UpdateHUD();
      }
    }

    public void OnTeleported(StoryTeleportPoint Point) {
      m_CurrentPoint = Point;
      UpdateHUD();
    }

    public void UpdateHUD() {

      if (m_MainCamera == null)
        return;
      
      m_ArrowsParent.localPosition = Vector3.Lerp(m_ArrowsParent.localPosition, m_MainCamera.localPosition + Vector3.up * m_Height, Time.deltaTime * 5f);

      for (int i = 0; i < m_TeleportPoints.Length; i++) {
        if (m_CurrentPoint.ActivePoints.Contains(m_TeleportPoints[i])) {
          var arrow = m_Arrows [i];
          arrow.gameObject.SetActive(true);

          var fwd = (m_TeleportPoints[i].transform.position - m_MainCamera.position + Vector3.up * 0.9f).normalized * m_DistanceFromPlayer;
          var rot = Quaternion.LookRotation(fwd, Vector3.up).eulerAngles;
          arrow.transform.localPosition = new Vector3(fwd.x, 0f, fwd.z);
          arrow.transform.localEulerAngles = new Vector3(rot.x * 0.0f, rot.y, rot.z);
          arrow.transform.localScale = Vector3.Lerp(arrow.transform.localScale, Vector3.one * (0.7f + (arrow.m_Hovered ? 0.3f : 0f)), 8.0f * Time.deltaTime);
        } else {
          m_Arrows[i].gameObject.SetActive(false);
        }
      }
    }
  }
}