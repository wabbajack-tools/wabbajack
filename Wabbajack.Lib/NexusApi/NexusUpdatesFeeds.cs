using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using System.Xml;
using Wabbajack.Common;

namespace Wabbajack.Lib.NexusApi
{
    public class NexusUpdatesFeeds
    {

        public static async Task<List<UpdateRecord>> GetUpdates()
        {
            var updated = GetFeed(new Uri("https://www.nexusmods.com/rss/updatedtoday"));
            var newToday = GetFeed(new Uri("https://www.nexusmods.com/rss/newtoday"));

            var sorted = (await updated).Concat(await newToday).OrderByDescending(f => f.TimeStamp);
            var deduped = sorted.GroupBy(g => (g.Game, g.ModId)).Select(g => g.First()).ToList();
            return deduped;
        }

        private static bool TryParseGameUrl(SyndicationLink link, out Game game, out long modId)
        {
            var parts = link.Uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (!GameRegistry.TryGetByFuzzyName(parts[0], out var foundGame))
            {
                game = Game.Oblivion;
                modId = 0;
                return false;
            }

            if (long.TryParse(parts[2], out modId))
            {
                game = foundGame.Game;
                return true;
            }

            game = Game.Oblivion;
            modId = 0;
            return false;
        }

        private static async Task<IEnumerable<UpdateRecord>> GetFeed(Uri uri)
        {
            var client = new Common.Http.Client();
            var data = await client.GetStringAsync(uri);
            var reader = XmlReader.Create(new StringReader(data));
            var results = SyndicationFeed.Load(reader);
            return results.Items
                .Select(itm =>
                {
                    if (TryParseGameUrl(itm.Links.First(), out var game, out var modId))
                    {
                        return new UpdateRecord
                        {
                            TimeStamp = itm.PublishDate.UtcDateTime, 
                            Game = game, 
                            ModId = modId
                        };
                    }

                    return null;
                })
                .NotNull();
        }


        public class UpdateRecord
        {
            public Game Game { get; set; }
            public long ModId { get; set; }
            public DateTime TimeStamp { get; set; }
        }
            
    }
}
