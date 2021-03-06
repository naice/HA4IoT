﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using HA4IoT.Contracts.Api;
using HA4IoT.Contracts.Core;
using HA4IoT.Contracts.Logging;
using HA4IoT.Contracts.Services;
using HA4IoT.Networking;

namespace HA4IoT.Core.Scheduling
{
    public class SchedulerService : ServiceBase, ISchedulerService
    {
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, Schedule> _schedules = new Dictionary<string, Schedule>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly IHomeAutomationTimer _timer;

        public SchedulerService(IHomeAutomationTimer timer)
        {
            if (timer == null) throw new ArgumentNullException(nameof(timer));

            _timer = timer;
        }

        public TimedAction In(TimeSpan dueTime)
        {
            return new TimedAction(dueTime, TimeSpan.Zero, _timer);
        }

        public override void HandleApiRequest(IApiContext apiContext)
        {
            lock (_syncRoot)
            {
                foreach (var schedule in _schedules)
                {
                    var scheduleObject = new JsonObject();
                    scheduleObject.SetNamedDateTime("LastExecutionTimestamp", schedule.Value.LastExecutionTimestamp);
                    scheduleObject.SetNamedTimeSpan("LastExecutionDuration", schedule.Value.LastExecutionDuration);
                    scheduleObject.SetNamedTimeSpan("Interval", schedule.Value.Interval);
                    scheduleObject.SetNamedString("LastErrorMessage", schedule.Value.LastErrorMessage);

                    apiContext.Response.SetNamedObject(schedule.Key, scheduleObject);
                }
            }
        }

        public void RegisterSchedule(string name, TimeSpan interval, Action action)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (action == null) throw new ArgumentNullException(nameof(action));

            lock (_syncRoot)
            {
                if (_schedules.ContainsKey(name))
                {
                    throw new InvalidOperationException($"Schedule with name '{name}' is already registered.");
                }

                var schedule = new Schedule(name, interval, action);
                _schedules.Add(name, schedule);

                var task = Task.Factory.StartNew(
                    () => ExecuteSchedule(schedule),
                    _cancellationTokenSource.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

                task.ConfigureAwait(false);

                Log.Info($"Registerd schedule '{name}' with interval of {interval}.");
            }
        }

        private void ExecuteSchedule(Schedule schedule)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                stopwatch.Restart();
                try
                {
                    Log.Verbose($"Executing schedule '{schedule.Name}'.");
                    schedule.Action();
                    schedule.LastErrorMessage = null;
                }
                catch (Exception exception)
                {
                    Log.Error(exception, $"Error while executing schedule '{schedule.Name}'.");
                    schedule.LastErrorMessage = exception.Message;
                }
                finally
                {
                    stopwatch.Stop();

                    schedule.LastExecutionDuration = stopwatch.Elapsed;
                    schedule.LastExecutionTimestamp = DateTime.Now;

                    Task.Delay(schedule.Interval).Wait();
                }
            }
        }
    }
}
