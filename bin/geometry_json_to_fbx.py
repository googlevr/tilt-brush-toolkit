#!/usr/bin/env python

# Copyright 2016 Google Inc. All Rights Reserved.
# 
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
# 
#     http://www.apache.org/licenses/LICENSE-2.0
# 
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

# Historical sample code that converts Tilt Brush '.json' exports to .fbx.
# This script is superseded by Tilt Brush native .fbx exports.
# 
# There are command-line options to fine-tune the fbx creation.
# The defaults are:
# 
# - Weld vertices
# - Join strokes using the same brush into a single mesh
# - Don't create backface geometry for single-sided brushes"""

import argparse
from itertools import groupby
import os
import platform
import sys

try:
  sys.path.append(os.path.join(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__))), 'Python'))
  from tiltbrush.export import iter_meshes, TiltBrushMesh, SINGLE_SIDED_FLAT_BRUSH
except ImportError:
  print >>sys.stderr, "Please put the 'Python' directory in your PYTHONPATH"
  sys.exit(1)

try:
  arch = 'x64' if '64' in platform.architecture()[0] else 'x86'
  sys.path.append(r'c:\Program Files\Autodesk\FBX\FBX Python SDK\2015.1\lib\Python27_'+arch)
  from fbx import *
except ImportError:
  print >>sys.stderr, "Please install the Python fbx sdk:\nhttp://images.autodesk.com/adsk/files/fbx20151_fbxpythonsdk_win.exe"
  sys.exit(1)


# ----------------------------------------------------------------------
# Utils
# ----------------------------------------------------------------------

def as_fvec4(tup, scale=1):
  if len(tup) == 3:
    return FbxVector4(tup[0]*scale, tup[1]*scale, tup[2]*scale)
  else:
    return FbxVector4(tup[0]*scale, tup[1]*scale, tup[2]*scale, tup[3]*scale)

def as_fvec2(tup):
  return FbxVector2(tup[0], tup[1])

def as_fcolor(abgr_int, memo={}):
  try:
    return memo[abgr_int]
  except KeyError:
    a = (abgr_int >> 24) & 0xff
    b = (abgr_int >> 16) & 0xff
    g = (abgr_int >>  8) & 0xff
    r = (abgr_int      ) & 0xff
    scale = 1.0 / 255.0
    memo[abgr_int] = val = FbxColor(r * scale, g * scale, b * scale, a * scale)
    return val

# ----------------------------------------------------------------------
# Export
# ----------------------------------------------------------------------

def write_fbx_meshes(meshes, outf_name):
  """Emit a TiltBrushMesh as a .fbx file"""
  import FbxCommon
  (sdk, scene) = FbxCommon.InitializeSdkObjects()

  for mesh in meshes:
    add_mesh_to_scene(sdk, scene, mesh)
  
  FbxCommon.SaveScene(sdk, scene, outf_name)


def create_fbx_layer(fbx_mesh, data, converter_fn, layer_class,
                     allow_index=False, allow_allsame=False):
  """Returns an instance of layer_class populated with the passed data,
  or None if the passed data is empty/nonexistent.
  
  fbx_mesh      FbxMesh
  data          list of Python data
  converter_fn  Function converting data -> FBX data
  layer_class   FbxLayerElementXxx class
  allow_index   Allow the use of eIndexToDirect mode. Useful if the data
                has many repeated values. Unity3D doesn't seem to like it
                when this is used for vertex colors, though.
  allow_allsame Allow the use of eAllSame mode. Useful if the data might
                be entirely identical."""
  # No elements, or all missing data.
  if len(data) == 0 or data[0] == None:
    return None

  layer_elt = layer_class.Create(fbx_mesh, "")
  direct = layer_elt.GetDirectArray()
  index = layer_elt.GetIndexArray()
  
  if allow_allsame or allow_index:
    unique_data = sorted(set(data))

  # Something about this eIndexToDirect code isn't working for vertex colors and UVs.
  # Do it the long-winded way for now, I guess.
  allow_index = False
  if allow_allsame and len(unique_data) == 1:
    layer_elt.SetMappingMode(FbxLayerElement.eAllSame)
    layer_elt.SetReferenceMode(FbxLayerElement.eDirect)
    direct.Add(converter_fn(unique_data[0]))
  elif allow_index and len(unique_data) <= len(data) * .7:
    layer_elt.SetMappingMode(FbxLayerElement.eByControlPoint)
    layer_elt.SetReferenceMode(FbxLayerElement.eIndexToDirect)
    for datum in unique_data:
      direct.Add(converter_fn(datum))
    for i in range(len(data)-len(unique_data)-5):
      direct.Add(converter_fn(unique_data[0]))
    data_to_index = dict((d, i) for (i, d) in enumerate(unique_data))
    for i,datum in enumerate(data):
      #index.Add(data_to_index[datum])
      index.Add(data_to_index[datum])
  else:
    layer_elt.SetMappingMode(FbxLayerElement.eByControlPoint)
    layer_elt.SetReferenceMode(FbxLayerElement.eDirect)
    for datum in data:
      direct.Add(converter_fn(datum))

  return layer_elt


def add_mesh_to_scene(sdk, scene, mesh):
  """Emit a TiltBrushMesh as a .fbx file"""
  name = mesh.name or 'Tilt Brush'

  # Todo: pass scene instead?
  fbx_mesh = FbxMesh.Create(sdk, name)
  fbx_mesh.CreateLayer()
  layer0 = fbx_mesh.GetLayer(0)

  # Verts

  fbx_mesh.InitControlPoints(len(mesh.v))
  for i, v in enumerate(mesh.v):
    fbx_mesh.SetControlPointAt(as_fvec4(v, scale=100), i)

  layer_elt = create_fbx_layer(
      fbx_mesh, mesh.n, as_fvec4, FbxLayerElementNormal)
  if layer_elt is not None:
    layer0.SetNormals(layer_elt)

  layer_elt = create_fbx_layer(
      fbx_mesh, mesh.c, as_fcolor, FbxLayerElementVertexColor,
      allow_index = True,
      allow_allsame = True)
  if layer_elt is not None:
    layer0.SetVertexColors(layer_elt)

  # Tilt Brush may have 3- or 4-element UV channels, and may have multiple
  # UV channels. This only handles the standard case of 2-component UVs
  layer_elt = create_fbx_layer(
    fbx_mesh, mesh.uv0, as_fvec2, FbxLayerElementUV,
    allow_index = True)
  if layer_elt is not None:
    layer0.SetUVs(layer_elt, FbxLayerElement.eTextureDiffuse)
    pass

  # Polygons

  for triplet in mesh.tri:
    fbx_mesh.BeginPolygon(-1, -1, False)
    fbx_mesh.AddPolygon(triplet[0])
    fbx_mesh.AddPolygon(triplet[1])
    fbx_mesh.AddPolygon(triplet[2])
    fbx_mesh.EndPolygon()

  # Node tree

  root = scene.GetRootNode()
  node = FbxNode.Create(sdk, name)
  node.SetNodeAttribute(fbx_mesh)
  node.SetShadingMode(FbxNode.eTextureShading)  # Hmm
  root.AddChild(node)


# ----------------------------------------------------------------------
# main
# ----------------------------------------------------------------------

def main():
  import argparse
  parser = argparse.ArgumentParser(description="""Converts Tilt Brush '.json' exports to .fbx.""")
  parser.add_argument('filename', help="Exported .json files to convert to fbx")
  grp = parser.add_argument_group(description="Merging and optimization")
  grp.add_argument('--merge-stroke', action='store_true',
                   help="Merge all strokes into a single mesh")

  grp.add_argument('--merge-brush', action='store_true',
                   help="(default) Merge strokes that use the same brush into a single mesh")
  grp.add_argument('--no-merge-brush', action='store_false', dest='merge_brush',
                   help="Turn off --merge-brush")

  grp.add_argument('--weld-verts', action='store_true',
                   help="(default) Weld vertices")
  grp.add_argument('--no-weld-verts', action='store_false', dest='weld_verts',
                   help="Turn off --weld-verts")

  parser.add_argument('--add-backface', action='store_true',
                   help="Add backfaces to strokes that don't have them")

  parser.add_argument('-o', dest='output_filename', metavar='FILE',
                      help="Name of output file; defaults to <filename>.fbx")
  parser.set_defaults(merge_brush=True, weld_verts=True)
  args = parser.parse_args()

  if args.output_filename is None:
    args.output_filename = os.path.splitext(args.filename)[0] + '.fbx'

  meshes = list(iter_meshes(args.filename))
  for mesh in meshes:
    mesh.remove_degenerate()
    if args.add_backface and mesh.brush_guid in SINGLE_SIDED_FLAT_BRUSH:
      mesh.add_backface()

  if args.merge_stroke:
    meshes = [ TiltBrushMesh.from_meshes(meshes, name='strokes') ]
  elif args.merge_brush:
    def by_guid(m): return (m.brush_guid, m.brush_name)
    meshes = [ TiltBrushMesh.from_meshes(list(group), name='All %s' % (key[1], ))
               for (key, group) in groupby(sorted(meshes, key=by_guid), key=by_guid) ]

  if args.weld_verts:
    for mesh in meshes:
      # We don't write out tangents, so it's safe to ignore them when welding
      mesh.collapse_verts(ignore=('t',))
      mesh.remove_degenerate()

  write_fbx_meshes(meshes, args.output_filename)
  print "Wrote", args.output_filename


if __name__ == '__main__':
  main()
