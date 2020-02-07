using System;

namespace SharpAvi.Output
{
    /// <summary>
    /// Interface of an object performing actual writing for the streams.
    /// </summary>
    internal interface IAviStreamWriteHandler
    {
        void WriteVideoFrame(AviVideoStream stream, bool isKeyFrame, Memory<byte> frameData);
        void WriteAudioBlock(AviAudioStream stream, Memory<byte> frameData);

        void WriteStreamHeader(AviVideoStream stream);
        void WriteStreamHeader(AviAudioStream stream);

        void WriteStreamFormat(AviVideoStream stream);
        void WriteStreamFormat(AviAudioStream stream);
    }
}
