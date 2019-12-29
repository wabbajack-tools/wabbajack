using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.CacheServer.DTOs
{
    public static class SerializerSettings
    {
        public static void Init()
        {
            var dis = new TypeDiscriminator(typeof(AbstractDownloadState), AbstractDownloadState.NameToType,
                AbstractDownloadState.TypeToName);
            BsonSerializer.RegisterDiscriminatorConvention(typeof(AbstractDownloadState), dis);
        }
    }


    public class TypeDiscriminator : IDiscriminatorConvention
    {
        private readonly Type defaultType;
        private readonly Dictionary<string, Type> typeMap;
        private Dictionary<Type, string> revMap;

        public TypeDiscriminator(Type defaultType,
            Dictionary<string, Type> typeMap, Dictionary<Type, string> revMap)
        {
            this.defaultType = defaultType;
            this.typeMap = typeMap;
            this.revMap = revMap;
        }


        /// <summary>
        /// Element Name
        /// </summary>
        public string ElementName => "_wjType";

        public Type GetActualType(IBsonReader bsonReader, Type nominalType)
        {
            Type type = defaultType;
            var bookmark = bsonReader.GetBookmark();
            bsonReader.ReadStartDocument();
            if (bsonReader.FindElement(ElementName))
            {
                var value = bsonReader.ReadString();
                if (typeMap.ContainsKey(value))
                    type = typeMap[value];
            }

            bsonReader.ReturnToBookmark(bookmark);
            return type;
        }

        public BsonValue GetDiscriminator(Type nominalType, Type actualType)
        {
            return revMap[actualType];
        }
    }
}
