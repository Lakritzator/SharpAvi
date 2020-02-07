﻿using System;
using System.Diagnostics.Contracts;

namespace SharpAvi.Codecs
{
    /// <summary>
    /// Encoder of audio streams.
    /// </summary>
    [ContractClass(typeof(Contracts.AudioEncoderContract))]
    public interface IAudioEncoder
    {
        /// <summary>
        /// Number of channels in encoded audio.
        /// </summary>
        int ChannelCount { get; }

        /// <summary>
        /// Sample rate of encoded audio, in samples per second.
        /// </summary>
        int SamplesPerSecond { get; }

        /// <summary>
        /// Number of bits per sample per single channel in encoded audio (usually 8 or 16).
        /// </summary>
        int BitsPerSample { get; }

        /// <summary>
        /// Format of encoded audio.
        /// </summary>
        short Format { get; }

        /// <summary>
        /// Byte rate of encoded audio, in bytes per second.
        /// </summary>
        int BytesPerSecond { get; }

        /// <summary>
        /// Size in bytes of minimum item of encoded data.
        /// </summary>
        /// <remarks>
        /// Corresponds to <c>nBlockAlign</c> field of <c>WAVEFORMATEX</c> structure.
        /// </remarks>
        int Granularity { get; }

        /// <summary>
        /// Extra data defined by a specific format which should be added to the stream header.
        /// </summary>
        /// <remarks>
        /// Contains data of specific structure like <c>MPEGLAYER3WAVEFORMAT</c> that follow
        /// common <c>WAVEFORMATEX</c> field.
        /// </remarks>
        byte[] FormatSpecificData { get; }

        /// <summary>
        /// Gets the maximum number of bytes in encoded data for a given number of source bytes.
        /// </summary>
        /// <param name="sourceCount">Number of source bytes. Specify <c>0</c> for a flush buffer size.</param>
        /// <seealso cref="EncodeBlock"/>
        /// <seealso cref="Flush"/>
        int GetMaxEncodedLength(int sourceCount);

        /// <summary>
        /// Encodes block of audio data.
        /// </summary>
        /// <param name="source">Buffer with audio data.</param>
        /// <param name="destination">Buffer for encoded audio data.</param>
        /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
        /// <seealso cref="GetMaxEncodedLength"/>
        int EncodeBlock(Memory<byte> source, Memory<byte> destination);

        /// <summary>
        /// Flushes internal encoder buffers if any.
        /// </summary>
        /// <param name="destination">Buffer for encoded audio data.</param>
        /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
        /// <seealso cref="GetMaxEncodedLength"/>
        int Flush(Memory<byte> destination);
    }


    namespace Contracts
    {
        [ContractClassFor(typeof(IAudioEncoder))]
        internal abstract class AudioEncoderContract : IAudioEncoder
        {
            public int ChannelCount
            {
                get
                {
                    Contract.Ensures(Contract.Result<int>() > 0);
                    throw new NotImplementedException();
                }
            }

            public int SamplesPerSecond
            {
                get
                {
                    Contract.Ensures(Contract.Result<int>() > 0);
                    throw new NotImplementedException();
                }
            }

            public int BitsPerSample
            {
                get {
                    Contract.Ensures(Contract.Result<int>() > 0);
                    throw new NotImplementedException();
                }
            }

            public short Format => throw new NotImplementedException();

            public int BytesPerSecond
            {
                get
                {
                    Contract.Ensures(Contract.Result<int>() > 0);
                    throw new NotImplementedException();
                }
            }

            public int Granularity
            {
                get
                {
                    Contract.Ensures(Contract.Result<int>() > 0);
                    throw new NotImplementedException();
                }
            }

            public byte[] FormatSpecificData => throw new NotImplementedException();

            public int GetMaxEncodedLength(int sourceCount)
            {
                Contract.Requires(sourceCount >= 0);
                Contract.Ensures(Contract.Result<int>() >= 0);
                throw new NotImplementedException();
            }

            public int EncodeBlock(Memory<byte> source, Memory<byte> dest)
            {
                Contract.Requires(!source.IsEmpty);
                Contract.Requires(!dest.IsEmpty);
                Contract.Ensures(Contract.Result<int>() >= 0);
                throw new NotImplementedException();
            }

            public int Flush(Memory<byte> dest)
            {
                Contract.Requires(!dest.IsEmpty);
                Contract.Ensures(Contract.Result<int>() >= 0);
                throw new NotImplementedException();
            }
        }

    }
}
