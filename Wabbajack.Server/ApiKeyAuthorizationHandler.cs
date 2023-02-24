using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.GitHub;
using Wabbajack.Networking.GitHub.DTOs;
using Wabbajack.Server.DataModels;
using Wabbajack.Server.DTOs;

namespace Wabbajack.BuildServer;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "API Key";
    public string AuthenticationType = DefaultScheme;
    public string Scheme => DefaultScheme;
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ProblemDetailsContentType = "application/problem+json";
    public const string ApiKeyHeaderName = "X-Api-Key";
    private readonly DTOSerializer _dtos;
    private readonly AppSettings _settings;
    private readonly AuthorKeys _authorKeys;
    private readonly Metrics _metricsStore;
    private readonly TarLog _tarLog;
    private readonly Client _githubClient;
    private readonly MemoryCache _githubCache;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        AuthorKeys authorKeys,
        Client githubClient,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        DTOSerializer dtos,
        Metrics metricsStore,
        TarLog tarlog,
        AppSettings settings) : base(options, logger, encoder, clock)
    {

        _tarLog = tarlog;
        _metricsStore = metricsStore;
        _dtos = dtos;
        _authorKeys = authorKeys;
        _settings = settings;
        _githubClient = githubClient;
        _githubCache = new MemoryCache(new MemoryCacheOptions());
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var metricsKey = Request.Headers[_settings.MetricsKeyHeader].FirstOrDefault();
        // Never needed this, disabled for now
        //await LogRequest(metricsKey);
        if (metricsKey != default)
        {
            if (await _tarLog.Contains(metricsKey))
            {
                await _metricsStore.Ingest(new Metric
                {
                    Subject = metricsKey,
                    Action = "tarlog",
                    MetricsKey = metricsKey,
                    UserAgent = Request.Headers.UserAgent,
                    Ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
                });
                await Task.Delay(TimeSpan.FromSeconds(20));
                throw new Exception("Error, lipsum timeout of the cross distant cloud.");
            }
        }

        var authorKey = Request.Headers[ApiKeyHeaderName].FirstOrDefault();

        if (authorKey == null)
            Request.Cookies.TryGetValue(ApiKeyHeaderName, out authorKey);


        if (authorKey == null && metricsKey == null) return AuthenticateResult.NoResult();
        
        if (authorKey != null)
        {

            var owner = await _authorKeys.AuthorForKey(authorKey);
            if (owner == null)
            {
                var ghUser = await GetGithubUserInfo(authorKey);
                
                if (ghUser == null) 
                    return AuthenticateResult.Fail("Invalid author key");

                owner = "github/" + ghUser.Login;
            }

            if (await _tarLog.Contains(owner))
                return AuthenticateResult.Fail("Banned author key");
            
            var claims = new List<Claim> {new(ClaimTypes.Name, owner)};

            claims.Add(new Claim(ClaimTypes.Role, "Author"));
            claims.Add(new Claim(ClaimTypes.Role, "User"));

            var identity = new ClaimsIdentity(claims, Options.AuthenticationType);
            var identities = new List<ClaimsIdentity> {identity};
            var principal = new ClaimsPrincipal(identities);
            var ticket = new AuthenticationTicket(principal, Options.Scheme);

            return AuthenticateResult.Success(ticket);
        }

        
        if (!string.IsNullOrWhiteSpace(metricsKey))
        {
            var claims = new List<Claim> {new(ClaimTypes.Role, "User")};


            var identity = new ClaimsIdentity(claims, Options.AuthenticationType);
            var identities = new List<ClaimsIdentity> {identity};
            var principal = new ClaimsPrincipal(identities);
            var ticket = new AuthenticationTicket(principal, Options.Scheme);

            return AuthenticateResult.Success(ticket);
        }

        return AuthenticateResult.NoResult();
    }

    protected async Task<UserInfo?> GetGithubUserInfo(string authToken)
    {
        if (_githubCache.TryGetValue<UserInfo>(authToken, out var value)) return value;

        var info = await _githubClient.GetUserInfoFromPAT(authToken);
        if (info != null)
            _githubCache.Set(authToken, info,
                new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(6)));

        return info;
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
    public static AuthenticationBuilder AddApiKeySupport(this AuthenticationBuilder authenticationBuilder,
        Action<ApiKeyAuthenticationOptions> options)
    {
        return authenticationBuilder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme, options);
    }
}