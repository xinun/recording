using System.Diagnostics;
using System.Text.RegularExpressions;
using NAudio.CoreAudioApi;

namespace MeetRecorder;

internal sealed class MainForm : Form
{
    private readonly ComboBox _speakerBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _microphoneBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _titleBox = new() { PlaceholderText = "예: ERP 일일회의" };
    private readonly TextBox _folderBox = new() { ReadOnly = true };
    private readonly Button _browseButton = new() { Text = "찾아보기" };
    private readonly Button _refreshButton = new() { Text = "장치 새로고침" };
    private readonly Button _recordButton = new() { Text = "녹음 시작" };
    private readonly Button _openFolderButton = new() { Text = "저장 폴더 열기" };
    private readonly Label _statusLabel = new() { Text = "녹음 준비", AutoSize = true };
    private readonly Label _timerLabel = new() { Text = "00:00:00", AutoSize = true };
    private readonly ProgressBar _progressBar = new() { Visible = false, Minimum = 0, Maximum = 100 };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private readonly AudioRecorder _recorder = new();
    private MMDeviceEnumerator? _deviceEnumerator;
    private DateTime _recordingStartedAt;
    private bool _busy;

    public MainForm()
    {
        Text = "Windows 음성 녹음기";
        ClientSize = new Size(540, 475);
        MinimumSize = new Size(500, 500);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Malgun Gothic", 9F);
        BackColor = Color.FromArgb(246, 247, 249);

        BuildLayout();
        _folderBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Windows 녹음");
        _titleBox.Text = $"{DateTime.Today:yyyy-MM-dd} 회의록";
        _recordButton.Click += RecordButtonClick;
        _browseButton.Click += BrowseButtonClick;
        _openFolderButton.Click += (_, _) => OpenSaveFolder();
        _refreshButton.Click += (_, _) => LoadDevices();
        _timer.Tick += (_, _) => UpdateElapsedTime();
        FormClosing += MainFormClosing;
        Shown += (_, _) => LoadDevices();
    }

    private void BuildLayout()
    {
        var title = new Label
        {
            Text = "Windows 음성 녹음기",
            Font = new Font(Font.FontFamily, 18F, FontStyle.Bold),
            AutoSize = true,
        };
        var description = new Label
        {
            Text = "컴퓨터에서 들리는 회의 소리와 내 마이크를 하나의 MP3로 저장합니다.",
            ForeColor = Color.FromArgb(90, 98, 108),
            AutoSize = true,
        };

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 10,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.Controls.Add(title);
        content.Controls.Add(description);
        content.Controls.Add(CreateField("녹음 파일 제목", _titleBox));
        content.Controls.Add(CreateDeviceField());
        content.Controls.Add(CreateField("마이크", _microphoneBox));
        content.Controls.Add(CreateFolderField());
        content.Controls.Add(CreateStatusPanel());
        content.Controls.Add(_progressBar);
        content.Controls.Add(CreateActionPanel());
        Controls.Add(content);
    }

    private Control CreateDeviceField()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.Controls.Add(CreateField("소리가 나오는 스피커·헤드폰(화면 녹음이 안된다면 장비를 확인하시고 새로고침을 눌러주세요)", _speakerBox), 0, 0);
        _refreshButton.Margin = new Padding(8, 24, 0, 0);
        _refreshButton.AutoSize = true;
        panel.Controls.Add(_refreshButton, 1, 0);
        return panel;
    }

    private Control CreateFolderField()
    {
        var wrapper = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var field = CreateField("저장 폴더", _folderBox);
        wrapper.Controls.Add(field, 0, 0);
        _browseButton.Margin = new Padding(8, 24, 0, 0);
        _browseButton.AutoSize = true;
        wrapper.Controls.Add(_browseButton, 1, 0);
        return wrapper;
    }

    private static Control CreateField(string label, Control input)
    {
        input.Dock = DockStyle.Top;
        input.Height = 30;
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, RowCount = 2 };
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 5, 0, 4) });
        panel.Controls.Add(input);
        return panel;
    }

    private Control CreateStatusPanel()
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.White, Margin = new Padding(0, 12, 0, 8) };
        _statusLabel.Location = new Point(14, 19);
        _timerLabel.Font = new Font("Consolas", 17F, FontStyle.Bold);
        _timerLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _timerLabel.Location = new Point(390, 13);
        panel.Resize += (_, _) => _timerLabel.Left = panel.ClientSize.Width - _timerLabel.Width - 14;
        panel.Controls.Add(_statusLabel);
        panel.Controls.Add(_timerLabel);
        return panel;
    }

    private Control CreateActionPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
        };
        _recordButton.AutoSize = true;
        _recordButton.MinimumSize = new Size(115, 38);
        _recordButton.BackColor = Color.FromArgb(20, 24, 31);
        _recordButton.ForeColor = Color.White;
        _recordButton.FlatStyle = FlatStyle.Flat;
        _openFolderButton.AutoSize = true;
        _openFolderButton.MinimumSize = new Size(105, 38);
        panel.Controls.Add(_recordButton);
        panel.Controls.Add(_openFolderButton);
        return panel;
    }

    private void LoadDevices()
    {
        if (_recorder.IsRecording || _busy) return;

        try
        {
            _deviceEnumerator?.Dispose();
            _deviceEnumerator = new MMDeviceEnumerator();
            var speakers = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Select(device => new DeviceItem(device)).ToArray();
            var microphones = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .Select(device => new DeviceItem(device)).ToArray();

            _speakerBox.Items.Clear();
            _microphoneBox.Items.Clear();
            _speakerBox.Items.AddRange(speakers);
            _microphoneBox.Items.Add("사용 안 함 (컴퓨터 소리만 녹음)");
            _microphoneBox.Items.AddRange(microphones);
            SelectDefault(_speakerBox, TryGetDefaultDeviceId(_deviceEnumerator, DataFlow.Render, Role.Multimedia, Role.Console));
            SelectDefault(_microphoneBox, TryGetDefaultDeviceId(_deviceEnumerator, DataFlow.Capture, Role.Communications, Role.Multimedia, Role.Console));
            _statusLabel.Text = speakers.Length > 0 ? "녹음 준비" : "사용 가능한 출력 장치를 확인하세요.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"오디오 장치를 불러오지 못했습니다.\n\n{exception.Message}", "장치 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string? TryGetDefaultDeviceId(MMDeviceEnumerator enumerator, DataFlow flow, params Role[] roles)
    {
        foreach (var role in roles)
        {
            try
            {
                using var device = enumerator.GetDefaultAudioEndpoint(flow, role);
                return device.ID;
            }
            catch
            {
                // 일부 PC에는 기본 통신 장치가 지정되어 있지 않을 수 있다.
            }
        }
        return null;
    }

    private static void SelectDefault(ComboBox box, string? deviceId)
    {
        if (deviceId is not null)
        {
            for (var index = 0; index < box.Items.Count; index++)
            {
                if (box.Items[index] is DeviceItem item && item.Device.ID == deviceId)
                {
                    box.SelectedIndex = index;
                    return;
                }
            }
        }
        if (box.Items.Count > 0) box.SelectedIndex = 0;
    }

    private async void RecordButtonClick(object? sender, EventArgs e)
    {
        if (_busy) return;
        if (_recorder.IsRecording)
            await StopRecordingAsync();
        else
            StartRecording();
    }

    private void StartRecording()
    {
        if (_speakerBox.SelectedItem is not DeviceItem speaker)
        {
            MessageBox.Show(this, "회의 소리가 나오는 스피커 또는 헤드폰을 선택해주세요.", "장치 선택", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var microphone = _microphoneBox.SelectedItem as DeviceItem;

        try
        {
            Directory.CreateDirectory(_folderBox.Text);
            _recorder.Start(speaker.Device, microphone?.Device, _folderBox.Text);
            _recordingStartedAt = DateTime.Now;
            _timer.Start();
            SetInputsEnabled(false);
            _statusLabel.Text = "● 녹음 중";
            _statusLabel.ForeColor = Color.FromArgb(190, 30, 45);
            _recordButton.Text = "녹음 종료";
            _recordButton.BackColor = Color.FromArgb(190, 30, 45);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"녹음을 시작하지 못했습니다.\n\n{exception.Message}", "녹음 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task StopRecordingAsync()
    {
        _busy = true;
        _timer.Stop();
        _recordButton.Enabled = false;
        _statusLabel.Text = "MP3 저장 중…";
        _statusLabel.ForeColor = Color.FromArgb(45, 55, 70);
        _progressBar.Visible = true;
        _progressBar.Value = 0;

        var outputPath = BuildOutputPath();
        var progress = new Progress<int>(value => _progressBar.Value = Math.Clamp(value, 0, 100));
        try
        {
            await _recorder.StopAndSaveAsync(outputPath, progress);
            _statusLabel.Text = "저장 완료";
            MessageBox.Show(this, $"MP3 파일을 저장했습니다.\n\n{outputPath}", "저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenSaveFolder();
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "저장 실패";
            MessageBox.Show(this, $"MP3 저장 중 오류가 발생했습니다.\n\n{exception.Message}", "저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
            _progressBar.Visible = false;
            _recordButton.Enabled = true;
            _recordButton.Text = "녹음 시작";
            _recordButton.BackColor = Color.FromArgb(20, 24, 31);
            _timerLabel.Text = "00:00:00";
            SetInputsEnabled(true);
        }
    }

    private string BuildOutputPath()
    {
        var title = string.IsNullOrWhiteSpace(_titleBox.Text) ? "회의" : _titleBox.Text.Trim();
        title = Regex.Replace(title, $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", "_");
        var defaultTitle = $"{_recordingStartedAt:yyyy-MM-dd} 회의록";
        var baseName = title == defaultTitle
            ? $"{_recordingStartedAt:yyyy-MM-dd_HHmm}_회의록"
            : $"{_recordingStartedAt:yyyy-MM-dd_HHmm}_{title}";
        var path = Path.Combine(_folderBox.Text, baseName + ".mp3");
        var suffix = 2;
        while (File.Exists(path)) path = Path.Combine(_folderBox.Text, $"{baseName}_{suffix++}.mp3");
        return path;
    }

    private void BrowseButtonClick(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = _folderBox.Text, ShowNewFolderButton = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) _folderBox.Text = dialog.SelectedPath;
    }

    private void OpenSaveFolder()
    {
        Directory.CreateDirectory(_folderBox.Text);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_folderBox.Text}\"") { UseShellExecute = true });
    }

    private void UpdateElapsedTime()
    {
        var elapsed = DateTime.Now - _recordingStartedAt;
        _timerLabel.Text = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    private void SetInputsEnabled(bool enabled)
    {
        _speakerBox.Enabled = enabled;
        _microphoneBox.Enabled = enabled;
        _titleBox.Enabled = enabled;
        _browseButton.Enabled = enabled;
        _refreshButton.Enabled = enabled;
    }

    private async void MainFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_recorder.IsRecording && !_busy)
        {
            _deviceEnumerator?.Dispose();
            _recorder.Dispose();
            return;
        }

        e.Cancel = true;
        if (_busy) return;
        var result = MessageBox.Show(this, "녹음 중입니다. 저장하지 않고 종료할까요?", "종료 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;
        _busy = true;
        await _recorder.CancelAsync();
        _recorder.Dispose();
        _deviceEnumerator?.Dispose();
        FormClosing -= MainFormClosing;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _recorder.Dispose();
            _deviceEnumerator?.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed record DeviceItem(MMDevice Device)
    {
        public override string ToString() => Device.FriendlyName;
    }
}
