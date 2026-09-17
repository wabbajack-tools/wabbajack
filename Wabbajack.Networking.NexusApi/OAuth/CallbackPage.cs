#nullable enable
using System.Net;

namespace Wabbajack.Networking.NexusApi.OAuth;

/// <summary>
///     The page the loopback server hands back to the browser. It is the last thing the user sees there, so
///     its whole job is to say which way it went and send them back to Wabbajack.
///     <para>
///         Everything is inline. The page is served by a socket that closes immediately afterwards, so a
///         stylesheet or an image would be a second request to a server that is no longer there.
///     </para>
/// </summary>
public static class CallbackPage
{
    public static string For(NexusOAuthResult? rejection)
    {
        return rejection == null
            ? Render("Signed in",
                "Nexus Mods has sent your login back to Wabbajack. You can close this tab and carry on there.")
            : Render("Not signed in",
                $"{rejection.Detail?.TrimEnd('.')}. Close this tab and try again from Wabbajack.");
    }

    private static string Render(string heading, string message)
    {
        return $$"""
                <!doctype html>
                <html lang="en">
                <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Wabbajack</title>
                <style>
                :root { color-scheme: light dark; }
                body {
                  margin: 0;
                  min-height: 100vh;
                  display: flex;
                  align-items: center;
                  justify-content: center;
                  background: #16171a;
                  color: #e6e6e6;
                  font: 16px/1.5 "Segoe UI", system-ui, sans-serif;
                }
                main { max-width: 30rem; padding: 2rem; text-align: center; }
                h1 { font-size: 1.5rem; font-weight: 600; margin: 0 0 0.75rem; }
                p { margin: 0; color: #a9adb4; }
                </style>
                </head>
                <body>
                <main>
                <h1>{{WebUtility.HtmlEncode(heading)}}</h1>
                <p>{{WebUtility.HtmlEncode(message)}}</p>
                </main>
                </body>
                </html>
                """;
    }
}
