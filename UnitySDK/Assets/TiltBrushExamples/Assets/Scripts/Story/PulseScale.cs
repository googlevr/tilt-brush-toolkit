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
public class PulseScale : MonoBehaviour {

  public float m_Amount = 0.15f;
  public float m_Speed = 2f;

  Vector3 m_OriginalScale;

  void Start () {
    m_OriginalScale = transform.localScale;
  }


  void Update () {
    transform.localScale = m_OriginalScale * (1 + Mathf.Sin(Time.time * m_Speed) * m_Amount);
  }
}
}