using System;
using System.Data;
using Dapper;
using Wabbajack.DTOs;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.Server.DataLayer;

public partial class SqlService
{
    private static DTOSerializer _dtoStatic;

    static SqlService()
    {
        SqlMapper.AddTypeHandler(new HashMapper());
        SqlMapper.AddTypeHandler(new RelativePathMapper());
        SqlMapper.AddTypeHandler(new JsonMapper<IDownloadState>());
        SqlMapper.AddTypeHandler(new JsonMapper<FileDefinition>());
        SqlMapper.AddTypeHandler(new JsonMapper<ModlistMetadata>());
        SqlMapper.AddTypeHandler(new VersionMapper());
        SqlMapper.AddTypeHandler(new GameMapper());
        SqlMapper.AddTypeHandler(new DateTimeHandler());
    }

    /// <summary>
    ///     Needed to make sure dates are all in UTC format
    /// </summary>
    private class DateTimeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            parameter.Value = value;
        }

        public override DateTime Parse(object value)
        {
            return DateTime.SpecifyKind((DateTime) value, DateTimeKind.Utc);
        }
    }

    private class JsonMapper<T> : SqlMapper.TypeHandler<T>
    {
        public override void SetValue(IDbDataParameter parameter, T value)
        {
            parameter.Value = _dtoStatic.Serialize(value);
        }

        public override T Parse(object value)
        {
            return _dtoStatic.Deserialize<T>((string) value)!;
        }
    }

    private class RelativePathMapper : SqlMapper.TypeHandler<RelativePath>
    {
        public override void SetValue(IDbDataParameter parameter, RelativePath value)
        {
            parameter.Value = value.ToString();
        }

        public override RelativePath Parse(object value)
        {
            return (RelativePath) (string) value;
        }
    }

    private class HashMapper : SqlMapper.TypeHandler<Hash>
    {
        public override void SetValue(IDbDataParameter parameter, Hash value)
        {
            parameter.Value = (long) value;
        }

        public override Hash Parse(object value)
        {
            return Hash.FromLong((long) value);
        }
    }

    private class VersionMapper : SqlMapper.TypeHandler<Version>
    {
        public override void SetValue(IDbDataParameter parameter, Version value)
        {
            parameter.Value = value.ToString();
        }

        public override Version Parse(object value)
        {
            return Version.Parse((string) value);
        }
    }

    private class GameMapper : SqlMapper.TypeHandler<Game>
    {
        public override void SetValue(IDbDataParameter parameter, Game value)
        {
            parameter.Value = value.ToString();
        }

        public override Game Parse(object value)
        {
            return GameRegistry.GetByFuzzyName((string) value).Game;
        }
    }
}