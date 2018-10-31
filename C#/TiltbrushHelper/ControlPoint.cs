using System;

namespace TiltbrushToolkit
{
    public class ControlPoint
    {
        public float[] Position { get; set; }
        public float[] Orientation { get; set; }
        public ControlPointExtension Extension { get; set; }
        public ControlPoint()
        {
            Position = new float[3];
            Orientation = new float[4];
            Extension = new ControlPointExtension();
        }
    }
}
