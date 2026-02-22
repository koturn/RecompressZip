using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace RecompressZip
{
    /// <summary>
    /// Provides a task scheduler that ensures a maximum concurrency level while running on top of the thread pool.
    /// </summary>
    /// <remarks>
    /// <seealso href="https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler"/>
    /// </remarks>
    public sealed class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
    {
        /// <summary>
        /// Indicates whether the current thread is processing work items.
        /// </summary>
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;

        /// <summary>
        /// The maximum concurrency level allowed by this scheduler.
        /// </summary>
        public sealed override int MaximumConcurrencyLevel => _maxDegreeOfParallelism;

        /// <summary>
        /// The list of tasks to be executed.
        /// </summary>
        private readonly LinkedList<Task> _tasks = new();
        /// <summary>
        /// The maximum concurrency level allowed by this scheduler.
        /// </summary>
        private int _maxDegreeOfParallelism;
        /// <summary>
        /// <para>Indicates whether the scheduler is currently processing work items.</para>
        /// <para>This variable locked by <see cref="_taskListLock"/>.</para>
        /// </summary>
        private int _delegatesQueuedOrRunning = 0;
        /// <summary>
        /// Lock object for <see cref="_tasks"/> and <see cref="_delegatesQueuedOrRunning"/>.
        /// </summary>
#if NET9_0_OR_GREATER
        private readonly Lock _taskListLock = new();
#else
        private readonly object _taskListLock = new();
#endif  // !NET9_0_OR_GREATER
        /// <summary>
        /// True to allow non long running task in new (non pooled) thread.
        /// </summary>
        private readonly bool _allowTORunNonLongRunningTaskOnNewThread;


        /// <summary>
        /// Creates a new instance with the specified degree of parallelism.
        /// </summary>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <param name="allowRunNonLongRunningTaskInNonPooledThread">True to allow non long running task in new (non pooled) thread.</param>
        public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism, bool allowRunNonLongRunningTaskInNonPooledThread = true)
        {
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDegreeOfParallelism, 1);
#else
            ThrowIfLessThan(maxDegreeOfParallelism, 1, nameof(maxDegreeOfParallelism));
#endif  // NET8_0_OR_GREATER
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
            _allowTORunNonLongRunningTaskOnNewThread = allowRunNonLongRunningTaskInNonPooledThread;
        }


        /// <summary>
        /// Changes <see cref="MaximumConcurrencyLevel"/>.
        /// </summary>
        /// <param name="maxDegreeOfParallelism"></param>
        public void SetMaximumConcurrencyLevel(int maxDegreeOfParallelism)
        {
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDegreeOfParallelism, 1);
#else
            ThrowIfLessThan(maxDegreeOfParallelism, 1, nameof(maxDegreeOfParallelism));
#endif  // NET8_0_OR_GREATER
            var maxDegreeOfParallelismOld = _maxDegreeOfParallelism;
            _maxDegreeOfParallelism = maxDegreeOfParallelism;

            if (maxDegreeOfParallelismOld >= maxDegreeOfParallelism)
            {
                return;
            }

            lock (_taskListLock)
            {
                var diff = Math.Min(maxDegreeOfParallelism, _tasks.Count) - _delegatesQueuedOrRunning;
                if (diff > 0)
                {
                    _delegatesQueuedOrRunning += diff;
                    for (int i = 0; i < diff; i++)
                    {
                        NotifyThreadPoolOfPendingWork();
                    }
                }
            }
        }


        /// <summary>
        /// Queues a task to the scheduler.
        /// </summary>
        /// <param name="task">A task.</param>
        protected sealed override void QueueTask(Task task)
        {
            lock (_taskListLock)
            {
                if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
                {
                    _delegatesQueuedOrRunning++;
                    if ((task.CreationOptions & TaskCreationOptions.LongRunning) == TaskCreationOptions.None)
                    {
                        // Add the task to the list of tasks to be processed.
                        _tasks.AddLast(task);
                        NotifyThreadPoolOfPendingWork();
                    }
                    else
                    {
                        // Run the long running task on the new thread.
                        RunTaskOnNewThread(task);
                    }
                }
                else
                {
                    // If there aren't enough delegates currently queued or running to process tasks, schedule another.
                    _tasks.AddLast(task);
                }
            }
        }

        /// <summary>
        /// Attempts to execute the specified task on the current thread.
        /// </summary>
        /// <param name="task">A task to execute.</param>
        /// <param name="taskWasPreviouslyQueued">A flag whether <paramref name="task"/> is queued previously.</param>
        /// <returns><c>true</c> if task was successfully executed, <c>false</c> if it was not.</returns>
        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining.
            // If the task was previously queued, remove it from the queue.
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
            lock (_taskListLock)
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

#if NET9_0_OR_GREATER
            try
            {
                lockTaken = _taskListLock.TryEnter();
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
                    _taskListLock.Exit();
                }
            }
#else
            try
            {
                Monitor.TryEnter(_taskListLock, ref lockTaken);
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
                    Monitor.Exit(_taskListLock);
                }
            }
#endif  // NET9_0_OR_GREATER
        }


        /// <summary>
        /// Inform the <see cref="ThreadPool"/> that there's work to be executed for this scheduler.
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
                        Task task;
                        lock (_taskListLock)
                        {
                            // Terminate tasks exceeding the maximum concurrency level.
                            if (_delegatesQueuedOrRunning > _maxDegreeOfParallelism)
                            {
                                _delegatesQueuedOrRunning--;
                                break;
                            }
                            // When there are no more items to be processed,
                            // note that we're done processing, and get out.
                            var node = _tasks.First;
                            if (node == null)
                            {
                                _delegatesQueuedOrRunning--;
                                break;
                            }
                            // Get the next item from the queue.
                            task = node.Value;
                            _tasks.RemoveFirst();
                        }

                        // If the task we pulled out is a long running task,
                        // executing the task on the pool thread.
                        if ((task.CreationOptions & TaskCreationOptions.LongRunning) != TaskCreationOptions.None)
                        {
                            RunTaskOnNewThread(task);
                            break;
                        }

                        // Execute the task we pulled out of the queue.
                        TryExecuteTask(task);
                    }
                }
                // We're done processing items on the current thread.
                finally
                {
                    _currentThreadIsProcessingItems = false;
                }
            }, null);
        }

        /// <summary>
        /// Run specified task on new (non pooled) thread.
        /// </summary>
        /// <param name="task">A task to execute.</param>
        private void RunTaskOnNewThread(Task task)
        {
            new Thread(param =>
            {
                // Note that the current thread is now processing work items.
                // This is necessary to enable inlining of tasks into this thread.
                _currentThreadIsProcessingItems = true;
                try
                {
                    TryExecuteTask((Task)param!);
                    while (true)
                    {
                        Task nextTask;
                        lock (_taskListLock)
                        {
                            if (_delegatesQueuedOrRunning > _maxDegreeOfParallelism)
                            {
                                _delegatesQueuedOrRunning--;
                                break;
                            }
                            // When there are no more items to be processed,
                            // note that we're done processing, and get out.
                            var node = _tasks.First;
                            if (node == null)
                            {
                                _delegatesQueuedOrRunning--;
                                break;
                            }
                            // Get the next item from the queue
                            nextTask = node.Value;

                            // If the task we pulled out is not a long running task and not allowed in non pooled thread,
                            // executing the task on the pool thread.
                            if (!_allowTORunNonLongRunningTaskOnNewThread
                                && (nextTask.CreationOptions & TaskCreationOptions.LongRunning) == TaskCreationOptions.None)
                            {
                                NotifyThreadPoolOfPendingWork();
                                break;
                            }

                            _tasks.RemoveFirst();
                        }

                        // Execute the task we pulled out of the queue.
                        TryExecuteTask(nextTask);
                    }
                }
                // We're done processing items on the current thread.
                finally
                {
                    _currentThreadIsProcessingItems = false;
                }
            })
            {
                IsBackground = true
            }.Start(task);
        }


#if !NET8_0_OR_GREATER
        /// <summary>
        /// Throw <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        /// <typeparam name="T">The type of the objects.</typeparam>
        /// <param name="value">The maxDegreeOfParallelism of the argument that causes this exception.</param>
        /// <param name="other">The maxDegreeOfParallelism to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        /// <exception cref="ArgumentOutOfRangeException">Always thrown.</exception>
#if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1
        [DoesNotReturn]
#endif  // NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1
        private static void ThrowLess<T>(T value, T other, string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"'{value}' must be greater than or equal to '{other}'.");
        }

        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is less than <paramref name="other"/>.
        /// </summary>
        /// <typeparam name="T">The type of the objects to validate.</typeparam>
        /// <param name="value">The argument to validate as greater than or equal to <paramref name="other"/>.</param>
        /// <param name="other">The maxDegreeOfParallelism to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="value"/> is less than <paramref name="other"/>.</exception>
        internal static void ThrowIfLessThan<T>(T value, T other, string paramName)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) < 0)
            {
                ThrowLess(value, other, paramName);
            }
        }
#endif  // !NET8_0_OR_GREATER
    }
}
