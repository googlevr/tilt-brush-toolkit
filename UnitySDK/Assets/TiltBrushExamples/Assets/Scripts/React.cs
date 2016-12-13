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
using UnityEngine.Events;
using System.Collections;

namespace TiltBrushToolkit {
  [System.Serializable]
  public class ReaktionFloatEvent : UnityEvent<float> { }

  public class React : MonoBehaviour {
    [Tooltip("Input Reaktion script")]
    public Reaktion.Reaktor m_Input;
    [Tooltip("Output value when beat output is at zero")]
    public float m_OutputLow = 0.0f;
    [Tooltip("Output value when beat output is at one")]
    public float m_OutputHigh = 1.0f;
    [SerializeField]
    public ReaktionFloatEvent m_Event;

    void Start() {
      if (m_Input == null)
        m_Input = FindObjectOfType<Reaktion.Reaktor> ();
    }

    void Update() {
      if (m_Input != null)
        m_Event.Invoke(Mathf.Lerp(m_OutputLow, m_OutputHigh, Mathf.Clamp01(m_Input.output)));
    }
  }
}