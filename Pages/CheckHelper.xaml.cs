using System.Text.RegularExpressions;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace CheckHelper.Pages
{
    public partial class CheckHelper : ContentPage
    {
        private delegate string CalcFunc();

        private Dictionary<RadioButton, CalcFunc> m_CalcFunctions = new Dictionary<RadioButton, CalcFunc>();
        private byte[]? m_InputByte;
        private string? m_frameString;
        private bool m_IsInputValid;
        private RadioButton? m_CurrentRadio;

        public CheckHelper()
        {
            InitializeComponent();

            m_CalcFunctions.Add(rbCRC16, CalcCRC16);
            m_CalcFunctions.Add(rbFCS16, CalcFCS16);
            m_CalcFunctions.Add(rbPlus0x33, CalcAdd33);
            m_CalcFunctions.Add(rbMinus0x33, CalcMinus33);
            m_CalcFunctions.Add(rbInverse, CalcInverse);
            m_CalcFunctions.Add(rbCheckSum, CalcChkSum);
            m_CalcFunctions.Add(rbXORSum, CalcXORSum);
            m_CalcFunctions.Add(rbASCII, CalcASCII);

            m_CalcFunctions.Add(rbFloat, CalcFloat);
            m_CalcFunctions.Add(rbDouble, CalcDouble);
            m_CalcFunctions.Add(rbU32, CalcU32);
            m_CalcFunctions.Add(rbU16, CalcU16);
            m_CalcFunctions.Add(rbS32, CalcS32);
            m_CalcFunctions.Add(rbS16, CalcS16);
        }

        private void InputEditor_TextChanged(object sender, EventArgs e)
        {
            if (PrepareInputString(InputEditor.Text, out m_frameString, out m_InputByte))
            {
                InputEditor.Text = m_frameString;
                CalcSum();
            }
        }

        private void CalcSum()
        {
            if (m_CurrentRadio == null)
            {
                return;
            }

            if (m_InputByte == null || m_InputByte.Length == 0)
            {
                return;
            }

            if (m_CalcFunctions.Keys.Contains(m_CurrentRadio))
            {
                ResultEditor.Text = m_CalcFunctions[m_CurrentRadio]();
                if (m_IsInputValid)
                {
                    TextBlock_Hint.BackgroundColor = new Color(0, 1, 0);
                    TextBlock_Hint.Text = $"{m_InputByte.Length} 字节";
                }
                else
                {
                    TextBlock_Hint.BackgroundColor = new Color(1, 0, 0);
                    TextBlock_Hint.Text = "格式错误";
                }
            }
        }

        private void OnClearInput(object? sender, EventArgs e)
        {
            InputEditor.Text = string.Empty;
            ResultEditor.Text = string.Empty;
        }

        private async void OnCopyResult(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(ResultEditor.Text))
            {
                await Clipboard.Default.SetTextAsync(ResultEditor.Text);
            }
        }

        private void OnCalculate(object? sender, EventArgs e)
        {
            if (!PrepareInputString(InputEditor.Text, out string _, out byte[]? bytes))
            {
                return;
            }

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

        private static readonly byte[] _auchCRCHi = new byte[]//crc高位表
            {
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1,
                0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1,
                0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40,
                0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1,
                0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40,
                0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40,
                0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1,
                0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40
            };

        private static readonly byte[] _auchCRCLo = new byte[]//crc低位表
            {
                0x00, 0xC0, 0xC1, 0x01, 0xC3, 0x03, 0x02, 0xC2, 0xC6, 0x06,
                0x07, 0xC7, 0x05, 0xC5, 0xC4, 0x04, 0xCC, 0x0C, 0x0D, 0xCD,
                0x0F, 0xCF, 0xCE, 0x0E, 0x0A, 0xCA, 0xCB, 0x0B, 0xC9, 0x09,
                0x08, 0xC8, 0xD8, 0x18, 0x19, 0xD9, 0x1B, 0xDB, 0xDA, 0x1A,
                0x1E, 0xDE, 0xDF, 0x1F, 0xDD, 0x1D, 0x1C, 0xDC, 0x14, 0xD4,
                0xD5, 0x15, 0xD7, 0x17, 0x16, 0xD6, 0xD2, 0x12, 0x13, 0xD3,
                0x11, 0xD1, 0xD0, 0x10, 0xF0, 0x30, 0x31, 0xF1, 0x33, 0xF3,
                0xF2, 0x32, 0x36, 0xF6, 0xF7, 0x37, 0xF5, 0x35, 0x34, 0xF4,
                0x3C, 0xFC, 0xFD, 0x3D, 0xFF, 0x3F, 0x3E, 0xFE, 0xFA, 0x3A,
                0x3B, 0xFB, 0x39, 0xF9, 0xF8, 0x38, 0x28, 0xE8, 0xE9, 0x29,
                0xEB, 0x2B, 0x2A, 0xEA, 0xEE, 0x2E, 0x2F, 0xEF, 0x2D, 0xED,
                0xEC, 0x2C, 0xE4, 0x24, 0x25, 0xE5, 0x27, 0xE7, 0xE6, 0x26,
                0x22, 0xE2, 0xE3, 0x23, 0xE1, 0x21, 0x20, 0xE0, 0xA0, 0x60,
                0x61, 0xA1, 0x63, 0xA3, 0xA2, 0x62, 0x66, 0xA6, 0xA7, 0x67,
                0xA5, 0x65, 0x64, 0xA4, 0x6C, 0xAC, 0xAD, 0x6D, 0xAF, 0x6F,
                0x6E, 0xAE, 0xAA, 0x6A, 0x6B, 0xAB, 0x69, 0xA9, 0xA8, 0x68,
                0x78, 0xB8, 0xB9, 0x79, 0xBB, 0x7B, 0x7A, 0xBA, 0xBE, 0x7E,
                0x7F, 0xBF, 0x7D, 0xBD, 0xBC, 0x7C, 0xB4, 0x74, 0x75, 0xB5,
                0x77, 0xB7, 0xB6, 0x76, 0x72, 0xB2, 0xB3, 0x73, 0xB1, 0x71,
                0x70, 0xB0, 0x50, 0x90, 0x91, 0x51, 0x93, 0x53, 0x52, 0x92,
                0x96, 0x56, 0x57, 0x97, 0x55, 0x95, 0x94, 0x54, 0x9C, 0x5C,
                0x5D, 0x9D, 0x5F, 0x9F, 0x9E, 0x5E, 0x5A, 0x9A, 0x9B, 0x5B,
                0x99, 0x59, 0x58, 0x98, 0x88, 0x48, 0x49, 0x89, 0x4B, 0x8B,
                0x8A, 0x4A, 0x4E, 0x8E, 0x8F, 0x4F, 0x8D, 0x4D, 0x4C, 0x8C,
                0x44, 0x84, 0x85, 0x45, 0x87, 0x47, 0x46, 0x86, 0x82, 0x42,
                0x43, 0x83, 0x41, 0x81, 0x80, 0x40
            };

        public static ushort CRC16(Byte[]? buffer, int Sset, int Eset)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            byte crcHi = 0xff; // 高位初始化

            byte crcLo = 0xff; // 低位初始化

            for (int i = Sset; i <= Eset; i++)
            {
                int crcIndex = crcHi ^ buffer[i]; //查找crc表值

                crcHi = (byte)(crcLo ^ _auchCRCHi[crcIndex]);
                crcLo = _auchCRCLo[crcIndex];
            }

            return (ushort)(crcHi << 8 | crcLo);
        }

        /// <summary>
        /// 将输入的报文字符串格式化为
        /// 正规格式, 并输出字节串
        /// </summary>
        /// <param name="inputString">输入的报文字符串</param>
        /// <param name="outputSting">格式化后的正规字符串</param>
        /// <param name="outputByteArray">输出的字节串</param>
        /// <returns>True: 输入的报文字符串格式正确; False: 输入的报文字符串格式错误</returns>
        public static bool PrepareInputString(string inputString, out string outputSting, out byte[]? outputByteArray)
        {
            List<byte> list = new List<byte>();
            string s = inputString;
            bool matched = false;

            outputSting = "";
            string[] charsToRemove = [" ", ",", "\r", "\n", "\t", "\r\n", "\u00A0"];
            foreach (string c in charsToRemove)
            {
                s = s.Replace(c, string.Empty);
            }

            Regex r0 = new("^([0-9a-fA-F]{2})*[0-9a-fA-F]{2}$");
            Regex r1 = new("(0x[0-9a-fA-F]{1,2})+");

            if (r0.IsMatch(s))
            {
                int len = s.Length / 2;

                for (int i = 0; i < len; i++)
                {
                    byte b = (byte)Convert.ToInt32(s.Substring(2 * i, 2), 16);
                    outputSting += string.Format("{0:X2} ", b);
                    list.Add(b);
                }

                matched = true;
            }
            else if (r1.IsMatch(s))
            {
                string[] sa = s.Split(new String[] { "0x" }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < sa.Length; i++)
                {
                    byte b = (byte)Convert.ToInt32(sa[i], 16);
                    outputSting += string.Format("{0:X2} ", b);
                    list.Add(b);
                }

                matched = true;
            }

            if (matched)
            {
                outputByteArray = list.ToArray();
            }
            else
            {
                outputByteArray = null;
            }

            outputSting = outputSting.Trim();

            return matched;
        }

        public static void InverseArray(byte[] buf)
        {
            byte b;
            int bufSize = buf.Length;
            for (int i = 0; i < bufSize / 2; i++)
            {
                b = buf[i];
                buf[i] = buf[bufSize - i - 1];
                buf[bufSize - i - 1] = b;
            }
        }

        private string CalcCRC16()
        {
            if (m_InputByte == null)
            {
                return "NULL Buffer";
            }

            ushort crc = CRC16(m_InputByte, 0, m_InputByte.Length - 1);
            byte[] b = BitConverter.GetBytes(crc);
            InverseArray(b);
            m_IsInputValid = true;
            return BitConverter.ToString(b).Replace('-', ' ');
        }

        private string CalcFCS16()
        {
            ushort crc = FCS16(m_InputByte);
            byte[] b = BitConverter.GetBytes(crc);
            m_IsInputValid = true;
            return BitConverter.ToString(b).Replace('-', ' ');
        }

        private string CalcAdd33()
        {
            byte[] b = new byte[m_InputByte.Length];
            Array.Copy(m_InputByte, b, b.Length);

            Add33(b);
            m_IsInputValid = true;
            return BitConverter.ToString(b).Replace('-', ' ');
        }

        private string CalcMinus33()
        {
            byte[] b = new byte[m_InputByte.Length];
            Array.Copy(m_InputByte, b, b.Length);
            Minus33(b);

            return BitConverter.ToString(b).Replace('-', ' ');
        }

        private string CalcInverse()
        {
            byte[] b = new byte[m_InputByte.Length];
            Array.Copy(m_InputByte, b, b.Length);
            InverseArray(b);
            m_IsInputValid = true;
            return BitConverter.ToString(b).Replace('-', ' ');
        }

        private string CalcChkSum()
        {
            uint sum = ChkSum(m_InputByte);
            byte[] l = BitConverter.GetBytes(sum);
            m_IsInputValid = true;
            return BitConverter.ToString(l).Replace('-', ' ');
        }

        private string CalcXORSum()
        {
            byte[] l = new byte[1];
            l[0] = XORSum(m_InputByte);
            m_IsInputValid = true;
            return BitConverter.ToString(l);
        }

        private string CalcASCII()
        {
            byte[] l = new byte[m_InputByte.Length];
            Array.Copy(m_InputByte, l, l.Length);

            System.Text.ASCIIEncoding asciiEncoding = new System.Text.ASCIIEncoding();
            m_IsInputValid = true;
            return asciiEncoding.GetString(l);
        }

        private string CalcFloat()
        {
            if (m_InputByte.Length != 4)
            {
                TextBlock_Hint.Text = "格式错误";
                return "";
            }

            float f = BitConverter.ToSingle(m_InputByte, 0);
            m_IsInputValid = true;
            return f.ToString();
        }

        private string CalcDouble()
        {
            if (m_InputByte.Length != 8)
            {
                m_IsInputValid = false;
                return "";
            }

            double d = BitConverter.ToDouble(m_InputByte, 0);
            m_IsInputValid = true;
            return d.ToString();
        }

        private string CalcU16()
        {
            if (m_InputByte.Length != 2)
            {
                m_IsInputValid = false;
                return "";
            }

            ushort u = BitConverter.ToUInt16(m_InputByte, 0);
            m_IsInputValid = true;
            return u.ToString();
        }

        private string CalcU32()
        {
            if (m_InputByte.Length != 4)
            {
                m_IsInputValid = false;
                return "";
            }

            uint u = BitConverter.ToUInt32(m_InputByte, 0);
            m_IsInputValid = true;
            return u.ToString();
        }

        private string CalcS16()
        {
            if (m_InputByte.Length != 2)
            {
                m_IsInputValid = false;
                return "";
            }

            short s = BitConverter.ToInt16(m_InputByte, 0);
            m_IsInputValid = true;
            return s.ToString();
        }

        private string CalcS32()
        {
            if (m_InputByte.Length != 4)
            {
                m_IsInputValid = false;
                return "";
            }

            int s = BitConverter.ToInt32(m_InputByte, 0);
            m_IsInputValid = true;
            return s.ToString();
        }

        public static readonly ushort PPPINITFCS16 = 0xffff; /* Initial FCS value */

        public static readonly ushort[] fcstab = new ushort[]{
        0x0000, 0x1189, 0x2312, 0x329b, 0x4624, 0x57ad, 0x6536, 0x74bf,
        0x8c48, 0x9dc1, 0xaf5a, 0xbed3, 0xca6c, 0xdbe5, 0xe97e, 0xf8f7,
        0x1081, 0x0108, 0x3393, 0x221a, 0x56a5, 0x472c, 0x75b7, 0x643e,
        0x9cc9, 0x8d40, 0xbfdb, 0xae52, 0xdaed, 0xcb64, 0xf9ff, 0xe876,
        0x2102, 0x308b, 0x0210, 0x1399, 0x6726, 0x76af, 0x4434, 0x55bd,
        0xad4a, 0xbcc3, 0x8e58, 0x9fd1, 0xeb6e, 0xfae7, 0xc87c, 0xd9f5,
        0x3183, 0x200a, 0x1291, 0x0318, 0x77a7, 0x662e, 0x54b5, 0x453c,
        0xbdcb, 0xac42, 0x9ed9, 0x8f50, 0xfbef, 0xea66, 0xd8fd, 0xc974,
        0x4204, 0x538d, 0x6116, 0x709f, 0x0420, 0x15a9, 0x2732, 0x36bb,
        0xce4c, 0xdfc5, 0xed5e, 0xfcd7, 0x8868, 0x99e1, 0xab7a, 0xbaf3,
        0x5285, 0x430c, 0x7197, 0x601e, 0x14a1, 0x0528, 0x37b3, 0x263a,
        0xdecd, 0xcf44, 0xfddf, 0xec56, 0x98e9, 0x8960, 0xbbfb, 0xaa72,
        0x6306, 0x728f, 0x4014, 0x519d, 0x2522, 0x34ab, 0x0630, 0x17b9,
        0xef4e, 0xfec7, 0xcc5c, 0xddd5, 0xa96a, 0xb8e3, 0x8a78, 0x9bf1,
        0x7387, 0x620e, 0x5095, 0x411c, 0x35a3, 0x242a, 0x16b1, 0x0738,
        0xffcf, 0xee46, 0xdcdd, 0xcd54, 0xb9eb, 0xa862, 0x9af9, 0x8b70,
        0x8408, 0x9581, 0xa71a, 0xb693, 0xc22c, 0xd3a5, 0xe13e, 0xf0b7,
        0x0840, 0x19c9, 0x2b52, 0x3adb, 0x4e64, 0x5fed, 0x6d76, 0x7cff,
        0x9489, 0x8500, 0xb79b, 0xa612, 0xd2ad, 0xc324, 0xf1bf, 0xe036,
        0x18c1, 0x0948, 0x3bd3, 0x2a5a, 0x5ee5, 0x4f6c, 0x7df7, 0x6c7e,
        0xa50a, 0xb483, 0x8618, 0x9791, 0xe32e, 0xf2a7, 0xc03c, 0xd1b5,
        0x2942, 0x38cb, 0x0a50, 0x1bd9, 0x6f66, 0x7eef, 0x4c74, 0x5dfd,
        0xb58b, 0xa402, 0x9699, 0x8710, 0xf3af, 0xe226, 0xd0bd, 0xc134,
        0x39c3, 0x284a, 0x1ad1, 0x0b58, 0x7fe7, 0x6e6e, 0x5cf5, 0x4d7c,
        0xc60c, 0xd785, 0xe51e, 0xf497, 0x8028, 0x91a1, 0xa33a, 0xb2b3,
        0x4a44, 0x5bcd, 0x6956, 0x78df, 0x0c60, 0x1de9, 0x2f72, 0x3efb,
        0xd68d, 0xc704, 0xf59f, 0xe416, 0x90a9, 0x8120, 0xb3bb, 0xa232,
        0x5ac5, 0x4b4c, 0x79d7, 0x685e, 0x1ce1, 0x0d68, 0x3ff3, 0x2e7a,
        0xe70e, 0xf687, 0xc41c, 0xd595, 0xa12a, 0xb0a3, 0x8238, 0x93b1,
        0x6b46, 0x7acf, 0x4854, 0x59dd, 0x2d62, 0x3ceb, 0x0e70, 0x1ff9,
        0xf78f, 0xe606, 0xd49d, 0xc514, 0xb1ab, 0xa022, 0x92b9, 0x8330,
        0x7bc7, 0x6a4e, 0x58d5, 0x495c, 0x3de3, 0x2c6a, 0x1ef1, 0x0f78
        };

        public static ushort PPPFCS16(ushort fcs, byte[] cp)
        {
            for (int i = 0; i < cp.Length; i++)
            {
                fcs = (ushort)((fcs >> 8) ^ fcstab[(fcs ^ cp[i]) & 0xff]);
            }

            return fcs;
        }

        public static ushort FCS16(byte[] cp)
        {
            /* add on output */
            ushort fcs = PPPFCS16(PPPINITFCS16, cp);
            fcs ^= 0xffff; /* complement */

            return fcs;
        }

        public static uint ChkSum(byte[] buf)
        {
            uint sumint = 0;

            for (int i = 0; i < buf.Length; i++)
            {
                sumint += buf[i];
            }

            return sumint;
        }

        public static byte XORSum(byte[] buf)
        {
            byte sum = 0;

            for (int i = 0; i < buf.Length; i++)
            {
                sum ^= buf[i];
            }

            return sum;
        }

        public static void Add33(byte[] buf)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] += 0x33;
            }
        }

        public static void Minus33(byte[] buf)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] -= 0x33;
            }
        }

        private void RadioButton_Checked(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value) // 被选中
            {
                m_CurrentRadio = sender as RadioButton;
                CalcSum();
            }
        }
    }
}