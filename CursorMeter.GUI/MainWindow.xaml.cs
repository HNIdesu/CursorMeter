using CursorMeter.GUI.Properties;
using LiveChartsCore.SkiaSharpView;
using System.Collections.ObjectModel;
using System.Windows;
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
        private int mMaxRecordCount = 50;
        private readonly nint mHookId;
        private bool mHoldToMeasure = false;
        private bool mIsCursorInWindow = false;
        private readonly WinAPI.LowLevelMouseProc mMouseHookCallback;
        private readonly ObservableCollection<double> mSpeedValues = [];
        private readonly ObservableCollection<double> mDirectionValues = [];

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
        private void LoadSettings()
        {
            AlwaysOnTopCheck.IsChecked = Settings.Default.AlwaysOnTop;
            HoldToMeasureCheck.IsChecked = Settings.Default.HoldToMeasure;
            DeltaTime.Text = Settings.Default.DeltaTime.ToString();
            MaxRecordCount.Text = Settings.Default.MaxRecordCount.ToString();
        }
        private static double GetAngleWithHorizontal(Point p1, Point p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;

            double angleRadians = Math.Atan2(dy, dx);
            double angleDegrees = angleRadians * (180.0 / Math.PI);

            if (angleDegrees < 0)
                angleDegrees += 360;

            return angleDegrees;
        }
        public MainWindow()
        {
            InitializeComponent();
            mTimer = new DispatcherTimer(
                new TimeSpan(0, 0, 0, 0, mInterval),
                DispatcherPriority.Input,
                (sender, ev) =>
                {
                    WinAPI.GetCursorPos(out var rawPosition);
                    var position = new Point(rawPosition.X, rawPosition.Y);
                    if (mPrevPosition == null)
                        mPrevPosition = position;
                    else
                    {
                        var distance = Point.Subtract(position, mPrevPosition.Value).Length;
                        var speed = distance / mInterval * 1000;
                        while (mSpeedValues.Count >= mMaxRecordCount)
                            mSpeedValues.RemoveAt(0);
                        mSpeedValues.Add(speed);
                        SpeedText.Text = $"Speed: {speed:F2} px/s";
                        var direction = GetAngleWithHorizontal(mPrevPosition.Value, position);
                        while (mDirectionValues.Count >= mMaxRecordCount)
                            mDirectionValues.RemoveAt(0);
                        mDirectionValues.Add(direction);
                        DirectionText.Text = $"Direction: {direction:F0} degree";
                        var speedAvg = mSpeedValues.Average();
                        AverageSpeed.Text = $"Avg: {speedAvg:F2}";
                        MaxSpeed.Text = $"Max: {mSpeedValues.Max():F2}";
                        SpeedVar.Text = $"Var: {mSpeedValues.Select(v => Math.Pow(v - speedAvg, 2)).Average():F2}";
                        var directionAvg = mDirectionValues.Average();
                        AverageDirection.Text = $"Avg: {directionAvg:F2}";
                        MaxDirection.Text = $"Max: {mDirectionValues.Max():F2}";
                        DirectionVar.Text = $"Var: {mDirectionValues.Select(v => Math.Pow(v - directionAvg, 2)).Average():F2}";
                        mPrevPosition = position;
                    }
                },
                Dispatcher.CurrentDispatcher
            );
            mTimer.Stop();
            MouseEnter += (sender, ev) =>
            {
                mIsCursorInWindow = true;
            };
            MouseLeave += (sender, ev) =>
            {
                mIsCursorInWindow = false;
            };
            CursorMeterSwitch.Checked += (sender, ev) =>
            {
                mTimer.Start();
                CursorMeterSwitch.Content = "Stop";
                if (mIsCursorInWindow)
                    HoldToMeasureCheck.IsChecked = false;
            };
            CursorMeterSwitch.Unchecked += (sender, ev) =>
            {
                mTimer.Stop();
                CursorMeterSwitch.Content = "Start";
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
            MaxRecordCount.TextChanged += (sender, ev) =>
            {
                mMaxRecordCount = int.Parse(MaxRecordCount.Text);
                if (mMaxRecordCount != Settings.Default.MaxRecordCount)
                {
                    Settings.Default.MaxRecordCount = mMaxRecordCount;
                    Settings.Default.Save();
                }
            };
            Loaded += (sender, ev) =>
            {
                if (Settings.Default.AlwaysOnTop)
                    MakeTopMost();
            };
            LoadSettings();
            SpeedChart.Series = [new LineSeries<double> { Values = mSpeedValues }];
            DirectionChart.Series = [new LineSeries<double> { Values = mDirectionValues }];
            var mouseHookCallback = new WinAPI.LowLevelMouseProc((nCode, wParam, lParam) =>
            {
                if (nCode >= 0)
                {
                    switch ((int)wParam)
                    {
                        case WinAPI.WM_LBUTTONDOWN:
                            if (!mIsCursorInWindow && mHoldToMeasure)
                            {
                                CursorMeterSwitch.IsChecked = true;
                            }
                            break;
                        case WinAPI.WM_LBUTTONUP:
                            {
                                if (mHoldToMeasure)
                                    CursorMeterSwitch.IsChecked = false;
                            }
                            break;
                    }
                }
                return WinAPI.CallNextHookEx(mHookId, nCode, wParam, lParam);
            });
            mMouseHookCallback = mouseHookCallback;
            mHookId = WinAPI.SetWindowsHookEx(WinAPI.WH_MOUSE_LL, mouseHookCallback, 0, 0);
        }

        protected override void OnClosed(EventArgs e)
        {
            WinAPI.UnhookWindowsHookEx(mHookId);
        }

    }
}