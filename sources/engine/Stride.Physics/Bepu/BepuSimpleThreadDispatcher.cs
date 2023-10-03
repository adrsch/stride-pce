using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using BepuUtilities;
using BepuUtilities.Memory;

namespace Stride.Physics.Engine
{
    internal class BepuSimpleThreadDispatcher : IThreadDispatcher, IDisposable
    {
        public int ThreadCount => Stride.Core.Threading.Dispatcher.MaxPhysicsParallelism;
        private BepuUtilities.Memory.BufferPool[] buffers;

        public BepuSimpleThreadDispatcher()
        {
            buffers = new BufferPool[ThreadCount];
            for (int i = 0; i < ThreadCount; i++)
            {
                buffers[i] = new BufferPool();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DispatchWorkers(Action<int> workerBody, int maximumWorkerCount = int.MaxValue)
        {
            Stride.Core.Threading.Dispatcher.For(0, ThreadCount, workerBody);
        }

        public void Dispose()
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                buffers[i].Clear();
                buffers[i] = null;
            }

            buffers = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferPool GetThreadMemoryPool(int workerIndex)
        {
            return buffers[workerIndex];
        }
    }
}
