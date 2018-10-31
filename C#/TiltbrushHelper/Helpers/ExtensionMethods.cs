using System;

namespace TiltbrushToolkit
{
    internal static class ExtensionMethods
    {
        public static T[] ExtendArray<T>(this T[] original, T[] additional)
        {
            int offset = original.Length;
            var newMesh = new T[original.Length + additional.Length];
            original.CopyTo(newMesh, 0);
            additional.CopyTo(newMesh, offset);
            return newMesh;
        }
    }
}
