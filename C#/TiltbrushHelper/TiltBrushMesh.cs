using System;
using System.Collections.Generic;
using System.Linq;

namespace TiltbrushToolkit
{
    public class TiltBrushMesh
    {
        public Guid BrushGuid { get; set; }
        public string BrushName { get; set; }
        public string Name { get; set; }
        public Tuple<float, float, float>[] v { get; set; }
        public Tuple<float, float, float>[] n { get; set; }
        public Tuple<float, float, float?, float?>[] uv0 { get; set; }
        public Tuple<float, float, float?, float?>[] uv1 { get; set; }
        public uint[] c { get; set; }
        public Tuple<float, float, float?, float?>[] t { get; set; }
        public Tuple<int, int, int>[] tri { get; set; }
        public void RemoveBackfaces()
        {
            //use list to allow easy addition
            List<Tuple<int, int, int>> seen = new List<Tuple<int, int, int>>();

            foreach (var triangle in tri)
            {
                if (!seen.Contains(triangle) && !seen.Contains(new Tuple<int, int, int>(triangle.Item1, triangle.Item3, triangle.Item2)))
                {
                    seen.Add(triangle);
                }
            }
            tri = seen.ToArray();
        }
        public void Recenter()
        {
            float a0 = v.Sum(vert => vert.Item1) / v.Length;
            float a1 = v.Sum(vert => vert.Item2) / v.Length;
            float a2 = v.Sum(vert => vert.Item3) / v.Length;
            Tuple<float, float, float>[] newV = new Tuple<float, float, float>[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var oldItem = v[i];
                newV[i] = new Tuple<float, float, float>(oldItem.Item1 - a0, oldItem.Item2 - a1, oldItem.Item3 - a2);
            }
            v = newV;
        }
        public void RemoveDegenerate()
        {
            tri = tri.Where(t => t.Item1 != t.Item2 && t.Item2 != t.Item3 && t.Item3 != t.Item1).ToArray();
        }
        public void AddBackfaces()
        {
            int numVerts = v.Length;
            v = v.ExtendArray(v);
            n = n.ExtendArray(n.Select(no => new Tuple<float, float, float>(no.Item1 * -1, no.Item2 * -1, no.Item3 * -1)).ToArray());
            uv0 = uv0.ExtendArray(uv0);
            if (uv1 != null)
            {
                uv1 = uv1.ExtendArray(uv1);
            }
            c = c.ExtendArray(c);
            t = t.ExtendArray(t);
            Tuple<int, int, int>[] additionalTris = new Tuple<int, int, int>[tri.Length];
            for (int i = 0; i < tri.Length; i++)
            {
                additionalTris[i] = new Tuple<int, int, int>(numVerts + tri[i].Item1, numVerts + tri[i].Item2, numVerts + tri[i].Item3);
            }
            tri = tri.ExtendArray(additionalTris);
        }
        /// <summary>
        /// Public method to create a TiltBrushMesh instance by merging multiple existing instances
        /// </summary>
        /// <param name="meshes"></param>
        /// <returns></returns>
        public static TiltBrushMesh FromMeshes(TiltBrushMesh[] meshes)
        {
            return Export.FromMeshes(meshes);
        }
        /// <summary>
        /// Public method to create an array of TiltBrushMesh instances from a specified .json Tilt Brush export
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static TiltBrushMesh[] FromFile(string filePath)
        {
            return Export.FromFile(filePath);
        }

    }
}
