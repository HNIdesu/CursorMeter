using CursorMeter.GUI.Properties;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
namespace CursorMeter.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer mTimer;
        private Point? mPrevPosition = null;
        private int mInterval = 50;
        private static nint mHookId = 0;
        private static bool mIsMouseLeftButtonPressed = false;
        private static bool mIsMouseRightButtonPressed = false;
        private bool mHoldToMeasure = false;

        private static nint MouseHookCallback(int nCode, nint wParam, nint lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<WinAPI.MSLLHOOKSTRUCT>(lParam);
                switch ((int)wParam)
                {
                    case WinAPI.WM_LBUTTONDOWN:
                        mIsMouseLeftButtonPressed = true;
                        break;
                    case WinAPI.WM_LBUTTONUP:
                        mIsMouseLeftButtonPressed = false;
                        break;
                    case WinAPI.WM_RBUTTONDOWN:
                        mIsMouseRightButtonPressed = true;
                        break;
                    case WinAPI.WM_RBUTTONUP:
                        mIsMouseRightButtonPressed = false;
                        break;
                }
            }

            return WinAPI.CallNextHookEx(mHookId, nCode, wParam, lParam);
        }

        private void RemoveTopMost()
        {
            var hWnd = new WindowInteropHelper(this).Handle;
            WinAPI.SetWindowPos(hWnd, WinAPI.HWND_NOTOPMOST, 0, 0, 0, 0, WinAPI.SWP_NOMOVE | WinAPI.SWP_NOSIZE | WinAPI.SWP_SHOWWINDOW);
        }
        private void MakeTopMost()
        {
            var hWnd = new WindowInteropHelper(this).Handle;
            WinAPI.SetWindowPos(hWnd, WinAPI.HWND_TOPMOST, 0, 0, 0, 0, WinAPI.SWP_NOMOVE | WinAPI.SWP_NOSIZE | WinAPI.SWP_SHOWWINDOW);
        }

        private void StartCursorMeter()
        {
            mTimer.Start();
            mHookId = WinAPI.SetWindowsHookEx(WinAPI.WH_MOUSE_LL, MouseHookCallback, 0, 0);
            CursorMeterSwitch.Content = "Stop";
        }

        private void StopCursorMeter()
        {
            mTimer.Stop();
            WinAPI.UnhookWindowsHookEx(mHookId);
            CursorMeterSwitch.Content = "Start";
        }
        private void LoadSettings()
        {
            AlwaysOnTopCheck.IsChecked = Settings.Default.AlwaysOnTop;
            HoldToMeasureCheck.IsChecked = Settings.Default.HoldToMeasure;
            DeltaTime.Text = Settings.Default.DeltaTime.ToString();
        }
        public MainWindow()
        {
            InitializeComponent();
            CursorMeterSwitch.Checked += (sender, ev) =>
            {
                StartCursorMeter();
            };
            CursorMeterSwitch.Unchecked += (sender, ev) =>
            {
                StopCursorMeter();
            };
            HoldToMeasureCheck.Checked += (sender, ev) =>
            {
                mHoldToMeasure = true;
                if (Settings.Default.HoldToMeasure != mHoldToMeasure)
                {
                    Settings.Default.HoldToMeasure = mHoldToMeasure;
                    Settings.Default.Save();
                }
            };
            HoldToMeasureCheck.Unchecked += (sender, ev) =>
            {
                mHoldToMeasure = false;
                if (Settings.Default.HoldToMeasure != mHoldToMeasure)
                {
                    Settings.Default.HoldToMeasure = mHoldToMeasure;
                    Settings.Default.Save();
                }
            };
            AlwaysOnTopCheck.Checked += (sender, ev) =>
            {
                MakeTopMost();
                if (!Settings.Default.AlwaysOnTop)
                {
                    Settings.Default.AlwaysOnTop = true;
                    Settings.Default.Save();
                }
            };
            AlwaysOnTopCheck.Unchecked += (sender, ev) =>
            {
                RemoveTopMost();
                if (Settings.Default.AlwaysOnTop)
                {
                    Settings.Default.AlwaysOnTop = false;
                    Settings.Default.Save();
                }
            };
            DeltaTime.TextChanged += (sender, ev) =>
            {
                mInterval = int.Parse(DeltaTime.Text);
                if (mInterval != Settings.Default.DeltaTime)
                {
                    Settings.Default.DeltaTime = mInterval;
                    Settings.Default.Save();
                }
            };
            Loaded += (sender, ev) =>
            {
                if (Settings.Default.AlwaysOnTop)
                    MakeTopMost();
            };
            LoadSettings();
            mTimer = new DispatcherTimer(
                new TimeSpan(0, 0, 0, 0, mInterval),
                DispatcherPriority.Input,
                (sender, ev) =>
                {
                    if ((!mHoldToMeasure) || mIsMouseLeftButtonPressed || mIsMouseRightButtonPressed)
                    {
                        WinAPI.GetCursorPos(out var rawPosition);
                        var position = new Point(rawPosition.X, rawPosition.Y);
                        if (mPrevPosition == null)
                            mPrevPosition = position;
                        else
                        {
                            var distance = Point.Subtract(position, mPrevPosition.Value).Length;
                            var speed = distance / mInterval * 1000;
                            SpeedText.Text = $"Speed: {speed:F2} px/s";
                            mPrevPosition = position;
                        }
                    }
                    else
                    {
                        SpeedText.Text = "Speed: 0.00 px/s";
                    }
                },
                Dispatcher.CurrentDispatcher);
            mTimer.Stop();
        }

    }
}