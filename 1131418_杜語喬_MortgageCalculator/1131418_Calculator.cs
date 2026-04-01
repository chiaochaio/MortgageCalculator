using System;
using System.Linq;
using System.Windows.Forms;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;

namespace _1131418_杜語喬_MortgageCalculator
{
    public partial class Form1 : Form
    {
        // 防止互相觸發事件導致遞迴
        private bool isUpdatingDownFields = false;

        public Form1()
        {
            InitializeComponent();
            SetupPlaceholdersAndEvents();

            /*
              設定 TabIndex（按 Tab 鍵會依這個順序移動焦點）。若你想改順序，調整數字即可（從 0 開始）。
            */
            this.tbTotal.TabIndex = 0;
            this.tbDown.TabIndex = 1;
            this.tbDown2.TabIndex = 2;
            this.tbRate.TabIndex = 3;
            this.tbTerm.TabIndex = 4;
            this.tbYear.TabIndex = 5;
            this.btnEnter.TabIndex = 6;
            this.btnClear.TabIndex = 7;

            // 確保這些控制項可以被 Tab 選取（按你的需求：按鈕不納入 Tab 序列）
            this.tbTotal.TabStop = true;
            this.tbDown.TabStop = true;
            this.tbDown2.TabStop = true;
            this.tbRate.TabStop = true;
            this.tbTerm.TabStop = true;
            this.tbYear.TabStop = true;

            // 按鈕不要被 Tab 到
            this.btnEnter.TabStop = false;
            this.btnClear.TabStop = false;

            // 綁定 tbDown 的 TextChanged 事件（讓輸入百分比時即時計算 tbDown2）
            this.tbDown.TextChanged += new System.EventHandler(this.tbDown_TextChanged);

            // 綁定按鈕點擊與 Enter key
            this.btnEnter.Click += new System.EventHandler(this.BtnEnter_Click);
            // btnClear 的 Click 已由 Designer 綁定到 button2_Click；在此保留不重複綁定

            this.KeyPreview = true;
            this.KeyDown += new KeyEventHandler(this.Form1_KeyDown);
        }

        // 初始化 placeholder（把目前 Text 當作 placeholder 存入 Tag）並綁定 Enter/Leave/KeyPress 事件
        private void SetupPlaceholdersAndEvents()
        {
            // 覆寫 tbTotal / tbDown2 的 placeholder 為「萬元」
            if (tbTotal != null) tbTotal.Tag = "(NTD)";
            if (tbDown2 != null) tbDown2.Tag = "(NTD)";

            // 要處理 placeholder 的 textbox 列表
            var boxes = new[] { tbTotal, tbDown, tbDown2, tbRate, tbTerm, tbYear };

            foreach (var tb in boxes)
            {
                if (tb == null) continue;
                // 如果 Designer 沒設定 Tag，就把目前 Text 當作 placeholder（但 tbTotal/tbDown2 已覆寫）
                if (tb.Tag == null)
                    tb.Tag = tb.Text ?? string.Empty;

                // 若目前文字為空或等於預設 placeholder，確保顯示 placeholder
                var placeholder = tb.Tag as string ?? string.Empty;
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    tb.Text = placeholder;
                    tb.ForeColor = SystemColors.WindowFrame;
                }
                else if (tb.Text == placeholder)
                {
                    tb.ForeColor = SystemColors.WindowFrame;
                }

                // 綁定事件
                tb.Enter += TextBox_Enter;
                tb.Leave += TextBox_Leave;

                // 綁定 KeyPress 防呆：不同欄位允許不同字元
                if (tb == tbTotal || tb == tbDown2 || tb == tbRate)
                {
                    tb.KeyPress += NumericDecimalKeyPress; // 允許數字、小數點、控制鍵
                }
                else if (tb == tbDown)
                {
                    tb.KeyPress += PercentNumericKeyPress; // 允許數字、小數點、% 和控制鍵
                }
                else if (tb == tbTerm || tb == tbYear)
                {
                    tb.KeyPress += IntegerKeyPress; // 只允許整數
                }
            }
        }

        // KeyPress：允許數字、小數點、控制鍵（Backspace）
        private void NumericDecimalKeyPress(object sender, KeyPressEventArgs e)
        {
            char ch = e.KeyChar;
            var tb = sender as TextBox;
            if (char.IsControl(ch)) return;
            if (ch == '.' || ch == CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator.FirstOrDefault())
            {
                // 只允許出現一次小數點
                if (tb.Text.Contains('.') || tb.Text.Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator))
                    e.Handled = true;
                return;
            }
            if (!char.IsDigit(ch))
            {
                e.Handled = true;
            }
        }

        // KeyPress：允許數字、小數點、%（一次）與控制鍵
        private void PercentNumericKeyPress(object sender, KeyPressEventArgs e)
        {
            char ch = e.KeyChar;
            var tb = sender as TextBox;
            if (char.IsControl(ch)) return;
            if (ch == '%')
            {
                if (tb.Text.Contains('%')) e.Handled = true;
                return;
            }
            if (ch == '.' || ch == CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator.FirstOrDefault())
            {
                if (tb.Text.Contains('.') || tb.Text.Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator))
                    e.Handled = true;
                return;
            }
            if (!char.IsDigit(ch))
            {
                e.Handled = true;
            }
        }

        // KeyPress：只允許數字與控制鍵（整數欄位）
        private void IntegerKeyPress(object sender, KeyPressEventArgs e)
        {
            char ch = e.KeyChar;
            if (char.IsControl(ch)) return;
            if (!char.IsDigit(ch))
            {
                e.Handled = true;
            }
        }

        // 當使用者進入 textbox：若內容等於 placeholder，清空並把前景色改回可輸入色
        // 針對 tbTotal / tbDown2：若原先已格式化（有逗點），移除逗點並選取全部，方便編輯
        // 針對 tbDown：若已有 '%'，進入編輯時會移除 '%' 並選取全部，方便輸入數字
        private void TextBox_Enter(object sender, EventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;
            var placeholder = tb.Tag as string ?? string.Empty;
            if (!string.IsNullOrEmpty(placeholder) && tb.Text == placeholder)
            {
                tb.Text = "";
                tb.ForeColor = SystemColors.WindowText;
                return;
            }

            // 如果是 tbTotal 或 tbDown2 且目前文字不是 placeholder，移除千分位逗號以便編輯，並選取全部
            if ((tb == tbTotal || tb == tbDown2) && !string.IsNullOrWhiteSpace(tb.Text) && tb.Text != placeholder)
            {
                var raw = tb.Text.Replace(",", "").Replace(" ", "");
                tb.Text = raw;
                tb.ForeColor = SystemColors.WindowText;
                tb.SelectAll();
                return;
            }

            // 如果是 tbDown，移除尾端的 % 以便編輯，並選取全部
            if (tb == tbDown && !string.IsNullOrWhiteSpace(tb.Text) && tb.Text != placeholder)
            {
                var raw = tb.Text.Replace("%", "").Trim();
                tb.Text = raw;
                tb.ForeColor = SystemColors.WindowText;
                tb.SelectAll();
                return;
            }
        }

        // 當離開 textbox：若內容為空，還原 placeholder 並把前景色改為提示色
        // 並在離開時做防呆驗證（總價非負、百分比範圍、整數欄位非負等）
        private void TextBox_Leave(object sender, EventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;
            var placeholder = tb.Tag as string ?? string.Empty;

            // 對 tbTotal / tbDown2 嘗試格式化數字（但不要格式化 placeholder）
            if ((tb == tbTotal || tb == tbDown2))
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    tb.Text = placeholder;
                    tb.ForeColor = SystemColors.WindowFrame;
                    return;
                }
                if (tb.Text != placeholder)
                {
                    // 解析並格式化（視為萬元）
                    string formatted = TryFormatNumberWithCommas(tb.Text);
                    if (formatted != null)
                    {
                        // 驗證數值非負
                        if (TryParseTextBoxNumber(formatted, out double v) && v >= 0)
                        {
                            tb.Text = formatted;
                            tb.ForeColor = SystemColors.WindowText;
                        }
                        else
                        {
                            ShowValidationError("數值須為非負數（單位：萬元）。", tb);
                        }
                        return;
                    }
                    else
                    {
                        // 無法解析則還原 placeholder（視作錯誤）
                        ShowValidationError("輸入不是有效數字。", tb);
                        return;
                    }
                }
            }

            // tbDown（百分比）離開時驗證 0..100 並加上 %
            if (tb == tbDown)
            {
                if (string.IsNullOrWhiteSpace(tb.Text) || tb.Text == placeholder)
                {
                    tb.Text = placeholder;
                    tb.ForeColor = SystemColors.WindowFrame;
                    return;
                }
                var downRaw = tb.Text.Replace("%", "").Trim();
                if (!double.TryParse(downRaw, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out double percent))
                {
                    ShowValidationError("自備款比例必須是數字（%）。", tb);
                    return;
                }
                if (percent < 0 || percent > 100)
                {
                    ShowValidationError("自備款比例需在 0 到 100 之間。", tb);
                    return;
                }
                // 合法，顯示時自動加入 % 符號，並可能觸發 tbDown_TextChanged 更新 tbDown2
                tb.Text = percent.ToString("0.##", CultureInfo.CurrentCulture) + "%";
                tb.ForeColor = SystemColors.WindowText;
                return;
            }

            // tbRate（利率）離開時驗證非負
            if (tb == tbRate)
            {
                if (string.IsNullOrWhiteSpace(tb.Text) || tb.Text == placeholder)
                {
                    tb.Text = placeholder;
                    tb.ForeColor = SystemColors.WindowFrame;
                    return;
                }
                var raw = tb.Text.Replace("%", "").Trim();
                if (!double.TryParse(raw, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out double rate))
                {
                    ShowValidationError("貸款年利率必須是數字。", tb);
                    return;
                }
                if (rate < 0)
                {
                    ShowValidationError("利率不得為負。", tb);
                    return;
                }
                tb.Text = rate.ToString("0.##", CultureInfo.CurrentCulture);
                tb.ForeColor = SystemColors.WindowText;
                return;
            }

            // tbTerm / tbYear（整數）離開時驗證為非負整數
            if (tb == tbTerm || tb == tbYear)
            {
                if (string.IsNullOrWhiteSpace(tb.Text) || tb.Text == placeholder)
                {
                    tb.Text = placeholder;
                    tb.ForeColor = SystemColors.WindowFrame;
                    return;
                }
                var digits = new string(tb.Text.Where(c => char.IsDigit(c) || c == '-').ToArray());
                if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.CurrentCulture, out int ivalue))
                {
                    ShowValidationError("請輸入非負整數。", tb);
                    return;
                }
                if (ivalue < 0)
                {
                    ShowValidationError("數值不得為負。", tb);
                    return;
                }
                tb.Text = ivalue.ToString(CultureInfo.CurrentCulture);
                tb.ForeColor = SystemColors.WindowText;
                return;
            }

            // 一般 textbox 邏輯：內容為空時還原 placeholder
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.Text = placeholder;
                tb.ForeColor = SystemColors.WindowFrame;
            }
        }

        // 顯示驗證錯誤並還原 placeholder
        private void ShowValidationError(string message, TextBox tb)
        {
            MessageBox.Show(message, "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            var placeholder = tb.Tag as string ?? string.Empty;
            tb.Text = placeholder;
            tb.ForeColor = SystemColors.WindowFrame;
            tb.Focus();
        }

        // 嘗試把輸入解析成數字並回傳帶千分位的格式字串（視為萬元）
        // 回傳 null 表示無法解析
        private string TryFormatNumberWithCommas(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            // 移除常見非數字字元
            var cleaned = input.Replace(",", "").Replace("$", "").Replace(" ", "").Trim();

            // 只保留數字、小數點與負號
            var digits = new string(cleaned.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            if (string.IsNullOrWhiteSpace(digits)) return null;

            // 先用當前文化嘗試解析，若失敗再用 invariant
            if (!double.TryParse(digits, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out double value))
            {
                if (!double.TryParse(digits, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                    return null;
            }

            // 根據是否有小數決定格式（整數顯示無小數、有小數顯示至多兩位且去尾零）
            if (Math.Abs(value - Math.Round(value)) < 0.0000001)
            {
                return value.ToString("N0", CultureInfo.CurrentCulture); // 整數（萬元）
            }
            else
            {
                string s = value.ToString("N2", CultureInfo.CurrentCulture);
                if (s.Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator))
                {
                    s = s.TrimEnd('0');
                    if (s.EndsWith(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator))
                        s = s.TrimEnd(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator.ToCharArray());
                }
                return s;
            }
        }

        // tbDown2 (自備款金額，單位為萬元) 的 TextChanged 事件處理：
        // 當使用者輸入自備款金額（萬元）時，自動計算比例並填入 tbDown（%）
        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            if (isUpdatingDownFields) return;

            // 如果 tbDown2 為 placeholder 或空，則清空 tbDown（顯示 placeholder）並結束
            var tbDown2Placeholder = tbDown2.Tag as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tbDown2.Text) || tbDown2.Text == tbDown2Placeholder)
            {
                try
                {
                    isUpdatingDownFields = true;
                    var downPlaceholder = tbDown.Tag as string ?? string.Empty;
                    tbDown.Text = downPlaceholder;
                    tbDown.ForeColor = SystemColors.WindowFrame;
                }
                finally
                {
                    isUpdatingDownFields = false;
                }
                return;
            }

            // tbTotal 若為 placeholder 或空，無法計算
            if (string.IsNullOrWhiteSpace(tbTotal.Text) || (tbTotal.Tag as string) == tbTotal.Text) return;

            // 解析總價（萬元）與自備款金額（萬元）
            double totalWan;
            double downWan;

            // 解析 tbTotal（萬元）
            if (!TryParseTextBoxNumber(tbTotal.Text, out totalWan)) return;
            if (Math.Abs(totalWan) < double.Epsilon) return;

            // 解析 tbDown2（萬元）
            if (!TryParseTextBoxNumber(tbDown2.Text, out downWan)) return;

            // 驗證非負與不超過總價
            if (downWan < 0)
            {
                ShowValidationError("自備款金額不得為負。", tbDown2);
                return;
            }
            if (downWan > totalWan)
            {
                ShowValidationError("自備款金額不得超過房屋總價。", tbDown2);
                return;
            }

            // 計算比例（同單位：萬元）
            double percent = downWan / totalWan * 100.0;

            // 更新 tbDown（百分比），採用保護旗標避免遞迴
            try
            {
                isUpdatingDownFields = true;
                tbDown.Text = percent.ToString("0.##", CultureInfo.CurrentCulture); // 最多兩位小數
            }
            finally
            {
                isUpdatingDownFields = false;
            }
        }

        // tbDown (百分比) 的 TextChanged 事件處理：當使用者輸入百分比時，自動計算金額並填入 tbDown2（萬元）
        private void tbDown_TextChanged(object sender, EventArgs e)
        {
            if (isUpdatingDownFields) return;

            // 如果 tbDown 為 placeholder 或空，則清空 tbDown2（顯示 placeholder）並結束
            var tbDownPlaceholder = tbDown.Tag as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tbDown.Text) || tbDown.Text == tbDownPlaceholder)
            {
                try
                {
                    isUpdatingDownFields = true;
                    var down2Placeholder = tbDown2.Tag as string ?? string.Empty;
                    tbDown2.Text = down2Placeholder;
                    tbDown2.ForeColor = SystemColors.WindowFrame;
                }
                finally
                {
                    isUpdatingDownFields = false;
                }
                return;
            }

            // tbTotal 若為 placeholder 或空，無法計算
            if (string.IsNullOrWhiteSpace(tbTotal.Text) || (tbTotal.Tag as string) == tbTotal.Text) return;

            // 解析總價（萬元）與百分比
            double totalWan;
            double percent;
            if (!TryParseTextBoxNumber(tbTotal.Text, out totalWan)) return;
            if (Math.Abs(totalWan) < double.Epsilon) return;

            // 解析 tbDown（允許帶 %）
            var downRaw = tbDown.Text.Replace("%", "").Trim();
            if (!double.TryParse(downRaw, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out percent))
            {
                if (!double.TryParse(downRaw, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out percent))
                    return;
            }

            if (percent < 0 || percent > 100)
            {
                // 不立即跳錯，但不計算
                return;
            }

            // 計算金額（萬元），四捨五入到小數最多兩位並格式化顯示
            double amountWan = totalWan * percent / 100.0;

            try
            {
                isUpdatingDownFields = true;
                // 格式化：整數萬元顯示無小數，有小數顯示最多兩位並去尾零
                if (Math.Abs(amountWan - Math.Round(amountWan)) < 0.0000001)
                    tbDown2.Text = ((long)Math.Round(amountWan)).ToString("N0", CultureInfo.CurrentCulture);
                else
                {
                    string s = amountWan.ToString("N2", CultureInfo.CurrentCulture);
                    if (s.Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator))
                    {
                        s = s.TrimEnd('0');
                        if (s.EndsWith(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator))
                            s = s.TrimEnd(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator.ToCharArray());
                    }
                    tbDown2.Text = s;
                }
            }
            finally
            {
                isUpdatingDownFields = false;
            }
        }

        // 輔助：解析 textbox 中的數字字串（允許千分位、$、空格），回傳 double（代表萬元）
        private bool TryParseTextBoxNumber(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var cleaned = text.Replace(",", "").Replace("$", "").Replace(" ", "").Trim();
            var digits = new string(cleaned.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            if (string.IsNullOrWhiteSpace(digits)) return false;

            if (!double.TryParse(digits, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out value))
            {
                if (!double.TryParse(digits, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                    return false;
            }
            return true;
        }

        // 按鈕點擊或 Enter 鍵呼叫
        private void BtnEnter_Click(object sender, EventArgs e)
        {
            CalculateAndShowResults();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                CalculateAndShowResults();
                return;
            }
            // Ctrl+R 清除所有
            if (e.Control && e.KeyCode == Keys.R)
            {
                e.SuppressKeyPress = true;
                ClearAllInputs();
                return;
            }
        }

        // 計算並顯示結果（格式：千分位逗號 + 兩位小數）
        private void CalculateAndShowResults()
        {
            // 先檢查必要欄位（除了寬限期 tbYear 可為 placeholder）
            var totalPlaceholder = tbTotal.Tag as string ?? string.Empty;
            var downPlaceholder = tbDown.Tag as string ?? string.Empty;
            var down2Placeholder = tbDown2.Tag as string ?? string.Empty;
            var ratePlaceholder = tbRate.Tag as string ?? string.Empty;
            var termPlaceholder = tbTerm.Tag as string ?? string.Empty;

            if (string.IsNullOrWhiteSpace(tbTotal.Text) || tbTotal.Text == totalPlaceholder)
            {
                MessageBox.Show("請輸入房屋總價。", "資料不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 解析總價
            if (!TryParseTextBoxNumber(tbTotal.Text, out double totalValue))
            {
                MessageBox.Show("房屋總價格式錯誤。", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 解析自備款：優先使用 tbDown2（數額），否則使用 tbDown（%）
            double downAmount = 0.0;
            bool downParsed = false;
            if (!string.IsNullOrWhiteSpace(tbDown2.Text) && tbDown2.Text != down2Placeholder)
            {
                if (!TryParseTextBoxNumber(tbDown2.Text, out downAmount))
                {
                    MessageBox.Show("自備款金額格式錯誤。", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                downParsed = true;
            }
            else if (!string.IsNullOrWhiteSpace(tbDown.Text) && tbDown.Text != downPlaceholder)
            {
                var downRaw = tbDown.Text.Replace("%", "").Trim();
                if (!double.TryParse(downRaw, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out double percent))
                {
                    MessageBox.Show("自備款比例格式錯誤。", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (percent < 0 || percent > 100)
                {
                    MessageBox.Show("自備款比例需在 0 到 100 之間。", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                downAmount = totalValue * percent / 100.0;
                downParsed = true;
            }
            else
            {
                // 若兩者皆為 placeholder 或空，視為自備款為 0
                downAmount = 0.0;
                downParsed = true;
            }

            // 驗證自備款不超過總價
            if (downAmount < 0 || downAmount > totalValue)
            {
                MessageBox.Show("自備款不得為負或超過房屋總價。", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 解析利率
            if (string.IsNullOrWhiteSpace(tbRate.Text) || tbRate.Text == ratePlaceholder)
            {
                MessageBox.Show("請輸入貸款年利率。", "資料不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var rateRaw = tbRate.Text.Replace("%", "").Trim();
            if (!double.TryParse(rateRaw, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out double annualRate))
            {
                MessageBox.Show("貸款年利率格式錯誤。", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (annualRate < 0)
            {
                MessageBox.Show("利率不得為負。", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 解析貸款年限
            if (string.IsNullOrWhiteSpace(tbTerm.Text) || tbTerm.Text == termPlaceholder)
            {
                MessageBox.Show("請輸入貸款年限。", "資料不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!int.TryParse(new string(tbTerm.Text.Where(c => char.IsDigit(c) || c == '-').ToArray()), NumberStyles.Integer, CultureInfo.CurrentCulture, out int termYears))
            {
                MessageBox.Show("貸款年限格式錯誤。", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (termYears <= 0)
            {
                MessageBox.Show("貸款年限需為正整數。", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 解析寬限期（可為 placeholder）
            int graceYears = 0;
            var gracePlaceholder = tbYear.Tag as string ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(tbYear.Text) && tbYear.Text != gracePlaceholder)
            {
                if (!int.TryParse(new string(tbYear.Text.Where(c => char.IsDigit(c) || c == '-').ToArray()), NumberStyles.Integer, CultureInfo.CurrentCulture, out graceYears))
                {
                    MessageBox.Show("寬限期格式錯誤。", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (graceYears < 0) graceYears = 0;
            }

            // 計算貸款總金額 (principal)
            double principal = totalValue - downAmount;

            if (principal < 0)
            {
                MessageBox.Show("計算後貸款金額為負，請檢查輸入。", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int totalMonths = termYears * 12;
            int graceMonths = Math.Min(graceYears * 12, totalMonths);
            int amortMonths = totalMonths - graceMonths;
            if (amortMonths <= 0 && totalMonths > 0)
            {
                // 全部為寬限期，僅付利息
                amortMonths = 0;
            }

            double monthlyRate = annualRate / 100.0 / 12.0;

            double amortMonthlyPayment = 0.0; // 若有攤還期，攤還期的每月本息
            double firstPaymentInterest = 0.0;
            double firstPaymentPrincipal = 0.0;
            double totalInterest = 0.0;
            double totalRepayment = 0.0;
            double displayedMonthly = 0.0;

            // 若攤還期月份 > 0，計算一般等額本息每月金額
            if (amortMonths > 0)
            {
                if (Math.Abs(monthlyRate) < 1e-12)
                {
                    amortMonthlyPayment = principal / amortMonths;
                }
                else
                {
                    double r = monthlyRate;
                    double pow = Math.Pow(1 + r, amortMonths);
                    amortMonthlyPayment = principal * r * pow / (pow - 1);
                }

                // first payment: 若存在寬限期，第一筆實際攤還時（在寬限期結束後）的首期本息為 amortMonthlyPayment，
                // 但「首期利息」和「首期本金」根據他要求我們顯示「第一筆實際付款（若有寬限期，第一筆是利息-only）」：
                if (graceMonths > 0)
                {
                    // 第一筆付款是在表面上的第一期（若使用者想看第一次繳款為寬限期內），則該期為利息 only
                    firstPaymentInterest = principal * monthlyRate;
                    firstPaymentPrincipal = 0.0;
                    // lblMonth 我們顯示的是常態的攤還月付（本+息）
                    displayedMonthly = amortMonthlyPayment;
                }
                else
                {
                    // 無寬限期，第一期即為攤還期的第一個月
                    firstPaymentInterest = principal * monthlyRate;
                    firstPaymentPrincipal = amortMonthlyPayment - firstPaymentInterest;
                    displayedMonthly = amortMonthlyPayment;
                }

                // 總利息：若有寬限期，須加上寬限期支付的利息
                double interestDuringGrace = principal * monthlyRate * graceMonths;
                double interestDuringAmort = amortMonthlyPayment * amortMonths - principal;
                totalInterest = interestDuringGrace + interestDuringAmort;
                totalRepayment = principal + totalInterest;
            }
            else
            {
                // 沒有攤還期（全部為寬限期或 term為0），每期僅付利息
                displayedMonthly = principal * monthlyRate; // interest only
                firstPaymentInterest = displayedMonthly;
                firstPaymentPrincipal = 0.0;
                totalInterest = displayedMonthly * totalMonths; // 所有期數都是利息
                totalRepayment = principal + totalInterest;
            }

            // 顯示結果（格式化：千分位 + 2 小數）
            lblTotal.Text = FormatNumber(principal);
            lblMonth.Text = FormatNumber(displayedMonthly);
            lblInterest.Text = FormatNumber(firstPaymentInterest);
            lblCapital.Text = FormatNumber(firstPaymentPrincipal);
            lblTpay.Text = FormatNumber(totalInterest);
            lblRepayment.Text = FormatNumber(totalRepayment);

            // 更新元餅圖（本金 vs 利息）
            UpdatePieChart(principal, totalInterest);
        }

        private string FormatNumber(double v)
        {
            return v.ToString("N2", CultureInfo.CurrentCulture);
        }

        // 更新元餅圖：傳入本金與利息（同單位），顯示百分比比較
        private void UpdatePieChart(double principalAmount, double interestAmount)
        {
            if (this.chartPie == null) return;

            try
            {
                // 清除舊資料與區域
                chartPie.Series.Clear();
                chartPie.Legends.Clear();
                chartPie.ChartAreas.Clear();

                // 建立 ChartArea（必要）
                var area = new System.Windows.Forms.DataVisualization.Charting.ChartArea("Default");
                chartPie.ChartAreas.Add(area);

                // 圖例（顯示項目名稱）
                var legend = new System.Windows.Forms.DataVisualization.Charting.Legend();
                legend.Docking = System.Windows.Forms.DataVisualization.Charting.Docking.Bottom;
                chartPie.Legends.Add(legend);

                // 建立圓餅 series，並指派到 ChartArea
                var series = new System.Windows.Forms.DataVisualization.Charting.Series("本金利息")
                {
                    ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Pie,
                    ChartArea = "Default",
                    IsValueShownAsLabel = true,
                    // 只顯示百分比（小數 1 位），不顯示金額
                    Label = "#PERCENT{P1}"
                };

                // 防護：若兩者都為 0，給予預設值以避免空圖
                double p = Math.Max(0.0, principalAmount);
                double i = Math.Max(0.0, interestAmount);
                if (Math.Abs(p) < 1e-12 && Math.Abs(i) < 1e-12)
                {
                    p = 1.0;
                    i = 0.0;
                }

                var dpPrincipal = new System.Windows.Forms.DataVisualization.Charting.DataPoint(0, p)
                {
                    AxisLabel = "本金",
                    LegendText = "本金"
                };
                var dpInterest = new System.Windows.Forms.DataVisualization.Charting.DataPoint(0, i)
                {
                    AxisLabel = "利息",
                    LegendText = "利息"
                };

                series.Points.Add(dpPrincipal);
                series.Points.Add(dpInterest);

                // 標籤樣式：放在扇外（若空間不足會自動調整）
                series["PieLabelStyle"] = "Outside";

                chartPie.Series.Add(series);

                // 確保圖表可見與重繪
                chartPie.Visible = true;
                chartPie.Invalidate();
            }
            catch (Exception)
            {
                // 不中斷 UI；除錯時可顯示例外內容
            }
        }

        // 清除所有輸入與結果（在 btnClear click 與 Ctrl+R 時呼叫）
        private void ClearAllInputs()
        {
            try
            {
                // TextBox 清回 placeholder 並設為提示色
                var boxes = new[] { tbTotal, tbDown, tbDown2, tbRate, tbTerm, tbYear };
                foreach (var tb in boxes)
                {
                    if (tb == null) continue;
                    var placeholder = tb.Tag as string ?? string.Empty;
                    tb.Text = placeholder;
                    tb.ForeColor = SystemColors.WindowFrame;
                }

                // 清空結果顯示
                lblMonth.Text = string.Empty;
                lblTotal.Text = string.Empty;
                lblInterest.Text = string.Empty;
                lblCapital.Text = string.Empty;
                lblTpay.Text = string.Empty;
                lblRepayment.Text = string.Empty;

                // 清空圖表
                try
                {
                    if (chartPie != null)
                    {
                        chartPie.Series.Clear();
                        chartPie.Legends.Clear();
                    }
                }
                catch { }

                // 取消控制項焦點（讓表單自身取得焦點）
                try
                {
                    this.ActiveControl = null;
                }
                catch { /* 無害失敗 */ }
            }
            finally
            {
                // 確保不會留下互動保護旗標
                isUpdatingDownFields = false;
            }
        }

        private void label3_Click(object sender, EventArgs e) { }
        private void label13_Click(object sender, EventArgs e) { }
        private void textBox6_TextChanged(object sender, EventArgs e) { }
        // btnClear 的 Designer 綁定會呼叫此方法
        private void button2_Click(object sender, EventArgs e)
        {
            ClearAllInputs();
        }
        private void label10_Click(object sender, EventArgs e) { }
        private void label22_Click(object sender, EventArgs e) { }
        private void label27_Click(object sender, EventArgs e) { }
        private void label28_Click(object sender, EventArgs e) { }
        private void label23_Click(object sender, EventArgs e) { }
        private void label29_Click(object sender, EventArgs e) { }
        private void label30_Click(object sender, EventArgs e) { }
        private void label33_Click(object sender, EventArgs e) { }
        private void label7_Click(object sender, EventArgs e) { }

        private void label5_Click(object sender, EventArgs e)
        {
            // label5 為裝飾用標籤（"房屋總價 :"），不需要額外邏輯。
            // 保留空的事件處理器以符合 Designer 的事件綁定。
        }

        private void label1_Click(object sender, EventArgs e)
        {
            // label1 為標題 "輸入貸款詳情"，無需動作。
            // 空實作以避免 Designer 綁定造成錯誤。
        }

        private void label41_Click(object sender, EventArgs e) { /* 可留空或加入日後邏輯 */ }
        private void label37_Click(object sender, EventArgs e) { /* 可留空或加入日後邏輯 */ }
        private void label25_Click(object sender, EventArgs e) { /* 可留空或加入日後邏輯 */ }
    }
}
