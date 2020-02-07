using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace SharpAvi.Output
{
    /// <summary>
    /// Adds asynchronous writes support for underlying stream.
    /// </summary>
    internal class AsyncAudioStreamWrapper : AudioStreamWrapperBase
    {
        private readonly SequentialInvoker _writeInvoker = new SequentialInvoker();

        public AsyncAudioStreamWrapper(IAviAudioStreamInternal baseStream)
            : base(baseStream)
        {
            Contract.Requires(baseStream != null);
        }

        public override void WriteBlock(Memory<byte> data)
        {
            _writeInvoker.Invoke(() => base.WriteBlock(data));
        }

        public override Task WriteBlockAsync(Memory<byte> data)
        {
            return _writeInvoker.InvokeAsync(() => base.WriteBlock(data));
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
