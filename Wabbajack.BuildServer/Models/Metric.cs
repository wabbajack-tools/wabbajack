using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.GraphQL;
using Wabbajack.Common;


namespace Wabbajack.BuildServer.Models
{
    public class Metric
    {
        [BsonId]
        public ObjectId Id;
        public DateTime Timestamp;
        public string Action;
        public string Subject;
        public string MetricsKey;

        
        public static async Task<IEnumerable<MetricResult>> Report(DBContext db, string grouping)
        {
            var data = await db.Metrics.AsQueryable()
                .Where(m => m.MetricsKey != null)
                .Where(m => m.Action == grouping)
                .Where(m => m.Subject != "Default")
                .ToListAsync();

            var minDate = DateTime.Parse(data.Min(d => d.Timestamp.ToString("yyyy-MM-dd")));
            var maxDate = DateTime.Parse(data.Max(d => d.Timestamp.ToString("yyyy-MM-dd")));

            var dateArray = Enumerable.Range(0, (int)(maxDate - minDate).TotalDays + 1)
                .Select(idx => minDate + TimeSpan.FromDays(idx))
                .Select(date => date.ToString("yyyy-MM-dd"))
                .ToList();
            
            var results = data
                .Where(d => !Guid.TryParse(d.Subject, out var _))
                .GroupBy(d => d.Subject)
                .Select(by_series =>
                {
                    var by_day = by_series.GroupBy(d => d.Timestamp.ToString("yyyy-MM-dd"))
                        .Select(d => (d.Key, d.DistinctBy(v => v.MetricsKey ?? "").Count()))
                        .OrderBy(r => r.Key);
                    
                    var by_day_idx = by_day.ToDictionary(d => d.Key);

                    (string Key, int) GetEntry(string date)
                    {
                        if (by_day_idx.TryGetValue(date, out var result))
                            return result;
                        return (date, 0);
                    } 
                    
                    return new MetricResult
                    {
                        SeriesName = by_series.Key,
                        Labels = dateArray.Select(d => GetEntry(d).Key).ToList(),
                        Values = dateArray.Select(d => GetEntry(d).Item2).ToList()
                    };
                })
                .OrderBy(f => f.SeriesName);

            return results;
        }
    }
}
