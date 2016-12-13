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

import contextlib
import os
import shutil
import unittest

from tiltbrush.tilt import Tilt


@contextlib.contextmanager
def copy_of_tilt(tilt_file='data/sketch1.tilt', as_filename=False):
  """Returns a mutate-able copy of tilt_file, and removes it when done."""
  base = os.path.abspath(os.path.dirname(__file__))
  full_filename = os.path.join(base, tilt_file)
  tmp_filename = os.path.splitext(full_filename)[0] + '_tmp.tilt'
  shutil.copy(src=full_filename, dst=tmp_filename)
  try:
    if as_filename:
      yield tmp_filename
    else:
      yield Tilt(tmp_filename)
  finally:
    if os.path.exists(tmp_filename):
      os.unlink(tmp_filename)


def as_float32(f):
  import struct
  return struct.unpack('f', struct.pack('f', f))[0]


class TestTiltMutations(unittest.TestCase):
  def test_as_directory(self):
    # Test Tilt.as_directory
    with copy_of_tilt(as_filename=True) as tilt_filename:
      with Tilt.as_directory(tilt_filename):
        self.assertTrue(os.path.isdir(tilt_filename))
        self.assertTrue(os.path.exists(os.path.join(tilt_filename, 'metadata.json')))

  def test_can_mutate_metadata(self):
    import uuid
    random_guid = str(uuid.uuid4())
    with copy_of_tilt() as tilt:
      with tilt.mutable_metadata() as dct:
        # Check that they are different references
        dct['EnvironmentPreset'] = random_guid
        self.assertNotEqual(
          tilt.metadata['EnvironmentPreset'], dct['EnvironmentPreset'])
      # Check that it's copied back on exit from mutable_metadata
      self.assertEqual(tilt.metadata['EnvironmentPreset'], random_guid)
      # Check that the mutations persist
      tilt2 = Tilt(tilt.filename)
      self.assertEqual(tilt2.metadata['EnvironmentPreset'], random_guid)

  def test_can_del_sketch(self):
    # Test that "del tilt.sketch" forces it to re-load from disk
    with copy_of_tilt() as tilt:
      stroke = tilt.sketch.strokes[0]
      del tilt.sketch
      stroke2 = tilt.sketch.strokes[0]
      assert stroke is not stroke2
    
  def test_mutate_control_point(self):
    # Test that control point mutations are saved
    with copy_of_tilt() as tilt:
      stroke = tilt.sketch.strokes[0]
      new_y = as_float32(stroke.controlpoints[0].position[1] + 3)
      stroke.controlpoints[0].position[1] = new_y
      tilt.write_sketch()
      del tilt.sketch
      self.assertEqual(tilt.sketch.strokes[0].controlpoints[0].position[1], new_y)

  def test_stroke_extension(self):
    # Test that control point extensions can be added and removed
    with copy_of_tilt() as tilt:
      stroke = tilt.sketch.strokes[0]
      # This sketch was made before stroke scale was a thing
      self.assertEqual(stroke.flags, 0)
      self.assertRaises(AttributeError, (lambda: stroke.scale))
      # Test adding some extension data
      stroke.scale = 1.25
      self.assertEqual(stroke.scale, 1.25)
      # Test removing extension data
      del stroke.flags
      self.assertRaises(AttributeError (lambda: stroke.flags))
      # Test that the changes survive a save+load
      tilt.write_sketch()
      stroke2 = Tilt(tilt.filename).sketch.strokes[0]
      self.assertEqual(stroke2.scale, 1.25)
      self.assertRaises(AttributeError (lambda: stroke2.flags))


if __name__ == '__main__':
  unittest.main()
