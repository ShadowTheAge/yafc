using System;

namespace UI
{
    public abstract class UnmanagedResource : IDisposable
    {
        internal IntPtr handle;
        
        protected abstract void ReleaseUnmanagedResources();

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                ReleaseUnmanagedResources();
                handle = IntPtr.Zero;
                GC.SuppressFinalize(this);
            }
        }

        ~UnmanagedResource()
        {
            ReleaseUnmanagedResources();
        }
    }
}