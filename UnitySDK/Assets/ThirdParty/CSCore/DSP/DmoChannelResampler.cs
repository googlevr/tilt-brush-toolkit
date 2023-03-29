﻿using System;
using CSCore.DMO;

namespace CSCore.DSP
{
    /// <summary>
    ///     Resampler based on the <see cref="DmoResampler" /> which can change the number of channels based on a
    ///     <see cref="ChannelMatrix" />. Supported since Windows XP.
    /// </summary>
    public class DmoChannelResampler : DmoResampler
    {
        private readonly ChannelMatrix _channelMatrix;

        /// <summary>
        ///     Initializes a new instance of the <see cref="DmoChannelResampler" /> class.
        /// </summary>
        /// <param name="source">Underlying source which has to get resampled.</param>
        /// <param name="channelMatrix"><see cref="ChannelMatrix" /> which defines how to map each channel.</param>
        public DmoChannelResampler(IWaveSource source, ChannelMatrix channelMatrix)
            : this(source, channelMatrix, source.WaveFormat.SampleRate)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DmoChannelResampler"/> class.
        /// </summary>
        /// <param name="source">Underlying source which has to get resampled.</param>
        /// <param name="channelMatrix"><see cref="ChannelMatrix" /> which defines how to map each channel.</param>
        /// <param name="outputFormat">Waveformat, which specifies the new format. Note, that by far not all formats are supported.</param>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// channelMatrix
        /// or
        /// outputFormat
        /// </exception>
        /// <exception cref="System.ArgumentException">The number of channels of the source has to be equal to the number of input channels specified by the channelMatrix.</exception>
        public DmoChannelResampler(IWaveSource source, ChannelMatrix channelMatrix, WaveFormat outputFormat)
            : base(source, outputFormat)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (channelMatrix == null)
                throw new ArgumentNullException("channelMatrix");
            if(outputFormat == null)
                throw new ArgumentNullException("outputFormat");

            if (source.WaveFormat.Channels != channelMatrix.InputChannelCount)
            {
                throw new ArgumentException(
                    "The number of channels of the source has to be equal to the number of input channels specified by the channelMatrix.");
            }

            var inputFormat = new WaveFormatExtensible(
                source.WaveFormat.SampleRate,
                source.WaveFormat.BitsPerSample,
                source.WaveFormat.Channels,
                WaveFormatExtensible.SubTypeFromWaveFormat(source.WaveFormat),
                channelMatrix.InputMask);

            Outputformat = new WaveFormatExtensible(
                outputFormat.SampleRate,
                outputFormat.BitsPerSample,
                outputFormat.Channels,
                WaveFormatExtensible.SubTypeFromWaveFormat(outputFormat),
                channelMatrix.OutputMask);

            Initialize(inputFormat, Outputformat);
            _channelMatrix = channelMatrix;
            CommitChannelMatrixChanges();
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="DmoChannelResampler" /> class.
        /// </summary>
        /// <param name="source">Underlying source which has to get resampled.</param>
        /// <param name="channelMatrix"><see cref="ChannelMatrix" /> which defines how to map each channel.</param>
        /// <param name="destinationSampleRate">The destination sample rate.</param>
        public DmoChannelResampler(IWaveSource source, ChannelMatrix channelMatrix, int destinationSampleRate)
            : this(source, channelMatrix, GetOutputWaveFormat(source, destinationSampleRate, channelMatrix))
        {
        }

        private static WaveFormat GetOutputWaveFormat(IWaveSource source, int sampleRate, ChannelMatrix channelMatrix)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (channelMatrix == null)
                throw new ArgumentNullException("channelMatrix");

            var waveFormat = channelMatrix.BuildOutputWaveFormat(source);
            waveFormat.SampleRate = sampleRate;
            return waveFormat;
        }

        /// <summary>
        /// Gets the channel matrix.
        /// </summary>
        /// <remarks>If any changes to the channel matrix are made, use the <see cref="CommitChannelMatrixChanges"/> method to commit them.</remarks>
        public ChannelMatrix ChannelMatrix
        {
            get { return _channelMatrix; }
        }

        /// <summary>
        /// Commits all channel-matrix-changes.
        /// </summary>
        public void CommitChannelMatrixChanges()
        {
            using (Resampler.MediaObject.Lock())
            {
                Resampler.MediaObject.SetOutputType(0, Resampler.MediaObject.GetOutputCurrentType(0), SetTypeFlags.None);
                Resampler.ResamplerProps.SetUserChannelMtx(_channelMatrix.GetOneDimensionalMatrix());
            }
        }
    }
}