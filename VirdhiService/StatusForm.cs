using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Vml;
using System;
using System.Windows.Forms;

namespace DeviceService
{
    public class StatusForm : Form
    {
        public event EventHandler? StopRequested;
        private readonly System.Windows.Forms.TextBox _logBox;

        public StatusForm()
        {
            Text = "VirdhiService - Debug Console";
            Width = 800;
            Height = 500;

            _logBox = new System.Windows.Forms.TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new System.Drawing.Font("Consolas", 9)
            };

            var stopButton = new Button
            {
                Text = "Stop Service",
                Dock = DockStyle.Bottom,
                Height = 35
            };
            stopButton.Click += (_, _) =>
            {
                AppendLog("Stop requested by user...");
                StopRequested?.Invoke(this, EventArgs.Empty);
            };

            Controls.Add(_logBox);
            Controls.Add(stopButton);
        }

        public void AppendLog(string message)
        {
            if (_logBox.IsDisposed) return;

            if (_logBox.InvokeRequired)
            {
                _logBox.BeginInvoke(new Action(() => AppendLogInternal(message)));
            }
            else
            {
                AppendLogInternal(message);
            }
        }

        private void AppendLogInternal(string message)
        {
            _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
    }
}