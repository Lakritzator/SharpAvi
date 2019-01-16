using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using SharpAvi.Codecs;

namespace SharpAvi.Output
{
    /// <summary>
    /// Used to write an AVI file.
    /// </summary>
    /// <remarks>
    /// After writing begin to any of the streams, no property changes or stream addition are allowed.
    /// </remarks>
    public class AviWriter : IDisposable, IAviStreamWriteHandler
    {
        private const int MaxSuperIndexEntries = 256;
        private const int MaxIndexEntries = 15000;
        private const int Index1EntrySize = 4 * sizeof(uint);
        private const int RiffAviSizeThreshold = 512 * 1024 * 1024;
        private const int RiffAvixSizeThreshold = int.MaxValue - 1024 * 1024;

        private readonly BinaryWriter _fileWriter;
        private bool _isClosed;
        private bool _startedWriting;
        private readonly object _syncWrite = new object();

        private bool _isFirstRiff = true;
        private RiffItem _currentRiff;
        private RiffItem _currentMovie;
        private RiffItem _header;
        private int _riffSizeThreshold;
        private int _riffAviFrameCount = -1;
        private int _index1Count;

        private readonly List<IAviStreamInternal> _streams = new List<IAviStreamInternal>();
        private StreamInfo[] _streamsInfo;

        /// <summary>
        /// Creates a new instance of <see cref="AviWriter"/> for writing to a file.
        /// </summary>
        /// <param name="fileName">Path to an AVI file being written.</param>
        public AviWriter(string fileName)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));

            var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024);
            _fileWriter = new BinaryWriter(fileStream);
        }

        /// <summary>
        /// Creates a new instance of <see cref="AviWriter"/> for writing to a stream.
        /// </summary>
        /// <param name="stream">Stream being written to.</param>
        /// <param name="leaveOpen">Whether to leave the stream open when closing <see cref="AviWriter"/>.</param>
        public AviWriter(Stream stream, bool leaveOpen = false)
        {
            Contract.Requires(stream.CanWrite);
            Contract.Requires(stream.CanSeek);
            _fileWriter = new BinaryWriter(stream, Encoding.Default, leaveOpen);
        }

        /// <summary>Frame rate.</summary>
        /// <remarks>
        /// The value of the property is rounded to 3 fractional digits.
        /// Default value is <c>1</c>.
        /// </remarks>
        public decimal FramesPerSecond
        {
            get { return _framesPerSecond; }
            set
            {
                Contract.Requires(value > 0);

                lock (_syncWrite)
                {
                    CheckNotStartedWriting();
                    _framesPerSecond = decimal.Round(value, 3);
                }
            }
        }
        private decimal _framesPerSecond = 1;
        private uint _frameRateNumerator;
        private uint _frameRateDenominator;

        /// <summary>
        /// Whether to emit index used in AVI v1 format.
        /// </summary>
        /// <remarks>
        /// By default, only index conformant to OpenDML AVI extensions (AVI v2) is emitted. 
        /// Presence of v1 index may improve the compatibility of generated AVI files with certain software, 
        /// especially when there are multiple streams.
        /// </remarks>
        public bool EmitIndex1
        {
            get { return _emitIndex1; }
            set
            {
                lock (_syncWrite)
                {
                    CheckNotStartedWriting();
                    _emitIndex1 = value;
                }
            }
        }
        private bool _emitIndex1;

        /// <summary>AVI streams that have been added so far.</summary>
        public IReadOnlyList<IAviStream> Streams => _streams;

        /// <summary>Adds new video stream.</summary>
        /// <param name="width">Frame's width.</param>
        /// <param name="height">Frame's height.</param>
        /// <param name="bitsPerPixel">Bits per pixel.</param>
        /// <returns>Newly added video stream.</returns>
        /// <remarks>
        /// Stream is initialized to be ready for uncompressed video (bottom-up BGR) with specified parameters.
        /// However, properties (such as <see cref="IAviVideoStream.Codec"/>) can be changed later if the stream is
        /// to be fed with pre-compressed data.
        /// </remarks>
        public IAviVideoStream AddVideoStream(int width = 1, int height = 1, BitsPerPixel bitsPerPixel = BitsPerPixel.Bpp32)
        {
            Contract.Requires(width > 0);
            Contract.Requires(height > 0);
            Contract.Requires(Enum.IsDefined(typeof(BitsPerPixel), bitsPerPixel));
            Contract.Requires(Streams.Count < 100);
            Contract.Ensures(Contract.Result<IAviVideoStream>() != null);

            return AddStream<IAviVideoStreamInternal>(index => 
                {
                    var stream = new AviVideoStream(index, this, width, height, bitsPerPixel);
                    var asyncStream = new AsyncVideoStreamWrapper(stream);
                    return asyncStream;
                });
        }

        /// <summary>Adds new encoding video stream.</summary>
        /// <param name="encoder">Encoder to be used.</param>
        /// <param name="ownsEncoder">Whether encoder should be disposed with the writer.</param>
        /// <param name="width">Frame's width.</param>
        /// <param name="height">Frame's height.</param>
        /// <returns>Newly added video stream.</returns>
        /// <remarks>
        /// <para>
        /// Stream is initialized to be to be encoded with the specified encoder.
        /// Method <see cref="IAviVideoStream.WriteFrame"/> expects data in the same format as encoders,
        /// that is top-down BGR32 bitmap. It is passed to the encoder and the encoded result is written
        /// to the stream.
        /// Parameters <c>isKeyFrame</c> and <c>length</c> are ignored by encoding streams,
        /// as encoders determine on their own which frames are keys, and the size of input bitmaps is fixed.
        /// </para>
        /// <para>
        /// Properties <see cref="IAviVideoStream.Codec"/> and <see cref="IAviVideoStream.BitsPerPixel"/> 
        /// are defined by the encoder, and cannot be modified.
        /// </para>
        /// </remarks>
        public IAviVideoStream AddEncodingVideoStream(IVideoEncoder encoder, bool ownsEncoder = true, int width = 1, int height = 1)
        {
            Contract.Requires(encoder != null);
            Contract.Requires(Streams.Count < 100);
            Contract.Ensures(Contract.Result<IAviVideoStream>() != null);

            return AddStream<IAviVideoStreamInternal>(index => 
                {
                    var stream = new AviVideoStream(index, this, width, height, BitsPerPixel.Bpp32);
                    var encodingStream = new EncodingVideoStreamWrapper(stream, encoder, ownsEncoder);
                    var asyncStream = new AsyncVideoStreamWrapper(encodingStream);
                    return asyncStream;
                });
        }

        /// <summary>Adds new audio stream.</summary>
        /// <param name="channelCount">Number of channels.</param>
        /// <param name="samplesPerSecond">Sample rate.</param>
        /// <param name="bitsPerSample">Bits per sample (per single channel).</param>
        /// <returns>Newly added audio stream.</returns>
        /// <remarks>
        /// Stream is initialized to be ready for uncompressed audio (PCM) with specified parameters.
        /// However, properties (such as <see cref="IAviAudioStream.Format"/>) can be changed later if the stream is
        /// to be fed with pre-compressed data.
        /// </remarks>
        public IAviAudioStream AddAudioStream(int channelCount = 1, int samplesPerSecond = 44100, int bitsPerSample = 16)
        {
            Contract.Requires(channelCount > 0);
            Contract.Requires(samplesPerSecond > 0);
            Contract.Requires(bitsPerSample > 0 && (bitsPerSample % 8) == 0);
            Contract.Requires(Streams.Count < 100);
            Contract.Ensures(Contract.Result<IAviAudioStream>() != null);

            return AddStream<IAviAudioStreamInternal>(index => 
                {
                    var stream = new AviAudioStream(index, this, channelCount, samplesPerSecond, bitsPerSample);
                    var asyncStream = new AsyncAudioStreamWrapper(stream);
                    return asyncStream;
                });
        }

        /// <summary>Adds new encoding audio stream.</summary>
        /// <param name="encoder">Encoder to be used.</param>
        /// <param name="ownsEncoder">Whether encoder should be disposed with the writer.</param>
        /// <returns>Newly added audio stream.</returns>
        /// <remarks>
        /// <para>
        /// Stream is initialized to be to be encoded with the specified encoder.
        /// Method <see cref="IAviAudioStream.WriteBlock"/> expects data in the same format as encoder (see encoder's docs). 
        /// The data is passed to the encoder and the encoded result is written to the stream.
        /// </para>
        /// <para>
        /// The encoder defines the following properties of the stream:
        /// <see cref="IAviAudioStream.ChannelCount"/>, <see cref="IAviAudioStream.SamplesPerSecond"/>,
        /// <see cref="IAviAudioStream.BitsPerSample"/>, <see cref="IAviAudioStream.BytesPerSecond"/>,
        /// <see cref="IAviAudioStream.Granularity"/>, <see cref="IAviAudioStream.Format"/>,
        /// <see cref="IAviAudioStream.FormatSpecificData"/>.
        /// These properties cannot be modified.
        /// </para>
        /// </remarks>
        public IAviAudioStream AddEncodingAudioStream(IAudioEncoder encoder, bool ownsEncoder = true)
        {
            Contract.Requires(encoder != null);
            Contract.Requires(Streams.Count < 100);
            Contract.Ensures(Contract.Result<IAviAudioStream>() != null);

            return AddStream<IAviAudioStreamInternal>(index => 
                {
                    var stream = new AviAudioStream(index, this, 1, 44100, 16);
                    var encodingStream = new EncodingAudioStreamWrapper(stream, encoder, ownsEncoder);
                    var asyncStream = new AsyncAudioStreamWrapper(encodingStream);
                    return asyncStream;
                });
        }

        private TStream AddStream<TStream>(Func<int, TStream> streamFactory)
            where TStream : IAviStreamInternal
        {
            Contract.Requires(streamFactory != null);
            Contract.Requires(Streams.Count < 100);

            lock (_syncWrite)
            {
                CheckNotClosed();
                CheckNotStartedWriting();

                var stream = streamFactory.Invoke(Streams.Count);
                _streams.Add(stream);
                return stream;
            }
        }

        /// <summary>
        /// Closes the writer and AVI file itself.
        /// </summary>
        public void Close()
        {
            if (!_isClosed)
            {
                bool finishWriting;
                lock (_syncWrite)
                {
                    finishWriting = _startedWriting;
                }
                // Call FinishWriting without holding the lock
                // because additional writes may be performed inside
                if (finishWriting)
                {
                    foreach (var stream in _streams)
                    {
                        stream.FinishWriting();
                    }
                }

                lock (_syncWrite)
                {
                    if (_startedWriting)
                    {
                        foreach (var stream in _streams)
                        {
                            FlushStreamIndex(stream);
                        }

                        CloseCurrentRiff();

                        // Rewrite header with actual data like frames count, super index, etc.
                        _fileWriter.BaseStream.Position = _header.ItemStart;
                        WriteHeader();
                    }

                    _fileWriter.Close();
                    _isClosed = true;
                }

                foreach (var disposableStream in _streams.OfType<IDisposable>())
                {
                    disposableStream.Dispose();
                }
            }
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        private void CheckNotStartedWriting()
        {
            if (_startedWriting)
            {
                throw new InvalidOperationException("No stream information can be changed after starting to write frames.");
            }
        }

        private void CheckNotClosed()
        {
            if (_isClosed)
            {
                throw new ObjectDisposedException(typeof(AviWriter).Name);
            }
        }

        private void PrepareForWriting()
        {
            _startedWriting = true;
            foreach (var stream in _streams)
            {
                stream.PrepareForWriting();
            }
            AviUtils.SplitFrameRate(FramesPerSecond, out _frameRateNumerator, out _frameRateDenominator);

            _streamsInfo = _streams.Select(s => new StreamInfo(KnownFourCCs.Chunks.IndexData(s.Index))).ToArray();

            _riffSizeThreshold = RiffAviSizeThreshold;

            _currentRiff = _fileWriter.OpenList(KnownFourCCs.Lists.Avi, KnownFourCCs.ListTypes.Riff);
            WriteHeader();
            _currentMovie = _fileWriter.OpenList(KnownFourCCs.Lists.Movie);
        }

        private void CreateNewRiffIfNeeded(int approximateSizeOfNextChunk)
        {
            var estimatedSize = _fileWriter.BaseStream.Position + approximateSizeOfNextChunk - _currentRiff.ItemStart;
            if (_isFirstRiff && _emitIndex1)
            {
                estimatedSize += RiffItem.ItemHeaderSize + _index1Count * Index1EntrySize;
            }

            if (estimatedSize <= _riffSizeThreshold)
            {
                return;
            }

            CloseCurrentRiff();

            _currentRiff = _fileWriter.OpenList(KnownFourCCs.Lists.AviExtended, KnownFourCCs.ListTypes.Riff);
            _currentMovie = _fileWriter.OpenList(KnownFourCCs.Lists.Movie);
        }

        private void CloseCurrentRiff()
        {
            _fileWriter.CloseItem(_currentMovie);

            // Several special actions for the first RIFF (AVI)
            if (_isFirstRiff)
            {
                _riffAviFrameCount = _streams.OfType<IAviVideoStream>().Max(s => _streamsInfo[s.Index].FrameCount);
                if (_emitIndex1)
                {
                    WriteIndex1();
                }
                _riffSizeThreshold = RiffAvixSizeThreshold;
            }

            _fileWriter.CloseItem(_currentRiff);
            _isFirstRiff = false;
        }


        #region IAviStreamDataHandler implementation

        void IAviStreamWriteHandler.WriteVideoFrame(AviVideoStream stream, bool isKeyFrame, byte[] frameData, int startIndex, int count)
        {
            WriteStreamFrame(stream, isKeyFrame, frameData, startIndex, count);
        }

        void IAviStreamWriteHandler.WriteAudioBlock(AviAudioStream stream, byte[] blockData, int startIndex, int count)
        {
            WriteStreamFrame(stream, true, blockData, startIndex, count);
        }

        private void WriteStreamFrame(AviStreamBase stream, bool isKeyFrame, byte[] frameData, int startIndex, int count)
        {
            lock (_syncWrite)
            {
                CheckNotClosed();

                if (!_startedWriting)
                {
                    PrepareForWriting();
                }

                var si = _streamsInfo[stream.Index];
                if (si.SuperIndex.Count == MaxSuperIndexEntries)
                {
                    throw new InvalidOperationException("Cannot write more frames to this stream.");
                }

                if (ShouldFlushStreamIndex(si.StandardIndex))
                {
                    FlushStreamIndex(stream);
                }

                var shouldCreateIndex1Entry = _emitIndex1 && _isFirstRiff;

                CreateNewRiffIfNeeded(count + (shouldCreateIndex1Entry ? Index1EntrySize : 0));

                var chunk = _fileWriter.OpenChunk(stream.ChunkId, count);
                _fileWriter.Write(frameData, startIndex, count);
                _fileWriter.CloseItem(chunk);

                si.OnFrameWritten(chunk.DataSize);
                var dataSize = (uint)chunk.DataSize;
                // Set highest bit for non-key frames according to the OpenDML spec
                if (!isKeyFrame)
                {
                    dataSize |= 0x80000000U;
                }

                var newEntry = new StandardIndexEntry
                {
                    DataOffset = chunk.DataStart,
                    DataSize = dataSize
                };
                si.StandardIndex.Add(newEntry);

                if (!shouldCreateIndex1Entry)
                {
                    return;
                }

                var index1Entry = new Index1Entry
                {
                    IsKeyFrame = isKeyFrame,
                    DataOffset = (uint)(chunk.ItemStart - _currentMovie.DataStart),
                    DataSize = dataSize
                };
                si.Index1.Add(index1Entry);
                _index1Count++;
            }
        }

        void IAviStreamWriteHandler.WriteStreamHeader(AviVideoStream videoStream)
        {
            // See AVISTREAMHEADER structure
            _fileWriter.Write((uint)videoStream.StreamType);
            _fileWriter.Write((uint)videoStream.Codec);
            _fileWriter.Write(0U); // StreamHeaderFlags
            _fileWriter.Write((ushort)0); // priority
            _fileWriter.Write((ushort)0); // language
            _fileWriter.Write(0U); // initial frames
            _fileWriter.Write(_frameRateDenominator); // scale (frame rate denominator)
            _fileWriter.Write(_frameRateNumerator); // rate (frame rate numerator)
            _fileWriter.Write(0U); // start
            _fileWriter.Write((uint)_streamsInfo[videoStream.Index].FrameCount); // length
            _fileWriter.Write((uint)_streamsInfo[videoStream.Index].MaxChunkDataSize); // suggested buffer size
            _fileWriter.Write(0U); // quality
            _fileWriter.Write(0U); // sample size
            _fileWriter.Write((short)0); // rectangle left
            _fileWriter.Write((short)0); // rectangle top
            short right = (short)videoStream.Width;
            short bottom = (short)videoStream.Height;
            _fileWriter.Write(right); // rectangle right
            _fileWriter.Write(bottom); // rectangle bottom
        }

        void IAviStreamWriteHandler.WriteStreamHeader(AviAudioStream audioStream)
        {
            // See AVISTREAMHEADER structure
            _fileWriter.Write((uint)audioStream.StreamType);
            _fileWriter.Write(0U); // no codec
            _fileWriter.Write(0U); // StreamHeaderFlags
            _fileWriter.Write((ushort)0); // priority
            _fileWriter.Write((ushort)0); // language
            _fileWriter.Write(0U); // initial frames
            _fileWriter.Write((uint)audioStream.Granularity); // scale (sample rate denominator)
            _fileWriter.Write((uint)audioStream.BytesPerSecond); // rate (sample rate numerator)
            _fileWriter.Write(0U); // start
            _fileWriter.Write((uint)_streamsInfo[audioStream.Index].TotalDataSize); // length
            _fileWriter.Write((uint)(audioStream.BytesPerSecond / 2)); // suggested buffer size (half-second)
            _fileWriter.Write(-1); // quality
            _fileWriter.Write(audioStream.Granularity); // sample size
            _fileWriter.SkipBytes(sizeof(short) * 4);
        }

        void IAviStreamWriteHandler.WriteStreamFormat(AviVideoStream videoStream)
        {
            // See BITMAPINFOHEADER structure
            _fileWriter.Write(40U); // size of structure
            _fileWriter.Write(videoStream.Width);
            _fileWriter.Write(videoStream.Height);
            _fileWriter.Write((short)1); // planes
            _fileWriter.Write((ushort)videoStream.BitsPerPixel); // bits per pixel
            _fileWriter.Write((uint)videoStream.Codec); // compression (codec FOURCC)
            var sizeInBytes = videoStream.Width * videoStream.Height * (((int)videoStream.BitsPerPixel) / 8);
            _fileWriter.Write((uint)sizeInBytes); // image size in bytes
            _fileWriter.Write(0); // X pixels per meter
            _fileWriter.Write(0); // Y pixels per meter

            // Writing grayscale palette for 8-bit uncompressed stream
            // Otherwise, no palette
            if (videoStream.BitsPerPixel == BitsPerPixel.Bpp8 && videoStream.Codec == KnownFourCCs.Codecs.Uncompressed)
            {
                _fileWriter.Write(256U); // palette colors used
                _fileWriter.Write(0U); // palette colors important
                for (int i = 0; i < 256; i++)
                {
                    _fileWriter.Write((byte)i);
                    _fileWriter.Write((byte)i);
                    _fileWriter.Write((byte)i);
                    _fileWriter.Write((byte)0);
                }
            }
            else
            {
                _fileWriter.Write(0U); // palette colors used
                _fileWriter.Write(0U); // palette colors important
            }
        }

        void IAviStreamWriteHandler.WriteStreamFormat(AviAudioStream audioStream)
        {
            // See WAVEFORMATEX structure
            _fileWriter.Write(audioStream.Format);
            _fileWriter.Write((ushort)audioStream.ChannelCount);
            _fileWriter.Write((uint)audioStream.SamplesPerSecond);
            _fileWriter.Write((uint)audioStream.BytesPerSecond);
            _fileWriter.Write((ushort)audioStream.Granularity);
            _fileWriter.Write((ushort)audioStream.BitsPerSample);
            if (audioStream.FormatSpecificData != null)
            {
                _fileWriter.Write((ushort)audioStream.FormatSpecificData.Length);
                _fileWriter.Write(audioStream.FormatSpecificData);
            }
            else
            {
                _fileWriter.Write((ushort)0);
            }
        }

        #endregion


        #region Header

        private void WriteHeader()
        {
            _header = _fileWriter.OpenList(KnownFourCCs.Lists.Header);
            WriteFileHeader();
            foreach (var stream in _streams)
            {
                WriteStreamList(stream);
            }
            WriteOdmlHeader();
            WriteJunkInsteadOfMissingSuperIndexEntries();
            _fileWriter.CloseItem(_header);
        }

        private void WriteJunkInsteadOfMissingSuperIndexEntries()
        {
            var missingEntriesCount = _streamsInfo.Sum(si => MaxSuperIndexEntries - si.SuperIndex.Count);
            if (missingEntriesCount <= 0)
            {
                return;
            }

            var junkDataSize = missingEntriesCount * sizeof(uint) * 4 - RiffItem.ItemHeaderSize;
            var chunk = _fileWriter.OpenChunk(KnownFourCCs.Chunks.Junk, junkDataSize);
            _fileWriter.SkipBytes(junkDataSize);
            _fileWriter.CloseItem(chunk);
        }

        private void WriteFileHeader()
        {
            // See AVIMAINHEADER structure
            var chunk = _fileWriter.OpenChunk(KnownFourCCs.Chunks.AviHeader);
            _fileWriter.Write((uint)Decimal.Round(1000000m / FramesPerSecond)); // microseconds per frame
            // TODO: More correct computation of byterate
            _fileWriter.Write((uint)Decimal.Truncate(FramesPerSecond * _streamsInfo.Sum(s => s.MaxChunkDataSize))); // max bytes per second
            _fileWriter.Write(0U); // padding granularity
            var flags = MainHeaderFlags.IsInterleaved | MainHeaderFlags.TrustChunkType;
            if (_emitIndex1)
            {
                flags |= MainHeaderFlags.HasIndex;
            }
            _fileWriter.Write((uint)flags); // MainHeaderFlags
            _fileWriter.Write(_riffAviFrameCount); // total frames (in the first RIFF list containing this header)
            _fileWriter.Write(0U); // initial frames
            _fileWriter.Write((uint)Streams.Count); // stream count
            _fileWriter.Write(0U); // suggested buffer size
            var firstVideoStream = _streams.OfType<IAviVideoStream>().First();
            _fileWriter.Write(firstVideoStream.Width); // video width
            _fileWriter.Write(firstVideoStream.Height); // video height
            _fileWriter.SkipBytes(4 * sizeof(uint)); // reserved
            _fileWriter.CloseItem(chunk);
        }

        private void WriteOdmlHeader()
        {
            var list = _fileWriter.OpenList(KnownFourCCs.Lists.OpenDml);
            var chunk = _fileWriter.OpenChunk(KnownFourCCs.Chunks.OpenDmlHeader);
            _fileWriter.Write(_streams.OfType<IAviVideoStream>().Max(s => _streamsInfo[s.Index].FrameCount)); // total frames in file
            _fileWriter.SkipBytes(61 * sizeof(uint)); // reserved
            _fileWriter.CloseItem(chunk);
            _fileWriter.CloseItem(list);
        }

        private void WriteStreamList(IAviStreamInternal stream)
        {
            var list = _fileWriter.OpenList(KnownFourCCs.Lists.Stream);
            WriteStreamHeader(stream);
            WriteStreamFormat(stream);
            WriteStreamName(stream);
            WriteStreamSuperIndex(stream);
            _fileWriter.CloseItem(list);
        }

        private void WriteStreamHeader(IAviStreamInternal stream)
        {
            var chunk = _fileWriter.OpenChunk(KnownFourCCs.Chunks.StreamHeader);
            stream.WriteHeader();
            _fileWriter.CloseItem(chunk);
        }

        private void WriteStreamFormat(IAviStreamInternal stream)
        {
            var chunk = _fileWriter.OpenChunk(KnownFourCCs.Chunks.StreamFormat);
            stream.WriteFormat();
            _fileWriter.CloseItem(chunk);
        }

        private void WriteStreamName(IAviStream stream)
        {
            if (string.IsNullOrEmpty(stream.Name))
            {
                return;
            }

            var bytes = Encoding.ASCII.GetBytes(stream.Name);
            var chunk = _fileWriter.OpenChunk(KnownFourCCs.Chunks.StreamName);
            _fileWriter.Write(bytes);
            _fileWriter.Write((byte)0);
            _fileWriter.CloseItem(chunk);
        }

        private void WriteStreamSuperIndex(IAviStream stream)
        {
            var superIndex = _streamsInfo[stream.Index].SuperIndex;

            // See AVISUPERINDEX structure
            var chunk = _fileWriter.OpenChunk(KnownFourCCs.Chunks.StreamIndex);
            _fileWriter.Write((ushort)4); // DWORDs per entry
            _fileWriter.Write((byte)0); // index sub-type
            _fileWriter.Write((byte)IndexType.Indexes); // index type
            _fileWriter.Write((uint)superIndex.Count); // entries count
            _fileWriter.Write((uint)((IAviStreamInternal)stream).ChunkId); // chunk ID of the stream
            _fileWriter.SkipBytes(3 * sizeof(uint)); // reserved
            
            // entries
            foreach (var entry in superIndex)
            {
                _fileWriter.Write((ulong)entry.ChunkOffset); // offset of sub-index chunk
                _fileWriter.Write((uint)entry.ChunkSize); // size of sub-index chunk
                _fileWriter.Write((uint)entry.Duration); // duration of sub-index data (number of frames it refers to)
            }

            _fileWriter.CloseItem(chunk);
        }

        #endregion


        #region Index

        private void WriteIndex1()
        {
            var chunk = _fileWriter.OpenChunk(KnownFourCCs.Chunks.Index1);

            var indices = _streamsInfo
                .Select((si, i) => new {si.Index1, ChunkId = (uint)_streams.ElementAt(i).ChunkId})
                .Where(a => a.Index1.Count > 0)
                .ToList();

            while (_index1Count > 0)
            {
                var minOffset = indices[0].Index1[0].DataOffset;
                var minIndex = 0;
                for (var i = 1; i < indices.Count; i++)
                {
                    var offset = indices[i].Index1[0].DataOffset;
                    if (offset < minOffset)
                    {
                        minOffset = offset;
                        minIndex = i;
                    }
                }

                var index = indices[minIndex];
                _fileWriter.Write(index.ChunkId);
                _fileWriter.Write(index.Index1[0].IsKeyFrame ? 0x00000010U : 0);
                _fileWriter.Write(index.Index1[0].DataOffset);
                _fileWriter.Write(index.Index1[0].DataSize);

                index.Index1.RemoveAt(0);
                if (index.Index1.Count == 0)
                {
                    indices.RemoveAt(minIndex);
                }

                _index1Count--;
            }

            _fileWriter.CloseItem(chunk);
        }

        private bool ShouldFlushStreamIndex(IList<StandardIndexEntry> index)
        {
            // Check maximum number of entries
            if (index.Count >= MaxIndexEntries){
                return true;
            }

            // Check relative offset
            if (index.Count <= 0 || _fileWriter.BaseStream.Position - index[0].DataOffset <= uint.MaxValue)
            {
                return false;
            }

            return true;

        }

        private void FlushStreamIndex(IAviStreamInternal stream)
        {
            var si = _streamsInfo[stream.Index];
            var index = si.StandardIndex;
            var entriesCount = index.Count;
            if (entriesCount == 0)
            {
                return;
            }

            var baseOffset = index[0].DataOffset;
            var indexSize = 24 + entriesCount * 8;

            CreateNewRiffIfNeeded(indexSize);

            // See AVISTDINDEX structure
            var chunk = _fileWriter.OpenChunk(si.StandardIndexChunkId, indexSize);
            _fileWriter.Write((ushort)2); // DWORDs per entry
            _fileWriter.Write((byte)0); // index sub-type
            _fileWriter.Write((byte)IndexType.Chunks); // index type
            _fileWriter.Write((uint)entriesCount); // entries count
            _fileWriter.Write((uint)stream.ChunkId); // chunk ID of the stream
            _fileWriter.Write((ulong)baseOffset); // base offset for entries
            _fileWriter.SkipBytes(sizeof(uint)); // reserved

            foreach (var entry in index)
            {
                _fileWriter.Write((uint)(entry.DataOffset - baseOffset)); // chunk data offset
                _fileWriter.Write(entry.DataSize); // chunk data size
            }

            _fileWriter.CloseItem(chunk);

            var superIndex = _streamsInfo[stream.Index].SuperIndex;
            var newEntry = new SuperIndexEntry
            {
                ChunkOffset = chunk.ItemStart,
                ChunkSize = chunk.ItemSize,
                Duration = entriesCount
            };
            superIndex.Add(newEntry);

            index.Clear();
        }

        #endregion
    }
}
