using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace YAFC.UI {
    public class UiSyncronizationContext : SynchronizationContext {
        public override void Post(SendOrPostCallback d, object state) {
            Ui.DispatchInMainThread(d, state);
        }

        private class SendCommand {
            public SendOrPostCallback d;
            public object state;
            public Exception ex;

            public static readonly SendOrPostCallback Call = a => {
                SendCommand send = a as SendCommand;
                try {
                    send.d(send.state);
                }
                catch (Exception ex) {
                    send.ex = ex;
                }

                lock (send) {
                    Monitor.Pulse(send);
                }
            };
        }

        public override void Send(SendOrPostCallback d, object state) {
            SendCommand send = new SendCommand { d = d, state = state };
            lock (send) {
                Post(SendCommand.Call, send);
                _ = Monitor.Wait(send);
            }
            if (send.ex != null) {
                throw send.ex;
            }
        }
    }

    public readonly struct EnterThreadPoolAwaitable : INotifyCompletion {
        public EnterThreadPoolAwaitable GetAwaiter() {
            return this;
        }

        public void GetResult() { }
        public bool IsCompleted => !Ui.IsMainThread();
        public void OnCompleted(Action continuation) {
            _ = ThreadPool.QueueUserWorkItem(ThreadPoolPost, continuation);
        }

        private static readonly WaitCallback ThreadPoolPost = a => ((Action)a)();
    }

    public readonly struct EnterMainThreadAwaitable : INotifyCompletion {
        public EnterMainThreadAwaitable GetAwaiter() {
            return this;
        }

        public void GetResult() { }
        public bool IsCompleted => Ui.IsMainThread();
        public void OnCompleted(Action continuation) {
            Ui.DispatchInMainThread(MainThreadPost, continuation);
        }

        private static readonly SendOrPostCallback MainThreadPost = a => ((Action)a)();
    }

}
