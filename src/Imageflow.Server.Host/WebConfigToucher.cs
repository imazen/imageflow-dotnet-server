using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Imageflow.Server.Host;

internal partial class WebConfigToucher
{

    public WebConfigToucher(string webConfigPath, ILogger? logger)
    {
        this.webConfigPath = webConfigPath;
        this.logger = logger;
    }

    private static Regex touchedComment = TouchedComment();
    private string webConfigPath;
    private ILogger? logger;

    [System.Text.RegularExpressions.GeneratedRegex("<!-- This comment has been modified \\[(\\d+)\\] times to trigger an app restart -->")]
    private static partial System.Text.RegularExpressions.Regex TouchedComment();

    internal void TriggerRestart()
    {
        // Edit Web.config to trigger an app restart
        // Search for <!-- This comment has been modified [10] times to trigger an app restart --> in the file and increment
        // the number in the comment. If missing, add it to the end
        // This is a hack to trigger an app restart without having to use IIS
        if (!File.Exists(webConfigPath))
        {
            return;
        }
        else
        {
            var existingText = File.ReadAllText(webConfigPath);
            var defaultComment = "<!-- This comment has been modified [1] times to trigger an app restart -->";

            // Replace the comment with the new number
            var newText = touchedComment.Replace(existingText, (match) =>
            {
                if (int.TryParse(match.Groups[1].Value, out var number))
                {
                    return $"<!-- This comment has been modified [{number + 1}] times to trigger an app restart -->";
                }
                else
                {
                    return defaultComment;
                }
            });

            if (newText == existingText)
            {
                // Comment not found, add it to the end
                newText += defaultComment;
            }
            // write to disk
            try
            {
                File.WriteAllText(webConfigPath, newText);
            }
            catch (Exception e)
            {
                logger?.LogWarning("Imageflow Server was unable to automatically trigger an app restart following an imageflow.toml change: editing {webConfigPath} failed with {e}. Please restart the web app manually.", webConfigPath, e);
                // We might not have access. Log to the event log that we weren't able to update the file
                // We don't want to throw an exception because we don't want to break the app
                // We also don't want to log to the console because that might expose sensitive info
                // This is a fire-and-forget operation
                if (logger == null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var eventLog = new EventLog("Application", ".", "Imageflow");
                    eventLog.WriteEntry($"Imageflow Server was unable to automatically trigger an app restart following an imageflow.toml change: editing {webConfigPath} failed with {e}. Please restart the web app manually.", EventLogEntryType.Warning);
                }
            }
        }
    }


}

