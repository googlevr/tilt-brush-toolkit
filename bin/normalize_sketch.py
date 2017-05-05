#!/usr/bin/env python

# Copyright 2017 Google Inc. All Rights Reserved.
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

"""This is sample Python 2.7 code that uses the tiltbrush.tilt module
to scale, rotate, and translate the sketch so that the resetting the
transform will bring you back to the initial size, orientation, and
position. But the environment size, orientation, and position will also
be reset."""

import os
import shutil
import sys

try:
  sys.path.append(os.path.join(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__))), 'Python'))
  from tiltbrush.tilt import Tilt
except ImportError:
  print >>sys.stderr, "Please put the 'Python' directory in your PYTHONPATH"
  sys.exit(1)

def _quaternion_multiply_quaternion(q0, q1):
  x0, y0, z0, w0 = q0
  x1, y1, z1, w1 = q1
  return [
    w0*x1 + x0*w1 + y0*z1 - z0*y1,
    w0*y1 + y0*w1 + z0*x1 - x0*z1,
    w0*z1 + z0*w1 + x0*y1 - y0*x1,
    w0*w1 - x0*x1 - y0*y1 - z0*z1]

def _quaternion_conjugate(q):
  x, y, z, w = q
  return [-x, -y, -z, w]

def _quaternion_multiply_vector(q, v):
  qv = v + [0]
  return _quaternion_multiply_quaternion(_quaternion_multiply_quaternion(q, qv), _quaternion_conjugate(q))[:3]

def _transform_point(scene_translation, scene_rotation, scene_scale, pos):
  pos = [scene_scale * i for i in pos]
  pos = _quaternion_multiply_vector(scene_rotation, pos)
  pos = [i+j for i,j in zip(scene_translation, pos)]
  return pos

def _adjust_guide(scene_translation, scene_rotation, scene_scale, guide):
  guide[u'Extents'] = [scene_scale * b for b in guide[u'Extents']]
  _adjust_transform(scene_translation, scene_rotation, scene_scale, guide[u'Transform'])

def _adjust_transform(scene_translation, scene_rotation, scene_scale, transform):
  scaledTranslation = [scene_scale * b for b in transform[0]]
  rotatedTranslation = _quaternion_multiply_vector(scene_rotation, scaledTranslation)
  translatedTranslation = [b+a for a,b in zip(scene_translation, rotatedTranslation)]

  transform[0] = translatedTranslation
  transform[1] = _quaternion_multiply_quaternion(scene_rotation, transform[1])
  transform[2] = scene_scale * transform[2]

def normalize_tilt_file(tilt_file):
  scene_translation = tilt_file.metadata[u'SceneTransformInRoomSpace'][0]
  scene_rotation = tilt_file.metadata[u'SceneTransformInRoomSpace'][1]
  scene_scale = tilt_file.metadata[u'SceneTransformInRoomSpace'][2]

  # Normalize strokes
  for stroke in tilt_file.sketch.strokes:
    if stroke.has_stroke_extension('scale'):
      stroke.scale *= scene_scale
    else:
      stroke.scale = scene_scale
    for cp in stroke.controlpoints:
      pos = cp.position
      pos = _transform_point(scene_translation, scene_rotation, scene_scale, pos)
      cp.position = pos
      cp.orientation = _quaternion_multiply_quaternion(scene_rotation, cp.orientation)

  with tilt_file.mutable_metadata() as metadata:
    # Reset scene transform to be identity.
    metadata[u'SceneTransformInRoomSpace'][0] = [0., 0., 0.]
    metadata[u'SceneTransformInRoomSpace'][1] = [0., 0., 0., 1.]
    metadata[u'SceneTransformInRoomSpace'][2] = 1.

    # Adjust guide transforms to match.
    if u'GuideIndex' in metadata:
      for guide_type in metadata[u'GuideIndex']:
        for guide in guide_type[u'States']:
          _adjust_guide(scene_translation, scene_rotation, scene_scale, guide)

    # Adjust model transforms to match.
    if u'ModelIndex' in metadata:
      for model_type in metadata[u'ModelIndex']:
        for transform in model_type[u'Transforms']:
          _adjust_transform(scene_translation, scene_rotation, scene_scale, transform)

    # Adjust image transforms to match.
    if u'ImageIndex' in metadata:
      for image_type in metadata[u'ImageIndex']:
        for transform in image_type[u'Transforms']:
          _adjust_transform(scene_translation, scene_rotation, scene_scale, transform)

    # Adjust lights to match.
    if u'Lights' in metadata:
      metadata[u'Lights'][u'Shadow'][u'Orientation'] = _quaternion_multiply_quaternion(scene_rotation, metadata[u'Lights'][u'Shadow'][u'Orientation'])
      metadata[u'Lights'][u'NoShadow'][u'Orientation'] = _quaternion_multiply_quaternion(scene_rotation, metadata[u'Lights'][u'NoShadow'][u'Orientation'])

    # Adjust environment to match.
    if u'Environment' in metadata:
      metadata[u'Environment'][u'FogDensity'] /= scene_scale
      metadata[u'Environment'][u'GradientSkew'] = _quaternion_multiply_quaternion(scene_rotation, metadata[u'Environment'][u'GradientSkew'])

    # u'Mirror' and u'ThumbnailCameraTransformInRoomSpace' are in room space so don't need to be normalized.

def main():
  import argparse
  parser = argparse.ArgumentParser(description=
    "Create a normalized version of the sketch (with 'Normalized' appended to\
    the file name) which is scaled, rotated, and translated so that resetting the\
    transform will bring you back to the initial size, orientation, and position.\
    But the environment size, orientation, and position will also be reset.")
  parser.add_argument('files', type=str, nargs='+', help="Sketches to normalize")

  args = parser.parse_args()

  for filename in args.files:
    name, ext = os.path.splitext(filename)
    filename_normalized = name + 'Normalized' + ext
    shutil.copy(filename, filename_normalized)
    tilt_file = Tilt(filename_normalized)

    normalize_tilt_file(tilt_file)

    tilt_file.write_sketch()
    print 'WARNING: Environment position has changed in ' + filename + '.'

if __name__ == '__main__':
  main()
