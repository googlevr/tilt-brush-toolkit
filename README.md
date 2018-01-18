# Tilt Brush Toolkit

The Tilt Brush Toolkit is a collection of scripts and assets that allow you to use [Tilt Brush](http://g.co/tiltbrush) data in your creative projects. If you simply want to import Tilt Brush geometry into Unity, this package is mostly superseded by [Poly Toolkit](https://github.com/googlevr/poly-toolkit-unity).

## Contents

### Unity SDK

![Unity SDK](http://i.imgur.com/UdJg4Tz.gif)

Scripts, shaders and tools for importing and manipulating Tilt Brush .fbx exports in [Unity](http://unity3d.com/). Unless you specifically need to use .fbx -- for instance, because Maya is part of your workflow -- try Poly Toolkit first.

* Easily import sketches into Unity
* Brush shaders and materials
* Audio reactive features
* Examples and reusable scripts to create animations and add interactivity

**Click [here](../../releases) to download the latest version of the Tilt Brush Unity SDK**

Check out the [Documentation](https://docs.google.com/document/d/1YID89te9oDjinCkJ9R65bLZ3PpJk1W4S1SM2Ccc6-9w) to get started !

![Unity SDK](http://i.imgur.com/VLWEkV6.png?1)

### Command Line Tools
Python 2.7 code and scripts for advanced Tilt Brush data manipulation.

 * `bin` - command-line tools
   * `dump_tilt.py` - Sample code that uses the tiltbrush.tilt module to view raw Tilt Brush data.
   * `geometry_json_to_fbx.py` - Sample code that shows how to postprocess the raw per-stroke geometry in various ways that might be needed for more-sophisticated workflows involving DCC tools and raytracers. This variant packages the result as a .fbx file.
   * `geometry_json_to_obj.py` - Sample code that shows how to postprocess the raw per-stroke geometry in various ways that might be needed for more-sophisticated workflows involving DCC tools and raytracers. This variant packages the result as a .obj file.
   * `tilt_to_strokes_dae.py` - Converts .tilt files to a Collada .dae containing spline data.
   * `unpack_tilt.py` - Converts .tilt files from packed format (zip) to unpacked format (directory) and vice versa, optionally applying compression.
 * `Python` - Put this in your `PYTHONPATH`
   * `tiltbrush` - Python package for manipulating Tilt Brush data.
     * `export.py` - Parse the legacy .json export format. This format contains the raw per-stroke geometry in a form intended to be easy to postprocess.
     * `tilt.py` - Read and write .tilt files. This format contains no geometry, but does contain timestamps, pressure, controller position and orientation, metadata, and so on -- everything Tilt Brush needs to regenerate the geometry.
     * `unpack.py` - Convert .tilt files from packed format to unpacked format and vice versa.