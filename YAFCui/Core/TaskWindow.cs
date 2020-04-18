using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace YAFC.UI
{
    public abstract class TaskWindow<T> : WindowUtility
    {
        private TaskCompletionSource<T> tcs;

        protected TaskWindow()
        {
            tcs = new TaskCompletionSource<T>();
        }

        public TaskAwaiter<T> GetAwaiter() => tcs.Task.GetAwaiter();
        
        protected void CloseWithResult(T result)
        {
            tcs?.TrySetResult(result);
            tcs = null;
            Close();
        }

        protected internal override void Close()
        {
            tcs?.TrySetResult(default);
            tcs = null;
            base.Close();
        }
    }
}