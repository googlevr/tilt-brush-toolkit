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

"""Converts a .tilt file from packed format to unpacked format,
and vice versa. Applies sanity checks when packing."""

from cStringIO import StringIO
import os
import sys
import struct
import zipfile

__all__ = ('ConversionError', 'convert_zip_to_dir', 'convert_dir_to_zip')

HEADER_FMT = '<4sHH'
HEADER_V1_FMT = HEADER_FMT + 'II'

STANDARD_FILE_ORDER = [
  'header.bin',
  'thumbnail.png',
  'metadata.json',
  'main.json',
  'data.sketch'
]
STANDARD_FILE_ORDER = dict( (n,i) for (i,n) in enumerate(STANDARD_FILE_ORDER) )

class ConversionError(Exception):
  """An error occurred in the zip <-> directory conversion process"""
  pass


def _destroy(file_or_dir):
  """Ensure that *file_or_dir* does not exist in the filesystem,
  deleting it if necessary."""
  import stat
  if os.path.isfile(file_or_dir):
    os.chmod(file_or_dir, stat.S_IWRITE)
    os.unlink(file_or_dir)
  elif os.path.isdir(file_or_dir):
    import shutil, stat
    for r,ds,fs in os.walk(file_or_dir, topdown=False):
      for f in fs:
        os.chmod(os.path.join(r, f), stat.S_IWRITE)
        os.unlink(os.path.join(r, f))
      for d in ds:
        os.rmdir(os.path.join(r, d))
    os.rmdir(file_or_dir)
  if os.path.exists(file_or_dir):
    raise Exception("'%s' is not empty" % file_or_dir)


def _read_and_check_header(inf):
  """Returns header bytes, or raise ConversionError if header looks invalid."""
  base_bytes = inf.read(struct.calcsize(HEADER_FMT))
  try:
    (sentinel, headerSize, headerVersion) = struct.unpack(HEADER_FMT, base_bytes)
  except struct.error as e:
    raise ConversionError("Unexpected header error: %s" % (e,))

  if sentinel != 'tilT':
    raise ConversionError("Sentinel looks weird: %r" % sentinel)

  more = headerSize - len(base_bytes)
  if more < 0:
    raise ConversionError("Strange header size %s" % headerSize)

  more_bytes = inf.read(more)
  if len(more_bytes) < more:
    raise ConversionError("Bad header size (claim %s, actual %s)" % (more, len(more_bytes)))

  zip_sentinel = inf.read(4)
  if zip_sentinel != '' and zip_sentinel != 'PK\x03\x04':
    raise ConversionError("Don't see zip sentinel after header: %r" % (zip_sentinel,))

  if headerVersion != 1:
    raise ConversionError("Bogus version %s" % headerVersion)
  return base_bytes + more_bytes


def convert_zip_to_dir(in_name):
  """Returns True if compression was used"""
  with file(in_name, 'rb') as inf:
    header_bytes = _read_and_check_header(inf)

  compression = False
  out_name = in_name + '._part'
  if os.path.exists(out_name):
    raise ConversionError("Remove %s first" % out_name)

  try:
    os.makedirs(out_name)

    with zipfile.ZipFile(in_name) as zf:
      for member in zf.infolist():
        if member.compress_size != member.file_size:
          compression = True
        zf.extract(member, out_name)
    with file(os.path.join(out_name, 'header.bin'), 'wb') as outf:
      outf.write(header_bytes)

    tmp = in_name + '._prev'
    os.rename(in_name, tmp)
    os.rename(out_name, in_name)
    _destroy(tmp)

    return compression
  finally:
    _destroy(out_name)


def convert_dir_to_zip(in_name, compress):
  in_name = os.path.normpath(in_name)  # remove trailing '/' if any
  out_name = in_name + '.part'
  if os.path.exists(out_name):
    raise ConversionError("Remove %s first" % out_name)
  
  def by_standard_order(filename):
    lfile = filename.lower()
    try:
      idx = STANDARD_FILE_ORDER[lfile]
    except KeyError:
      raise ConversionError("Unknown file %s; this is probably not a .tilt" % filename)
    return (idx, lfile)

  # Make sure metadata.json looks like valid utf-8 (rather than latin-1
  # or something else that will cause mojibake)
  try:
    with file(os.path.join(in_name, 'metadata.json')) as inf:
      import json
      json.load(inf)
  except IOError as e:
    raise ConversionError("Cannot validate metadata.json: %s" % e)
  except UnicodeDecodeError as e:
    raise ConversionError("metadata.json is not valid utf-8: %s" % e)
  except ValueError as e:
    raise ConversionError("metadata.json is not valid json: %s" % e)

  compression = zipfile.ZIP_DEFLATED if compress else zipfile.ZIP_STORED
  try:
    header_bytes = None

    zipf = StringIO()
    with zipfile.ZipFile(zipf, 'a', compression, False) as zf:
      for (r, ds, fs) in os.walk(in_name):
        fs.sort(key=by_standard_order)
        for f in fs:
          fullf = os.path.join(r, f)
          if f == 'header.bin':
            header_bytes = file(fullf).read()
            continue
          arcname = fullf[len(in_name)+1:]
          zf.write(fullf, arcname, compression)

    if header_bytes is None:
      print "Missing header; using default"
      header_bytes = struct.pack(HEADER_V1_FMT, 'tilT', struct.calcsize(HEADER_V1_FMT), 1, 0, 0)

    if not _read_and_check_header(StringIO(header_bytes)):
      raise ConversionError("Invalid header.bin")

    with file(out_name, 'wb') as outf:
      outf.write(header_bytes)
      outf.write(zipf.getvalue())

    tmp = in_name + '._prev'
    os.rename(in_name, tmp)
    os.rename(out_name, in_name)
    _destroy(tmp)

  finally:
    _destroy(out_name)
