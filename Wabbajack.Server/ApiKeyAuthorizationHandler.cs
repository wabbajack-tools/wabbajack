using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;


namespace Wabbajack.BuildServer
{

    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        public const string DefaultScheme = "API Key";
        public string Scheme => DefaultScheme;
        public string AuthenticationType = DefaultScheme;
    }

    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        private const string ProblemDetailsContentType = "application/problem+json";
        private readonly SqlService _sql;
        public const string ApiKeyHeaderName = "X-Api-Key";

        private MetricsKeyCache _keyCache;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            MetricsKeyCache keyCache,
            SqlService db) : base(options, logger, encoder, clock)
        {
            _sql = db;
            _keyCache = keyCache;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var metricsKey = Request.Headers[Consts.MetricsKeyHeader].FirstOrDefault();
            // Never needed this, disabled for now
            //await LogRequest(metricsKey);
            if (metricsKey != default)
            {
                await _keyCache.AddKey(metricsKey);
                if (await _sql.IsTarKey(metricsKey))
                {
                    await _sql.IngestMetric(new Metric
                    {
                        Action = "TarKey",
                        Subject = "Auth",
                        MetricsKey = metricsKey,
                        Timestamp = DateTime.UtcNow
                    });
                    await Task.Delay(TimeSpan.FromSeconds(60));
                    throw new Exception("Error, lipsum timeout of the cross distant cloud.");
                }
            }

            var authorKey = Request.Headers[ApiKeyHeaderName].FirstOrDefault();

            if (authorKey == null)
                Request.Cookies.TryGetValue(ApiKeyHeaderName, out authorKey);
                

            if (authorKey == null && metricsKey == null)
            {
                return AuthenticateResult.NoResult();
            }


            if (authorKey != null)
            {
                var owner = await _sql.LoginByApiKey(authorKey);
                if (owner == null)
                    return AuthenticateResult.Fail("Invalid author key");

                var claims = new List<Claim> {new Claim(ClaimTypes.Name, owner)};

                claims.Add(new Claim(ClaimTypes.Role, "Author"));
                claims.Add(new Claim(ClaimTypes.Role, "User"));

                var identity = new ClaimsIdentity(claims, Options.AuthenticationType);
                var identities = new List<ClaimsIdentity> {identity};
                var principal = new ClaimsPrincipal(identities);
                var ticket = new AuthenticationTicket(principal, Options.Scheme);

                return AuthenticateResult.Success(ticket);
            }
            

            if (!await _keyCache.IsValidKey(metricsKey))
            {
                return AuthenticateResult.Fail("Invalid Metrics Key");
            }
            else
            {
                var claims = new List<Claim> {new(ClaimTypes.Role, "User")};


                var identity = new ClaimsIdentity(claims, Options.AuthenticationType);
                var identities = new List<ClaimsIdentity> {identity};
                var principal = new ClaimsPrincipal(identities);
                var ticket = new AuthenticationTicket(principal, Options.Scheme);

                return AuthenticateResult.Success(ticket);
            }
        }

        [JsonName("RequestLog")]
        public class RequestLog
        {
            public string Path { get; set; }
            public string Query { get; set; }
            public Dictionary<string, string[]> Headers { get; set; }
        }
        private async Task LogRequest(string metricsKey)
        {
            var action = new RequestLog {
                Path = Request.Path, 
                Query = Request.QueryString.Value, 
                Headers = Request.Headers.GroupBy(s => s.Key)
                    .ToDictionary(s => s.Key, s => s.SelectMany(v => v.Value).ToArray())
            };
            var ip = Request.Headers["CF-Connecting-IP"].FirstOrDefault() ??
                     Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
                     Request.HttpContext.Connection.RemoteIpAddress.ToString();
            await _sql.IngestAccess(ip, action.ToJson());
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 401;
            Response.ContentType = ProblemDetailsContentType;
            await Response.WriteAsync("Unauthorized");
        }

        protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 403;
            Response.ContentType = ProblemDetailsContentType;
            await Response.WriteAsync("forbidden");
        }
    }

    public static class ApiKeyAuthorizationHandlerExtensions
    {
        public static AuthenticationBuilder AddApiKeySupport(this AuthenticationBuilder authenticationBuilder, Action<ApiKeyAuthenticationOptions> options)
        {
            return authenticationBuilder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.DefaultScheme, options);
        }

    }
}
