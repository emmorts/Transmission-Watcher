using System;
using System.Threading.Tasks;
using Transmission.API.RPC;
using System.Collections.Generic;
using System.Threading;
using Transmission.API.RPC.Entity;

namespace transmission_watcher
{
    public class TransmissionWatcher
    {
        private readonly string[] _fields = { "id", "isPrivate", "name", "status", "uploadRatio", "downloadDir", "peersConnected" };

        private Client _transmissionClient;

        public async Task Start()
        {
            Console.WriteLine("Starting Transmission Watcher...");
            var transmissionPath = Environment.GetEnvironmentVariable("TRANSMISSION_PATH");
            var url = $"http://{transmissionPath}";
            _transmissionClient = new Client(url);
            await CheckTorrents();
        }

        private async Task CheckTorrents()
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));

            while (await timer.WaitForNextTickAsync())
            {
                TransmissionTorrents torrents;
                
                try
                {
                    torrents = await _transmissionClient.TorrentGetAsync(_fields);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to fetch torrents!");

                    continue;
                }
                Console.WriteLine($"Total Torrents: {torrents.Torrents.Length}");
                var toStopList = new List<object>();
                double.TryParse(Environment.GetEnvironmentVariable("STOP_RATIO"), out var stopRatio);
                bool.TryParse(Environment.GetEnvironmentVariable("HELP_SOLO_PEERS"), out var helpSoloPeers);
                foreach (var torrent in torrents.Torrents)
                {
                    if (torrent.Status == 6) //seeding
                    {
                        Console.WriteLine($"{torrent.Name} is finished, it's seed ratio is {torrent.uploadRatio}");
                        
                        if (torrent.uploadRatio >= stopRatio && (!helpSoloPeers || torrent.PeersConnected > 2 || torrent.PeersConnected == 0))
                        {
                            Console.WriteLine($"Goodbye forever, {torrent.Name}. You won't be missed.");
                            toStopList.Add(torrent.ID);
                        }
                    }
                }
                if (toStopList.Count > 0)
                {
                    Console.WriteLine($"Stopping {toStopList.Count} torrents.");
                    _transmissionClient.TorrentStopAsync(toStopList.ToArray());
                }
                
                Console.WriteLine("Sleeping for 60 seconds.");
            }
        }
    }
}