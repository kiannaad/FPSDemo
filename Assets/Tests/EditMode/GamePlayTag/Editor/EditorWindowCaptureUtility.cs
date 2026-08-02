using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace CGame.GameplayTags.Editor.Tests
{
    public static class EditorWindowCaptureUtility
    {
        private const int Srccopy = 0x00CC0020;
        private const int Captureblt = 0x40000000;

        public static void Capture(EditorWindow window, string path)
        {
            window.Focus();
            IntPtr unityWindow = Process.GetCurrentProcess().MainWindowHandle;
            if (unityWindow == IntPtr.Zero || !SetForegroundWindow(unityWindow))
            {
                throw new InvalidOperationException("Could not bring the Unity Editor process to the foreground.");
            }

            Thread.Sleep(250);
            GetWindowThreadProcessId(GetForegroundWindow(), out uint foregroundProcessId);
            if (foregroundProcessId != (uint)Process.GetCurrentProcess().Id)
            {
                throw new InvalidOperationException("Foreground process is not Unity; screenshot aborted for privacy.");
            }

            float scale = EditorGUIUtility.pixelsPerPoint;
            Rect logical = window.position;
            int x = Mathf.RoundToInt(logical.x * scale);
            int y = Mathf.RoundToInt(logical.y * scale);
            int width = Mathf.Max(1, Mathf.RoundToInt(logical.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(logical.height * scale));

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memoryDc = CreateCompatibleDC(screenDc);
            IntPtr bitmap = CreateCompatibleBitmap(screenDc, width, height);
            IntPtr previous = SelectObject(memoryDc, bitmap);
            try
            {
                if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y, Srccopy | Captureblt))
                {
                    throw new InvalidOperationException("BitBlt failed while capturing the Editor window.");
                }

                var info = new BitmapInfo
                {
                    Header = new BitmapInfoHeader
                    {
                        Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                        Width = width,
                        Height = height,
                        Planes = 1,
                        BitCount = 32,
                        Compression = 0,
                        SizeImage = (uint)(width * height * 4)
                    }
                };
                byte[] pixels = new byte[width * height * 4];
                int scanLines = GetDIBits(memoryDc, bitmap, 0, (uint)height, pixels, ref info, 0);
                if (scanLines == 0)
                {
                    throw new InvalidOperationException("GetDIBits failed while capturing the Editor window.");
                }

                var texture = new Texture2D(width, height, TextureFormat.BGRA32, false);
                texture.LoadRawTextureData(pixels);
                texture.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                SelectObject(memoryDc, previous);
                DeleteObject(bitmap);
                DeleteDC(memoryDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        public static void CaptureTopLevelWindow(string title, string path)
        {
            IntPtr targetWindow = IntPtr.Zero;
            EnumWindows(
                (window, _) =>
                {
                    GetWindowThreadProcessId(window, out uint processId);
                    if (processId != (uint)Process.GetCurrentProcess().Id || !IsWindowVisible(window))
                    {
                        return true;
                    }

                    int length = GetWindowTextLength(window);
                    var buffer = new StringBuilder(length + 1);
                    GetWindowText(window, buffer, buffer.Capacity);
                    if (buffer.ToString().IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        targetWindow = window;
                        return false;
                    }

                    return true;
                },
                IntPtr.Zero);

            if (targetWindow == IntPtr.Zero || !GetWindowRect(targetWindow, out NativeRect rect))
            {
                throw new InvalidOperationException($"Unity top-level window '{title}' was not found.");
            }

            SetForegroundWindow(targetWindow);
            Thread.Sleep(250);
            GetWindowThreadProcessId(GetForegroundWindow(), out uint foregroundProcessId);
            if (foregroundProcessId != (uint)Process.GetCurrentProcess().Id)
            {
                throw new InvalidOperationException("Foreground process is not Unity; screenshot aborted for privacy.");
            }

            CaptureScreenRectangle(
                rect.Left,
                rect.Top,
                Mathf.Max(1, rect.Right - rect.Left),
                Mathf.Max(1, rect.Bottom - rect.Top),
                path);
        }

        public static void CaptureForegroundUnityRectangle(int x, int y, int width, int height, string path)
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint foregroundProcessId);
            if (foregroundProcessId != (uint)Process.GetCurrentProcess().Id)
            {
                throw new InvalidOperationException("Foreground process is not Unity; screenshot aborted for privacy.");
            }

            CaptureScreenRectangle(x, y, width, height, path);
        }

        private static void CaptureScreenRectangle(int x, int y, int width, int height, string path)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memoryDc = CreateCompatibleDC(screenDc);
            IntPtr bitmap = CreateCompatibleBitmap(screenDc, width, height);
            IntPtr previous = SelectObject(memoryDc, bitmap);
            try
            {
                if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y, Srccopy | Captureblt))
                {
                    throw new InvalidOperationException("BitBlt failed while capturing the native window.");
                }

                var info = new BitmapInfo
                {
                    Header = new BitmapInfoHeader
                    {
                        Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                        Width = width,
                        Height = height,
                        Planes = 1,
                        BitCount = 32,
                        Compression = 0,
                        SizeImage = (uint)(width * height * 4)
                    }
                };
                byte[] pixels = new byte[width * height * 4];
                int scanLines = GetDIBits(memoryDc, bitmap, 0, (uint)height, pixels, ref info, 0);
                if (scanLines == 0)
                {
                    throw new InvalidOperationException("GetDIBits failed while capturing the native window.");
                }

                var texture = new Texture2D(width, height, TextureFormat.BGRA32, false);
                texture.LoadRawTextureData(pixels);
                texture.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                SelectObject(memoryDc, previous);
                DeleteObject(bitmap);
                DeleteDC(memoryDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ClrUsed;
            public uint ClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint Colors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr deviceContext);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr deviceContext, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr target);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr target);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(
            IntPtr destination,
            int xDestination,
            int yDestination,
            int width,
            int height,
            IntPtr source,
            int xSource,
            int ySource,
            int operation);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(
            IntPtr deviceContext,
            IntPtr bitmap,
            uint start,
            uint lines,
            [Out] byte[] bits,
            ref BitmapInfo info,
            uint usage);
    }
}
