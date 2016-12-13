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

namespace TiltBrushToolkit {

// Simple audio input example for audio reactive brushes
public class VisualizerAudioInput : MonoBehaviour {

  private float[] m_WaveformFloats;
  private bool m_Active = false;

  public bool IsAudioPresent() {
    return m_Active;
  }

  void Start() {
    m_WaveformFloats = new float[VisualizerManager.FFT_SIZE];
  }

  public void Activate(bool bActive) {
    m_Active = bActive;
  }

  void Update() {
    if (m_Active) {
      // Get audio data from Unity (Hint: replace this with your own!)
      AudioListener.GetOutputData(m_WaveformFloats, 0);

      // Send audio data to be processed into the shaders
      VisualizerManager.m_Instance.ProcessAudio(m_WaveformFloats, AudioSettings.outputSampleRate);
    }
  }
}

}  // namespace TiltBrushToolkit