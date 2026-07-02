using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BexioOrderImport.Wpf.Helpers;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class WindowHelper
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static Rect GetAbsolutePlacement(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight);
        }

        if (GetWindowRect(hwnd, out RECT rect))
        {
            var source = PresentationSource.FromVisual(window);
            double dpiX = 1.0;
            double dpiY = 1.0;
            if (source?.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            double left = rect.Left / dpiX;
            double top = rect.Top / dpiY;
            double width = (rect.Right - rect.Left) / dpiX;
            double height = (rect.Bottom - rect.Top) / dpiY;

            return new Rect(left, top, width, height);
        }

        return new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight);
    }
}
