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

"""Code for parsing Tilt Brush's json-based geometry export format.
Typically you should prefer the .fbx exported straight out of Tilt Brush.
See:
  iter_strokes()
  class TiltBrushMesh"""

import base64
from itertools import izip_longest
import json
import struct
from uuid import UUID

SINGLE_SIDED_FLAT_BRUSH = set([
  UUID("cb92b597-94ca-4255-b017-0e3f42f12f9e"), # Fire
  UUID("cf019139-d41c-4eb0-a1d0-5cf54b0a42f3"), # Highlighter
  UUID("e8ef32b1-baa8-460a-9c2c-9cf8506794f5"), # Hypercolor
  UUID("2241cd32-8ba2-48a5-9ee7-2caef7e9ed62"), # Light
  UUID("c33714d1-b2f9-412e-bd50-1884c9d46336"), # Plasma
  UUID("ad1ad437-76e2-450d-a23a-e17f8310b960"), # Rainbow
  UUID("44bb800a-fbc3-4592-8426-94ecb05ddec3"), # Streamers
  UUID("d229d335-c334-495a-a801-660ac8a87360"), # Velvet Ink
])

def _grouper(n, iterable, fillvalue=None):
  """grouper(3, 'ABCDEFG', 'x') --> ABC DEF Gxx"""
  args = [iter(iterable)] * n
  return izip_longest(fillvalue=fillvalue, *args)


def iter_meshes(filename):
  """Given a Tilt Brush .json export, yields TiltBrushMesh instances."""
  obj = json.load(file(filename, 'rb'))
  lookup = obj['brushes']
  for dct in lookup:
    dct['guid'] = UUID(dct['guid'])
  for json_stroke in obj['strokes']:
    yield TiltBrushMesh._from_json(json_stroke, lookup)


class TiltBrushMesh(object):
  """Geometry for a single stroke/mesh.
  Public attributes:
    .brush_name Roughly analagous to a material
    .brush_guid

    .v          list of positions (3-tuples)
    .n          list of normals (3-tuples, or None if missing)
    .uv0        list of uv0 (2-, 3-, 4-tuples, or None if missing)
    .uv1        see uv0
    .c          list of colors, as a uint32. abgr little-endian, rgba big-endian
    .t          list of tangents (4-tuples, or None if missing)

    .tri        list of triangles (3-tuples of ints)
  """
  VERTEX_ATTRIBUTES = [
    # Attribute name, type code
    ('v',  'f', None),
    ('n',  'f', 3),
    ('uv0','f', None),
    ('uv1','f', None),
    ('c',  'I', 1),
    ('t',  'f', 4),
  ]

  @classmethod
  def _from_json(cls, obj, brush_lookup):
    """Factory method: For use by iter_meshes."""
    empty = None

    stroke = TiltBrushMesh()
    brush = brush_lookup[obj['brush']]
    stroke.brush_name = brush['name']
    stroke.brush_guid = UUID(str(brush['guid']))

    # Vertex attributes
    # If stroke is non-empty, 'v' is always present, and always comes first
    num_verts = 0
    for attr, typechar, expected_stride in cls.VERTEX_ATTRIBUTES:
      if attr in obj:
        data_bytes = base64.b64decode(obj[attr])
        if len(data_bytes) == 0:
          data_grouped = []
        else:
          fmt = "<%d%c" % (len(data_bytes) / 4, typechar)
          data_words = struct.unpack(fmt, data_bytes)
          if attr == 'v':
            num_verts = len(data_words) / 3
          assert (len(data_words) % num_verts) == 0
          stride_words = len(data_words) / num_verts
          assert (expected_stride is None) or (stride_words == expected_stride)
          if stride_words > 1:
            data_grouped = list(_grouper(stride_words, data_words))
          else:
            data_grouped = list(data_words)
        setattr(stroke, attr, data_grouped)
      else:
        # For convenience, fill in with an empty array
        if empty is None:
          empty = [None,] * num_verts
        setattr(stroke, attr, empty)

    # Triangle indices. 'tri' might not exist, if empty
    if 'tri' in obj:
      data_bytes = base64.b64decode(obj['tri'])
      data_words = struct.unpack("<%dI" % (len(data_bytes) / 4), data_bytes)
      assert len(data_words) % 3 == 0
      stroke.tri = list(_grouper(3, data_words))
    else:
      stroke.tri = []

    return stroke

  @classmethod
  def from_meshes(cls, strokes, name=None):
    """Collapses multiple TiltBrushMesh instances into one.
    Pass an iterable of at least 1 stroke.
    Uses the brush from the first stroke."""
    stroke_list = list(strokes)
    dest = TiltBrushMesh()
    dest.name = name
    dest.brush_name = stroke_list[0].brush_name
    dest.brush_guid = stroke_list[0].brush_guid
    dest.v = []
    dest.n = []
    dest.uv0 = []
    dest.uv1 = []
    dest.c = []
    dest.t = []
    dest.tri = []
    for stroke in stroke_list:
      offset = len(dest.v)
      dest.v.extend(stroke.v)
      dest.n.extend(stroke.n)
      dest.uv0.extend(stroke.uv0)
      dest.uv1.extend(stroke.uv1)
      dest.c.extend(stroke.c)
      dest.t.extend(stroke.t)
      dest.tri.extend([ (t[0] + offset, t[1] + offset, t[2] + offset)
                        for t in stroke.tri ])
    return dest

  def __init__(self):
    self.name = None
    self.brush_name = self.brush_guid = None
    self.v = self.n = self.uv0 = self.uv1 = self.c = self.t = None
    self.tri = None

  def collapse_verts(self, ignore=None):
    """Collapse verts with identical data.
    Put triangle indices into a canonical order, with lowest index first.
    *ignore* is a list of attribute names to ignore when comparing."""
    # Convert from SOA to AOS
    compare = set(('n', 'uv0', 'uv1', 'c', 't'))
    if ignore is not None:
      compare -= set(ignore)
    compare = sorted(compare)
    compare.insert(0, 'v')

    struct_of_arrays = []
    for attr_name in sorted(compare):
      struct_of_arrays.append(getattr(self, attr_name))
    vert_structs = zip(*struct_of_arrays)

    vert_struct_to_new_index = {}
    old_index_to_new_index = []
    new_index_to_old_index = []

    for i_old, v in enumerate(vert_structs):
      i_next = len(vert_struct_to_new_index)
      i_new = vert_struct_to_new_index.setdefault(v, i_next)
      if i_next == i_new:
        # New vertex seen
        new_index_to_old_index.append(i_old)
      old_index_to_new_index.append(i_new)

    def permute(old_lst, new_to_old=new_index_to_old_index):
      # Returns content of old_lst in a new order
      return [old_lst[i_old] for (i_new, i_old) in enumerate(new_to_old)]

    def remap_tri((t0, t1, t2), old_to_new=old_index_to_new_index):
      # Remaps triangle indices; remapped triangle indices will be
      # rotated so that the lowest vert index comes first.
      t0 = old_to_new[t0]
      t1 = old_to_new[t1]
      t2 = old_to_new[t2]
      if t0 <= t1 and t0 <= t2:
        return (t0, t1, t2)
      elif t1 <= t2:
        return (t1, t2, t0)
      else:
        return (t2, t0, t1)

    self.v   = permute(self.v)
    self.n   = permute(self.n)
    self.uv0 = permute(self.uv0)
    self.uv1 = permute(self.uv1)
    self.c   = permute(self.c)
    self.t   = permute(self.t)

    self.tri = map(remap_tri, self.tri)

  def add_backfaces(self):
    """Double the number of triangles by adding an oppositely-wound
    triangle for every existing triangle."""
    num_verts = len(self.v)

    def flip_vec3(val):
      if val is None: return None
      return (-val[0], -val[1], -val[2])

    # Duplicate vert data, flipping normals
    # This is safe because the values are tuples (and immutable)
    self.v *= 2
    self.n += map(flip_vec3, self.n)
    self.uv0 *= 2
    self.uv1 *= 2
    self.c *= 2
    self.t *= 2

    more_tris = []
    for tri in self.tri:
      more_tris.append((num_verts + tri[0],
                        num_verts + tri[2],
                        num_verts + tri[1]))
    self.tri += more_tris
    
  def remove_backfaces(self):
    """Remove backfaces, defined as any triangle that follows
    an oppositely-wound triangle using the same indices.
    Assumes triangle indices are in canonical order."""
    # (also removes duplicates, if any exist)
    seen = set()
    new_tri = []
    for tri in self.tri:
      # Since triangle indices are in a canonical order, the reverse
      # winding will always be t[0], t[2], t[1]
      if tri in seen or (tri[0], tri[2], tri[1]) in seen:
        pass
      else:
        seen.add(tri)
        new_tri.append(tri)
    self.tri = new_tri

  def remove_degenerate(self):
    """Removes degenerate triangles."""
    def is_degenerate((t0, t1, t2)):
      return t0==t1 or t1==t2 or t2==t0
    self.tri = [t for t in self.tri if not is_degenerate(t)]

  def add_backfaces_if_necessary(self):
    """Try to detect geometry that is missing backface geometry"""

  def recenter(self):
    a0 = sum(v[0] for v in self.v) / len(self.v)
    a1 = sum(v[1] for v in self.v) / len(self.v)
    a2 = sum(v[2] for v in self.v) / len(self.v)
    for i,v in enumerate(self.v):
      self.v[i] = (v[0]-a0, v[1]-a1, v[2]-a2)

  def dump(self, verbose=False):
    print "  Brush: %s, %d verts, %d tris" % (self.brush_guid, len(self.v), len(self.tri)/3)
    if verbose:
      print '  v'
      for v in self.v:
        print '  ',v
      print '  t'
      for t in self.tri:
        print '  ',t
