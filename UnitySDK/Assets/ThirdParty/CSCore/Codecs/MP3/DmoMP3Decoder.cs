﻿using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CSCore.DMO;
using CSCore.Tags.ID3;

namespace CSCore.Codecs.MP3
{
    /// <summary>
    /// DirectX Media Object MP3 Decoder wrapper.
    /// </summary>
    public class DmoMp3Decoder : DmoStream
    {
        private Stream _stream;

        private DmoMP3DecoderObject _comObj;
        private FrameInfoCollection _frameInfoCollection;
        private Mp3Format _inputFormat;

        private long _position;
        private readonly bool _canSeek;

        private readonly bool _closeStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="DmoMp3Decoder"/> class.
        /// </summary>
        /// <param name="filename">File which contains raw MP3 data.</param>
        public DmoMp3Decoder(string filename)
            : this(File.OpenRead(filename))
        {
            _closeStream = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DmoMp3Decoder"/> class.
        /// </summary>
        /// <param name="stream">Stream which contains raw MP3 data.</param>
        public DmoMp3Decoder(Stream stream)
            : this(stream, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DmoMp3Decoder"/> class.
        /// </summary>
        /// <param name="stream">Stream wich contains raw MP3 data.</param>
        /// <param name="enableSeeking">Bool defining if seek should be used on the MP3 stream.</param>
        public DmoMp3Decoder(Stream stream, bool enableSeeking)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (!stream.CanRead)
                throw new ArgumentException("Stream is not readable.", "stream");
            if (!stream.CanSeek && enableSeeking)
                throw new ArgumentException("Stream is not seekable.", "stream");

            _closeStream = true;
            _stream = stream;
            _canSeek = enableSeeking;

            ParseForMp3Frames(stream, enableSeeking);
            Initialize();
        }

        /// <summary>
        /// Gets or sets the position of the <see cref="DmoMp3Decoder"/> in bytes.
        /// </summary>
        public override long Position
        {
            get { return _position; }
            [MethodImpl(MethodImplOptions.Synchronized)] 
            set { SetPosition(value); }
        }

        /// <summary>
        /// Gets the length of the <see cref="DmoMp3Decoder"/> in bytes.
        /// </summary>
        public override long Length
        {
            get { return _frameInfoCollection.TotalSamples * WaveFormat.BytesPerBlock; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="IAudioSource"/> supports seeking.
        /// </summary>
        public override bool CanSeek
        {
            get { return _canSeek; }
        }

        private void ParseForMp3Frames(Stream stream, bool enableSeeking)
        {
            Mp3Frame frame = null;
            long offsetOfFirstFrame = 0;
            stream = new BufferedStream(stream);

            if (enableSeeking)
            {
                while (ID3v2.SkipTag(stream))
                {
                    /* skip all id3 tags (see https://github.com/filoe/cscore/issues/63)
                 * there are some files with multiple id3v2 tags
                 * not sure whether this is according to the id3 specification but we have to handle it anyway
                 * as long as the SkipTag method returns true, another id3 tag has been found
                 */
                }
            }

            while (frame == null)
            {
                if (enableSeeking && stream.IsEndOfStream())
                    break;
                
                if(enableSeeking)
                    offsetOfFirstFrame = stream.Position;
                frame = Mp3Frame.FromStream(stream);
            }

            if (frame == null)
                throw new Exception("Could not find any MP3-Frames in the stream.");

            if (enableSeeking)
            {
                XingHeader xingHeader = XingHeader.FromFrame(frame);
                if (xingHeader != null)
                    offsetOfFirstFrame = stream.Position;
            }
            _inputFormat = new Mp3Format(frame.SampleRate, frame.ChannelCount, frame.FrameLength, frame.BitRate);

            //Prescan stream
            if (enableSeeking)
            {
                _frameInfoCollection = new FrameInfoCollection();
                while (_frameInfoCollection.AddFromMp3Stream(stream))
                {
                }

                stream.Position = offsetOfFirstFrame;
            }
        }

        /// <summary>
        ///     Reads a sequence of bytes from the stream.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the read bytes.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the stream</param>
        /// <returns>The actual number of read bytes.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = base.Read(buffer, offset, count);
            _position += read;
            return read;
        }

        /// <summary>
        /// Returns a <see cref="MediaObject"/> to decode the mp3 data.
        /// </summary>
        /// <param name="inputFormat">Format of the mp3 data to decode.</param>
        /// <param name="outputFormat">Output format.</param>
        /// <returns><see cref="MediaObject"/> to decode the mp3 data.</returns>
        protected override MediaObject CreateMediaObject(WaveFormat inputFormat, WaveFormat outputFormat)
        {
            //this can be experimental. We maybe have to switch to the CoCreateInstance method.
            //IntPtr ptr = IntPtr.Zero;
            //NativeMethods.CoCreateInstance(new Guid("bbeea841-0a63-4f52-a7ab-a9b3a84ed38a"), IntPtr.Zero,
            //    NativeMethods.CLSCTX.CLSCTX_INPROC_SERVER, typeof (MediaObject).GUID, out ptr);

            _comObj = new DmoMP3DecoderObject();
            var ptr = Marshal.GetComInterfaceForObject(_comObj, typeof (IMediaObject));
            try
            {
                return new MediaObject(ptr);
            }
            catch (Exception)
            {
                Marshal.Release(ptr);
                Marshal.ReleaseComObject(_comObj);
                throw;
            }
        }

        /// <summary>
        /// Returns the input format.
        /// </summary>
        /// <returns>Input format.</returns>
        protected override WaveFormat GetInputFormat()
        {
            return _inputFormat;
        }

        /// <summary>
        /// Returns the output format.
        /// </summary>
        /// <returns>Output format.</returns>
        protected override WaveFormat GetOutputFormat()
        {
            return new WaveFormat(_inputFormat.SampleRate, 16, _inputFormat.Channels);
        }

        /// <summary>
        /// Gets raw mp3 data to decode.
        /// </summary>
        /// <param name="inputDataBuffer">Byte array which will hold the raw mp3 data to decode.</param>
        /// <param name="requested">Number of requested bytes.</param>
        /// <returns>Total amount of read bytes.</returns>
        protected override int GetInputData(ref byte[] inputDataBuffer, int requested)
        {
            Mp3Frame frame = ReadNextMP3Frame(ref inputDataBuffer);
            if (frame == null)
            {
                inputDataBuffer = new byte[0];
                return 0;
            }

            if (inputDataBuffer.Length > frame.FrameLength)
                Array.Clear(inputDataBuffer, frame.FrameLength, inputDataBuffer.Length - frame.FrameLength);

            return frame.FrameLength;
        }

        private void SetPosition(long value)
        {
            value = Math.Max(Math.Min(value, Length), 0);
            value -= (value % WaveFormat.BlockAlign);

            for (int i = 0; i < _frameInfoCollection.Count; i++)
            {
                if (value / WaveFormat.BytesPerBlock <= _frameInfoCollection[i].SampleIndex ||
                    i == _frameInfoCollection.Count - 1)
                {
                    int index = i;
                    if (value / WaveFormat.BytesPerBlock < _frameInfoCollection[i].SampleIndex)
                        index = Math.Max(0, i - 1);

                    _stream.Position = _frameInfoCollection[index].StreamPosition;
                    _frameInfoCollection.PlaybackIndex = index;

                    _position = _frameInfoCollection[index].SampleIndex * WaveFormat.BlockAlign;

                    ResetOverflowBuffer();
                    int diff = (int) (value - _position);
                    diff -= diff % WaveFormat.BlockAlign;
                    if (diff > 0)
                    {
                        this.ReadBytes(diff);
                    }

                    break;
                }
            }
        }

// ReSharper disable once InconsistentNaming
        private Mp3Frame ReadNextMP3Frame(ref byte[] frameBuffer)
        {
            Mp3Frame frame = Mp3Frame.FromStream(_stream, ref frameBuffer);
            if (frame != null && _frameInfoCollection != null)
                _frameInfoCollection.PlaybackIndex++;

            return frame;
        }

        /// <summary>
        /// Disposes the <see cref="DmoMp3Decoder"/>.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_comObj != null)
            {
                Marshal.ReleaseComObject(_comObj);
                _comObj = null;
            }
            if (_frameInfoCollection != null)
            {
                _frameInfoCollection.Dispose();
                _frameInfoCollection = null;
            }
            if (_stream != null && _closeStream)
            {
                _stream.Dispose();
                _stream = null;
            }
        }

        [ComImport]
        [Guid("bbeea841-0a63-4f52-a7ab-a9b3a84ed38a")]
        private sealed class DmoMP3DecoderObject
        {
        }
    }
}