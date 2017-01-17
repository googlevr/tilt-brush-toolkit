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
using System.Collections.Generic;
using System.IO;

namespace TiltBrushToolkit {

public class EditorUtils {

  #region Tilt Menu

  [MenuItem("Tilt Brush/Labs/Separate strokes by brush color")]
  public static void ExplodeSketchByColor() {
    if (!EditorUtility.DisplayDialog ("Different Strokes", "Separate brush strokes of different colors into separate objects? \n* Note: This is an experimental feature!", "OK", "Cancel"))
      return;
    Undo.IncrementCurrentGroup();
    Undo.SetCurrentGroupName("Separate sketch by color");
    List<GameObject> newSelection = new List<GameObject>();
    bool cancel = false;

    var tri = new int[3] { 0, 0, 0 };

    foreach (var o in Selection.gameObjects) {
      if (cancel) break;
      var obj = GameObject.Instantiate(o, o.transform.position, o.transform.rotation) as GameObject;
      obj.name = o.name + " (Separated)";
      Undo.RegisterCreatedObjectUndo(obj, "Separate sketch by color");
      newSelection.Add(obj);
      int count = 0;

      foreach (var m in obj.GetComponentsInChildren<MeshFilter>()) {
        if (cancel) break;
        var mesh = m.sharedMesh;
        var meshColors = mesh.colors;

        // Keep a list of triangles for each color (as a vector) we find
        Dictionary<Vector3, List<int>> colors = new Dictionary<Vector3, List<int>>();

        var triangles = mesh.triangles;
        const int PROGRESS_FREQUENCY = 600;

        for (int i = 0; i < triangles.Length; i += 3) {
          if (++count > PROGRESS_FREQUENCY) {
            count = 0;
            cancel = EditorUtility.DisplayCancelableProgressBar("Separating sketch", "Processing " + mesh.name, (float)i / (float)triangles.Length);
            if (cancel) break;
          }

          tri[0] = triangles[i];
          tri[1] = triangles[i + 1];
          tri[2] = triangles[i + 2];

          // Get the triangle's average color
          var color = GetTriangleColorVec(meshColors, tri);

          // Add the triangle to the triangle-by-color list
          List<int> trianglesForColor;
          if (!colors.TryGetValue(color, out trianglesForColor))
            trianglesForColor = colors[color] = new List<int>();
          trianglesForColor.AddRange(tri);
        }

        if (cancel)
          break;

        // make a new mesh for each color
        int colorIndex = 0;
        count = 0;
        foreach (var color in colors.Keys) {
          if (++count > PROGRESS_FREQUENCY) {
            count = 0;
            cancel = EditorUtility.DisplayCancelableProgressBar("Separating sketch", "Processing " + mesh.name, (float)colorIndex / (float)colors.Keys.Count);
            if (cancel) break;
          }
          // Clone the gameobject with the mesh
          var newObj = GameObject.Instantiate(m.gameObject, m.transform.position, m.transform.rotation) as GameObject;
          newObj.name = string.Format("{0} {1}", m.name, colorIndex);
          newObj.transform.SetParent(m.transform.parent, true);
          Undo.RegisterCreatedObjectUndo(newObj, "Separate sketch by color");

          // get the subset of triangles for this color and make a new mesh out of it. TODO: only use the vertices used by the triangles
          var newMesh = GetMeshSubset(mesh, colors[color].ToArray());
          newObj.GetComponent<MeshFilter>().mesh = newMesh;
          colorIndex++;
        }
        Undo.DestroyObjectImmediate(m.gameObject);
      }
      // Delete the original object?
      Undo.DestroyObjectImmediate(o);
    }
    if (!cancel) {
      // Select the newly created objects
      Selection.objects = newSelection.ToArray();
    } else {
      Undo.RevertAllInCurrentGroup();
    }
    EditorUtility.ClearProgressBar();
  }

  [MenuItem("Tilt Brush/Labs/Separate strokes by brush color", true)]
  public static bool ExplodeSketchByColorValidate() {
    // TODO: validate that selection is a model
    foreach (var o in Selection.gameObjects) {
      if (o.GetComponent<MeshFilter>() != null)
        return true;
      if (o.GetComponentsInChildren<MeshFilter>().Length > 0)
        return true;
    }
    return false;
  }

  /// <summary>
  /// Gets the average color of a triangle and returns it as a vector for easier comparison
  /// </summary>
  public static Vector3 GetTriangleColorVec(Color[] meshColors, int[] Triangle) {
    Vector3 v = new Vector3();
    for (int i = 0; i < Triangle.Length; i++) {
      var c = meshColors[Triangle[i]];
      v.x += c.r;
      v.y += c.g;
      v.z += c.b;
    }
    v /= Triangle.Length;
    return v;
  }

  public static Mesh GetMeshSubset(Mesh OriginalMesh, int[] Triangles) {
    Mesh newMesh = new Mesh();
    newMesh.name = OriginalMesh.name;
    newMesh.vertices = OriginalMesh.vertices;
    newMesh.triangles = Triangles;
    newMesh.uv = OriginalMesh.uv;
    newMesh.uv2 = OriginalMesh.uv2;
    newMesh.uv2 = OriginalMesh.uv2;
    newMesh.colors = OriginalMesh.colors;
    newMesh.subMeshCount = OriginalMesh.subMeshCount;
    newMesh.normals = OriginalMesh.normals;
    //AssetDatabase.CreateAsset(newMesh, "Assets/"+mesh.name+"_submesh["+index+"].asset");
    return newMesh;
  }

  #endregion

  public static string m_TiltBrushDirectoryName = "TiltBrush";
  static string m_TiltBrushDirectory = "";
  public static string TiltBrushDirectory {
    get {
      if (string.IsNullOrEmpty (m_TiltBrushDirectory)) {
        foreach (var s in AssetDatabase.FindAssets ("EditorUtils")) {
          var path = AssetDatabase.GUIDToAssetPath (s);
          if (!path.Contains (m_TiltBrushDirectoryName))
            continue;
          m_TiltBrushDirectory = path.Substring (0, path.IndexOf (m_TiltBrushDirectoryName) + m_TiltBrushDirectoryName.Length);
        }
      }
      if (string.IsNullOrEmpty (m_TiltBrushDirectory))
        Debug.LogErrorFormat ("Could not find the TiltBrush directory. Reimport the Tilt Brush Unity SDK to ensure it's unmodified.");
      return m_TiltBrushDirectory;
    }
  }

  /// <summary>
  ///  Takes a texture (usually with height 1) and stretches it into single line height for debugging
  /// </summary>
  public static void LayoutCustomLabel(string Label, int FontSize = 11, FontStyle Style = FontStyle.Normal, TextAnchor Anchor = TextAnchor.MiddleLeft) {
    var gs = new GUIStyle(GUI.skin.label);
    gs.fontStyle = Style;
    gs.fontSize = FontSize;
    gs.alignment = Anchor;
    gs.richText = true;
    EditorGUILayout.LabelField(Label, gs);
  }

  /// <summary>
  ///  Takes a texture (usually with height 1) and stretches it into single line height for debugging
  /// </summary>
  public static void LayoutTexture(string Label, Texture2D Texture) {
    EditorGUILayout.LabelField(Label);
    var r = EditorGUILayout.BeginVertical(GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2));
    EditorGUILayout.Space();
    GUI.DrawTexture(r, Texture, ScaleMode.StretchToFill, true);
    //EditorGUI.DrawTextureTransparent(r,t.WaveFormTexture, ScaleMode.StretchToFill);
    EditorGUILayout.EndVertical();
  }

  public static void LayoutBar(string Label, float Value, Color Color) {
    Value = Mathf.Clamp01(Value);
    EditorGUILayout.Space();
    var r = EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
    EditorGUILayout.Space();
    EditorGUI.LabelField(new Rect(r.x, r.y, r.width * 0.5f, r.height), Label);
    DrawBar(new Rect(r.x + r.width * .5f, r.y, r.width * .5f, r.height), Value, Color);
    EditorGUILayout.EndHorizontal();
  }

  public static void LayoutBarVec4(string Label, Vector4 Value, Color Color, bool ClampTo01 = true) {
    Value.x = ClampTo01 ? Mathf.Clamp01(Value.x) : Value.x % 1f;
    Value.y = ClampTo01 ? Mathf.Clamp01(Value.y) : Value.y % 1f;
    Value.z = ClampTo01 ? Mathf.Clamp01(Value.z) : Value.z % 1f;
    Value.w = ClampTo01 ? Mathf.Clamp01(Value.w) : Value.w % 1f;
    EditorGUILayout.Space();
    var r = EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2f));
    EditorGUILayout.Space();
    var gs = new GUIStyle(GUI.skin.label);
    gs.alignment = TextAnchor.UpperRight;
    EditorGUI.LabelField(new Rect(r.x, r.y, r.width * 0.5f, r.height), Label + " ", gs);
    DrawBar(new Rect(r.x + r.width * .5f, r.y + (r.height / 4f) * 0, r.width * .5f, r.height / 4f - 2), Value.x, Color);
    DrawBar(new Rect(r.x + r.width * .5f, r.y + (r.height / 4f) * 1, r.width * .5f, r.height / 4f - 2), Value.y, Color);
    DrawBar(new Rect(r.x + r.width * .5f, r.y + (r.height / 4f) * 2, r.width * .5f, r.height / 4f - 2), Value.z, Color);
    DrawBar(new Rect(r.x + r.width * .5f, r.y + (r.height / 4f) * 3, r.width * .5f, r.height / 4f - 2), Value.w, Color);
    EditorGUILayout.EndHorizontal();
  }

  static void DrawBar(Rect r, float Value, Color Color) {
    EditorGUI.DrawRect(r, new Color(0.7f, 0.7f, 0.7f));
    EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * Value, r.height), Color * new Color(0.8f, 0.8f, 0.8f));
  }

  public static bool IconButton(Rect Rect, Texture2D Texture, Color Color, string Tooltip = "") {
    var c = GUI.color;
    GUI.color = Color;
    var gs = new GUIStyle(GUI.skin.label);
    gs.alignment = TextAnchor.MiddleCenter;
    bool result = GUI.Button(Rect, new GUIContent(Texture, Tooltip), gs);
    GUI.color = c;
    return result;
  }

  public static GameObject[] GetFramesFromFolder(Object FolderAsset) {

    if (!(FolderAsset is UnityEditor.DefaultAsset))
      return null;

    var frames = new List<GameObject> ();

    string sAssetFolderPath = AssetDatabase.GetAssetPath(FolderAsset);
    string sDataPath  = Application.dataPath;
    string sFolderPath = sDataPath.Substring(0 ,sDataPath.Length-6)+sAssetFolderPath;

    GrabObjectsAtDirectory (sFolderPath, ref frames); // get files in top directory
    RecursiveSearch (sFolderPath, ref frames); // go through subdirectories
    GameObject[] array = frames.ToArray ();
    System.Array.Sort (array, new AlphanumericComparer ());
    return array;

  }

  public static void GrabObjectsAtDirectory(string sDir, ref List<GameObject> List) {
    foreach (string f in Directory.GetFiles(sDir))
    {
      var frame =  AssetDatabase.LoadAssetAtPath<GameObject>(f.Substring(Application.dataPath.Length-6));
      if (frame != null)
        List.Add (frame);
    }
  }
  public static void RecursiveSearch(string sDir, ref List<GameObject> List)
  {
    try
    {
      foreach (string d in Directory.GetDirectories(sDir))
      {
        GrabObjectsAtDirectory(d, ref List);
        RecursiveSearch(d, ref List);
      }
    }
    catch (System.Exception excpt)
    {
      Debug.LogError(excpt.Message);
    }
  }

  private static string RemoveWhitespace(string String) {
    return string.Join("", String.Split(default(string[]), System.StringSplitOptions.RemoveEmptyEntries));
  }

}

public class AlphanumericComparer : IComparer<Object> {
  public int Compare(Object x, Object y) {
    return string.Compare (Pad (x.name), Pad (y.name));
  }

  string Pad(string Input) {
    // turn ABC10 into ABC000000000010 so it gets sorted as ABC1,ABC2,ABC10 instead of ABC1,ABC10,ABC2
    return System.Text.RegularExpressions.Regex.Replace (Input, "[0-9]+", match => match.Value.PadLeft (10, '0'));
  }
}

}  // namespace TiltBrushToolkit