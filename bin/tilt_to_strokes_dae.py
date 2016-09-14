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

import os
import sys
import collections
import xml.etree.ElementTree as ET

try:
  sys.path.append(os.path.join(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__))), 'Python'))
  from tiltbrush.tilt import Tilt
except ImportError:
  print >>sys.stderr, "Please put the 'Python' directory in your PYTHONPATH"
  sys.exit(1)


def Element(tag, children=None, text=None, **attribs):
  """Wrapper around ET.Element that makes adding children and text easier"""
  child = ET.Element(tag, **attribs)
  if text is not None:
    child.text = text
  if children is not None:
    child.extend(children)
  return child


def _indent(elem, level=0):
  """Pretty-print indent an ElementTree.Element instance"""
  i = "\n" + level*"\t"
  if len(elem):
    if not elem.text or not elem.text.strip():
      elem.text = i + "\t"
    if not elem.tail or not elem.tail.strip():
      elem.tail = i
    for elem in elem:
      _indent(elem, level+1)
    if not elem.tail or not elem.tail.strip():
      elem.tail = i
  else:
    if level and (not elem.tail or not elem.tail.strip()):
      elem.tail = i


class ColladaFile(object):
  def __init__(self):
    self.next_ids = collections.defaultdict(int)

    self.root = ET.Element(
      'COLLADA',
      xmlns="http://www.collada.org/2008/03/COLLADASchema",
      version="1.5.0")
    self.tree = ET.ElementTree(self.root)
    self._init_asset()
    self.library_effects = ET.SubElement(self.root, 'library_effects')
    self.library_materials = ET.SubElement(self.root, 'library_materials')
    self.library_geometries = ET.SubElement(self.root, 'library_geometries')
    self.library_visual_scenes = ET.SubElement(self.root, 'library_visual_scenes')

    self.material = self._init_material()
    self.visual_scene = self._init_scene()

  def _init_asset(self):
    import datetime
    now = datetime.datetime.now()
    self.root.append(
      Element('asset', children=[
        Element('contributor', children=[
          Element('authoring_tool', text='Tilt Brush COLLADA stroke converter')
        ]),
        Element('created', text=now.isoformat()),
        Element('modified', text=now.isoformat()),
        Element('unit', meter='.1', name='decimeter'),
        Element('up_axis', text='Y_UP')
      ])
    )
  
  def _init_material(self):
    effect = ET.SubElement(self.library_effects, 'effect', id=self.make_id('effect_'))
    effect.append(
      Element('profile_COMMON', children=[
        Element('technique', sid='COMMON', children=[
          Element('blinn', children=[
            Element('diffuse', children=[
              Element('color', text='0.8 0.8 0.8 1'),
            ]),
            Element('specular', children=[
              Element('color', text='0.2 0.2 0.2 1'),
            ]),
            Element('shininess', children=[Element('float', text='0.5')])
          ])
        ])
      ])
    )
    material = ET.SubElement(
      self.library_materials, 'material', id=self.make_id('material_'),
      name="Mat")
    ET.SubElement(material, 'instance_effect', url='#' + effect.get('id'))
    return material

  def _init_scene(self):
    visual_scene = ET.SubElement(self.library_visual_scenes, 'visual_scene',
                                 id=self.make_id('scene_'))
    self.root.append(
      Element('scene', children=[
        Element('instance_visual_scene', url='#' + visual_scene.get('id'))
      ])
    )
    return visual_scene

  def make_id(self, prefix='ID'):
    val = self.next_ids[prefix]
    self.next_ids[prefix] += 1
    new_id = prefix + str(val)
    return new_id

  def write(self, filename):
    header = '<?xml version="1.0" encoding="UTF-8"?>\n'
    _indent(self.root)
    with file(filename, 'wb') as outf:
      outf.write(header)
      self.tree.write(outf)

  def add_stroke(self, stroke):
    geometry = self._add_stroke_geometry(stroke)
    self._add_stroke_node(geometry)

  def _add_stroke_geometry(self, stroke):
    def flatten(lst):
      for elt in lst:
        for subelt in elt:
          yield subelt
    def get_rh_positions(stroke):
      for cp in stroke.controlpoints:
        yield (-cp.position[0], cp.position[1], cp.position[2])

    def iter_positions(stroke):
      for cp in stroke.controlpoints:
        # Switch from left-handed (unity) to right-handed
        yield -cp.position[0]
        yield cp.position[1]
        yield cp.position[2]

    raw_floats = list(flatten(get_rh_positions(stroke)))

    assert len(raw_floats) % 3 == 0

    geom_id = self.make_id('stroke_')
    source_id = geom_id + '_src'
    floats_id = geom_id + '_fs'
    verts_id  = geom_id + '_vs'
    
    geometry = ET.SubElement(self.library_geometries, 'geometry', id=geom_id)
    geometry.append(
      Element('mesh', children=[
        Element('source', id=source_id, children=[
          Element('float_array', id=floats_id,
                  count=str(len(raw_floats)),
                  text=' '.join(map(str, raw_floats))),
          Element('technique_common', children=[
            Element('accessor',
                    count=str(len(raw_floats)/3), stride='3',
                    source='#' + floats_id,
                    children=[
                      Element('param', name='X', type='float'),
                      Element('param', name='Y', type='float'),
                      Element('param', name='Z', type='float')
                    ])
          ])
        ]),
        Element('vertices', id=verts_id, children=[
          Element('input', semantic='POSITION', source='#' + source_id)
        ]),
        Element('linestrips', count='1', material='Material1', children=[
          Element('input', offset='0', semantic='VERTEX', set='0', source='#' + verts_id),
          Element('p', text=' '.join(map(str, xrange(len(raw_floats) / 3))))
        ])
      ])
    )

    return geometry

  def _add_stroke_node(self, geometry):
    name = 'Spline.' + geometry.get('id')
    self.visual_scene.append(
      Element('node', id=self.make_id('node_'), name=name, children=[
        Element('instance_geometry', url='#' + geometry.get('id'), children=[
          Element('bind_material', children=[
            Element('technique_common', children=[
              Element('instance_material', symbol='Material1',
                      target='#' + self.material.get('id'),
                      children=[
                        Element('bind_vertex_input',
                                semantic='UVSET0',
                                input_semantic='TEXCOORD',
                                input_set='0')
                      ])
            ])
          ])
        ])
      ])
    )


def main(args):
  import argparse
  parser = argparse.ArgumentParser(description="Converts .tilt files to a Collada .dae containing spline data.")
  parser.add_argument('files', type=str, nargs='*', help="Files to convert to dae")
  args = parser.parse_args(args)

  for filename in args.files:
    t = Tilt(filename)
    outf_name = os.path.splitext(os.path.basename(filename))[0] + '.dae'

    dae = ColladaFile()
    for stroke in t.sketch.strokes:
      dae.add_stroke(stroke)

    dae.write(outf_name)
    print 'Wrote', outf_name


if __name__ == '__main__':
  main(sys.argv[1:])
