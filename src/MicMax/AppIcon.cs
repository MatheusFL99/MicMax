using System;
using System.Drawing;
using System.Reflection;

namespace MicMax;

/// <summary>
/// Loads the application icon (Assets/MicMax.ico) that is embedded in the
/// executable, so the tray icon and the main window show the same branding as
/// the file shown by Explorer. Falls back to the generic system icon if the
/// resource is missing.
/// </summary>
internal static class AppIcon
{
    /// <summary>Embedded resource name (default naming: RootNamespace + folder + file).</summary>
    private const string ResourceName = "MicMax.Assets.MicMax.ico";

    /// <summary>Size requested from the multi-resolution .ico; 32px covers the
    /// 16px tray slot on standard DPI and stays sharp on high-DPI trays.</summary>
    private const int RequestedSize = 32;

    private static readonly Lazy<Icon> LazyIcon = new(Create);

    /// <summary>
    /// The shared application icon. Owned by the process (never disposed);
    /// <see cref="System.Windows.Forms.NotifyIcon"/> and forms only borrow it.
    /// </summary>
    public static Icon Icon => LazyIcon.Value;

    private static Icon Create()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(ResourceName);

            if (stream != null)
            {
                // Picks the closest size from the .ico and scales if needed.
                return new Icon(stream, RequestedSize, RequestedSize);
            }
        }
        catch (Exception)
        {
            // Corrupt or unreadable resource — fall back to the system icon.
        }

        return SystemIcons.Application;
    }
}
