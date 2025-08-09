using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using VRTDoubao.Win32;
using System.Windows.Forms.Integration;
using WF = System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace VRTDoubao.UI;

public partial class MainWindow : Window
{
    private nint _leftChild = nint.Zero;
    private nint _rightChild = nint.Zero;
    // Left uses DWM mirroring; right uses SetParent embedding for interactivity
    private nint _leftThumb = nint.Zero;
    private nint _rightThumb = nint.Zero;
    private WF.Control? _micDot;
    private WF.Control? _sendDot;
    private readonly System.Drawing.Size _dotSize = new System.Drawing.Size(36, 36);
    private readonly WF.Timer _zOrderTimer = new WF.Timer { Interval = 500 };

    public MainWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => ResizeEmbeddedChildren();
        Loaded += (_, _) => ResizeEmbeddedChildren();
        Loaded += (_, _) => { EnsureDotsCreated(); BringDotsToFront(); _zOrderTimer.Start(); };
        _zOrderTimer.Tick += (_, _) => BringDotsToFront();
        // 音频相关逻辑已移除
    }

        // Help button removed per UI simplification request

    private void ResizeEmbeddedChildren()
    {
        if (_leftChild != nint.Zero && _leftThumb != nint.Zero)
        {
            var w = (int)HostLeft.ActualWidth;
            var h = (int)HostLeft.ActualHeight;
            var offset = GetHostOffset(HostLeft);
            WindowMirror.UpdateLayout(_leftThumb, GetTopLevelHwnd(), offset.x, offset.y, w, h);
        }
        if (_rightChild != nint.Zero)
        {
            var w = (int)HostRight.ActualWidth;
            var h = (int)HostRight.ActualHeight;
            WindowEmbedder.ResizeToHost(_rightChild, w, h);
        }
        BringDotsToFront();
    }

    private void BtnEmbedLeftHwnd_OnClick(object sender, RoutedEventArgs e)
    {
        _leftChild = ParseHwndText(TbLeftHwnd.Text);
        if (_leftChild == nint.Zero)
        {
            System.Windows.MessageBox.Show("左侧 HWND 无效");
            return;
        }
        // 先显示宿主并强制布局，避免第一次无法显示
        HostLeft.Visibility = System.Windows.Visibility.Visible;
        this.UpdateLayout();

        var hostHandle = GetTopLevelHwnd();
        WindowMirror.Unmirror(ref _leftThumb);
        if (WindowMirror.MirrorToHost(_leftChild, hostHandle, out _leftThumb))
        {
            if (PlaceholderLeft != null) PlaceholderLeft.Visibility = System.Windows.Visibility.Collapsed;
            ResizeEmbeddedChildren();
        }
        else
        {
            System.Windows.MessageBox.Show("镜像失败 (左)");
        }
    }

    private void BtnEmbedRightHwnd_OnClick(object sender, RoutedEventArgs e)
    {
        _rightChild = ParseHwndText(TbRightHwnd.Text);
        if (_rightChild == nint.Zero)
        {
            System.Windows.MessageBox.Show("右侧 HWND 无效");
            return;
        }
        // 先显示宿主并强制创建句柄
        HostRight.Visibility = System.Windows.Visibility.Visible;
        this.UpdateLayout();
        PanelRight.CreateControl();
        var hostHandleRight = PanelRight.Handle;
        // Ensure right is interactive by embedding
        WindowMirror.Unmirror(ref _rightThumb);
        if (!WindowEmbedder.Embed(_rightChild, hostHandleRight))
        {
            System.Windows.MessageBox.Show("嵌入失败 (右)");
            _rightChild = nint.Zero;
            return;
        }
        if (PlaceholderRight != null) PlaceholderRight.Visibility = System.Windows.Visibility.Collapsed;
        ResizeEmbeddedChildren();
        EnsureDotsCreated();
        BringDotsToFront();
    }

    private async void BtnPickLeft_OnClick(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("点击确认后2秒内将鼠标移动到要获取句柄的窗口上");
        await Task.Delay(2000);
        var hwnd = GetWindowUnderCursorTopLevel();
        if (hwnd == nint.Zero)
        {
            System.Windows.MessageBox.Show("拾取失败 (左)");
            return;
        }
        TbLeftHwnd.Text = $"0x{hwnd.ToInt64():X}";
    }

    private async void BtnPickRight_OnClick(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("点击确认后2秒内将鼠标移动到要获取句柄的窗口上");
        await Task.Delay(2000);
        var hwnd = GetWindowUnderCursorTopLevel();
        if (hwnd == nint.Zero)
        {
            System.Windows.MessageBox.Show("拾取失败 (右)");
            return;
        }
        TbRightHwnd.Text = $"0x{hwnd.ToInt64():X}";
    }

    private static nint ParseHwndText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return nint.Zero;
        text = text.Trim();
        try
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var val = Convert.ToInt64(text, 16);
                return new nint(val);
            }
            else
            {
                if (long.TryParse(text, out var val)) return new nint(val);
            }
        }
        catch { }
        return nint.Zero;
    }

    private static nint GetWindowUnderCursorTopLevel()
    {
        if (!GetCursorPos(out var pt)) return nint.Zero;
        var hwnd = WindowFromPoint(pt);
        if (hwnd == nint.Zero) return nint.Zero;
        var root = GetAncestor(hwnd, 2); // GA_ROOT
        return root != nint.Zero ? root : hwnd;
    }

    // Removed polling watchdog since we use DWM mirroring instead of SetParent embedding

    // 音频（扬声器/麦克风复制）相关逻辑已彻底移除

    // duplicate method removed (merged into existing StartSpeakerDupAsync above)

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint hWnd, uint gaFlags);

    private nint GetTopLevelHwnd()
    {
        var wih = new System.Windows.Interop.WindowInteropHelper(this);
        return wih.Handle;
    }

    private (int x, int y) GetHostOffset(WindowsFormsHost host)
    {
        var p = host.PointToScreen(new System.Windows.Point(0, 0));
        var topLeft = this.PointToScreen(new System.Windows.Point(0, 0));
        var x = (int)(p.X - topLeft.X);
        var y = (int)(p.Y - topLeft.Y);
        return (x, y);
    }

        private void EnsureDotsCreated()
    {
        if (_micDot == null)
        {
            _micDot = CreateDraggableDot("MicDot", System.Drawing.Color.MediumSeaGreen, new System.Drawing.Point(40, 40), 1);
            PanelRight.Controls.Add(_micDot);
                if (!_micDot.IsHandleCreated) _micDot.CreateControl();
        }
        if (_sendDot == null)
        {
            _sendDot = CreateDraggableDot("SendDot", System.Drawing.Color.CornflowerBlue, new System.Drawing.Point(100, 40), 2);
            PanelRight.Controls.Add(_sendDot);
                if (!_sendDot.IsHandleCreated) _sendDot.CreateControl();
        }
        BringDotsToFront();
        PanelRight.Resize -= PanelRight_Resize;
        PanelRight.Resize += PanelRight_Resize;
    }

    private void PanelRight_Resize(object? sender, EventArgs e)
    {
        ConstrainDotWithinPanel(_micDot);
        ConstrainDotWithinPanel(_sendDot);
        BringDotsToFront();
    }

        private WF.Control CreateDraggableDot(string name, System.Drawing.Color color, System.Drawing.Point initial, int label)
    {
        var dot = new WF.Panel
        {
            Name = name,
            BackColor = Color.Transparent,
            Size = _dotSize,
            Location = initial,
            Cursor = WF.Cursors.Hand
        };

        // Ensure layered (per-window alpha) so semi-transparency blends with embedded child
        void ApplyLayered()
        {
            if (dot.IsHandleCreated)
            {
                var exPtr = GetWindowLongPtr(dot.Handle, GWL_EXSTYLE);
                var ex = (int)((long)exPtr & 0xFFFFFFFF);
                SetWindowLongPtr(dot.Handle, GWL_EXSTYLE, (nint)(ex | WS_EX_LAYERED));
                SetLayeredWindowAttributes(dot.Handle, 0, 128, LWA_ALPHA); // 50% opacity
            }
        }
        dot.HandleCreated += (_, __) => ApplyLayered();
        if (!dot.IsHandleCreated) dot.CreateControl();
        ApplyLayered();

        dot.Paint += (_, pe) =>
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, _dotSize.Width - 1, _dotSize.Height - 1);
            dot.Region = new Region(path);
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var b = new SolidBrush(color);
            pe.Graphics.FillEllipse(b, 0, 0, _dotSize.Width - 1, _dotSize.Height - 1);
            using var pen = new Pen(System.Drawing.Color.White, 2);
            pe.Graphics.DrawEllipse(pen, 1, 1, _dotSize.Width - 3, _dotSize.Height - 3);
            var text = label.ToString();
            using var f = new Font(new FontFamily("Segoe UI"), 14, System.Drawing.FontStyle.Bold);
            var size = pe.Graphics.MeasureString(text, f);
            var tx = (_dotSize.Width - size.Width) / 2f;
            var ty = (_dotSize.Height - size.Height) / 2f;
            using var tb = new SolidBrush(Color.White);
            pe.Graphics.DrawString(text, f, tb, tx, ty);
        };

        System.Drawing.Point? dragStart = null;
        dot.MouseDown += (_, me) => { if (me.Button == WF.MouseButtons.Left) dragStart = me.Location; };
        dot.MouseUp += (_, __) => dragStart = null;
        dot.MouseMove += (_, me) =>
        {
            if (dragStart.HasValue && me.Button == WF.MouseButtons.Left)
            {
                var newLeft = dot.Left + (me.X - dragStart.Value.X);
                var newTop = dot.Top + (me.Y - dragStart.Value.Y);
                newLeft = Math.Max(0, Math.Min(newLeft, PanelRight.ClientSize.Width - dot.Width));
                newTop = Math.Max(0, Math.Min(newTop, PanelRight.ClientSize.Height - dot.Height));
                dot.Left = newLeft;
                dot.Top = newTop;
            }
        };
        return dot;
    }

    private void ConstrainDotWithinPanel(WF.Control? dot)
    {
        if (dot == null) return;
        var left = Math.Max(0, Math.Min(dot.Left, PanelRight.ClientSize.Width - dot.Width));
        var top = Math.Max(0, Math.Min(dot.Top, PanelRight.ClientSize.Height - dot.Height));
        dot.Left = left;
        dot.Top = top;
    }

    private void BringDotsToFront()
    {
        if (_rightChild != nint.Zero)
        {
            // Ensure embedded child sits at the bottom of sibling z-order
            SetWindowPos(_rightChild, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        if (_micDot != null)
        {
            // Re-apply layered style after z-order changes to avoid losing alpha on some systems
            var exPtr = GetWindowLongPtr(_micDot.Handle, GWL_EXSTYLE);
            SetWindowLongPtr(_micDot.Handle, GWL_EXSTYLE, (nint)(((long)exPtr) | WS_EX_LAYERED));
            SetLayeredWindowAttributes(_micDot.Handle, 0, 128, LWA_ALPHA);
            SetWindowPos(_micDot.Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        if (_sendDot != null)
        {
            var exPtr = GetWindowLongPtr(_sendDot.Handle, GWL_EXSTYLE);
            SetWindowLongPtr(_sendDot.Handle, GWL_EXSTYLE, (nint)(((long)exPtr) | WS_EX_LAYERED));
            SetLayeredWindowAttributes(_sendDot.Handle, 0, 128, LWA_ALPHA);
            SetWindowPos(_sendDot.Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    private System.Drawing.Point GetDotCenter(WF.Control dot)
    {
        return new System.Drawing.Point(dot.Left + dot.Width / 2, dot.Top + dot.Height / 2);
    }

    private void BtnMicAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (_rightChild == nint.Zero || _micDot == null) return;
        var p = GetDotCenter(_micDot);
        ClickEmbeddedAt(_rightChild, p.X, p.Y);
    }

    private void BtnSendAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (_rightChild == nint.Zero || _sendDot == null) return;
        var p = GetDotCenter(_sendDot);
        ClickEmbeddedAt(_rightChild, p.X, p.Y);
    }

    private void ClickEmbeddedAt(nint childHwnd, int x, int y)
    {
        if (childHwnd == nint.Zero) return;
        SetForegroundWindow(childHwnd);
        var lParam = MakeLParam(x, y);
        SendMessage(childHwnd, WM_MOUSEMOVE, 0, lParam);
        SendMessage(childHwnd, WM_LBUTTONDOWN, (nint)MK_LBUTTON, lParam);
        SendMessage(childHwnd, WM_LBUTTONUP, 0, lParam);
    }

    private static nint MakeLParam(int lo, int hi) => (nint)((hi << 16) | (lo & 0xFFFF));

    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int MK_LBUTTON = 0x0001;

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    // Use pointer variants for 64-bit correctness
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private static readonly nint HWND_TOP = (nint)0;
    private static readonly nint HWND_BOTTOM = (nint)1;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOREDRAW = 0x0008;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x02;
}


