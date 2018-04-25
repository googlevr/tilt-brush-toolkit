#!/usr/bin/env python

import os
import pprint
import shutil
import sys

from tiltbrush import tilt

def destroy(filename):
  try:
    os.unlink(filename)
  except OSError:
    pass


def increment_timestamp(stroke, increment):
  """Adds *increment* to all control points in stroke."""
  timestamp_idx = stroke.cp_ext_lookup['timestamp']
  for cp in stroke.controlpoints:
    cp.extension[timestamp_idx] += increment


def merge_metadata_from_tilt(tilt_dest, tilt_source):
  """Merges data from tilt_source into tilt_dest:
  - BrushIndex
  - ModelIndex
  - ImageIndex"""
  with tilt_dest.mutable_metadata() as md:
    to_append = set(tilt_source.metadata['BrushIndex']) - set(md['BrushIndex'])
    md['BrushIndex'].extend(sorted(to_append))

  if 'ImageIndex' in tilt_source.metadata:
    tilt_dest.metadata['ImageIndex'] = tilt_dest.metadata.get('ImageIndex', []) + \
                                       tilt_source.metadata['ImageIndex']

  if 'ModelIndex' in tilt_source.metadata:
    tilt_dest.metadata['ModelIndex'] = tilt_dest.metadata.get('ModelIndex', []) + \
                                       tilt_source.metadata['ModelIndex']


def concatenate(file_1, file_2, file_out):
  """Concatenate two .tilt files.
  file_out may be the same as one of the input files."""
  file_tmp = file_out + "__tmp"
  destroy(file_tmp)
  shutil.copyfile(file_1, file_tmp)
  tilt_out = tilt.Tilt(file_tmp)
  tilt_2 = tilt.Tilt(file_2)

  merge_metadata_from_tilt(tilt_out, tilt_2)

  tilt_out._guid_to_idx = dict(
    (guid, index)
    for (index, guid) in enumerate(tilt_out.metadata['BrushIndex']))

  final_stroke = tilt_out.sketch.strokes[-1]
  final_timestamp = final_stroke.get_cp_extension(final_stroke.controlpoints[-1], 'timestamp')
  timestamp_offset = final_timestamp + .03

  for stroke in tilt_2.sketch.strokes:
    copy = stroke.clone()

    # Convert brush index to one that works for tilt_out
    stroke_guid = tilt_2.metadata['BrushIndex'][stroke.brush_idx]
    copy.brush_idx = tilt_out._guid_to_idx[stroke_guid]
    tilt_out.sketch.strokes.append(copy)

    # Adjust timestamps to keep stroke times from overlapping.
    increment_timestamp(stroke, timestamp_offset)

  tilt_out.write_sketch()
  destroy(file_out)
  os.rename(file_tmp, file_out)


def main():
  import argparse
  parser = argparse.ArgumentParser(
    usage='%(prog)s -f FILE1 -f FILE2 ... -o OUTPUT_FILE'
  )
  parser.add_argument('-f', dest='files', metavar='FILE', action='append',
                      required=True,
                      help='A file to concatenate. May pass multiple times')
  parser.add_argument('-o', metavar='OUTPUT_FILE', dest='output_file',
                      required=True,
                      help='The name of the output file')
  args = parser.parse_args()
  if len(args.files) < 2:
    parser.error("Pass at least two files")

  concatenate(args.files[0], args.files[1], args.output_file)
  for filename in args.files[2:]:
    concatenate(args.output_file, filename, args.output_file)
  print "Wrote", args.output_file


if __name__ == '__main__':
  main()
