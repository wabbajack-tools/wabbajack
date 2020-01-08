using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Lib.Downloaders;
using Newtonsoft.Json.

namespace Wabbajack.BuildServer
{
    public static class SerializerSettings
    {
        public static void Init()
        {
            var dis = new TypeDiscriminator(typeof(AbstractDownloadState), AbstractDownloadState.NameToType,
                AbstractDownloadState.TypeToName);
            BsonSerializer.RegisterDiscriminatorConvention(typeof(AbstractDownloadState), dis);
            BsonClassMap.RegisterClassMap<AbstractDownloadState>(cm => cm.SetIsRootClass(true));

            dis = new TypeDiscriminator(typeof(AJobPayload), AJobPayload.NameToType, AJobPayload.TypeToName);
            BsonSerializer.RegisterDiscriminatorConvention(typeof(AJobPayload), dis);
            BsonClassMap.RegisterClassMap<AJobPayload>(cm => cm.SetIsRootClass(true));
           
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
            Type type = null;
            var bookmark = bsonReader.GetBookmark();
            bsonReader.ReadStartDocument();
            if (bsonReader.FindElement(ElementName))
            {
                var value = bsonReader.ReadString();
                if (typeMap.ContainsKey(value))
                    type = typeMap[value];
            }

            bsonReader.ReturnToBookmark(bookmark);
            if (type == null)
                throw new Exception($"Type mis-configuration can't find bson type for ${nominalType}");
            return type;
        }

        public BsonValue GetDiscriminator(Type nominalType, Type actualType)
        {
            return revMap[actualType];
        }
    }
}
