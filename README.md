# Tilt Brush Toolkit

The Tilt Brush Toolkit is a collection of scripts and assets that allow you to use [Tilt Brush](http://g.co/tiltbrush) data in your creative projects.

## Contents

### Unity SDK

![Unity SDK](http://i.imgur.com/UdJg4Tz.gif)

Scripts, shaders and tools for importing and manipulating Tilt Brush  sketches in [Unity](http://unity3d.com/)

* Easily import sketches into Unity
* Brush shaders and materials
* Audio reactive features
* Examples and reusable scripts to create animations and add interactivity

**Click [here](../../releases) to download the latest version of the Tilt Brush Unity SDK**

Check out the [Documentation](https://docs.google.com/document/d/1YID89te9oDjinCkJ9R65bLZ3PpJk1W4S1SM2Ccc6-9w) to get started !

![Unity SDK](http://i.imgur.com/VLWEkV6.png?1)

### Command Line Tools
Python scripts for advanced Tilt Brush data manipulation.

 * `bin` - command-line tools
   * `dump_tilt.py` - Sample code that uses the tiltbrush.tilt module to view raw Tilt Brush data.
   * `geometry_json_to_fbx.py` - Historical sample code that converts Tilt Brush .json exports to .fbx. This script is superseded by Tilt Brush native .fbx exports.
   * `geometry_json_to_obj.py` - Historical sample code that converts Tilt Brush .json exports to .obj. This script is superseded by Tilt Brush native .fbx exports.
   * `tilt_to_strokes_dae.py` - Converts .tilt files to a Collada .dae containing spline data.
   * `unpack_tilt.py` - Converts .tilt files from packed format (zip) to unpacked format (directory) and vice versa, optionally applying compression.
 * `Python` - Put this in your `PYTHONPATH`
   * `tiltbrush` - Python package for manipulating Tilt Brush data
     * `export.py` - Parse the legacy .json export format
     * `tilt.py` - Read and write .tilt files
     * `unpack.py` - Convert .tilt files from packed format to unpacked format and vice versa