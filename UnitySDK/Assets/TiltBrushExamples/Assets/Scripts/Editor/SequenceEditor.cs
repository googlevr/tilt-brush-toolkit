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
using UnityEditor;

namespace TiltBrushToolkit {

[CustomEditor(typeof(Sequence))]
public class SequenceEditor : Editor {

  Texture2D m_IconUp;
  Texture2D m_IconDown;
  Texture2D m_IconDelete;
  Texture2D m_IconAdd;

  public static bool FOLDOUT_DIRECTOR = false;

  void OnEnable() {
    
    m_IconUp = Resources.Load("UI/up") as Texture2D;
    m_IconDown = Resources.Load("UI/down") as Texture2D;
    m_IconDelete = Resources.Load("UI/delete") as Texture2D;
    m_IconAdd = Resources.Load("UI/add") as Texture2D;

    var t = (target as Sequence);

    // Update source types
    for(int i = 0; i < t.m_FrameSources.Count; i++) {
      var f = t.m_FrameSources[i];
      if (f.m_Source)
        f.m_SourceType = SequenceUtils.GetAssetType(f.m_Source);
      t.m_FrameSources[i] = f;
    }
  }
  
  public override void OnInspectorGUI() {

    Undo.RecordObject(target, "Add frames");
    serializedObject.Update();

    CustomLabel("Sequence", 16, FontStyle.Bold);
    CustomLabel("Creates an animated sequence", 12, FontStyle.BoldAndItalic);
    EditorGUILayout.Space();

    GUIStyle gs;
    var t = target as Sequence;
    EditorGUI.BeginChangeCheck();

    CustomLabel("Playback", 12, FontStyle.Italic, TextAnchor.MiddleRight);

    t.m_PlaybackMode = (Sequence.PlaybackMode)EditorGUILayout.EnumPopup("Playback Mode", t.m_PlaybackMode);
    if (t.m_PlaybackMode == Sequence.PlaybackMode.Constant) {
      EditorGUI.indentLevel++;
      t.m_ConstantFramesPerSecond = EditorGUILayout.FloatField(new GUIContent("Frames Per Second", "Speed of the animation"), t.m_ConstantFramesPerSecond);
      t.m_RandomizeStart = EditorGUILayout.Toggle(new GUIContent("Randomize start", "Choose the first frame at random? (to add variety)"), t.m_RandomizeStart);
      EditorGUI.indentLevel--;
    }

    if (t.m_PlaybackMode == Sequence.PlaybackMode.Constant) {
      EditorGUILayout.HelpBox("The sequence will show each frame for " + (Mathf.Round((1f / t.m_ConstantFramesPerSecond) * 100) / 100f) + " seconds", MessageType.Info);
    } else if (t.m_PlaybackMode == Sequence.PlaybackMode.EveryBeat) {
      EditorGUILayout.HelpBox("The sequence will play one frame per beat", MessageType.Info);
      if (FindObjectOfType<VisualizerManager>() == null)
        EditorGUILayout.HelpBox("Add the [TiltBrush Audio Reactivity] prefab to your scene.", MessageType.Error);
    }

#if TILTBRUSH_CINEMADIRECTORPRESENT
    EditorGUILayout.Space ();
    CustomLabel ("Export", 12, FontStyle.Italic, TextAnchor.MiddleRight);

    FOLDOUT_DIRECTOR = EditorGUILayout.Foldout (FOLDOUT_DIRECTOR, "Export to Cutscene Director");
    if (FOLDOUT_DIRECTOR) {
      t.m_DirectorFrameDuration = EditorGUILayout.FloatField (new GUIContent ("Time per frame", "How long should each frame last when exporting into a Cutscene?"), t.m_DirectorFrameDuration);
      if (GUILayout.Button ("Turn into Cutscene")) {
        SequenceUtils.CreateCutscene (t.m_FrameSources, t.m_DirectorFrameDuration);
      }
    }
#endif

    EditorGUILayout.Space();
    CustomLabel("Animation", 10, FontStyle.Italic, TextAnchor.MiddleRight);

    string dragBoxText = "Drag folders, models or game objects here to add frames";
    GUI.color = DRAGBOX_COLOR_NORMAL;

    var firstDraggedObject = DragAndDrop.objectReferences.Length > 0 ? DragAndDrop.objectReferences[0] : null;

    if (firstDraggedObject != null) {
      DragAndDrop.visualMode = DragAndDropVisualMode.Link;

      if (Event.current.type == EventType.DragPerform) {
        // Add all the dragged objects/folders
        var allAssets = DragAndDrop.objectReferences;
        System.Array.Sort(allAssets, new AlphanumericComparer());
        foreach (var asset in allAssets) {
          ProcessAsset(asset, true);
        }

      } else {
        // Just preview
        dragBoxText = ProcessAsset(firstDraggedObject);
        Repaint();
      }

    }
    // Box to drag things on
    var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(64));
    EditorGUILayout.Space();
    gs = new GUIStyle(GUI.skin.box);
    gs.normal.textColor = GUI.skin.label.normal.textColor;
    gs.alignment = TextAnchor.MiddleCenter;
    gs.fontSize = 14;
    gs.fontStyle = FontStyle.Bold;
    gs.onHover.textColor = Color.green;
    GUI.Box(rect, dragBoxText, gs);
    GUI.color = Color.white;
    EditorGUILayout.EndVertical();

    EditorGUILayout.Space();

    Rect r;
    int frameHeight = 70;
    r = EditorGUILayout.BeginVertical(GUILayout.Height(frameHeight * t.m_FrameSources.Count));
    GUI.Box(r, GUIContent.none, GUI.skin.box);

    if (EditorUtils.IconButton(
          new Rect(r.x + r.width - 28, r.y + 7, 20, 20),
          m_IconDelete,
          new Color(0.7f, 0.2f, 0.2f),
          "Delete all frames")
      && EditorUtility.DisplayDialogComplex("Delete all", "Delete all the frames in this sequence?", "Delete All", "Cancel", "") == 0)
      t.m_FrameSources.Clear();

    if (EditorUtils.IconButton(
        new Rect(r.x + r.width - 50, r.y + 7, 20, 20),
        m_IconAdd,
        EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.3f, 0.3f, 0.3f),
        "Add an empty frame"
      ))
      t.m_FrameSources.Add(new Sequence.FrameInfo());

    EditorGUILayout.Space();
    CustomLabel(t.m_FrameSources.Count + " Frames", 12, FontStyle.Bold, TextAnchor.MiddleCenter);
    EditorGUILayout.Space();

    if (Event.current.type != EventType.DragPerform) {
      for (int i = 0; i < t.m_FrameSources.Count; i++) {
        var frame = t.m_FrameSources[i];
        var isEmpty = frame.m_Source == null;
        r = EditorGUILayout.BeginHorizontal(GUILayout.Height(frameHeight));

        gs = new GUIStyle(GUI.skin.textArea);
        GUI.backgroundColor = i % 2 != 0 ? new Color(0.9f, 0.9f, 0.9f) : Color.white;
        GUI.Box(r, GUIContent.none, gs);

        float margin = 6;
        r.x += margin;
        r.y += margin;
        r.height -= margin * 2;

        gs = new GUIStyle(GUI.skin.box);
        gs.alignment = TextAnchor.MiddleLeft;
        gs.padding = new RectOffset(2, 2, 2, 2);

        GUI.color = isEmpty ? Color.gray : Color.white;
        GUI.Box(new Rect(r.x, r.y, r.height, r.height),
          isEmpty ? null : AssetPreview.GetAssetPreview(frame.m_Source), gs);
        GUI.color = Color.white;

        gs = new GUIStyle(GUI.skin.label);
        gs.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(r.x + r.height + 5, r.y, r.width * .5f - r.height - 5, r.height),
          isEmpty ? "(Empty)" : frame.m_Source.name, gs);

        if (!isEmpty && Event.current.type == EventType.MouseDown &&
          new Rect(r.x, r.y, r.width * .5f, r.height).Contains(Event.current.mousePosition)) {
          EditorGUIUtility.PingObject(frame.m_Source);
        }

        var repetitionsRect = new Rect(r.width - 55, r.y, 30, r.height);
        gs = new GUIStyle(GUI.skin.label);
        gs.alignment = TextAnchor.LowerCenter;
        gs.fontSize = 9;
        GUI.Label(
          new Rect(repetitionsRect.x - 7, repetitionsRect.y, repetitionsRect.width + 10, repetitionsRect.height * .5f - 4),
          new GUIContent("Repeat", "How many times is this frame repeated?"), gs
        );
        gs = new GUIStyle(GUI.skin.textField);
        gs.alignment = TextAnchor.MiddleCenter;
        frame.m_Repetitions = EditorGUI.IntField(
          new Rect(repetitionsRect.x, repetitionsRect.y + repetitionsRect.height * .5f, repetitionsRect.width, repetitionsRect.height * .5f),
          new GUIContent("", "How many times is this frame repeated?"),
          frame.m_Repetitions,
          gs
        );

        t.m_FrameSources[i] = frame;

        Color button_color_normal = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.3f, 0.3f, 0.3f);
        float icon_height = r.height / 3f;
        Rect rect_delete = new Rect(r.width - 15, r.y, 20, icon_height);
        Rect rect_up = new Rect(r.width - 15, r.y + icon_height * 1, 20, icon_height);
        Rect rect_down = new Rect(r.width - 15, r.y + icon_height * 2, 20, icon_height);

        if (EditorUtils.IconButton(rect_delete, m_IconDelete, new Color(0.7f, 0.2f, 0.2f), "Delete")) {
          t.m_FrameSources.RemoveAt(i);
          i--;
        }
        if (EditorUtils.IconButton
        (rect_up, m_IconUp,
            i > 0 ? button_color_normal : new Color(0.5f, 0.5f, 0.5f, 0.25f),
            "Move up"
          ) && i > 0) {
          var prev = t.m_FrameSources[i - 1];
          t.m_FrameSources[i - 1] = t.m_FrameSources[i];
          t.m_FrameSources[i] = prev;
        }
        if (EditorUtils.IconButton(
            rect_down, m_IconDown,
            i < t.m_FrameSources.Count - 1 ? button_color_normal : new Color(0.5f, 0.5f, 0.5f, 0.25f),
            "Move down"
          ) && i < t.m_FrameSources.Count - 1) {
          var next = t.m_FrameSources[i + 1];
          t.m_FrameSources[i + 1] = t.m_FrameSources[i];
          t.m_FrameSources[i] = next;
        }


        EditorGUILayout.Space();
        EditorGUILayout.EndHorizontal();
      }
    }
    if (t.m_FrameSources.Count == 0) {
      EditorGUILayout.HelpBox("There's no frames! Add some models", MessageType.Warning);
      EditorGUILayout.Space();
    }
    EditorGUILayout.EndVertical();

    t.m_PreviewFirstFrame = EditorGUILayout.ToggleLeft("Preview first frame", t.m_PreviewFirstFrame);

    if (EditorGUI.EndChangeCheck()) {
      t.EnsureEditorPreview(true);
    }

  }

  void CustomLabel(string Text, int FontSize, FontStyle FontStyle = FontStyle.Normal, TextAnchor Alignment = TextAnchor.MiddleLeft) {
    var gs = new GUIStyle(GUI.skin.label);
    gs.fontSize = FontSize;
    gs.fontStyle = FontStyle;
    gs.alignment = Alignment;
    EditorGUILayout.LabelField(Text, gs, GUILayout.MinHeight(FontSize * 1.5f));
  }

  static Color DRAGBOX_COLOR_NORMAL = Color.white;
  static Color DRAGBOX_COLOR_ONDRAG_FOLDER = new Color(1.0f, 1.0f, 0.5f);
  static Color DRAGBOX_COLOR_ONDRAG_FILE = new Color(0.5f, 1.0f, 0.5f);
  static Color DRAGBOX_COLOR_ERROR = new Color(1.0f, 0.3f, 0.3f);

  string ProcessAsset(Object Asset, bool Add = false) {
    var type = SequenceUtils.GetAssetType(Asset);
    if (type == Sequence.SourceType.Folder) {
      if (Add) {
        var frames = EditorUtils.GetFramesFromFolder(Asset);
        foreach (var f in frames) {
          (target as Sequence).AddFrame(f, Sequence.SourceType.Model);
          EditorUtility.SetDirty(target);
        }
      }
      GUI.color = DRAGBOX_COLOR_ONDRAG_FOLDER;
      return string.Format("Adding folder '{0}'", Asset.name);
    } else if (type != Sequence.SourceType.Unsupported) {
      if (Add) {
        (target as Sequence).AddFrame(Asset as GameObject, type);
        EditorUtility.SetDirty(target);
      }
      GUI.color = DRAGBOX_COLOR_ONDRAG_FILE;
      return "Adding '" + Asset.name + "'";
    }
    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
    GUI.color = DRAGBOX_COLOR_ERROR;
    return "This asset isn't compatible!";
  }
}

}  // namespace TiltBrushToolkit