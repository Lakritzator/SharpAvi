using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace SharpAvi.Output
{
    /// <summary>
    /// Adds asynchronous writes support for underlying stream.
    /// </summary>
    internal class AsyncVideoStreamWrapper : VideoStreamWrapperBase
    {
        private readonly SequentialInvoker _writeInvoker = new SequentialInvoker();

        public AsyncVideoStreamWrapper(IAviVideoStreamInternal baseStream)
            : base(baseStream)
        {
            Contract.Requires(baseStream != null);
        }

        public override void WriteFrame(bool isKeyFrame, Memory<byte> frameData)
        {
            _writeInvoker.Invoke(() => base.WriteFrame(isKeyFrame, frameData));
        }

        public override Task WriteFrameAsync(bool isKeyFrame, Memory<byte> frameData)
        {
            return _writeInvoker.InvokeAsync(() => base.WriteFrame(isKeyFrame, frameData));
        }

        public override void FinishWriting()
        {
            // Perform all pending writes and then let the base stream to finish
            // (possibly writing some more data synchronously)
            _writeInvoker.WaitForPendingInvocations();

            base.FinishWriting();
        }
    }
}
