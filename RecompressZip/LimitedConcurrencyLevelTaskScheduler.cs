using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace RecompressZip
{
    /// <summary>
    /// Provides a task scheduler that ensures a maximum concurrency level while
    /// running on top of the thread pool.
    /// Implementation of this class if found on https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler?view=netframework-4.7.2
    /// </summary>
    public class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
    {
        /// <summary>
        /// Indicates whether the current thread is processing work items.
        /// </summary>
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;


        /// <summary>
        /// The maximum concurrency level allowed by this scheduler.
        /// </summary>
        public sealed override int MaximumConcurrencyLevel { get; }


        /// <summary>
        /// The list of tasks to be executed
        /// </summary>
        private readonly LinkedList<Task> _tasks = new(); // protected by lock(_tasks)
        /// <summary>
        /// Indicates whether the scheduler is currently processing work items.
        /// </summary>
        private int _delegatesQueuedOrRunning = 0;


        /// <summary>
        /// Creates a new instance with the specified degree of parallelism.
        /// </summary>
        /// <param name="maxDegreeOfParallelism"></param>
        public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1) {
                throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
            }
            MaximumConcurrencyLevel = maxDegreeOfParallelism;
        }

        /// <summary>
        /// Queues a task to the scheduler.
        /// </summary>
        /// <param name="task">A task.</param>
        protected sealed override void QueueTask(Task task)
        {
            // Add the task to the list of tasks to be processed. If there aren't enough
            // delegates currently queued or running to process tasks, schedule another.
            lock (_tasks)
            {
                _tasks.AddLast(task);
                if (_delegatesQueuedOrRunning < MaximumConcurrencyLevel)
                {
                    _delegatesQueuedOrRunning++;
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }

        /// <summary>
        /// Inform the ThreadPool that there's work to be executed for this scheduler.
        /// </summary>
        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                // Note that the current thread is now processing work items.
                // This is necessary to enable inlining of tasks into this thread.
                _currentThreadIsProcessingItems = true;
                try
                {
                    // Process all available items in the queue.
                    while (true)
                    {
                        Task item;
                        lock (_tasks)
                        {
                            // When there are no more items to be processed,
                            // note that we're done processing, and get out.
                            var task = _tasks.First;
                            if (task == null)
                            {
                                _delegatesQueuedOrRunning--;
                                break;
                            }
                            // Get the next item from the queue
                            item = task.Value;
                            _tasks.RemoveFirst();
                        }
                        // Execute the task we pulled out of the queue
                        TryExecuteTask(item);
                    }
                }
                // We're done processing items on the current thread
                finally
                {
                    _currentThreadIsProcessingItems = false;
                }
            }, null);
        }

        /// <summary>
        /// Attempts to execute the specified task on the current thread.
        /// </summary>
        /// <param name="task">A task to execute.</param>
        /// <param name="taskWasPreviouslyQueued">A flag whether <paramref name="task"/> is queued previously.</param>
        /// <returns><c>true</c> if task was successfully executed, <c>false</c> if it was not.</returns>
        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining
            // If the task was previously queued, remove it from the queue
            return _currentThreadIsProcessingItems
                && (!taskWasPreviouslyQueued || TryDequeue(task))
                && TryExecuteTask(task);
        }

        /// <summary>
        /// Attempt to remove a previously scheduled task from the scheduler.
        /// </summary>
        /// <param name="task">A task to remove from the scheduler</param>
        /// <returns><c>true</c> if the element containing value is successfully removed; otherwise, <c>false</c>.</returns>
        protected sealed override bool TryDequeue(Task task)
        {
            lock (_tasks)
            {
                return _tasks.Remove(task);
            }
        }

        /// <summary>
        /// Gets an enumerable of the tasks currently scheduled on this scheduler.
        /// </summary>
        /// <returns>An enumerator of tasks.</returns>
        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            var lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (!lockTaken)
                {
                    throw new NotSupportedException();
                }
                return _tasks;
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_tasks);
                }
            }
        }
    }
}
