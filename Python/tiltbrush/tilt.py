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

"""Reads and writes .tilt files. The main export is 'class Tilt'."""

import os
import math
import json
import uuid
import struct
import contextlib
from collections import defaultdict
from cStringIO import StringIO

__all__ = ('Tilt', 'Sketch', 'Stroke', 'ControlPoint',
           'BadTilt', 'BadMetadata', 'MissingKey')

# Format characters are as for struct.pack/unpack, with the addition of
# '@' which is a 4-byte-length-prefixed data blob.
STROKE_EXTENSION_BITS = {
  0x1: ('flags', 'I'),
  0x2: ('scale', 'f'),
  'unknown': lambda bit: ('stroke_ext_%d' % math.log(bit, 2),
                          'I' if (bit & 0xffff) else '@')
}
STROKE_EXTENSION_BY_NAME = dict(
  (info[0], (bit, info[1]))
  for (bit, info) in STROKE_EXTENSION_BITS.iteritems()
  if bit != 'unknown'
)

CONTROLPOINT_EXTENSION_BITS = {
  0x1: ('pressure', 'f'),
  0x2: ('timestamp', 'I'),
  'unknown': lambda bit: ('cp_ext_%d' % math.log(bit, 2), 'I')
}

#
# Internal utils
#

class memoized_property(object):
  """Modeled after @property, but runs the getter exactly once"""
  def __init__(self, fget):
    self.fget = fget
    self.name = fget.__name__

  def __get__(self, instance, owner):
    if instance is None:
      return None
    value = self.fget(instance)
    # Since this isn't a data descriptor (no __set__ method),
    # instance attributes take precedence over the descriptor.
    setattr(instance, self.name, value)
    return value

class binfile(object):
  # Helper for parsing
  def __init__(self, inf):
    self.inf = inf

  def read(self, n):
    return self.inf.read(n)

  def write(self, data):
    return self.inf.write(data)

  def read_length_prefixed(self):
    n, = self.unpack("<I")
    return self.inf.read(n)

  def write_length_prefixed(self, data):
    self.pack("<I", len(data))
    self.inf.write(data)

  def unpack(self, fmt):
    n = struct.calcsize(fmt)
    data = self.inf.read(n)
    return struct.unpack(fmt, data)

  def pack(self, fmt, *args):
    data = struct.pack(fmt, *args)
    return self.inf.write(data)

class BadTilt(Exception): pass
class BadMetadata(BadTilt): pass
class MissingKey(BadMetadata): pass


def validate_metadata(dct):
  def lookup((path, parent), key):
    child_path = '%s.%s' % (path, key)
    if key not in parent:
      raise MissingKey('Missing %s' % child_path)
    return (child_path, parent[key])
  def check_string((path, val)):
    if not isinstance(val, (str, unicode)):
      raise BadMetadata('Not string: %s' % path)
  def check_float((path, val)):
    if not isinstance(val, (float, int, long)):
      raise BadMetadata('Not number: %s' % path)
  def check_array((path, val), desired_len=None, typecheck=None):
    if not isinstance(val, (list, tuple)):
      raise BadMetadata('Not array: %s' % path)
    if desired_len and len(val) != desired_len:
      raise BadMetadata('Not length %d: %s' % (desired_len, path))
    if typecheck is not None:
      for i, child_val in enumerate(val):
        child_path = '%s[%s]' % (path, i)
        typecheck((child_path, child_val))
  def check_guid((path, val)):
    try:
      uuid.UUID(val)
    except Exception as e:
      raise BadMetadata('Not UUID: %s %s' % (path, e))
  def check_xform(pathval):
    check_array(lookup(pathval, 'position'), 3, check_float)
    check_array(lookup(pathval, 'orientation'), 4, check_float)

  root = ('metadata', dct)
  try: check_xform(lookup(root, 'ThumbnailCameraTransformInRoomSpace'))
  except MissingKey: pass
  try: check_xform(lookup(root, 'SceneTransformInRoomSpace'))
  except MissingKey: pass
  try: check_xform(lookup(root, 'CanvasTransformInSceneSpace'))
  except MissingKey: pass
  check_array(lookup(root, 'BrushIndex'), None, check_guid)
  check_guid(lookup(root, 'EnvironmentPreset'))
  if 'Authors' in dct:
    check_array(lookup(root, 'Authors'), None, check_string)


#
# External
#

class Tilt(object):
  """Class representing a .tilt file. Attributes:
    .sketch     A tilt.Sketch instance. NOTE: this is read lazily.
    .metadata   A dictionary of data.

  To modify the sketch, see XXX.
  To modify the metadata, see mutable_metadata()."""
  @staticmethod
  @contextlib.contextmanager
  def as_directory(tilt_file):
    """Temporarily convert *tilt_file* to directory format."""
    if os.path.isdir(tilt_file):
      yield Tilt(tilt_file)
    else:
      import tiltbrush.unpack as unpack
      compressed = unpack.convert_zip_to_dir(tilt_file)
      try:
        yield Tilt(tilt_file)
      finally:
        unpack.convert_dir_to_zip(tilt_file, compressed)

  @staticmethod
  def iter(directory):
    for r,ds,fs in os.walk(directory):
      for f in ds+fs:
        if f.endswith('.tilt'):
          try:
            yield Tilt(os.path.join(r,f))
          except BadTilt:
            pass

  def __init__(self, filename):
    self.filename = filename
    self._sketch = None          # lazily-loaded
    with self.subfile_reader('metadata.json') as inf:
      self.metadata = json.load(inf)
      try:
        validate_metadata(self.metadata)
      except BadMetadata as e:
        print 'WARNING: %s' % e

  def write_sketch(self):
    if False:
      # Recreate BrushIndex. Not tested and not strictly necessary, so not enabled
      old_index_to_brush = list(self.metadata['BrushIndex'])
      old_brushes = set( old_index_to_brush )
      new_brushes = set( old_index_to_brush[s.brush_idx] for s in self.sketch.strokes )
      if old_brushes != new_brushes:
        new_index_to_brush = sorted(new_brushes)
        brush_to_new_index = dict( (b, i) for (i, b) in enumerate(new_index_to_brush) )
        old_index_to_new_index = map(brush_to_new_index.get, old_index_to_brush)
        for stroke in self.sketch.strokes:
          stroke.brush_idx = brush_to_new_index[old_index_to_brush[stroke.brush_idx]]
        with self.mutable_metadata() as dct:
          dct['BrushIndex'] = new_index_to_brush

    self.sketch.write(self)

  @contextlib.contextmanager
  def subfile_reader(self, subfile):
    if os.path.isdir(self.filename):
      with file(os.path.join(self.filename, subfile), 'rb') as inf:
        yield inf
    else:
      from zipfile import ZipFile
      with ZipFile(self.filename, 'r') as inzip:
        with inzip.open(subfile) as inf:
          yield inf

  @contextlib.contextmanager
  def subfile_writer(self, subfile):
    # Kind of a large hammer, but it works
    if os.path.isdir(self.filename):
      with file(os.path.join(self.filename, subfile), 'wb') as outf:
        yield outf
    else:
      with Tilt.as_directory(self.filename) as tilt2:
        with tilt2.subfile_writer(subfile) as outf:
          yield outf

  @contextlib.contextmanager
  def mutable_metadata(self):
    """Return a mutable copy of the metadata.
    When the context manager exits, the updated metadata will
    validated and written to disk."""
    import copy
    mutable_dct = copy.deepcopy(self.metadata)
    yield mutable_dct
    validate_metadata(mutable_dct)
    if self.metadata != mutable_dct:
      # Copy into self.metadata, preserving topmost reference
      for k in list(self.metadata.keys()):
        del self.metadata[k]
      for k,v in mutable_dct.iteritems():
        self.metadata[k] = copy.deepcopy(v)
        
      new_contents = json.dumps(
        mutable_dct, ensure_ascii=True, allow_nan=False,
        indent=2, sort_keys=True, separators=(',', ': '))
      with self.subfile_writer('metadata.json') as outf:
        outf.write(new_contents)

  @memoized_property
  def sketch(self):
    # Would be slightly more consistent semantics to do the data read
    # in __init__, and parse it here; but this is probably good enough.
    return Sketch(self)


def _make_ext_reader(ext_bits, ext_mask):
  """Helper for Stroke and ControlPoint parsing.
  Returns:
  - function reader(file) -> list<extension values>
  - function writer(file, values)
  - dict mapping extension_name -> extension_index
  """
  infos = []
  while ext_mask:
    bit = ext_mask & ~(ext_mask-1)
    ext_mask = ext_mask ^ bit
    try: info = ext_bits[bit]
    except KeyError: info = ext_bits['unknown'](bit)
    infos.append(info)

  if len(infos) == 0:
    return (lambda f: [], lambda f,vs: None, {})

  fmt = '<' + ''.join(info[1] for info in infos)
  names = [info[0] for info in infos]
  if '@' in fmt:
    # struct.unpack isn't general enough to do the job
    print fmt, names, infos
    fmts = ['<'+info[1] for info in infos]
    def reader(f, fmts=fmts):
      values = [None] * len(fmts)
      for i,fmt in enumerate(fmts):
        if fmt == '<@':
          nbytes, = struct.unpack('<I', f.read(4))
          values[i] = f.read(nbytes)
        else:
          values[i], = struct.unpack(fmt, f.read(4))
  else:
    def reader(f, fmt=fmt, nbytes=len(infos)*4):
      values = list(struct.unpack(fmt, f.read(nbytes)))
      return values

  def writer(f, values, fmt=fmt):
    return f.write(struct.pack(fmt, *values))

  lookup = dict( (name,i) for (i,name) in enumerate(names) )
  return reader, writer, lookup

def _make_stroke_ext_reader(ext_mask, memo={}):
  try:
    ret = memo[ext_mask]
  except KeyError:
    ret = memo[ext_mask] = _make_ext_reader(STROKE_EXTENSION_BITS, ext_mask)
  return ret

def _make_cp_ext_reader(ext_mask, memo={}):
  try:
    ret = memo[ext_mask]
  except KeyError:
    ret = memo[ext_mask] = _make_ext_reader(CONTROLPOINT_EXTENSION_BITS, ext_mask)
  return ret


class Sketch(object):
  """Stroke data from a .tilt file. Attributes:
    .strokes    List of tilt.Stroke instances
    .filename   Filename if loaded from file, but usually None
    .header     Opaque header data"""
  def __init__(self, source):
    """source is either a file name, a file-like instance, or a Tilt instance."""
    if isinstance(source, Tilt):
      with source.subfile_reader('data.sketch') as inf:
        self.filename = None
        self._parse(binfile(inf))
    elif hasattr(source, 'read'):
      self.filename = None
      self._parse(binfile(source))
    else:
      self.filename = source
      with file(source, 'rb') as inf:
        self._parse(binfile(inf))

  def write(self, destination):
    """destination is either a file name, a file-like instance, or a Tilt instance."""
    tmpf = StringIO()
    self._write(binfile(tmpf))
    data = tmpf.getvalue()

    if isinstance(destination, Tilt):
      with destination.subfile_writer('data.sketch') as outf:
        outf.write(data)
    elif hasattr(destination, 'write'):
      destination.write(data)
    else:
      with file(destination, 'wb') as outf:
        outf.write(data)

  def _parse(self, b):
    # b is a binfile instance
    # mutates self
    self.header = list(b.unpack("<3I"))
    self.additional_header = b.read_length_prefixed()
    (num_strokes, ) = b.unpack("<i")
    assert 0 <= num_strokes < 300000, num_strokes
    self.strokes = [Stroke.from_file(b) for i in xrange(num_strokes)]

  def _write(self, b):
    # b is a binfile instance.
    b.pack("<3I", *self.header)
    b.write_length_prefixed(self.additional_header)
    b.pack("<i", len(self.strokes))
    for stroke in self.strokes:
      stroke._write(b)


class Stroke(object):
  """Data for a single stroke from a .tilt file. Attributes:
    .brush_idx      Index into Tilt.metadata['BrushIndex']; tells you the brush GUID
    .brush_color    RGBA color, as 4 floats in the range [0, 1]
    .brush_size     Brush size, in decimeters, as a float. Multiply by
                    get_stroke_extension('scale') to get a true size.
    .controlpoints  List of tilt.ControlPoint instances.

    .flags          Wrapper around get/set_stroke_extension('flags')
    .scale          Wrapper around get/set_stroke_extension('scale')

  Also see has_stroke_extension(), get_stroke_extension()."""
  @classmethod
  def from_file(cls, b):
    inst = cls()
    inst._parse(b)
    return inst

  def clone(self):
    """Returns a deep copy of the stroke."""
    inst = self.shallow_clone()
    inst.controlpoints = map(ControlPoint.clone, inst.controlpoints)
    return inst

  def __getattr__(self, name):
    if name in STROKE_EXTENSION_BY_NAME:
      try:
        return self.get_stroke_extension(name)
      except LookupError:
        raise AttributeError("%s (extension attribute)" % name)
    raise AttributeError(name)

  def __setattr__(self, name, value):
    if name in STROKE_EXTENSION_BY_NAME:
      return self.set_stroke_extension(name, value)
    return super(Stroke, self).__setattr__(name, value)

  def __delattr__(self, name):
    if name in STROKE_EXTENSION_BY_NAME:
      try:
        self.delete_stroke_extension(name)
        return
      except LookupError:
        raise AttributeError("%s (extension attribute)" % name)
    raise AttributeError(name)

  def shallow_clone(self):
    """Clone everything but the control points themselves."""
    inst = self.__class__()
    for attr in ('brush_idx', 'brush_color', 'brush_size', 'stroke_mask', 'cp_mask',
                 'stroke_ext_writer', 'stroke_ext_lookup', 'cp_ext_writer', 'cp_ext_lookup'):
      setattr(inst, attr, getattr(self, attr))
    inst.extension = list(self.extension)
    inst.controlpoints = list(self.controlpoints)
    return inst

  def _parse(self, b):
    # b is a binfile instance
    (self.brush_idx, ) = b.unpack("<i")
    self.brush_color = b.unpack("<4f")
    (self.brush_size, self.stroke_mask, self.cp_mask) = b.unpack("<fII")
    stroke_ext_reader, self.stroke_ext_writer, self.stroke_ext_lookup = \
        _make_stroke_ext_reader(self.stroke_mask)
    self.extension = stroke_ext_reader(b)

    cp_ext_reader, self.cp_ext_writer, self.cp_ext_lookup = \
        _make_cp_ext_reader(self.cp_mask)
    
    (num_cp, ) = b.unpack("<i")
    assert num_cp < 10000, num_cp

    # Read the raw data up front, but parse it lazily
    bytes_per_cp = 4 * (3 + 4 + len(self.cp_ext_lookup))
    self._controlpoints = (cp_ext_reader, num_cp, b.inf.read(num_cp * bytes_per_cp))

  @memoized_property
  def controlpoints(self):
    (cp_ext_reader, num_cp, raw_data) = self.__dict__.pop('_controlpoints')
    b = binfile(StringIO(raw_data))
    return [ControlPoint.from_file(b, cp_ext_reader) for i in xrange(num_cp)]

  def has_stroke_extension(self, name):
    """Returns true if this stroke has the requested extension data.
    
    The current stroke extensions are:
      scale     Non-negative float. The size of the player when making this stroke.
                Multiply this by the brush size to get a true stroke size."""
    return name in self.stroke_ext_lookup

  def get_stroke_extension(self, name):
    """Returns the requested extension stroke data.
    Raises LookupError if it doesn't exist."""
    idx = self.stroke_ext_lookup[name]
    return self.extension[idx]

  def set_stroke_extension(self, name, value):
    """Sets stroke extension data.
    This method can be used to add extension data."""
    idx = self.stroke_ext_lookup.get(name, None)
    if idx is not None:
      self.extension[idx] = value
    else:
      # Convert from idx->value to name->value
      name_to_value = dict( (name, self.extension[idx])
                            for (name, idx) in self.stroke_ext_lookup.iteritems() )
      name_to_value[name] = value

      bit, exttype = STROKE_EXTENSION_BY_NAME[name]
      self.stroke_mask |= bit
      _, self.stroke_ext_writer, self.stroke_ext_lookup = \
          _make_stroke_ext_reader(self.stroke_mask)
      
      # Convert back to idx->value
      self.extension = [None] * len(self.stroke_ext_lookup)
      for (name, idx) in self.stroke_ext_lookup.iteritems():
        self.extension[idx] = name_to_value[name]
                                                          
  def delete_stroke_extension(self, name):
    """Remove stroke extension data.
    Raises LookupError if it doesn't exist."""
    idx = self.stroke_ext_lookup[name]

    # Convert from idx->value to name->value
    name_to_value = dict( (name, self.extension[idx])
                          for (name, idx) in self.stroke_ext_lookup.iteritems() )
    del name_to_value[name]

    bit, exttype = STROKE_EXTENSION_BY_NAME[name]
    self.stroke_mask &= ~bit
    _, self.stroke_ext_writer, self.stroke_ext_lookup = \
        _make_stroke_ext_reader(self.stroke_mask)

    # Convert back to idx->value
    self.extension = [None] * len(self.stroke_ext_lookup)
    for (name, idx) in self.stroke_ext_lookup.iteritems():
      self.extension[idx] = name_to_value[name]

  def has_cp_extension(self, name):
    """Returns true if control points in this stroke have the requested extension data.
    All control points in a stroke are guaranteed to use the same set of extensions.

    The current control point extensions are:
      timestamp         In seconds
      pressure          From 0 to 1"""
    return name in self.cp_ext_lookup

  def get_cp_extension(self, cp, name):
    """Returns the requested extension data, or raises LookupError if it doesn't exist."""
    idx = self.cp_ext_lookup[name]
    return cp.extension[idx]

  def _write(self, b):
    b.pack("<i", self.brush_idx)
    b.pack("<4f", *self.brush_color)
    b.pack("<fII", self.brush_size, self.stroke_mask, self.cp_mask)
    self.stroke_ext_writer(b, self.extension)
    b.pack("<i", len(self.controlpoints))
    for cp in self.controlpoints:
      cp._write(b, self.cp_ext_writer)


class ControlPoint(object):
  """Data for a single control point from a stroke. Attributes:
    .position    Position as 3 floats. Units are decimeters.
    .orientation Orientation of controller as a quaternion (x, y, z, w)."""
  @classmethod
  def from_file(cls, b, cp_ext_reader):
    # b is a binfile instance
    # reader reads controlpoint extension data from the binfile
    inst = cls()
    inst.position = list(b.unpack("<3f"))
    inst.orientation = list(b.unpack("<4f"))
    inst.extension = cp_ext_reader(b)
    return inst

  def clone(self):
    inst = self.__class__()
    for attr in ('position', 'orientation', 'extension'):
      setattr(inst, attr, list(getattr(self, attr)))
    return inst

  def _write(self, b, cp_ext_writer):
    p = self.position; o = self.orientation
    b.pack("<7f", p[0], p[1], p[2], o[0], o[1], o[2], o[3])
    cp_ext_writer(b, self.extension)
