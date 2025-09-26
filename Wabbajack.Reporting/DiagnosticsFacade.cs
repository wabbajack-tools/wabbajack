namespace Wabbajack.Reporting;

using System;
using System.IO;


public static class DiagnosticsFacade
{
    public static DiagnosticResult AnalyzeText(string logText,
                                                       string yamlUrl,
                                                       string rawBase)
    {
        var repo = RemoteTagRepository.LoadFromUrl(yamlUrl, rawBase);
        var svc = new LogDiagnosticService(repo);
        return svc.Analyze(logText ?? string.Empty);
    }
}
