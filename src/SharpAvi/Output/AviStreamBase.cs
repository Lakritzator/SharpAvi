using System;
using System.Diagnostics.Contracts;

namespace SharpAvi.Output
{
    internal abstract class AviStreamBase : IAviStream, IAviStreamInternal
    {
        private bool _isFrozen;
        private string _name;
        private FourCC _chunkId;

        protected AviStreamBase(int index)
        {
            Contract.Requires(index >= 0);

            Index = index;
        }

        public int Index { get; private set; }

        public string Name
        {
            get { return _name; }
            set
            {
                CheckNotFrozen();
                _name = value;
            }
        }

        public abstract FourCC StreamType { get; }

        public FourCC ChunkId
        {
            get 
            { 
                if (!_isFrozen)
                {
                    throw new InvalidOperationException("Chunk ID is not defined until the stream is frozen.");
                }

                return _chunkId; 
            }
        }

        public abstract void WriteHeader();

        public abstract void WriteFormat();

        /// <summary>
        /// Prepares the stream for writing.
        /// </summary>
        /// <remarks>
        /// Default implementation freezes properties of the stream (further modifications are not allowed).
        /// </remarks>
        public virtual void PrepareForWriting()
        {
            if (_isFrozen)
            {
                return;
            }

            _isFrozen = true;

            _chunkId = GenerateChunkId();
        }

        /// <summary>
        /// Performs actions before closing the stream.
        /// </summary>
        /// <remarks>
        /// Default implementation does nothing.
        /// </remarks>
        public virtual void FinishWriting()
        {
        }


        protected abstract FourCC GenerateChunkId();

        protected void CheckNotFrozen()
        {
            if (_isFrozen)
            {
                throw new InvalidOperationException("No stream information can be changed after starting to write frames.");
            }
        }
    }
}
