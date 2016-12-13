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
using UnityEditor;
#if TILTBRUSH_CINEMADIRECTORPRESENT
using CinemaDirector;
#endif

namespace TiltBrushToolkit {
  public class SequenceUtils {

    #region Menues

    [MenuItem("Assets/Create Looping Sequence")]
    private static void MenuCreateSequence() { CreateSequence (GetFramesFromSelection ()); }
    [MenuItem("Assets/Create Looping Sequence", true)]
    private static bool MenuCreateSequenceValidate() { return IsSelectingModels; }

#if TILTBRUSH_CINEMADIRECTORPRESENT
    [MenuItem("Assets/Create Cutscene")]
    private static void MenuCreateCutscene() { CreateCutscene (GetFramesFromSelection (), 0.5f); }
    [MenuItem("Assets/Create Cutscene", true)]
    private static bool MenuCreateCutsceneValidate() { return IsSelectingModels; }
#endif

    #endregion

    public static Sequence.SourceType GetAssetType(Object Asset) {
      if (Asset is DefaultAsset)
        return Sequence.SourceType.Folder;
      if (Asset is GameObject) {
        var path = AssetDatabase.GetAssetPath(Asset);
        if (string.IsNullOrEmpty(path))
          return Sequence.SourceType.SceneGameObject;
        foreach (var r in AssetDatabase.LoadAllAssetRepresentationsAtPath(path)) {
          if (r is Mesh)
            return Sequence.SourceType.Model;
        }
        return Sequence.SourceType.Prefab;
      }
      return Sequence.SourceType.Unsupported;
    }

    static bool IsSelectingModels {
      get {
        // Check that we're selecting a folder or an imported model, otherwise the context option will be greyed out
        bool supported = false;
        foreach (var o in Selection.objects) {
          var t = GetAssetType (o);
          supported = t == Sequence.SourceType.Folder || t == Sequence.SourceType.Model;
        }
        return supported;
      }
    }

    static List<GameObject> GetFramesFromSelection() {
      // Go through the selected objects, finding models inside the selected folders, or just adding the selected models
      var frames = new List<GameObject>();
      foreach (var f in Selection.objects) {
        var t = GetAssetType(f);
        if (t == Sequence.SourceType.Folder)
          frames.AddRange(EditorUtils.GetFramesFromFolder(f));
        else if (t == Sequence.SourceType.Model)
          frames.Add(f as GameObject);
      }
      return frames;
    }

#if TILTBRUSH_CINEMADIRECTORPRESENT
    public static void CreateCutscene(List<Sequence.FrameInfo> Frames, float DurationPerFrame) {
      string cutsceneName = DirectorHelper.getCutsceneItemName("New Cutscene", typeof(Cutscene));

      GameObject cutsceneGO = new GameObject(cutsceneName);
      Cutscene cutscene = cutsceneGO.AddComponent<Cutscene>();

      GameObject framesGO = new GameObject("Frames");
      framesGO.transform.SetParent(cutsceneGO.transform);

      GameObject triggersGO = new GameObject("Triggers");
      triggersGO.transform.SetParent(cutsceneGO.transform);

      List<Transform> frameTransforms = new List<Transform>();
      float time = 0;
      for(int i = 0; i < Frames.Count; i++) {
        if (Frames[i].m_Source == null) {
          time += DurationPerFrame * Frames[i].Duration;
          continue;
        }
        var frameGO = GameObject.Instantiate(Frames[i].m_Source) as GameObject;
        frameGO.name = Frames[i].m_Source.name;
        frameGO.transform.SetParent(framesGO.transform);
        frameGO.transform.localPosition = Vector3.zero;
        frameGO.transform.localEulerAngles = Vector3.zero;
        frameGO.transform.localScale = Vector3.one;
        frameGO.SetActive(false);
        frameTransforms.Add(frameGO.transform);
        var group = CutsceneItemFactory.CreateActorTrackGroup(cutscene, frameGO.transform) as ActorTrackGroup;
        group.transform.SetParent(triggersGO.transform);
        var track = CutsceneItemFactory.CreateActorItemTrack(group);
        var action = CutsceneItemFactory.CreateActorAction(track, typeof(EnableGameObjectAction),
          "Temporary Enable", time) as EnableGameObjectAction;
        action.Firetime = time;
        action.Duration = DurationPerFrame * Frames[i].Duration;
        time += action.Duration;

        CutsceneItemFactory.CreateActorEvent(track, typeof(DisableGameObject), "Disable", 0);
      }

      cutscene.Duration = time;
      cutscene.IsLooping = true;
      cutscene.IsSkippable = false;

      // Cutscene trigger
      GameObject cutsceneTriggerGO = new GameObject("Cutscene Trigger");
      cutsceneTriggerGO.transform.SetParent(cutsceneGO.transform);
      CutsceneTrigger cutsceneTrigger = cutsceneTriggerGO.AddComponent<CutsceneTrigger>();
      cutsceneTrigger.StartMethod = StartMethod.OnStart;
      cutsceneTrigger.Cutscene = cutscene;

      int undoIndex = Undo.GetCurrentGroup();

      Undo.RegisterCreatedObjectUndo(cutsceneGO, "Created New Cutscene");
      Undo.CollapseUndoOperations(undoIndex);

      Selection.activeTransform = cutsceneGO.transform;

      // Open director
      DirectorWindow window = EditorWindow.GetWindow(typeof(DirectorWindow)) as DirectorWindow;
      window.FocusCutscene(cutscene);
    }

    public static void CreateCutscene(List<GameObject> Frames, float DurationPerFrame) {
      var list = new List<Sequence.FrameInfo>();
      foreach (var f in Frames) {
        var fi = new Sequence.FrameInfo();
        fi.m_Source = f;
        fi.m_Repetitions = 0;
        list.Add(fi);
      }
      CreateCutscene(list, DurationPerFrame);
    }
#endif

    public static void CreateSequence(List<GameObject> Frames) {
      var list = new List<Sequence.FrameInfo>();
      foreach (var f in Frames) {
        var fi = new Sequence.FrameInfo();
        fi.m_Source = f;
        fi.m_Repetitions = 0;
        list.Add(fi);
      }

      var go = new GameObject("New Sequence");
      var sequence = go.AddComponent<Sequence>();
      sequence.m_FrameSources = list;

      Selection.activeObject = go;
    }

  }

}