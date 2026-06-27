using System;
using System.Runtime.InteropServices;

const int ClsCtxAll = 23;
const int StgmRead = 0;

var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
Console.WriteLine("enumerator ok");
var hr = enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var defaultDevice);
Console.WriteLine($"default endpoint hr={hr}");
defaultDevice.GetId(out var defaultId);
Console.WriteLine($"default id={defaultId}");
hr = enumerator.EnumAudioEndpoints(EDataFlow.Render, DeviceState.Active, out var collection);
Console.WriteLine($"enum endpoints hr={hr}");
collection.GetCount(out var count);
Console.WriteLine($"count={count}");
for (uint i = 0; i < count; i++)
{
    collection.Item(i, out var device);
    device.GetId(out var id);
    Console.WriteLine($"device {i} id={id}");
    device.OpenPropertyStore(StgmRead, out var store);
    var key = new PropertyKey{ fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid=14};
    var hr2 = store.GetValue(ref key, out var prop);
    Console.WriteLine($"  getvalue hr={hr2} vt={prop.vt} name={prop.GetString()}");
    PropVariantClear(ref prop);
}

[DllImport("ole32.dll")]
static extern int PropVariantClear(ref PropVariant propVariant);

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
class MMDeviceEnumeratorComObject { }

enum EDataFlow { Render = 0, Capture = 1, All = 2 }
enum ERole { Console = 0, Multimedia = 1, Communications = 2 }
[Flags] enum DeviceState { Active = 0x1, Disabled = 0x2, NotPresent = 0x4, Unplugged = 0x8, All = Active|Disabled|NotPresent|Unplugged }
[StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid fmtid; public int pid; }
[StructLayout(LayoutKind.Explicit)] struct PropVariant { [FieldOffset(0)] public ushort vt; [FieldOffset(8)] public IntPtr pointerValue; public string? GetString()=> vt == 31 ? Marshal.PtrToStringUni(pointerValue) : null; }

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(EDataFlow dataFlow, DeviceState stateMask, out IMMDeviceCollection devices);
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string deviceId, out IMMDevice device);
    int RegisterEndpointNotificationCallback(IntPtr client);
    int UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-C0A74EBD1A36")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceCollection
{
    int GetCount(out uint count);
    int Item(uint index, out IMMDevice device);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    int Activate([MarshalAs(UnmanagedType.LPStruct)] Guid interfaceId, int classContext, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    int OpenPropertyStore(int storageAccessMode, out IPropertyStore properties);
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string deviceId);
    int GetState(out DeviceState state);
}

[ComImport]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPropertyStore
{
    int GetCount(out uint propertyCount);
    int GetAt(uint propertyIndex, out PropertyKey key);
    int GetValue(ref PropertyKey key, out PropVariant value);
    int SetValue(ref PropertyKey key, ref PropVariant value);
    int Commit();
}
