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

try:
  sys.path.append(os.path.join(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__))), 'Python'))
  import tiltbrush.unpack
except ImportError:
  print >>sys.stderr, "Please put the 'Python' directory in your PYTHONPATH"
  sys.exit(1)


def convert(in_name, compress):
  if os.path.isdir(in_name):
    tiltbrush.unpack.convert_dir_to_zip(in_name, compress)
    print "Converted %s to zip format" % in_name
  elif os.path.isfile(in_name):
    tiltbrush.unpack.convert_zip_to_dir(in_name)
    print "Converted %s to directory format" % in_name
  else:
    raise tiltbrush.unpack.ConversionError("%s doesn't exist" % in_name)


def main():
  import argparse
  parser = argparse.ArgumentParser(description="Converts .tilt files from packed format (zip) to unpacked format (directory), optionally applying compression.")
  parser.add_argument('files', type=str, nargs='+',
                      help="Files to convert to the other format")
  parser.add_argument('--compress', action='store_true',
                      help="Use compression (default: off)")
  args = parser.parse_args()
  for arg in args.files:
    try:
      convert(arg, args.compress)
    except tiltbrush.unpack.ConversionError as e:
      print "ERROR: %s" % e

if __name__ == '__main__':
  main()
