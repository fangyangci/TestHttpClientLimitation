namespace TestLibary
{
    public class SyncQueue<T> : Queue<T?>
    {
        private readonly Semaphore _dequeueSemaphore = new Semaphore(0, int.MaxValue);
        private bool _disposed;
        public new void Enqueue(T? item)
        {
            if (_disposed)
            {
                return;
            }
            lock (this)
            {
                base.Enqueue(item);
            }
            _dequeueSemaphore.Release(1);
        }

        public new T? Dequeue()
        {
            _dequeueSemaphore.WaitOne();
            T? result;
            lock (this)
            {
                result = base.Dequeue();
            }
            return result;
        }

        public void Dispose()
        {
            Enqueue(default);
            _disposed = true;
            _dequeueSemaphore.Dispose();
        }
    }
}
