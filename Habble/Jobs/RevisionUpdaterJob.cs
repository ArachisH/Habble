using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;

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
        private readonly string _hashesPath = AppDomain.CurrentDomain.BaseDirectory + "Hashes.ini";
        private readonly string _apiDirectory =
#if DEBUG
            Environment.CurrentDirectory + "/api.harble.net/";
#else
            "/var/www/sites/api.harble.net/";
#endif

        public async Task Execute(IJobExecutionContext context)
        {
            "Updating Revisions...".WriteLine(ConsoleColor.Cyan);

            var lastChecked = DateTime.UtcNow;
            var lastCheckedGroups = new List<LastCheckedGroup>();

            var messagesDir = Directory.CreateDirectory(_apiDirectory + "messages");
            string hashesPath = _apiDirectory + "hashes.ini";
            string hashesMD5 = GetFileMD5(hashesPath);

            File.Copy(_hashesPath, hashesPath, true); // Always attempt to copy the hashes, in case they may have been updated.
            if (!GetFileMD5(hashesPath).Equals(hashesMD5, StringComparison.OrdinalIgnoreCase))
            {
                foreach (FileInfo file in messagesDir.EnumerateFiles())
                {
                    file.Delete();
                    $"Deleted '{file.Name}' due to a change in the hashes file.".WriteLine(ConsoleColor.Red);
                }
            }

            Array hotels = Enum.GetValues(typeof(HHotel));
            foreach (HHotel hotel in hotels)
            {
                if (hotel == HHotel.Unknown) continue;

                string revision = await HAPI.GetLatestRevisionAsync(hotel).ConfigureAwait(false);
                lastCheckedGroups.Add(new LastCheckedGroup(hotel, revision, lastChecked));

                if (File.Exists($"{_apiDirectory}messages/{revision}.json")) continue;

                ("Extracting Messages(Id, Name, Hash, Structure)... | ", revision).WriteLine(null, ConsoleColor.Yellow);
                HGame game = await HAPI.GetGameAsync(revision).ConfigureAwait(false);
                game.GenerateMessageHashes();

                await File.WriteAllTextAsync($"{_apiDirectory}messages/{revision}.json", JsonConvert.SerializeObject(new
                {
                    game.Revision,
                    game.FileLength,
                    Incoming = GetMessages(game, new Incoming(game, _hashesPath)),
                    Outgoing = GetMessages(game, new Outgoing(game, _hashesPath))
                }))
                .ConfigureAwait(false);
            }
            await File.WriteAllTextAsync($"{_apiDirectory}last.json", JsonConvert.SerializeObject(lastCheckedGroups)).ConfigureAwait(false);

            var nextFireDate = context.NextFireTimeUtc ?? DateTimeOffset.MinValue;
            if (nextFireDate != DateTimeOffset.MinValue)
            {
                ("Upcoming Revision Check: ", nextFireDate.ToString("MM/dd/yyyy HH:mm:ss GMT")).WriteLine(null, ConsoleColor.Yellow);
            }
        }

        private string GetFileMD5(string path)
        {
            if (!File.Exists(path)) return null;

            using (var md5 = MD5.Create())
            using (var pathStream = File.OpenRead(path))
            {
                return BitConverter.ToString(md5.ComputeHash(pathStream)).Replace("-", "").ToLower();
            }
        }
        private List<Message> GetMessages(HGame game, HMessages messages)
        {
            var messageGroups = new List<Message>();
            foreach (ushort id in messages)
            {
                if (id == ushort.MaxValue) continue;

                string structure = null;
                string name = messages.GetName(id);
                string hash = messages.GetHash(id);

                MessageItem message = game.Messages[hash][0];
                if ((message.Structure?.Length ?? 0) > 0)
                {
                    structure = message.Structure;
                }

                messageGroups.Add(new Message(id, name, hash, structure));
            }
            return messageGroups;
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