using System;
using System.Runtime.InteropServices;

namespace MicMax.Core.Audio;

/// <summary>
/// Windows Core Audio (WASAPI/MMDevice) interop definitions.
/// Contains the COM interface declarations needed to enumerate audio input
/// endpoints, read/write their master volume and mute state, and receive
/// event-driven change notifications. No audio content is ever captured —
/// only volume level and mute state are read or written (FR-015).
/// </summary>
public static class CoreAudioInterop
{
    /// <summary>CLSID for the MMDeviceEnumerator coclass.</summary>
    public static readonly Guid MMDeviceEnumeratorClsid = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    /// <summary>IID of IAudioEndpointVolume (used with IMMDevice.Activate).</summary>
    public static readonly Guid IAudioEndpointVolumeIid = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    /// <summary>PKEY_Device_FriendlyName {a45c254e-df1c-4efd-8020-67d146a850e0}, pid 14.</summary>
    public static readonly PropertyKey DeviceFriendlyNameKey =
        new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    /// <summary>PKEY_Device_DeviceDesc {a45c254e-df1c-4efd-8020-67d146a850e0}, pid 2.</summary>
    public static readonly PropertyKey DeviceDescKey =
        new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 2);

    /// <summary>CLSCTX_INPROC_SERVER — required for Activate.</summary>
    public const uint ClsCtxInprocServer = 0x1;

    /// <summary>STGM_READ — access mode for OpenPropertyStore.</summary>
    public const uint StgmRead = 0x0;

    /// <summary>DEVICE_STATE_ACTIVE.</summary>
    public const uint DeviceStateActive = 0x1;

    /// <summary>VARIANT type for a wide string (VT_LPWSTR).</summary>
    public const ushort VtLpwStr = 31;

    #region Enumerations

    /// <summary>EDataFlow — direction of the audio stream.</summary>
    public enum EDataFlow
    {
        Render = 0,
        Capture = 1,
        All = 2
    }

    /// <summary>ERole — the device role.</summary>
    public enum ERole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    #endregion

    #region Structures

    /// <summary>PROPERTYKEY — identifies a property (fmtid + pid).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        public Guid Fmtid;
        public uint Pid;

        public PropertyKey(Guid fmtid, uint pid)
        {
            Fmtid = fmtid;
            Pid = pid;
        }
    }

    /// <summary>
    /// Minimal PROPVARIANT sufficient to read a VT_LPWSTR value. The union
    /// payload starts at offset 8 on both x86 and x64 for pointer types.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PropVariant
    {
        public ushort Vt;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public IntPtr PwszVal;

        /// <summary>Reads the string payload when <see cref="Vt"/> is VT_LPWSTR; otherwise null.</summary>
        public readonly string? AsString()
        {
            if (Vt != VtLpwStr || PwszVal == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringUni(PwszVal);
        }
    }

    #endregion

    #region Consumed COM interfaces (implemented by Windows)

    /// <summary>
    /// IAudioEndpointVolume — controls master volume and mute for an endpoint.
    /// Methods are declared in exact vtable order from endpointvolume.h: the
    /// notification registration pair comes FIRST, followed by the channel and
    /// master level methods, mute, step and range methods.
    ///
    /// The optional LPCGUID event-context parameters are declared as IntPtr
    /// (pass IntPtr.Zero for "no context"): Nullable&lt;Guid&gt; is a generic
    /// type and the interop marshaller rejects it with
    /// MarshalDirectiveException ("Non-blittable generic types").
    /// </summary>
    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback? notify);

        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback? notify);

        [PreserveSig] int GetChannelCount(out uint channelCount);

        [PreserveSig] int SetMasterVolumeLevel(float levelDb, IntPtr eventContext);

        [PreserveSig] int SetMasterVolumeLevelScalar(float level, IntPtr eventContext);

        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);

        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);

        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, IntPtr eventContext);

        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, IntPtr eventContext);

        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);

        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);

        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, IntPtr eventContext);

        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);

        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);

        [PreserveSig] int VolumeStepUp(IntPtr eventContext);

        [PreserveSig] int VolumeStepDown(IntPtr eventContext);

        [PreserveSig] int QueryHardwareSupport(out uint hardwareSupportMask);

        [PreserveSig]
        int GetVolumeRange(out float minDb, out float maxDb, out float stepDb);
    }

    /// <summary>
    /// IMMDeviceEnumerator — enumerates endpoints and registers for change
    /// notifications. Declared in vtable order from mmdeviceapi.h.
    /// </summary>
    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IMMDeviceCollection? devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice? endpoint);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice? device);

        [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient? client);

        [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient? client);
    }

    /// <summary>IMMDeviceCollection — a collection of IMMDevice pointers.</summary>
    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint deviceCount);

        [PreserveSig] int Item(uint index, out IMMDevice? device);
    }

    /// <summary>IMMDevice — a single audio endpoint device.</summary>
    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            ref Guid iid,
            uint classCtx,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object? iface);

        [PreserveSig]
        int OpenPropertyStore(uint access, out IPropertyStore? properties);

        [PreserveSig] int GetId(out IntPtr id);

        [PreserveSig] int GetState(out uint state);
    }

    /// <summary>IPropertyStore — reads device properties (friendly name, etc.).</summary>
    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint propertyCount);

        [PreserveSig] int GetAt(uint index, out PropertyKey key);

        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);

        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);

        [PreserveSig] int Commit();
    }

    #endregion

    #region Managed-implemented COM interfaces (implemented by this app)

    /// <summary>
    /// IAudioEndpointVolumeCallback — implemented by managed code to receive
    /// volume/mute change notifications without polling.
    /// </summary>
    [Guid("657804FA-D6AD-4496-8A60-352752AF4F89")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    public interface IAudioEndpointVolumeCallback
    {
        /// <summary>Called by Windows when the endpoint volume or mute state changes.</summary>
        /// <param name="pNotify">Pointer to an AUDIO_VOLUME_NOTIFICATION_DATA structure.</param>
        /// <returns>S_OK (0) on success.</returns>
        [PreserveSig] int OnNotify(IntPtr pNotify);
    }

    /// <summary>
    /// IMMNotificationClient — implemented by managed code to receive device
    /// added/removed/state/default change notifications.
    /// </summary>
    [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    public interface IMMNotificationClient
    {
        void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, uint newState);

        void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

        void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

        void OnDefaultDeviceChanged(EDataFlow flow, ERole role, [MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId);

        void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PropertyKey key);
    }

    #endregion

    #region Native helpers

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant propvar);

    [DllImport("ole32.dll")]
    private static extern int CoTaskMemFree(IntPtr ptr);

    /// <summary>
    /// Creates the MMDeviceEnumerator COM object.
    /// </summary>
    public static IMMDeviceEnumerator CreateDeviceEnumerator()
    {
        var type = Type.GetTypeFromCLSID(MMDeviceEnumeratorClsid, throwOnError: true)!;
        return (IMMDeviceEnumerator)Activator.CreateInstance(type)!;
    }

    /// <summary>
    /// Releases a PROPVARIANT string obtained from IPropertyStore.GetValue.
    /// Safe to call for non-string variants.
    /// </summary>
    public static void ClearPropVariant(ref PropVariant value)
    {
        PropVariantClear(ref value);
    }

    /// <summary>
    /// Frees an endpoint ID string buffer obtained from IMMDevice.GetId.
    /// </summary>
    public static void FreeDeviceId(IntPtr id)
    {
        if (id != IntPtr.Zero)
        {
            CoTaskMemFree(id);
        }
    }

    /// <summary>
    /// Throws an exception when an HRESULT indicates failure.
    /// </summary>
    public static void ThrowIfFailed(int hr, string operation)
    {
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
            throw new COMException($"{operation} failed with HRESULT 0x{hr:X8}", hr);
        }
    }

    #endregion
}
