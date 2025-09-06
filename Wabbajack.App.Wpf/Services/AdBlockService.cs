using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DistillNET;
using Microsoft.Extensions.Logging;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Wpf.Services;

public class AdBlockService
{
    private readonly ILogger<AdBlockService> _logger;
    private readonly HttpClient _httpClient;
    private readonly Filter _filter;

    private readonly List<string> _filterListUrls = new()
    {
        // uBlock Origin filters
        "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/filters.txt",
        "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/badware.txt",
        "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/privacy.txt",
        "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/resource-abuse.txt",
        // EasyList
        "https://easylist.to/easylist/easylist.txt",
        "https://easylist.to/easylist/easyprivacy.txt",
        // Peter Lowe's
        "https://pgl.yoyo.org/adservers/serverlist.php?hostformat=adblockplus&mimetype=plaintext"
    };

    public AdBlockService(ILogger<AdBlockService> logger, HttpClient httpClient, TemporaryFileManager temporaryFileManager)
    {
        _logger = logger;
        _httpClient = httpClient;
        _filter = new Filter(storagePath: temporaryFileManager.CreateFolder());
    }

    public async Task Initialize()
    {
        _logger.LogInformation("Initializing AdBlockService...");
        try
        {
            var allFilters = new List<string>();
            foreach (var url in _filterListUrls)
            {
                try
                {
                    _logger.LogInformation("Downloading filter list from {url}", url);
                    var filterData = await _httpClient.GetStringAsync(url);
                    allFilters.Add(filterData);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download filter list from {url}", url);
                }
            }

            var combinedFilters = string.Join(Environment.NewLine, allFilters);
            await _filter.Parse(combinedFilters);
            _logger.LogInformation("AdBlockService initialized with {count} rules.", _filter.RuleCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize AdBlockService");
        }
    }

    public bool IsBlocked(Uri uri)
    {
        if (_filter.RuleCount == 0) return false;

        var requestInfo = new RequestInfo(uri.ToString(), uri.Host, "other");
        return _filter.ShouldFilter(requestInfo);
    }
}
