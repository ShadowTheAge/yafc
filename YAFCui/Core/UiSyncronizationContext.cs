using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace YAFC.UI
{
    public class UiSyncronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object state)
        {
            Ui.ExecuteInMainThread(d, state);
            base.Post(d, state);
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            throw new NotSupportedException("Synchronous send is not supported");
        }
    }

    public struct EnterThreadPoolAwaitable : INotifyCompletion
    {
        public EnterThreadPoolAwaitable GetAwaiter() => this;
        public void GetResult() {}
        public bool IsCompleted => !Ui.IsMainThread();
        public void OnCompleted(Action continuation)
        {
            ThreadPool.QueueUserWorkItem(ThreadPoolPost, continuation);
        }
        
        private static readonly WaitCallback ThreadPoolPost = a => ((Action) a)();
    }
    
    public struct EnterMainThreadAwaitable : INotifyCompletion
    {
        public EnterMainThreadAwaitable GetAwaiter() => this;
        public void GetResult() {}
        public bool IsCompleted => Ui.IsMainThread();
        public void OnCompleted(Action continuation)
        {
            Ui.ExecuteInMainThread(MainThreadPost, continuation);
        }

        private static readonly SendOrPostCallback MainThreadPost = a => ((Action) a)();
    }
    
}