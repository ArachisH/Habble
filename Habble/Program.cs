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
        private readonly IScheduler _scheduler;
        private readonly IDictionary<string, PhysicalCommandAttribute> _commands;

        private const ConsoleColor LOGO_COLOR = ConsoleColor.DarkCyan;

        public Program()
        {
            _scheduler = new StdSchedulerFactory().GetScheduler().GetAwaiter().GetResult();
            _commands = new Dictionary<string, PhysicalCommandAttribute>();

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

            var trigger = TriggerBuilder.Create()
                .WithDailyTimeIntervalSchedule(s => s
                    .OnEveryDay()
                    .WithIntervalInHours(6)
                    .StartingDailyAt(TimeOfDay.HourAndMinuteOfDay(1, 56)))
                .Build();

            await _scheduler.ScheduleJob(
                JobBuilder.Create<RevisionUpdaterJob>().Build(), trigger).ConfigureAwait(false);

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
        #endregion
    }
}