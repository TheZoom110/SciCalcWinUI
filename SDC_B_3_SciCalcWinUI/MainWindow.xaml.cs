using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SDC_B_3_SciCalcWinUI;
using System;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace SDC_B_3_SciCalcWinUI
{
    public sealed partial class MainWindow : Window
    {
        private bool _newEntry = true;
        private string _rawExpression = ""; // what the parser sees

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProc _newWndProc;
        private IntPtr _oldWndProc;
        private IntPtr _hWnd;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProc newProc);
        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const int GWLP_WNDPROC = -4;
        private const uint WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
        }

        public MainWindow()
        {
            this.InitializeComponent();

            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
                this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();

            _hWnd = WindowNative.GetWindowHandle(this);
            _newWndProc = CustomWndProc;
            _oldWndProc = SetWindowLongPtr(_hWnd, GWLP_WNDPROC, _newWndProc);

            var windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(400, 700));
        }

        private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                info.ptMinTrackSize.x = 320;
                info.ptMinTrackSize.y = 500;
                Marshal.StructureToPtr(info, lParam, false);
            }
            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        // ── Helpers ───────────────────────────────────────────────

        // Append both a display value and a raw parser value
        private void Append(string display, string raw = null)
        {
            raw ??= display; // if no raw provided, raw = display
            if (_newEntry)
            {
                DisplayText.Text = display;
                _rawExpression = raw;
                _newEntry = false;
            }
            else
            {
                DisplayText.Text += display;
                _rawExpression += raw;
            }
            ExpressionText.Text = DisplayText.Text;
        }

        private void AppendOperator(string display, string raw = null)
        {
            raw ??= display;
            if (_newEntry) _newEntry = false;
            DisplayText.Text += display;
            _rawExpression += raw;
            ExpressionText.Text = DisplayText.Text;
        }

        private void ShowResult(double result)
        {
            double rounded = Math.Round(result, 10);
            if (double.IsNaN(rounded) || double.IsInfinity(rounded))
            {
                DisplayText.Text = "Error";
                _rawExpression = "";
            }
            else
            {
                // Display uses G10 (may show E notation)
                DisplayText.Text = rounded.ToString("G10");
                // Raw expression uses R format to preserve full precision without E notation issues
                _rawExpression = rounded.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
            ExpressionText.Text = DisplayText.Text;
            _newEntry = true;
        }

        // ── Number Input ──────────────────────────────────────────

        private void Num_Click(object sender, RoutedEventArgs e)
            => Append(((Button)sender).Tag.ToString());

        private void Dot_Click(object sender, RoutedEventArgs e)
        {
            if (_newEntry)
            {
                DisplayText.Text = "0.";
                _rawExpression = "0.";
                _newEntry = false;
                ExpressionText.Text = DisplayText.Text;
                return;
            }

            string text = DisplayText.Text;
            int lastOpPos = -1;
            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (text[i] == '+' || text[i] == '-' ||
                    text[i] == '×' || text[i] == '÷' ||
                    text[i] == '(')
                { lastOpPos = i; break; }
            }
            string lastSegment = text.Substring(lastOpPos + 1);
            if (!lastSegment.Contains('.'))
            {
                DisplayText.Text += ".";
                _rawExpression += ".";
                ExpressionText.Text = DisplayText.Text;
            }
        }

        // ── Operators ─────────────────────────────────────────────

        private void Op_Click(object sender, RoutedEventArgs e)
        {
            string raw = ((Button)sender).Tag.ToString();
            string display = raw switch
            {
                "*" => "×",
                "/" => "÷",
                _ => raw
            };
            AppendOperator(display, raw);
        }

        // ── Parenthesis ──────────────────────────────────────────

        private void OpenParen_Click(object sender, RoutedEventArgs e)
    => Append("(");

        private void CloseParen_Click(object sender, RoutedEventArgs e)
            => Append(")");

        // ── Equals ────────────────────────────────────────────────

        private void Equals_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double result = ExpressionParser.Evaluate(_rawExpression);
                ShowResult(result);
            }
            catch
            {
                DisplayText.Text = "Error";
                _rawExpression = "";
                _newEntry = true;
            }
        }

        // ── Scientific Functions ──────────────────────────────────

        private void Sci_Click(object sender, RoutedEventArgs e)
        {
            string tag = ((Button)sender).Tag.ToString();
            switch (tag)
            {
                case "pi": Append("π", "pi"); break;
                case "e": Append("e"); break;
                case "sin": Append("sin("); break;
                case "cos": Append("cos("); break;
                case "tan": Append("tan("); break;
                case "log": Append("log("); break;
                case "ln": Append("ln("); break;
                case "pow": AppendOperator("^"); break;

                case "sq":
                    if (!_newEntry)
                    {
                        DisplayText.Text = $"({DisplayText.Text})^2";
                        _rawExpression = $"({_rawExpression})^2";
                        ExpressionText.Text = DisplayText.Text;
                    }
                    break;

                case "sqrt":
                    Append("√(", "sqrt(");
                    break;
            }
        }

        // ── Backspace ─────────────────────────────────────────────

        private void Backspace_Click(object sender, RoutedEventArgs e)
        {
            string text = DisplayText.Text;
            if (text == "Error" || text == "0")
            {
                DisplayText.Text = "0";
                _rawExpression = "";
                _newEntry = true;
                return;
            }

            // Display/raw pairs to remove together
            (string display, string raw)[] functions = {
                ("sin(", "sin("), ("cos(", "cos("), ("tan(", "tan("),
                ("log(", "log("), ("ln(",  "ln("),  ("√(",   "sqrt(")
            };

            foreach (var (display, raw) in functions)
            {
                if (text.EndsWith(display))
                {
                    DisplayText.Text = text.Length > display.Length ? text[..^display.Length] : "0";
                    _rawExpression = _rawExpression.Length > raw.Length
                        ? _rawExpression[..^raw.Length] : "";
                    ExpressionText.Text = DisplayText.Text;
                    return;
                }
            }

            // Handle × and ÷ — display is 1 char but raw is also 1 char
            char lastDisplay = text[^1];
            int rawCharsToRemove = lastDisplay switch
            {
                '×' => 1, // raw is *
                '÷' => 1, // raw is /
                'π' => 2, // raw is pi
                _ => 1
            };

            DisplayText.Text = text.Length > 1 ? text[..^1] : "0";
            _rawExpression = _rawExpression.Length > rawCharsToRemove
                ? _rawExpression[..^rawCharsToRemove] : "";
            ExpressionText.Text = DisplayText.Text;
        }

        // ── Utility ───────────────────────────────────────────────

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            DisplayText.Text = "0";
            ExpressionText.Text = "";
            _rawExpression = "";
            _newEntry = true;
        }

        private void Sign_Click(object sender, RoutedEventArgs e)
        {
            if (DisplayText.Text.StartsWith("-"))
            {
                DisplayText.Text = DisplayText.Text[1..];
                _rawExpression = _rawExpression.Length > 1 ? _rawExpression[1..] : "0";
            }
            else
            {
                DisplayText.Text = "-" + DisplayText.Text;
                _rawExpression = "-" + _rawExpression;
            }
            ExpressionText.Text = DisplayText.Text;
        }

        private void Percent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double result = ExpressionParser.Evaluate(_rawExpression) / 100;
                ShowResult(result);
            }
            catch { DisplayText.Text = "Error"; }
        }

        // ── Responsiveness ────────────────────────────────────────

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Height < 500)
            {
                ScientificPanel.Visibility = Visibility.Collapsed;
                DisplayText.FontSize = 32;
                RootGrid.Padding = new Microsoft.UI.Xaml.Thickness(8);
            }
            else
            {
                ScientificPanel.Visibility = Visibility.Visible;
                DisplayText.FontSize = 40;
                RootGrid.Padding = new Microsoft.UI.Xaml.Thickness(12);
            }
        }
    }
}