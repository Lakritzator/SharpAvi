#if NET471
using System;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Threading;
using System.Windows.Threading;

namespace SharpAvi.Codecs
{
    /// <summary>
    /// Ensures that all access to the enclosed <see cref="IVideoEncoder"/> instance is made
    /// on a single thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Especially useful for unmanaged encoders like <see cref="Mpeg4VideoEncoderVcm"/> in multi-threaded scenarios
    /// like asynchronous encoding.
    /// </para>
    /// <para>
    /// Uses <see cref="Dispatcher"/> under the hood.
    /// </para>
    /// </remarks>
    public class SingleThreadedVideoEncoderWrapper : IVideoEncoder, IDisposable
    {
        private readonly IVideoEncoder _encoder;
        private readonly Thread _thread;
        private readonly Dispatcher _dispatcher;

        /// <summary>
        /// Creates a new instance of <see cref="SingleThreadedVideoEncoderWrapper"/>.
        /// </summary>
        /// <param name="encoderFactory">
        /// Factory for creating an encoder instance.
        /// It will be invoked on the same thread as all subsequent operations of the <see cref="IVideoEncoder"/> interface.
        /// </param>
        public SingleThreadedVideoEncoderWrapper(Func<IVideoEncoder> encoderFactory)
        {
            Contract.Requires(encoderFactory != null);

            _thread = new Thread(RunDispatcher)
            {
                IsBackground = true,
                Name = typeof(SingleThreadedVideoEncoderWrapper).Name
            };
            // TODO: Make sure this is disposed
            var dispatcherCreated = new AutoResetEvent(false);
            _thread.Start(dispatcherCreated);
            dispatcherCreated.WaitOne();
            _dispatcher = Dispatcher.FromThread(_thread);
            // TODO: Create encoder on the first frame
            _encoder = DispatcherInvokeAndPropagateException(encoderFactory);
            if (_encoder == null)
            {
                throw new InvalidOperationException("Encoder factory has created no instance.");
            }
        }

        /// <summary>
        /// Disposes the enclosed encoder and stops the internal thread.
        /// </summary>
        public void Dispose()
        {
            if (!_thread.IsAlive)
            {
                return;
            }

            if (_encoder is IDisposable encoderDisposable)
            {
                DispatcherInvokeAndPropagateException(encoderDisposable.Dispose);
            }

            _dispatcher.InvokeShutdown();
            _thread.Join();
        }

        /// <summary>Codec ID.</summary>
        public FourCC Codec
        {
            get
            {
                return DispatcherInvokeAndPropagateException(() => _encoder.Codec);
            }
        }

        /// <summary>
        /// Number of bits per pixel in encoded image.
        /// </summary>
        public BitsPerPixel BitsPerPixel
        {
            get
            {
                return DispatcherInvokeAndPropagateException(() => _encoder.BitsPerPixel);
            }
        }

        /// <summary>
        /// Determines the amount of space needed in the destination buffer for storing the encoded data of a single frame.
        /// </summary>
        public int MaxEncodedSize
        {
            get
            {
                return DispatcherInvokeAndPropagateException(() => _encoder.MaxEncodedSize);
            }
        }

        /// <summary>
        /// Encodes video frame.
        /// </summary>
        public int EncodeFrame(byte[] source, int srcOffset, byte[] destination, int destOffset, out bool isKeyFrame)
        {
            var result = DispatcherInvokeAndPropagateException(
                () => EncodeFrame(source, srcOffset, destination, destOffset));
            isKeyFrame = result.IsKeyFrame;
            return result.EncodedLength;
        }

        private EncodeResult EncodeFrame(byte[] source, int srcOffset, byte[] destination, int destOffset)
        {
            var result = _encoder.EncodeFrame(source, srcOffset, destination, destOffset, out var isKeyFrame);
            return new EncodeResult
            {
                EncodedLength = result,
                IsKeyFrame = isKeyFrame
            };
        }

        private struct EncodeResult
        {
            public int EncodedLength;
            public bool IsKeyFrame;
        }


        private void DispatcherInvokeAndPropagateException(Action action)
        {
            Exception exOnDispatcherThread = null;
            _dispatcher.Invoke(() =>
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    exOnDispatcherThread = ex;
                }
            });

            if (exOnDispatcherThread != null)
            {
                WrapAndRethrowException(exOnDispatcherThread);
            }
        }

        private TResult DispatcherInvokeAndPropagateException<TResult>(Func<TResult> func)
        {
            Exception exOnDispatcherThread = null;
            var result = _dispatcher.Invoke(() =>
            {
                try
                {
                    return func.Invoke();
                }
                catch (Exception ex)
                {
                    exOnDispatcherThread = ex;
                    return default;
                }
            });

            if (exOnDispatcherThread != null)
            {
                WrapAndRethrowException(exOnDispatcherThread);
            }

            return result;
        }

        private static void WrapAndRethrowException(Exception wrappedException)
        {
            throw new TargetInvocationException("Error calling wrapped encoder.", wrappedException);
        }

        private void RunDispatcher(object parameter)
        {
            AutoResetEvent dispatcherCreated = (AutoResetEvent)parameter;
            _ = Dispatcher.CurrentDispatcher;
            dispatcherCreated.Set();

            Dispatcher.Run();
        }
    }
}
#endif