using System;

namespace TiltbrushToolkit
{
    public class Header
    {
        public uint Sentinal { get; set; }
        public int Version { get; set; }
        public int Unused { get; set; }
    }
}
