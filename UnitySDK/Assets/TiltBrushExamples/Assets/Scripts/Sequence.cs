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
using Reaktion;

namespace TiltBrushToolkit {
  /// <summary>
  /// Creates an animated sequence from models or GameObjects
  /// </summary>
  [ExecuteInEditMode()]
  [AddComponentMenu("Tilt Brush/Sequence")]
  public class Sequence : MonoBehaviour {

    public enum PlaybackMode {
      EveryBeat,
      Constant
    }
    public enum SourceType {
      Unsupported,
      Folder,
      Model,
      Prefab,
      SceneGameObject
    }

    [System.Serializable]
    public struct FrameInfo {
      [SerializeField]
      public GameObject m_Source;
      [SerializeField]
      public int m_Repetitions;
      [SerializeField]
      public SourceType m_SourceType;

      public float Duration { get { return 1 + m_Repetitions; } }
    }

    [SerializeField]
    public List<FrameInfo> m_FrameSources = new List<FrameInfo>();

    public float m_ConstantFramesPerSecond = 5;
    public PlaybackMode m_PlaybackMode = PlaybackMode.Constant;
    public float m_DirectorFrameDuration = 0.5f;
    public bool m_RandomizeStart = true;

    public bool m_PreviewFirstFrame = true;

    private float m_BeatOver = 0.95f;

    List<GameObject> m_Frames;

    GameObject m_EditorPreview;
    int m_Current = 0;
    Reaktor m_Reaktor;
    float _counter = 0;

    IEnumerator Start() {
#if UNITY_EDITOR
      DestroyPreview();
#endif
      if (Application.isPlaying) {
        if (transform.childCount > 0)
          Destroy (transform.GetChild (0).gameObject);

        m_Frames = new List<GameObject> ();
        foreach (var f in m_FrameSources) {
          for (int i = 0; i < f.Duration; i++) {
            GameObject o;
            if (f.m_Source != null) {
              o = Instantiate (f.m_Source) as GameObject;
              o.transform.position = f.m_Source.transform.position;
              o.transform.eulerAngles = f.m_Source.transform.eulerAngles;
              o.transform.localScale = f.m_Source.transform.lossyScale;
              o.transform.SetParent (transform, true);
              o.name = f.m_Source.name;
              if (f.m_SourceType == SourceType.SceneGameObject) {
                f.m_Source.SetActive (false);
              } else {
                o.transform.localPosition = Vector3.zero;
                o.transform.localEulerAngles = Vector3.zero;
                o.transform.localScale = Vector3.one;
              }
            } else {
              o = new GameObject ("(Empty frame)");
              o.transform.SetParent (transform);
            }
            o.SetActive (false);
            m_Frames.Add (o);
          }
        }

        if (m_PlaybackMode == PlaybackMode.Constant) {
          _counter = 1f / m_ConstantFramesPerSecond;
          if (m_RandomizeStart)
            m_Current = Random.Range (0, m_Frames.Count - 1);
        }
        m_Frames [m_Current].SetActive (true);

        yield return new WaitForSeconds (0.25f);
        m_Reaktor = FindObjectOfType<Reaktor> ();
        if (m_PlaybackMode == PlaybackMode.EveryBeat && m_Reaktor == null)
          Debug.LogError ("No audio reactivity found. Add the [TiltBrush Audio Reactivity] prefab to the scene.", this);
      }
    }

    void Update() {
      if (!Application.isPlaying) {
        EnsureEditorPreview (false);
        if (m_EditorPreview != null) {
          if (m_FrameSources[0].m_SourceType == SourceType.SceneGameObject) {
            m_EditorPreview.transform.position = m_FrameSources[0].m_Source.transform.position;
            m_EditorPreview.transform.eulerAngles = m_FrameSources[0].m_Source.transform.eulerAngles;
            m_EditorPreview.transform.localScale = m_FrameSources[0].m_Source.transform.lossyScale;
          } else {
            m_EditorPreview.transform.position = transform.position;
            m_EditorPreview.transform.eulerAngles = transform.eulerAngles;
            m_EditorPreview.transform.localScale = transform.lossyScale;
          }
        }
      } else {
        _counter -= Time.deltaTime;

        if (m_PlaybackMode == PlaybackMode.EveryBeat) {
          if (m_Reaktor && m_Reaktor.output >= m_BeatOver && _counter <= 0) {
            Next ();
            _counter = 0.1f;
          }
        } else if (m_PlaybackMode == PlaybackMode.Constant) {
          if (_counter <= 0) {
            _counter += 1f / m_ConstantFramesPerSecond;
            Next ();
          }
        }
      }
    }

    void OnEnable() {
      if (!Application.isPlaying)
        EnsureEditorPreview (true);
    }
    void OnDisable() {
      DestroyPreview ();
    }

    void Next() {
      m_Frames[m_Current].SetActive(false);
      m_Current = (m_Current + 1) % m_Frames.Count;
      m_Frames[m_Current].SetActive(true);
    }

    public void AddFrame(GameObject Frame, SourceType Type) {
      var info = new FrameInfo();
      info.m_Source = Frame;
      info.m_Repetitions = 0;
      info.m_SourceType = Type;
      m_FrameSources.Add(info);
    }


    public void EnsureEditorPreview(bool bForceRecreate = true) {
#if UNITY_EDITOR
      // Ignore if this is an asset
      if (!string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(gameObject)))
        return;

      // Destroy if no preview or no frame
      if (!m_PreviewFirstFrame || m_FrameSources.Count == 0 || m_FrameSources[0].m_Source == null || 
        !gameObject.activeInHierarchy || !gameObject.activeSelf || Application.isPlaying) {
        DestroyPreview ();
        return;
      }

      if (bForceRecreate)
        DestroyPreview ();
      
      if (m_EditorPreview == null) {
        m_EditorPreview = Instantiate(m_FrameSources[0].m_Source);
        m_EditorPreview.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy | HideFlags.HideInInspector;
        m_EditorPreview.SetActive(m_PreviewFirstFrame);
        m_EditorPreview.name = gameObject.name + " (Preview)";
        m_EditorPreview.tag = "EditorOnly";

        UnityEditor.SceneView.RepaintAll ();
      }
#endif
    }

    public void DestroyPreview() {
      if (m_EditorPreview == null)
        return;
      if (Application.isPlaying)
        Destroy (m_EditorPreview);
      else
        DestroyImmediate (m_EditorPreview);
#if UNITY_EDITOR
      UnityEditor.SceneView.RepaintAll ();
#endif
    }

  }
}