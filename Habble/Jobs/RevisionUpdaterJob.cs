using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

using Sulakore.Habbo;
using Sulakore.Habbo.Web;
using Sulakore.Habbo.Messages;

using Quartz;

using Newtonsoft.Json;

using static Habble.Utilities.HConsole;

namespace Habble.Jobs
{
    public class RevisionUpdaterJob : IJob
    {
        private const string BASE_DIRECTORY =
#if DEBUG
            "";
#else
            "/var/www/sites/api.harble.net/";
#endif

        public async Task Execute(IJobExecutionContext context)
        {
            "Updating Revisions...".WriteLine(ConsoleColor.Cyan);

            int newRevisions = 0;
            var lastChecked = DateTime.UtcNow;
            var lastCheckedGroups = new List<LastCheckedGroup>();

            Directory.CreateDirectory(BASE_DIRECTORY + "revisions");
            if (!File.Exists(BASE_DIRECTORY + "Hashes.ini"))
            {
                File.Copy("Hashes.ini", BASE_DIRECTORY + "Hashes.ini");
            }

            Array hotels = Enum.GetValues(typeof(HHotel));
            foreach (HHotel hotel in hotels)
            {
                if (hotel == HHotel.Unknown) continue;

                string revision = await HAPI.GetLatestRevisionAsync(hotel).ConfigureAwait(false);
                lastCheckedGroups.Add(new LastCheckedGroup(hotel, revision, lastChecked));

                if (File.Exists($"{BASE_DIRECTORY}revisions/{revision}.json")) continue;

                newRevisions++;
                ("Extracting Messages(Name, Hash, Structure)... | ", revision).WriteLine(null, ConsoleColor.Yellow);

                HGame game = await HAPI.GetGameAsync(revision).ConfigureAwait(false);
                game.GenerateMessageHashes();

                await File.WriteAllTextAsync($"{BASE_DIRECTORY}revisions/{revision}.json", JsonConvert.SerializeObject(new
                {
                    game.Revision,
                    game.FileLength,
                    Incoming = GetGroupedMessages(game, new Incoming(game, $"{BASE_DIRECTORY}Hashes.ini")),
                    Outgoing = GetGroupedMessages(game, new Outgoing(game, $"{BASE_DIRECTORY}Hashes.ini"))
                }))
                .ConfigureAwait(false);
            }

            ("Revision Updates Found: ", newRevisions).WriteLine(null, ConsoleColor.Green);
            await File.WriteAllTextAsync($"{BASE_DIRECTORY}last.json", JsonConvert.SerializeObject(lastCheckedGroups)).ConfigureAwait(false);

            var nextFireDate = (context.NextFireTimeUtc ?? DateTimeOffset.MinValue);
            if (nextFireDate != DateTimeOffset.MinValue)
            {
                ("Upcoming Revision Check: ", nextFireDate.ToString("MM/dd/yyyy HH:mm:ss GMT")).WriteLine(null, ConsoleColor.Yellow);
            }
        }

        private Dictionary<ushort, MessageGroup> GetGroupedMessages(HGame game, Identifiers identifiers)
        {
            var messageGroups = new Dictionary<ushort, MessageGroup>();
            foreach (ushort id in identifiers)
            {
                if (id == ushort.MaxValue) continue;

                string[] structure = null;
                string name = identifiers.GetName(id);
                string hash = identifiers.GetHash(id);

                MessageItem message = game.Messages[hash][0];
                if ((message.Structure?.Length ?? 0) > 0)
                {
                    structure = message.Structure;
                }

                messageGroups.Add(id, new MessageGroup(name, hash, structure));
            }
            return messageGroups;
        }

        private struct MessageGroup
        {
            public string Name { get; }
            public string Hash { get; }
            public string[] Structure { get; }

            public MessageGroup(string name, string hash, string[] structure)
            {
                Name = name;
                Hash = hash;
                Structure = structure;
            }
        }
        private struct LastCheckedGroup
        {
            public string Hotel { get; }
            public string Revision { get; }
            public DateTime LastChecked { get; }

            public LastCheckedGroup(HHotel hotel, string revision, DateTime lastChecked)
            {
                Hotel = "." + hotel.ToDomain();
                Revision = revision;
                LastChecked = lastChecked;
            }
        }
    }
}