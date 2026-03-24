using System.Text.RegularExpressions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace CheckHelper.Pages
{
    public partial class CheckHelper2 : ContentPage
    {
        public CheckHelper2()
        {
            InitializeComponent();
        }

        void OnClearInput(object? sender, EventArgs e)
        {
            InputEditor.Text = string.Empty;
            ResultEditor.Text = string.Empty;
        }

        async void OnCopyResult(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(ResultEditor.Text))
            {
                await Clipboard.Default.SetTextAsync(ResultEditor.Text);
            }
        }

        void OnCalculate(object? sender, EventArgs e)
        {
            var bytes = ParseHexBytes(InputEditor.Text ?? string.Empty);
            if (bytes == null || bytes.Length == 0)
            {
                ResultEditor.Text = "无法解析输入为16进制字节。请粘贴 2 位十六进制字节，用空格或换行分隔。";
                return;
            }

            // 校验逻辑
            if (rbCheckSum.IsChecked)
            {
                int sum = 0;
                foreach (var b in bytes) sum += b;
                ResultEditor.Text = $"CheckSum: 0x{(sum & 0xFF):X2}";
                return;
            }

            if (rbXORSum.IsChecked)
            {
                int x = 0;
                foreach (var b in bytes) x ^= b;
                ResultEditor.Text = $"XORSum: 0x{x:X2}";
                return;
            }

            if (rbPlus0x33.IsChecked)
            {
                var outBytes = bytes.Select(b => (byte)((b + 0x33) & 0xFF)).ToArray();
                ResultEditor.Text = "加0x33 后: " + string.Join(' ', outBytes.Select(b => b.ToString("X2")));
                return;
            }

            if (rbMinus0x33.IsChecked)
            {
                var outBytes = bytes.Select(b => (byte)((b - 0x33) & 0xFF)).ToArray();
                ResultEditor.Text = "减0x33 后: " + string.Join(' ', outBytes.Select(b => b.ToString("X2")));
                return;
            }

            if (rbASCII.IsChecked)
            {
                try
                {
                    var s = System.Text.Encoding.ASCII.GetString(bytes);
                    ResultEditor.Text = "ASCII: " + s;
                }
                catch
                {
                    ResultEditor.Text = "无法将字节转换为 ASCII。";
                }
                return;
            }

            // 未实现/占位
            if (rbCRC16.IsChecked || rbFCS16.IsChecked || rbInverse.IsChecked)
            {
                ResultEditor.Text = "所选算法尚未实现（占位）。";
                return;
            }

            ResultEditor.Text = "请选择一个校验或转换选项。";
        }

        static byte[]? ParseHexBytes(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var matches = Regex.Matches(input, "[0-9A-Fa-f]{2}");
            if (matches.Count == 0) return null;
            var list = new List<byte>();
            foreach (Match m in matches)
            {
                try
                {
                    list.Add(Convert.ToByte(m.Value, 16));
                }
                catch { }
            }
            return list.ToArray();
        }
    }
}
