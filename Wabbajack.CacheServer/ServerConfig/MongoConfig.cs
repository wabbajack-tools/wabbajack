using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using Wabbajack.CacheServer.DTOs;

namespace Wabbajack.CacheServer.ServerConfig
{
    public class MongoConfig<T>
    {
        public string Host { get; set; }
        public string Database { get; set; }
        public string Collection { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        private IMongoDatabase Client
        {
            get
            {
                if (Username != null && Password != null)
                    return new MongoClient($"mongodb://{Username}:{Password}@{Host}").GetDatabase(Database);
                return new MongoClient($"mongodb://{Host}").GetDatabase(Database);
            }
        }

        public IMongoCollection<T> Connect()
        {
            return Client.GetCollection<T>(Collection);
        }
    }
}
