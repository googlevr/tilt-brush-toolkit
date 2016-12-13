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

[RequireComponent(typeof(Collider))]
[AddComponentMenu("")]
public class BaseTrigger : MonoBehaviour {
  public enum TriggerType {
    SteamVRControllerEnter,
    SteamVRControllerInteract,
    SteamVRHeadEnter
  }
  [Tooltip("What triggers this?")]
  public TriggerType m_TriggerType = TriggerType.SteamVRControllerEnter;

  private Collider m_Collider;

  public bool m_Hovered { private set; get; }

  void Awake() {
    m_Collider = GetComponent<Collider>();
    if (!m_Collider.isTrigger) {
        Debug.LogWarning("Setting collider to Trigger", m_Collider);
        m_Collider.isTrigger = true;
    }
    m_Hovered = false;
  }

#if TILTBRUSH_STEAMVRPRESENT
  internal virtual void Update() {
    if (VRInput.Instance.IsSteamVRPresent) {

      if (m_TriggerType == TriggerType.SteamVRHeadEnter && VRInput.Instance.Head != null && IsPointInside(VRInput.Instance.HeadPosition)) {
        OnInteract();
      }
      else {
        bool leftinside = VRInput.Instance.LeftHand != null && IsPointInside(VRInput.Instance.LeftHandPosition);
        bool rightinside = VRInput.Instance.RightHand != null && IsPointInside(VRInput.Instance.RightHandPosition);
        m_Hovered = leftinside || rightinside;
        if (m_TriggerType == TriggerType.SteamVRControllerEnter && m_Hovered) {
          OnInteract();
        }
        else if (m_TriggerType == TriggerType.SteamVRControllerInteract) {
          if ((leftinside && VRInput.Instance.LeftTriggerPressDown) ||
            (rightinside && VRInput.Instance.RightTriggerPressDown))
            OnInteract();
        }
      }
    }
  }
  
  void OnDrawGizmosSelected() {
      if (!Application.isPlaying)
        return;
      if (VRInput.Instance.Head != null) Gizmos.DrawSphere(VRInput.Instance.HeadPosition, 0.5f);
      Gizmos.matrix = transform.localToWorldMatrix;
      bool headinside = VRInput.Instance.Head != null && IsPointInside(VRInput.Instance.HeadPosition);
      bool leftinside = VRInput.Instance.LeftHand != null && IsPointInside(VRInput.Instance.LeftHandPosition);
      bool rightinside = VRInput.Instance.RightHand != null && IsPointInside(VRInput.Instance.RightHandPosition);
      
      if (headinside) Gizmos.DrawCube(Vector3.up * 0.5f, new Vector3(0.5f, 0.5f, 0.5f));
      if (rightinside) Gizmos.DrawCube(Vector3.right * 0.5f, new Vector3(0.5f, 0.5f, 0.5f));
      if (leftinside) Gizmos.DrawCube(Vector3.left * 0.5f, new Vector3(0.5f, 0.5f, 0.5f));
    }
    
#endif
  bool IsPointInside(Vector3 Point) {
    var localPoint = transform.InverseTransformPoint(Point);
    if (m_Collider.GetType() == typeof(BoxCollider)) {
      return (m_Collider as BoxCollider).bounds.Contains(Point);
    }
    else if (m_Collider.GetType() == typeof(SphereCollider)) {
      return localPoint.magnitude < (m_Collider as SphereCollider).radius;
    }
    else {
      // any other type uses bounding box
      // TODO: support all colliders and actually check if controller is inside
      m_Collider.bounds.Contains(Point);
    }
    return false;
  }

  public virtual void OnInteract() { }


}
}