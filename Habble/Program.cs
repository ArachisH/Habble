using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;

using Habble.Jobs;

using Quartz;
using Quartz.Impl;

using static Habble.Utilities.HConsole;

namespace Habble
{
    public class Program
    {
        private TriggerKey _checkScheduleKey;
        private readonly IScheduler _scheduler;
        private readonly IJobDetail _checkRevisionsJob;
        private readonly IDictionary<string, PhysicalCommandAttribute> _commands;

        private const ConsoleColor LOGO_COLOR = ConsoleColor.DarkCyan;

        public Program()
        {
            _commands = new Dictionary<string, PhysicalCommandAttribute>();
            _checkRevisionsJob = JobBuilder.Create<RevisionUpdaterJob>().Build();
            _scheduler = new StdSchedulerFactory().GetScheduler().GetAwaiter().GetResult();

            foreach (MethodInfo method in GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var commandAtt = method.GetCustomAttribute<PhysicalCommandAttribute>();
                if (commandAtt == null) continue;

                commandAtt.Method = method;
                _commands.Add(commandAtt.Name, commandAtt);
            }
        }
        public static void Main(string[] args) =>
            new Program().RunAsync().GetAwaiter().GetResult();

        public async Task RunAsync()
        {
            EmptyLine();
            @"    _   _       _     _     _      ".AppendLine(LOGO_COLOR);
            @"   | | | | __ _| |__ | |__ | | ___ ".AppendLine(LOGO_COLOR);
            @"   | |_| |/ _` | '_ \| '_ \| |/ _ \".AppendLine(LOGO_COLOR);
            @"   |  _  | (_| | |_) | |_) | |  __/".AppendLine(LOGO_COLOR);
            @"   |_| |_|\__,_|_.__/|_.__/|_|\___|".AppendLine(LOGO_COLOR);
            EmptyLine();

            await RescheduleCheckCommandAsync("0 0 0/6 1/1 * ? *").ConfigureAwait(false);

            await _scheduler.Start().ConfigureAwait(false);
            while (!_scheduler.IsShutdown)
            {
                string line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var arguments = new Queue<string>(line.Split(' '));
                if (!_commands.TryGetValue(arguments.Dequeue().Trim().ToLower(), out PhysicalCommandAttribute command)) continue;

                using (Lock())
                {
                    try
                    {
                        object result = command.Invoke(this, arguments);
                        if (result is Task resultTask)
                        {
                            await resultTask.ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex) { Error(ex); }
                }
            }
        }

        #region Physical Commands
        [PhysicalCommand("exit")]
        private Task ExitCommand(bool waitForJobsToComplete = false)
        {
            "Shutting Down...".WriteLine(ConsoleColor.Yellow);
            return _scheduler.Shutdown(waitForJobsToComplete);
        }

        [PhysicalCommand("check")]
        private Task CheckCommand()
        {
            return _scheduler.ScheduleJob(
                JobBuilder.Create<RevisionUpdaterJob>().Build(),
                TriggerBuilder.Create().StartNow().Build());
        }

        [PhysicalCommand("rc")]
        private async Task RescheduleCheckCommandAsync(string cronExpression)
        {
            ITrigger trigger = TriggerBuilder.Create()
                .WithCronSchedule(cronExpression, c => c.InTimeZone(TimeZoneInfo.Utc))
                .Build();

            if (_checkScheduleKey != null)
            {
                _scheduler.RescheduleJob(_checkScheduleKey, trigger).GetAwaiter().GetResult();
            }
            else await _scheduler.ScheduleJob(_checkRevisionsJob, trigger).ConfigureAwait(false);

            _checkScheduleKey = trigger.Key;
            ("Upcoming Revision Check: ", $"{trigger.GetNextFireTimeUtc():MM/dd/yyyy HH:mm:ss} GMT").WriteLine(null, ConsoleColor.Yellow);
        }
        #endregion
    }
}