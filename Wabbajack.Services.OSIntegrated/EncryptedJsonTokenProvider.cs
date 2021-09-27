using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Services.OSIntegrated
{
    public class EncryptedJsonTokenProvider<T> : ITokenProvider<T>
    {
        private readonly ILogger _logger;
        private readonly string _key;
        private readonly DTOSerializer _dtos;

        public EncryptedJsonTokenProvider(ILogger logger, DTOSerializer dtos, string key)
        {
            _logger = logger;
            _key = key;
            _dtos = dtos;
        }

        private string? EnvValue => Environment.GetEnvironmentVariable(_key.ToUpperInvariant().Replace("-", "_"));

        public bool HaveToken()
        {
            return KeyPath.FileExists() || EnvValue != null;
        }

        public void DeleteToken()
        {
            _logger.LogInformation("Deleting token {token}", _key);
            if (HaveToken()) 
                KeyPath.Delete();
        }

        public async ValueTask SetToken(T token)
        {
            _logger.LogInformation("Setting token {token}", _key);
            await token.AsEncryptedJsonFile(KeyPath);
        }

        public async ValueTask<bool> TryDelete(T val)
        {
            if (!KeyPath.FileExists()) return false;
            KeyPath.Delete();
            return true;

        }

        public async ValueTask<T?> Get()
        {
            var path = KeyPath;
            if (path.FileExists())
            {
                return await path.FromEncryptedJsonFile<T>();
            }
            else
            {
                var value = EnvValue;
                if (value == default)
                {
                    _logger.LogCritical("No login data for {key}", _key);
                    throw new Exception("No login data for {_key}");
                }
                return _dtos.Deserialize<T>(EnvValue!);
            }
        }

        private AbsolutePath KeyPath => KnownFolders.WabbajackAppLocal.Combine("encrypted", _key);
    }
}