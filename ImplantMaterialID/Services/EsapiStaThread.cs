using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ImplantMaterialID.Services
{
    /// <summary>
    /// A single, long-lived STA thread that every ESAPI call is funneled through.
    ///
    /// ESAPI's native layer (vmod) asserts if its objects are touched from more than one
    /// thread - see the "Atomic access violation" crash this class exists to prevent.
    /// The rule from Varian's own ESAPI 18.x guidance is: create Application on a single STA
    /// thread, and never call into ESAPI from a worker thread, Task, background thread,
    /// async continuation, or PLINQ.
    ///
    /// This does NOT have to be the WPF UI thread (which is also STA, but is a different
    /// thread) - it just has to be *a* single, consistent STA thread for the whole
    /// application's lifetime. Running ESAPI on its own dedicated thread, rather than the UI
    /// thread, means the UI stays responsive (progress bar, etc.) while a slow ESAPI call
    /// (e.g. the mean-HU voxel loop) is in flight, since the two threads only ever exchange
    /// plain data (DTOs / exceptions), never live ESAPI objects.
    /// </summary>
    public sealed class EsapiStaThread : IDisposable
    {
        private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();
        private readonly Thread _thread;

        public EsapiStaThread()
        {
            _thread = new Thread(RunLoop) { IsBackground = true, Name = "ESAPI-STA" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        private void RunLoop()
        {
            // Runs for the lifetime of the app: dequeues and executes one delegate at a time,
            // always on this same thread. No WPF Dispatcher/message pump is needed here -
            // ESAPI's own (native) login dialog pumps its own modal message loop when shown.
            foreach (var action in _queue.GetConsumingEnumerable())
            {
                action();
            }
        }

        /// <summary>Runs func on the ESAPI thread and returns its result without blocking the caller.</summary>
        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Add(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        /// <summary>Runs action on the ESAPI thread and completes when it finishes, without blocking the caller.</summary>
        public Task InvokeAsync(Action action) => InvokeAsync<object>(() =>
        {
            action();
            return null;
        });

        /// <summary>
        /// Runs action on the ESAPI thread and blocks the caller until it finishes. Only use
        /// this for shutdown/cleanup (e.g. Dispose), where there is no UI thread left to keep
        /// responsive and you need a deterministic ordering before the process exits.
        /// </summary>
        public void InvokeAndWait(Action action, TimeSpan timeout)
        {
            using (var done = new ManualResetEventSlim(false))
            {
                _queue.Add(() =>
                {
                    try { action(); }
                    finally { done.Set(); }
                });
                done.Wait(timeout);
            }
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            _thread.Join(TimeSpan.FromSeconds(5));
            _queue.Dispose();
        }
    }
}
