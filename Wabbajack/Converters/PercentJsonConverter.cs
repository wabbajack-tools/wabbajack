using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack
{
    public class PercentJsonConverter : JsonConverter<Percent>
    {
        public override Percent ReadJson(JsonReader reader, Type objectType, [AllowNull] Percent existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            double d = (double)reader.Value;
            return Percent.FactoryPutInRange(d);
        }

        public override void WriteJson(JsonWriter writer, [AllowNull] Percent value, JsonSerializer serializer)
        {
            writer.WriteValue(value.Value);
        }
    }
}
