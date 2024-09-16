using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Yafc.UI;
public class UiSynchronizationContext : SynchronizationContext {
    public override void Post(SendOrPostCallback d, object? state) => Ui.DispatchInMainThread(d, state);

    private class SendCommand(SendOrPostCallback d, object? state) {
        public SendOrPostCallback d = d;
        public object? state = state;
        public Exception? ex;

        public static void Call(object state) {
            SendCommand send = (SendCommand)state;

            try {
                send.d(send.state);
            }
            catch (Exception ex) {
                send.ex = ex;
            }

            lock (send) {
                Monitor.Pulse(send);
            }
        }
    }

    public override void Send(SendOrPostCallback d, object? state) {
        SendCommand send = new SendCommand(d, state);

        lock (send) {
            Post(SendCommand.Call!, send); // null-forgiving: send is not null, so Call doesn't need to accept a null.
            _ = Monitor.Wait(send);
        }

        if (send.ex != null) {
            throw send.ex;
        }
    }
}

public readonly struct EnterThreadPoolAwaitable : INotifyCompletion {
    public EnterThreadPoolAwaitable GetAwaiter() => this;

    public void GetResult() { }
    public bool IsCompleted => !Ui.IsMainThread();
    public void OnCompleted(Action continuation) {
        Logging.GetLogger<EnterThreadPoolAwaitable>().Debug("Exiting UI thread for action {id}.", continuation.GetHashCode()); // Stack trace enricher will add the stack trace here.
        _ = ThreadPool.QueueUserWorkItem(ThreadPoolPost!, () => { // null-forgiving: this action is not null, so ThreadPoolPost doesn't need to accept a null.
            Logging.GetLogger<EnterThreadPoolAwaitable>().Debug("Resuming action {id} on a thread-pool thread.", continuation.GetHashCode());
            continuation();
        });
    }

    private static void ThreadPoolPost(object state) => ((Action)state)();
}

public readonly struct EnterMainThreadAwaitable : INotifyCompletion {
    public EnterMainThreadAwaitable GetAwaiter() => this;

    public void GetResult() { }
    public bool IsCompleted => Ui.IsMainThread();
    public void OnCompleted(Action continuation) {
        Logging.GetLogger<EnterMainThreadAwaitable>().Debug("Entering UI thread for action {id}.", continuation.GetHashCode()); // Stack trace enricher will add the stack trace here.
        Ui.DispatchInMainThread(MainThreadPost!, () => { // null-forgiving: this action is not null, so MainThreadPost doesn't need to accept a null.
            Logging.GetLogger<EnterMainThreadAwaitable>().Debug("Resuming action {id} on the UI thread.", continuation.GetHashCode());
            continuation();
        });
    }

    private static void MainThreadPost(object state) => ((Action)state)();
}
