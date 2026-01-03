using System;
using System.Runtime.InteropServices;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility;

public static class WineDetector
{
    [DllImport("ntdll.dll", EntryPoint = "wine_get_version", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr InternalWineGetVersion();

    public static bool IsRunningUnderWine()
    {
        try
        {
            // If we are on native Windows, ntdll exists but this function doesn't.
            // DllNotFoundException or EntryPointNotFoundException means it's NOT Wine.
            return InternalWineGetVersion() != IntPtr.Zero;
        }
        catch (EntryPointNotFoundException)
        {
            return false; // Function doesn't exist -> Real Windows
        }
        catch (DllNotFoundException)
        {
            return false; // Should only happen if not on Windows/Wine at all
        }
        catch
        {
            return false;
        }
    }
}