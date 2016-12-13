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
using System.Collections.Generic;
using System.Linq;

namespace TiltBrushToolkit {

/// <summary>
/// A point in space to teleport to.
/// </summary>
[AddComponentMenu("Tilt Brush/Story/Story Teleport Point")]
public class StoryTeleportPoint : MonoBehaviour {

  public static Color TELEPORT_MAINCOLOR = Color.cyan;

  [Tooltip("Make all teleport points available from here")]
  [SerializeField] public bool m_TeleportToAllPoints = false;

  [Tooltip("Specify which points can be teleported to from here")]
  [SerializeField] public StoryTeleportPoint[] m_Points = {};
    
  [Tooltip("How far down is the floor")]
  [SerializeField] private float m_FloorOffset = 1.1f;

  [Tooltip("Teleport into this point when the scene starts?")]
  [SerializeField] public bool m_TeleportOnStart = false;

  [Tooltip("Place the player's eyes at this point?")]
  [SerializeField] private bool m_MoveToSight = false;

  [Tooltip("Icon to visualize the point (can be empty)")]
  public Texture2D m_IconTexture;
  public Material  m_IconMaterial;

  private StoryScene m_ParentScene;

  public bool PointedAt { get { return m_PointedAt.Count > 0; } }

  private List<uint> m_PointedAt = new List<uint>(); // which pointers are pointing at this
  private Vector3 m_LocalScale;
  private GameObject m_IconObject;
  private StoryTeleportPoint[] m_RuntimePoints;

  static Vector3[] m_ArrowPoints = new Vector3[] {
    new Vector3 (-0.06f, 0, -0.06f),
    new Vector3 (0.06f, 0, -0.06f),
    new Vector3 (0.06f, 0, 0.06f),
    new Vector3 (0.12f, 0, 0.06f),
    new Vector3 (0f, 0, 0.2f),
    new Vector3 (-0.12f, 0, 0.06f),
    new Vector3 (-0.06f, 0, 0.06f),
  };

  public StoryTeleportPoint[] ActivePoints { get { return m_RuntimePoints; } }

  public static System.Action<StoryTeleportPoint> OnTeleported;

  public void TeleportHere() {
    // Move the scene into this position
    Vector3 targetPosition = transform.position;
#if TILTBRUSH_STEAMVRPRESENT
    if (m_MoveToSight)
      targetPosition -= VRInput.Instance.HeadPosition - VRInput.Instance.VR_PlayArea.transform.position;
    else
      targetPosition += Vector3.down * m_FloorOffset;
#endif
    if (VRInput.Instance.IsSteamVRPresent) {
#if TILTBRUSH_STEAMVRPRESENT
      VRInput.Instance.VR_PlayArea.transform.position = targetPosition;
#endif
    } else {
      if (m_ParentScene)
        m_ParentScene.transform.position = -targetPosition;
    }
    if (OnTeleported != null)
      OnTeleported(this);

    // Hide all other points, including myself
    foreach(var p in Resources.FindObjectsOfTypeAll<StoryTeleportPoint>())
      p.gameObject.SetActive(ActivePoints.Contains(p));
  }

  void Reset() {
    if (GetComponent<Collider>() != null) GetComponent<Collider>().isTrigger = true;
    if (GetComponent<SphereCollider>() != null) GetComponent<SphereCollider>().radius = 0.2f;
  }

  void Start() {
    if (GetComponent<Collider>() != null) GetComponent<Collider>().isTrigger = true;
    m_LocalScale = transform.localScale;

#if TILTBRUSH_STEAMVRPRESENT
    // Get callbacks from laser pointers
    foreach(var pointer in Resources.FindObjectsOfTypeAll<SteamVR_LaserPointer>()) {
      pointer.PointerIn += Pointer_PointerIn;
      pointer.PointerOut += Pointer_PointerOut;
    }
#endif
    // Create an icon
    if (m_IconTexture != null) {
      if (m_IconMaterial == null) {
          Debug.LogError("No material for the telporting icon defined, using default.", this);
          m_IconMaterial = new Material(Shader.Find("Diffuse"));
      }
      m_IconObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
      Destroy(m_IconObject.GetComponent<Collider>());
      m_IconObject.name = "Icon";
      m_IconObject.transform.SetParent(transform);
      m_IconObject.transform.localPosition = Vector3.zero;
      m_IconObject.transform.localScale = Vector3.one * GetComponent<SphereCollider>().radius * 2f;
      var shader = Shader.Find("Unlit/AlwaysVisible");
      if (shader == null) shader = Shader.Find("Unlit/Color");
      var renderer = m_IconObject.GetComponent<MeshRenderer>();
      renderer.material = m_IconMaterial;
      renderer.material.color = TELEPORT_MAINCOLOR;
      renderer.material.mainTexture = m_IconTexture;
      if (Camera.main)
        m_IconObject.transform.LookAt(Camera.main.transform);
    }

    // Find the parent scene
    var parent = transform.parent;
    while (parent != null) {
      m_ParentScene = parent.GetComponent<StoryScene> ();
      if (m_ParentScene != null)
        break;
      parent = parent.parent;
    }

    // Get target points
    if (m_TeleportToAllPoints) {
      var allpoints = new List<StoryTeleportPoint>();
      foreach(var p in Resources.FindObjectsOfTypeAll<StoryTeleportPoint>()) {
        if (p != this)
          allpoints.Add(p);
      }
      m_RuntimePoints = allpoints.ToArray();
    } else {
      m_RuntimePoints = m_Points;
    }

    // Teleport on start
    if (m_TeleportOnStart)
      TeleportHere();
  }
  
#if TILTBRUSH_STEAMVRPRESENT
  void OnDestroy() {
    foreach (var pointer in Resources.FindObjectsOfTypeAll<SteamVR_LaserPointer>()) {
      pointer.PointerIn -= Pointer_PointerIn;
      pointer.PointerOut -= Pointer_PointerOut;
    }
  }

  private void Pointer_PointerIn(object sender, PointerEventArgs e) {
    if (e.target == transform)
      m_PointedAt.Add(e.controllerIndex);
  }
  private void Pointer_PointerOut(object sender, PointerEventArgs e) {
    if(e.target == transform && m_PointedAt.Contains(e.controllerIndex))
      m_PointedAt.Remove(e.controllerIndex);
  }
#endif

  void Update() {
#if TILTBRUSH_STEAMVRPRESENT
    foreach (var i in m_PointedAt) {
      if (VRInput.Instance.IsTriggerPressedDown((int)i))
        StoryManager.m_Instance.TransitionTo(this, StoryManager.TransitionType.Fade, 0.7f, Color.black);
    }
#endif
    if (Camera.main != null) {
      if (m_IconObject != null && Camera.main)
        m_IconObject.transform.LookAt(Camera.main.transform);
      
      // Scale depending on distance to the camera
      var scale = Mathf.Clamp01 ((transform.position - Camera.main.transform.position).magnitude / 4f);
      scale = Mathf.Lerp (0.3f, 1f, scale) * (PointedAt ? 1.25f : 1f);
      transform.localScale = Vector3.Lerp (transform.localScale, m_LocalScale * scale, 8.0f * Time.deltaTime);
    }
  }
    
  void OnDrawGizmos() {
    Gizmos.color = Color.cyan;
    Gizmos.DrawWireSphere(transform.position, 0.2f);
  }

  void OnDrawGizmosSelected() {
    var color = Color.cyan;
    Gizmos.color = color;
    Gizmos.DrawSphere(transform.position, 0.05f);
    foreach (var p in (m_TeleportToAllPoints ? FindObjectsOfType<StoryTeleportPoint>() : m_Points)) {
      if (p == null || p == this) continue;
      DrawArrowGizmo(transform.position, p.transform.position);
      Gizmos.DrawWireSphere(p.transform.position, 0.05f);
    }

    // draw player
    Gizmos.color = new Color(1,1,1, 0.7f);
    Gizmos.matrix = transform.localToWorldMatrix;
    float standingHeight = 1.65f;
    var offset = Vector3.down * Mathf.Max(m_FloorOffset, m_MoveToSight ? standingHeight - 0.1f : 0);
    Gizmos.DrawWireCube(offset + Vector3.up * standingHeight * .5f, new Vector3(0.2f, standingHeight, 0.2f)); // player standing
    if (!m_MoveToSight) {
      float sittingHeight = 1.2f;
      offset = Vector3.down * Mathf.Max(m_FloorOffset, m_MoveToSight ? sittingHeight - 0.1f : 0);
      Gizmos.DrawWireCube(offset + Vector3.up * sittingHeight * .5f, new Vector3(0.4f, sittingHeight, 0.4f)); // player sitting
    }
    Gizmos.DrawWireCube(offset, new Vector3(1.5f, 0.01f, 1.5f)); // floor

    Vector3 arrowoffset = Vector3.forward * 0.5f;
    for (int i = 0; i < m_ArrowPoints.Length - 1; i++)
      Gizmos.DrawLine (arrowoffset + m_ArrowPoints [i], arrowoffset + m_ArrowPoints [i+1]);
    Gizmos.DrawLine (arrowoffset + m_ArrowPoints [0], arrowoffset + m_ArrowPoints [m_ArrowPoints.Length - 1]);
    Gizmos.matrix = Matrix4x4.identity;
  }

  void DrawArrowGizmo(Vector3 pos, Vector3 target, float arrowHeadLength = 0.5f, float arrowHeadAngle = 20.0f) {

    Gizmos.DrawLine(pos, target);

    var direction = (target - pos).normalized;
    Gizmos.DrawRay(pos, direction);

    if ((target - pos).magnitude > 0) {
      Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * new Vector3(0, 0, 1);
      Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * new Vector3(0, 0, 1);
      Gizmos.DrawRay(target, right * arrowHeadLength);
      Gizmos.DrawRay(target, left * arrowHeadLength);
      Gizmos.DrawLine(target + right * arrowHeadLength, target + left * arrowHeadLength);
    }
  }
}

}  // namepsace TiltBrushToolkit