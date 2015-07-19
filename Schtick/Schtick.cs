﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Schyntax
{
    public delegate void ScheduledTaskCallback(ScheduledTask task, DateTime timeIntendedToRun);
    public delegate Task ScheduledTaskAsyncCallback(ScheduledTask task, DateTime timeIntendedToRun);

    public class Schtick
    {
        private readonly object _lockTasks = new object();
        private readonly Dictionary<string, ScheduledTask> _tasks = new Dictionary<string, ScheduledTask>();
        public bool IsShuttingDown { get; private set; }

        public event Action<ScheduledTask, Exception> OnTaskException;

        /// <summary>
        /// Adds a scheduled task to this instance of Schtick.
        /// </summary>
        /// <param name="name">A unique name for this task. If null, a guid will be used.</param>
        /// <param name="schedule">A Schyntax schedule string.</param>
        /// <param name="callback">Function which will be called each time the task is supposed to run.</param>
        /// <param name="autoRun">If true, Start() will be called on the task automatically.</param>
        /// <param name="lastKnownRun">The last Date when the task is known to have run. Used for Task Windows.</param>
        /// <param name="window">
        /// The period of time after an event should have run where it would still be appropriate to run it.
        /// See Task Windows documentation for more details.
        /// </param>
        public ScheduledTask AddTask(
            string name,
            string schedule,
            ScheduledTaskCallback callback,
            bool autoRun = true,
            DateTime lastKnownRun = default(DateTime),
            TimeSpan window = default(TimeSpan))
        {
            if (schedule == null)
                throw new ArgumentNullException(nameof(schedule));

            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            return AddTaskImpl(name, new Schedule(schedule), callback, null, autoRun, lastKnownRun, window);
        }

        /// <summary>
        /// Adds a scheduled task to this instance of Schtick.
        /// </summary>
        /// <param name="name">A unique name for this task. If null, a guid will be used.</param>
        /// <param name="schedule">A Schyntax schedule string.</param>
        /// <param name="asyncCallback">Function which will be called each time the task is supposed to run.</param>
        /// <param name="autoRun">If true, Start() will be called on the task automatically.</param>
        /// <param name="lastKnownRun">The last Date when the task is known to have run. Used for Task Windows.</param>
        /// <param name="window">
        /// The period of time after an event should have run where it would still be appropriate to run it.
        /// See Task Windows documentation for more details.
        /// </param>
        public ScheduledTask AddAsyncTask(
            string name,
            string schedule,
            ScheduledTaskAsyncCallback asyncCallback,
            bool autoRun = true,
            DateTime lastKnownRun = default(DateTime),
            TimeSpan window = default(TimeSpan))
        {
            if (schedule == null)
                throw new ArgumentNullException(nameof(schedule));

            if (asyncCallback == null)
                throw new ArgumentNullException(nameof(asyncCallback));

            return AddTaskImpl(name, new Schedule(schedule), null, asyncCallback, autoRun, lastKnownRun, window);
        }

        /// <summary>
        /// Adds a scheduled task to this instance of Schtick.
        /// </summary>
        /// <param name="name">A unique name for this task. If null, a guid will be used.</param>
        /// <param name="schedule">A Schyntax Schedule object.</param>
        /// <param name="callback">Function which will be called each time the task is supposed to run.</param>
        /// <param name="autoRun">If true, Start() will be called on the task automatically.</param>
        /// <param name="lastKnownRun">The last Date when the task is known to have run. Used for Task Windows.</param>
        /// <param name="window">
        /// The period of time after an event should have run where it would still be appropriate to run it.
        /// See Task Windows documentation for more details.
        /// </param>
        public ScheduledTask AddTask(
            string name,
            Schedule schedule,
            ScheduledTaskCallback callback,
            bool autoRun = true,
            DateTime lastKnownRun = default(DateTime),
            TimeSpan window = default(TimeSpan))
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            return AddTaskImpl(name, schedule, callback, null, autoRun, lastKnownRun, window);
        }


        /// <summary>
        /// Adds a scheduled task to this instance of Schtick.
        /// </summary>
        /// <param name="name">A unique name for this task. If null, a guid will be used.</param>
        /// <param name="schedule">A Schyntax Schedule object.</param>
        /// <param name="asyncCallback">Function which will be called each time the task is supposed to run.</param>
        /// <param name="autoRun">If true, Start() will be called on the task automatically.</param>
        /// <param name="lastKnownRun">The last Date when the task is known to have run. Used for Task Windows.</param>
        /// <param name="window">
        /// The period of time after an event should have run where it would still be appropriate to run it.
        /// See Task Windows documentation for more details.
        /// </param>
        public ScheduledTask AddAsyncTask(
            string name,
            Schedule schedule,
            ScheduledTaskAsyncCallback asyncCallback,
            bool autoRun = true,
            DateTime lastKnownRun = default(DateTime),
            TimeSpan window = default(TimeSpan))
        {
            if (asyncCallback == null)
                throw new ArgumentNullException(nameof(asyncCallback));

            return AddTaskImpl(name, schedule, null, asyncCallback, autoRun, lastKnownRun, window);
        }

        private ScheduledTask AddTaskImpl(
            string name,
            Schedule schedule,
            ScheduledTaskCallback callback,
            ScheduledTaskAsyncCallback asyncCallback,
            bool autoRun,
            DateTime lastKnownRun,
            TimeSpan window)
        {
            if (schedule == null)
                throw new ArgumentNullException(nameof(schedule));

            if (name == null)
                name = Guid.NewGuid().ToString();

            ScheduledTask task;
            lock (_lockTasks)
            {
                if (IsShuttingDown)
                    throw new Exception("Cannot add a task to Schtick after Shutdown() has been called.");

                if (_tasks.ContainsKey(name))
                    throw new Exception($"A scheduled task named \"{name}\" already exists.");

                task = new ScheduledTask(name, schedule, callback, asyncCallback)
                {
                    Window = window,
                    IsAttached = true,
                };

                _tasks.Add(name, task);
            }

            task.OnException += TaskOnOnException;

            if (autoRun)
                task.StartSchedule(lastKnownRun);

            return task;
        }

        private void TaskOnOnException(ScheduledTask task, Exception ex)
        {
            var ev = OnTaskException;
            ev?.Invoke(task, ex);
        }

        public bool TryGetTask(string name, out ScheduledTask task)
        {
            return _tasks.TryGetValue(name, out task);
        }

        public ScheduledTask[] GetAllTasks()
        {
            lock (_lockTasks)
            {
                // could be one line of linq, but eh, this is cheaper
                var tasks = new ScheduledTask[_tasks.Count];
                var i = 0;
                foreach(var t in _tasks)
                {
                    tasks[i] = t.Value;
                    i++;
                }

                return tasks;
            }
        }

        public bool RemoveTask(string name)
        {
            lock (_lockTasks)
            {
                if (IsShuttingDown)
                    throw new Exception("Cannot remove a task from Schtick after Shutdown() has been called.");

                ScheduledTask task;
                if (!_tasks.TryGetValue(name, out task))
                    return false;

                if (task.IsScheduleRunning)
                    throw new Exception($"Cannot remove task \"{name}\". It is still running.");

                task.IsAttached = false;
                _tasks.Remove(name);
                return true;
            }
        }

        public async Task Shutdown()
        {
            ScheduledTask[] tasks;
            lock (_lockTasks)
            {
                IsShuttingDown = true;
                tasks = GetAllTasks();
            }
            
            foreach (var t in tasks)
            {
                t.IsAttached = false; // prevent anyone from calling start on the task again
                t.StopSchedule();
            }

            while (true)
            {
                var allStopped = true;
                foreach (var t in tasks)
                {
                    if (t.IsCallbackExecuting)
                    {
                        allStopped = false;
                        break;
                    }
                }

                if (allStopped)
                    return;

                await Task.Delay(10).ConfigureAwait(false); // wait 10 milliseconds, then check again
            }
        }
    }

    public class ScheduledTask
    {
        private readonly object _scheduleLock = new object();
        private int _runId = 0;
        private int _execLocked = 0;

        public string Name { get; }
        public Schedule Schedule { get; private set; }
        public ScheduledTaskCallback Callback { get; }
        public ScheduledTaskAsyncCallback AsyncCallback { get; }
        public bool IsScheduleRunning { get; internal set; }
        public bool IsCallbackExecuting => _execLocked == 1;
        public bool IsAttached { get; internal set; }
        public TimeSpan Window { get; set; }
        public DateTime NextEvent { get; private set; }
        public DateTime PrevEvent { get; private set; }

        public event Action<ScheduledTask, Exception> OnException;

        internal ScheduledTask(string name, Schedule schedule, ScheduledTaskCallback callback, ScheduledTaskAsyncCallback asyncCallback)
        {
            Name = name;
            Schedule = schedule;

            if ((callback == null) == (asyncCallback == null))
                throw new Exception("callback or asyncCallback must be specified, but not both.");

            Callback = callback;
            AsyncCallback = asyncCallback;
        }

        public void StartSchedule(DateTime lastKnownRun = default(DateTime))
        {
            lock (_scheduleLock)
            {
                if (!IsAttached)
                    throw new InvalidOperationException("Cannot start task which is not attached to a Schtick instance.");

                if (IsScheduleRunning)
                    return;

                var firstEvent = default(DateTime);
                var firstEventSet = false;
                var window = Window;
                if (window > TimeSpan.Zero && lastKnownRun != default(DateTime))
                {
                    // check if we actually want to run the first event right away
                    var prev = Schedule.Previous();
                    lastKnownRun = lastKnownRun.AddSeconds(1); // add a second for good measure
                    if (prev > lastKnownRun && prev > (DateTime.UtcNow - window))
                    {
                        firstEvent = prev;
                        firstEventSet = true;
                    }
                }

                if (!firstEventSet)
                    firstEvent = Schedule.Next();

                while (firstEvent <= PrevEvent)
                {
                    // we don't want to run the same event twice
                    firstEvent = Schedule.Next(firstEvent);
                }

                NextEvent = firstEvent;
                var runId = _runId;
                Task.Run(() => Run(runId));

                IsScheduleRunning = true;
            }
        }
        
        public void StopSchedule()
        {
            lock (_scheduleLock)
            {
                if (!IsScheduleRunning)
                    return;

                _runId++;
                IsScheduleRunning = false;
            }
        }

        public void UpdateSchedule(string schedule)
        {
            UpdateSchedule(new Schedule(schedule));
        }

        public void UpdateSchedule(Schedule schedule)
        {
            if (schedule == null)
                throw new ArgumentNullException(nameof(schedule));

            lock (_scheduleLock)
            {
                var wasRunning = IsScheduleRunning;
                if (wasRunning)
                    StopSchedule();

                Schedule = schedule;

                if (wasRunning)
                    StartSchedule();
            }
        }

        private async Task Run(int runId)
        {
            while (true)
            {
                if (runId != _runId)
                    return;

                var eventTime = NextEvent;
                var delay = eventTime - DateTime.UtcNow;

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay).ConfigureAwait(false);

                var execLockTaken = false;
                try
                {
                    lock (_scheduleLock)
                    {
                        if (runId != _runId)
                            return;

                        // take execution lock
                        execLockTaken = Interlocked.CompareExchange(ref _execLocked, 1, 0) == 0;
                    }

                    if (execLockTaken) // if lock wasn't taken, then we're still executing from a previous event, which means we skip this one.
                    {
                        PrevEvent = eventTime;
                        try
                        {
                            if (Callback != null)
                                Callback(this, eventTime);
                            else
                                await AsyncCallback(this, eventTime).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            RaiseException(ex);
                        }

                        // figure out the next time to run the schedule
                        lock (_scheduleLock)
                        {
                            if (runId != _runId)
                                return;

                            try
                            {
                                var next = Schedule.Next();
                                if (next <= eventTime)
                                    next = Schedule.Next(eventTime);

                                NextEvent = next;
                            }
                            catch(Exception ex)
                            {
                                _runId++;
                                RaiseException(new ScheduleCrashException("Schtick Schedule has been terminated because the next valid time could not be found.", this, ex));
                                return;
                            }
                        }
                    }
                }
                finally
                {
                    if (execLockTaken)
                        _execLocked = 0; // release exec lock
                }
            }
        }

        private void RaiseException(Exception ex)
        {
            Task.Run(() =>
            {
                var ev = OnException;
                ev?.Invoke(this, ex);

            }).ContinueWith(task => { });
        }
    }
}
