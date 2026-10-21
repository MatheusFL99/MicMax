namespace MicMax.Core.Models;

/// <summary>
/// Tracks which device the lock applies to and the selection mode.
/// </summary>
public class DeviceSelection
{
    /// <summary>
    /// Determines how the locked device is determined.
    /// </summary>
    public SelectionMode SelectionMode { get; set; }

    /// <summary>
    /// The GUID of the explicitly selected device, if SelectionMode is ExplicitDevice.
    /// </summary>
    public Guid? SelectedDeviceGUID { get; set; }

    /// <summary>
    /// The GUID of the fallback device (default communication device), if SelectionMode is FollowDefaultCommunication.
    /// </summary>
    public Guid? FallbackDeviceGUID { get; set; }

    /// <summary>
    /// The GUID of the originally locked device, for re-lock after fallback.
    /// </summary>
    public Guid? OriginalDeviceGUID { get; set; }
}

/// <summary>
/// Enum for selection mode.
/// </summary>
public enum SelectionMode
{
    /// <summary>
    /// Lock an explicitly chosen device.
    /// </summary>
    ExplicitDevice,

    /// <summary>
    /// Follow the Windows default communication device.
    /// </summary>
    FollowDefaultCommunication
}