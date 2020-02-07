using System;

namespace SharpAvi.Codecs.Lame
{
    /// <summary>
    /// Interface is used to access the API of the LAME DLL.
    /// </summary>
    /// <remarks>
    /// Clients of <see cref="Mp3AudioEncoderLame"/> class need not to work with
    /// this interface directly.
    /// </remarks>
    public interface ILameFacade
    {
        /// <summary>
        /// Number of audio channels.
        /// </summary>
        int ChannelCount { get; set; }

        /// <summary>
        /// Sample rate of source audio data.
        /// </summary>
        int InputSampleRate { get; set; }

        /// <summary>
        /// Bit rate of encoded data.
        /// </summary>
        int OutputBitRate { get; set; }

        /// <summary>
        /// Sample rate of encoded data.
        /// </summary>
        int OutputSampleRate { get; }

        /// <summary>
        /// Frame size of encoded data.
        /// </summary>
        int FrameSize { get; }

        /// <summary>
        /// Encoder delay.
        /// </summary>
        int EncoderDelay { get; }

        /// <summary>
        /// Initializes the encoding process.
        /// </summary>
        void PrepareEncoding();

        /// <summary>
        /// Encodes a chunk of audio data.
        /// </summary>
        int Encode(Memory<byte> source, Memory<byte> dest);

        /// <summary>
        /// Finalizes the encoding process.
        /// </summary>
        int FinishEncoding(Memory<byte> dest);
    }
}
