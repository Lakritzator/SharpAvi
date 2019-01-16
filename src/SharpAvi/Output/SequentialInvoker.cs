using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace SharpAvi.Output
{
    /// <summary>
    /// Serializes synchronous and asynchronous invocations in one queue.
    /// </summary>
    internal sealed class SequentialInvoker
    {
        private readonly object _sync = new object();
        private Task _lastTask;

        /// <summary>
        /// Creates a new instance of <see cref="SequentialInvoker"/>.
        /// </summary>
        public SequentialInvoker()
        {
            var tcs = new TaskCompletionSource<bool>();
            tcs.SetResult(true);

            // Initialize lastTask to already completed task
            _lastTask = tcs.Task;
        }

        /// <summary>
        /// Invokes an action synchronously.
        /// </summary>
        /// <param name="action">Action.</param>
        /// <remarks>
        /// Waits for any previously scheduled invocations to complete.
        /// </remarks>
        public void Invoke(Action action)
        {
            Contract.Requires(action != null);

            Task prevTask;
            // TODO: Check if TaskCreationOptions.RunContinuationsAsynchronously is needed
            var tcs = new TaskCompletionSource<bool>();

            lock (_sync)
            {
                prevTask = _lastTask;
                _lastTask = tcs.Task;
            }

            try
            {
                prevTask.Wait();
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                    throw;
                }
                tcs.SetResult(true);
            }
            finally
            {
                tcs.TrySetResult(false);
            }
        }

        /// <summary>
        /// Schedules an action asynchronously.
        /// </summary>
        /// <param name="action">Action.</param>
        /// <returns>Task corresponding to asunchronous invocation.</returns>
        /// <remarks>
        /// This action will be invoked after all previously scheduled invocations complete.
        /// </remarks>
        public Task InvokeAsync(Action action)
        {
            Contract.Requires(action != null);

            Task result;
            lock (_sync)
            {
                result = _lastTask.ContinueWith(_ => action.Invoke());
                _lastTask = result;
            }

            return result;
        }

        /// <summary>
        /// Waits for currently pending invocations to complete.
        /// </summary>
        /// <remarks>
        /// New invocations, which are possibly scheduled during this call, are not considered.
        /// </remarks>
        public void WaitForPendingInvocations()
        {
            Task taskToWait;
            lock (_sync)
            {
                taskToWait = _lastTask;
            }
            taskToWait.Wait();
        }
    }
}
