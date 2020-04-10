using System;

namespace UI
{
    public abstract class UnmanagedResource : IDisposable
    {
        protected IntPtr _handle;
        
        protected abstract void ReleaseUnmanagedResources();

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                ReleaseUnmanagedResources();
                _handle = IntPtr.Zero;
                GC.SuppressFinalize(this);
            }
        }

        ~UnmanagedResource()
        {
            ReleaseUnmanagedResources();
        }
    }
}