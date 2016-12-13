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
#if TILTBRUSH_STEAMVRPRESENT
using Valve.VR;
#endif

namespace TiltBrushToolkit {
  public class VRInput : MonoBehaviour {

    static VRInput m_Instance;
    public static VRInput Instance {
      get {
        if (m_Instance == null) {
          var go = new GameObject("VR Input");
          m_Instance = go.AddComponent<VRInput>();
        }
        return m_Instance;
      }
    }

#if TILTBRUSH_STEAMVRPRESENT
    internal SteamVR_ControllerManager VR_ControllerManager;
    internal SteamVR_PlayArea VR_PlayArea;

    public int LeftControllerIndex { get { return SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost, ETrackedDeviceClass.Controller); } }
    public int RightControllerIndex { get { return SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Rightmost, ETrackedDeviceClass.Controller); } }
    public int HeadIndex { get { return 0; } }

    public SteamVR_Controller.Device Head {       get { int index = HeadIndex;              return index >= 0 ? SteamVR_Controller.Input(index) : null; } }
    public SteamVR_Controller.Device LeftHand {   get { int index = LeftControllerIndex;    return index >= 0 ? SteamVR_Controller.Input(index) : null; } }
    public SteamVR_Controller.Device RightHand {  get { int index = RightControllerIndex;   return index >= 0 ? SteamVR_Controller.Input(index) : null; } }

    public bool RightTriggerPressDown { get { return RightHand != null && RightHand.GetPressDown(EVRButtonId.k_EButton_SteamVR_Trigger); } }
    public bool LeftTriggerPressDown { get { return LeftHand != null && LeftHand.GetPressDown(EVRButtonId.k_EButton_SteamVR_Trigger); } }

    public Vector3 HeadPosition { get { return VR_PlayArea.transform.position + Head.transform.pos; } }
    public Vector3 LeftHandPosition { get { return VR_PlayArea.transform.position + LeftHand.transform.pos; } }
    public Vector3 RightHandPosition { get { return VR_PlayArea.transform.position + RightHand.transform.pos; } }
        
    public bool IsSteamVRPresent { get { return VR_ControllerManager != null; } }
        
    public bool IsTriggerPressedDown(int Index) { return SteamVR_Controller.Input(Index).GetPressDown(EVRButtonId.k_EButton_SteamVR_Trigger); }
        
    void OnEnable() {
        m_Instance = this;
        VR_ControllerManager = FindObjectOfType<SteamVR_ControllerManager>();
        VR_PlayArea = FindObjectOfType<SteamVR_PlayArea>();
            
        if (VR_ControllerManager == null)
          Debug.LogWarning("Could not find Steam VR. Add [CameraRig] from the Steam VR toolkit for VR functionality");
    }
#else
    public bool IsSteamVRPresent { get { return false; } }

    void OnEnable() {
      m_Instance = this;
    }
#endif
  }
}