using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Ports;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace QnEvt.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {



        private const string DefaultPushEndpoint = "http://localhost:3000/api/push-random";

        [ObservableProperty]
        private string _serverUrl = DefaultPushEndpoint;

        [ObservableProperty]
        private string _pushKey = "";

        [ObservableProperty]
        private string? _selectedPort;

        [ObservableProperty]
        private int _selectedBaudRate = 2000000;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedTotalBytes))]
        [NotifyPropertyChangedFor(nameof(DynamicLblBytes))]
        private long _totalBytesUploaded;

        public string FormattedTotalBytes
        {
            get
            {
                if (TotalBytesUploaded >= 1073741824) return (TotalBytesUploaded / 1073741824.0).ToString("F2");
                if (TotalBytesUploaded >= 1048576) return (TotalBytesUploaded / 1048576.0).ToString("F2");
                if (TotalBytesUploaded >= 1024) return (TotalBytesUploaded / 1024.0).ToString("F2");
                return TotalBytesUploaded.ToString("N0");
            }
        }

        public string DynamicLblBytes
        {
            get
            {
                if (TotalBytesUploaded >= 1073741824) return "GB";
                if (TotalBytesUploaded >= 1048576) return "MB";
                if (TotalBytesUploaded >= 1024) return "KB";
                return IsChinese ? "字节" : "Bytes";
            }
        }

        [ObservableProperty]
        private double _currentThroughputKbps;

        [ObservableProperty]
        private string _terminalLogs = "";

        [ObservableProperty]
        private bool _isPushKeyVisible;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsResumeButtonVisible))]
        private bool _isFipsAlarm = false;

        [ObservableProperty]
        private string _fipsAlarmMessage = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsResumeButtonVisible))]
        private bool _isUploadPausedByError = false;

        public bool IsResumeButtonVisible => IsUploadPausedByError || IsFipsAlarm;

        [ObservableProperty]
        private bool _isKickedOffline = false;

        [ObservableProperty]
        private bool _isObservingHealth = false;

        private DateTime _lastKickTime = DateTime.MinValue;
        private CancellationTokenSource? _reconnectCts;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
        [NotifyPropertyChangedFor(nameof(LblKickedDesc))]
        private int _stabilityCountdownSeconds = 0;

        private readonly string _clientUniqueId = Guid.NewGuid().ToString("N");
        public string ClientUniqueId => _clientUniqueId;
        private string? _resolvedPushEndpoint;
        private DateTime _lastCloudPushLogAt = DateTime.MinValue;
        private long _bytesSinceLastCloudPushLog;
        private static readonly string SettingsDirectory = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QnEvt");
        private static readonly string SettingsFilePath = System.IO.Path.Combine(SettingsDirectory, "client-settings.json");
        private static DateTime _lastLogCleanupDate = DateTime.MinValue;

        public ObservableCollection<string> AvailablePorts { get; } = new();
        public ObservableCollection<int> AvailableBaudRates { get; } = new()
        {
            9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600, 1000000, 2000000
        };




        [ObservableProperty]
        private bool _isChinese = false;

        [ObservableProperty]
        private bool _isLightMode = false;

        private enum ConnectionState
        {
            Disconnected,
            Connecting,
            LinkActive,
            QqmWarning,
            QqmError,
            LinkReconnecting,
            Disconnecting,
            Kicked,
            NetworkError
        }

        private enum ParserState
        {
            FindSync,
            ReadType,
            ReadLength,
            ReadPayload,
            ReadCrcLo,
            ReadCrcHi
        }

        private ConnectionState _currentState = ConnectionState.Disconnected;


        public string StatusColor => _currentState switch
        {
            ConnectionState.Disconnected => "#FF1744",
            ConnectionState.Connecting => "#FFD600",
            ConnectionState.LinkActive => "#00E676",
            ConnectionState.QqmWarning => "#FFD600",
            ConnectionState.QqmError => "#FF1744",
            ConnectionState.LinkReconnecting => "#FFD600",
            ConnectionState.Disconnecting => "#FF6D00",
            ConnectionState.Kicked => "#FF1744",
            ConnectionState.NetworkError => "#FF9100",
            _ => "#FF1744"
        };


        public string ConnectionStatusText => _currentState switch
        {
            ConnectionState.Disconnected => IsChinese ? "连接已断开" : "DISCONNECTED",
            ConnectionState.Connecting => IsChinese ? "正在建立链路..." : "CONNECTING",
            ConnectionState.LinkActive => IsChinese ? "安全数据链路激活 (QQM OK)" : "LINK ACTIVE (QQM OK)",
            ConnectionState.QqmWarning => IsChinese ? "数据链路异常警告 (QQM WARN)" : "LINK WARNING (QQM WARN)",
            ConnectionState.QqmError => IsChinese ? "硬件故障/异常暂停 (QQM ERROR)" : "HARDWARE ERROR (QQM ERROR)",
            ConnectionState.LinkReconnecting => IsChinese ? "链路断开自动重连中..." : "LINK RECONNECTING",
            ConnectionState.Disconnecting => IsChinese ? "正在安全释放链路..." : "DISCONNECTING",
            ConnectionState.Kicked => StabilityCountdownSeconds > 0
                ? (IsChinese ? $"⚠️ 密钥冲突：冷却等待中 ({StabilityCountdownSeconds}s)" : $"⚠️ KEY CONFLICT: COOLDOWN ({StabilityCountdownSeconds}s)")
                : (IsChinese ? "⚠️ 密钥冲突：设备已被踢下线 (KICKED OFFLINE)" : "⚠️ KEY CONFLICT: KICKED OFFLINE"),
            ConnectionState.NetworkError => IsChinese ? "⚠️ 云端上报服务器连接失败 (NETWORK ERROR)" : "⚠️ UPLOAD CONNECTION FAILS (NETWORK ERROR)",
            _ => "DISCONNECTED"
        };




        public string ThemeBg => IsLightMode ? "#F3F4F6" : "#08080C";
        public string ThemePanelBg => IsLightMode ? "#FFFFFF" : "#0C0C14";
        public string ThemeBorderBrush => IsLightMode ? "#D1D5DB" : "#1A1A2E";
        public string ThemeTextPrimary => IsLightMode ? "#111827" : "#FFFFFF";
        public string ThemeTextSecondary => IsLightMode ? "#4B5563" : "#8F9FB3";
        public string ThemeSubtleBg => IsLightMode ? "#E5E7EB" : "#0F0F1A";
        public string ThemeTerminalBg => IsLightMode ? "#E5E7EB" : "#030305";
        public string ThemeTerminalText => IsLightMode ? "#0F172A" : "#A5F3FC";
        public string ThemeLogoStroke => IsLightMode ? "#0891B2" : "#00E5FF";




        public string LblTitle => "QnEvt";
        public string LblSubtitle => IsChinese ? "量子节点熵验证与传输协议" : "QUANTUM NODE ENTROPY VERIFICATION & TRANSFER PROTOCOL";
        public string LblControlDeck => IsChinese ? "控制面板" : "CONTROL DECK";
        public string LblControlDeckSub => IsChinese ? "配置硬件接口和云端上报参数" : "Configure hardware links and cloud parameters";
        public string LblPortSelector => IsChinese ? "串口连接选择" : "SERIAL PORT SELECTOR";
        public string LblBaudRate => IsChinese ? "波特率工作频率" : "BAUD RATE FREQUENCY";
        public string LblCloudServer => IsChinese ? "云端数据上报服务器" : "CLOUD AGGREGATION SERVER";
        public string LblPushKey => IsChinese ? "推送授权密钥 (BEARER TOKEN)" : "PUSH AUTH KEY (BEARER TOKEN)";
        public string LblReveal => IsChinese ? "显示密钥" : "Reveal";
        public string LblConnectTrigger => IsConnected
            ? (IsChinese ? "终止安全链路" : "TERMINATE SECURE LINK")
            : (IsChinese ? "激活数据链路" : "ACTIVATE TELEMETRY LINK");
        public string LblQuantumMetrics => IsChinese ? "数据量统计" : "QUANTUM METRICS";
        public string LblMetricsSub => IsChinese ? "实时数据上报计数器与速率统计" : "Real-time transmission counters and shifted bytes";
        public string LblTotalEntropy => IsChinese ? "累计上报熵数据" : "TOTAL ENTROPY SHIFTED";
        public string LblThroughput => IsChinese ? "实时数据上传速率" : "TELEMETRY STREAM THROUGHPUT";
        public string LblEngineState => IsChinese ? "硬件引擎运行状态" : "HARDWARE ENGINE STATE";
        public string LblLinkAnalyzer => IsChinese ? "数据链路稳定性分析" : "ACTIVE LINK ANALYZER";
        public string LblAnalyzerSub => IsChinese ? "信号脉冲与通道稳定性检测" : "Signal pulse and stream stability scanner";
        public string LblTerminalTitle => IsChinese ? "硬件遥测诊断矩阵" : "DIAGNOSTIC TELEMETRY MATRIX";
        public string LblTerminalBadge => IsChinese ? "在线诊断终端" : "ONLINE MATRIX TERMINAL";
        public string LblClearDiagnostics => IsChinese ? "✖ 清除诊断日志" : "✖ Clear Diagnostics";
        public string LblBytes => IsChinese ? "字节" : "Bytes";
        public string LblKbps => IsChinese ? "Kbps" : "Kbps";
        public string LblLanguageToggle => IsChinese ? "中文" : "EN";
        public string LblThemeToggle => IsLightMode ? (IsChinese ? "浅色" : "LIGHT") : (IsChinese ? "深色" : "DARK");
        public string ThemeToggleIcon => IsLightMode ? "☀" : "🌙";
        public string LblResumeUpload => IsChinese ? "恢复云端上报" : "RESUME CLOUD UPLOADS";
        public string LblKickedTitle => IsChinese ? "推送密钥冲突已下线" : "PUSH KEY CONFLICT OFFLINE";
        public string LblKickedDesc => StabilityCountdownSeconds > 0
            ? (IsChinese
                ? $"检测到重复被踢下线。为确保数据通道稳定，系统正在进行冷却等待，{StabilityCountdownSeconds} 秒后将重新自动上线上报数据..."
                : $"Repeated kick-offs detected. To ensure stability, the system is performing a cooldown wait, automatically reconnecting in {StabilityCountdownSeconds}s...")
            : (IsChinese
                ? "该推送密钥已被另一台设备抢占绑定。当前设备已被云端强制踢下线，重连已禁用！若要重试，请检查密钥或手动重新启动链路。"
                : "This push key is active on another device. Current device has been kicked offline by the server. Reconnection is disabled! Please check your key or reconnect manually.");

        public string LblRebootHardware => IsChinese ? "重启 Pico 2 硬件" : "REBOOT PICO 2 HARDWARE";
        public string LblEnterBootsel => IsChinese ? "💾 进入固件升级模式 (U盘)" : "💾 ENTER BOOTSEL MODE (U-DISK)";




        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        private Channel<byte>? _dataChannel;
        private CancellationTokenSource? _cts;
        private Task? _serialTask;
        private Task? _uploaderTask;
        private Task? _metricsTask;

        private long _bytesUploadedInCurrentSecond = 0;
        private readonly System.Collections.Generic.Queue<long> _bytesUploadedWindow = new();
        private SerialPort? _serialPort;
        private const int UploadBatchSize = 960;
        private byte[]? _lastUploadedBlock = null;
        private readonly object _telemetryLock = new object();
        private object? _latestTelemetry = null;
        private long _uploadSequence = 0;

        public MainWindowViewModel()
        {
            RefreshPorts();
            LoadSettings();
            CleanupPreviousWeekLogs();
            Log("QnEvt Telemetry Portal initialized successfully.");
            Log("Engine ready. System operating in default dark mode (English).");
        }




        partial void OnIsChineseChanged(bool value)
        {
            TriggerLocalizationPropertiesChanged();
            Log(value ? "系统语言已切换至：简体中文" : "System language switched to: English");
        }

        partial void OnIsLightModeChanged(bool value)
        {
            TriggerThemePropertiesChanged();
            Log(value ? "Visual interface morphed to: Light Mode" : "Visual interface morphed to: Dark Mode");
        }

        private void TriggerLocalizationPropertiesChanged()
        {
            OnPropertyChanged(nameof(LblTitle));
            OnPropertyChanged(nameof(LblSubtitle));
            OnPropertyChanged(nameof(LblControlDeck));
            OnPropertyChanged(nameof(LblControlDeckSub));
            OnPropertyChanged(nameof(LblPortSelector));
            OnPropertyChanged(nameof(LblBaudRate));
            OnPropertyChanged(nameof(LblCloudServer));
            OnPropertyChanged(nameof(LblPushKey));
            OnPropertyChanged(nameof(LblReveal));
            OnPropertyChanged(nameof(LblConnectTrigger));
            OnPropertyChanged(nameof(LblQuantumMetrics));
            OnPropertyChanged(nameof(LblMetricsSub));
            OnPropertyChanged(nameof(LblTotalEntropy));
            OnPropertyChanged(nameof(LblThroughput));
            OnPropertyChanged(nameof(LblEngineState));
            OnPropertyChanged(nameof(LblLinkAnalyzer));
            OnPropertyChanged(nameof(LblAnalyzerSub));
            OnPropertyChanged(nameof(LblTerminalTitle));
            OnPropertyChanged(nameof(LblTerminalBadge));
            OnPropertyChanged(nameof(LblClearDiagnostics));
            OnPropertyChanged(nameof(LblBytes));
            OnPropertyChanged(nameof(LblKbps));
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(LblLanguageToggle));
            OnPropertyChanged(nameof(LblThemeToggle));
            OnPropertyChanged(nameof(LblResumeUpload));
            OnPropertyChanged(nameof(LblKickedTitle));
            OnPropertyChanged(nameof(LblKickedDesc));
            OnPropertyChanged(nameof(LblRebootHardware));
            OnPropertyChanged(nameof(LblEnterBootsel));
        }

        private void TriggerThemePropertiesChanged()
        {
            OnPropertyChanged(nameof(ThemeBg));
            OnPropertyChanged(nameof(ThemePanelBg));
            OnPropertyChanged(nameof(ThemeBorderBrush));
            OnPropertyChanged(nameof(ThemeTextPrimary));
            OnPropertyChanged(nameof(ThemeTextSecondary));
            OnPropertyChanged(nameof(ThemeSubtleBg));
            OnPropertyChanged(nameof(ThemeTerminalBg));
            OnPropertyChanged(nameof(ThemeTerminalText));
            OnPropertyChanged(nameof(ThemeLogoStroke));
            OnPropertyChanged(nameof(LblThemeToggle));
            OnPropertyChanged(nameof(ThemeToggleIcon));
        }




        [RelayCommand]
        private void RefreshPorts()
        {
            AvailablePorts.Clear();
            AvailablePorts.Add("AUTO");

            try
            {
                var ports = SerialPort.GetPortNames();
                foreach (var port in ports)
                {
                    AvailablePorts.Add(port);
                }

                if (ports.Length > 0)
                {
                    SelectedPort = "AUTO";
                }
                else
                {
                    SelectedPort = "AUTO";
                    Log(IsChinese ? "警告：未检测到物理串口设备。自动连接开启中。" : "Warning: No hardware COM ports detected. Auto-detect active.");
                }
            }
            catch (Exception ex)
            {
                SelectedPort = "AUTO";
                Log($"{(IsChinese ? "扫描串口错误" : "Error scanning serial ports")}: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ToggleConnectionAsync()
        {
            if (IsConnected)
            {
                await DisconnectAsync();
            }
            else
            {
                await ConnectAsync();
            }
        }

        [RelayCommand]
        private void ResumeUpload()
        {
            PrepareHealthCheckedRecovery();

            if (_currentState == ConnectionState.QqmError || _currentState == ConnectionState.NetworkError)
            {
                _currentState = ConnectionState.LinkActive;
                OnPropertyChanged(nameof(ConnectionStatusText));
                OnPropertyChanged(nameof(StatusColor));
            }

            Log(IsChinese ? "[系统] 用户手动清除了异常锁定，恢复云端上报。" : "[System] User manually cleared error lock, resuming cloud uploads.");
            WriteLogToFile("INFO", "User manually cleared error lock, resuming cloud uploads.");
        }

        private void PrepareHealthCheckedRecovery()
        {
            IsKickedOffline = false;
            IsObservingHealth = true;
            IsUploadPausedByError = false;
            IsFipsAlarm = false;
            FipsAlarmMessage = "";
            _lastUploadedBlock = null;
        }

        [RelayCommand]
        private void ClearLogs()
        {
            TerminalLogs = "";
            Log(IsChinese ? "诊断终端已清空。" : "Terminal cleared.");
        }

        [RelayCommand]
        private async Task RebootPico2Async()
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                Log(IsChinese ? "[拒绝] 无法重启 Pico 2。串口未连接或未开启！" : "[Error] Cannot reboot Pico 2. Serial port is not connected or open!");
                return;
            }

            try
            {
                // Send reboot command packet: 0xAA (Sync), 0x88 (Type), 0x01 (Length), 0x00 (Soft Reset)
                byte[] cmd = new byte[] { 0xAA, 0x88, 0x01, 0x00 };
                _serialPort.Write(cmd, 0, cmd.Length);
                Log(IsChinese ? "[重启] ☁️ 已向 Pico 2 发送硬件重启指令..." : "[Reboot] ☁️ Sent hardware reboot command to Pico 2...");
                WriteLogToFile("REBOOT", "Sent hardware reboot command to Pico 2.");
            }
            catch (Exception ex)
            {
                Log($"{(IsChinese ? "发送重启指令失败" : "Failed to send reboot command")}: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task EnterBootselPico2Async()
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                Log(IsChinese ? "[拒绝] 无法切换模式。串口未连接或未开启！" : "[Error] Cannot switch mode. Serial port is not connected or open!");
                return;
            }

            try
            {
                // Send BOOTSEL command packet: 0xAA (Sync), 0x88 (Type), 0x01 (Length), 0x01 (BOOTSEL Reset)
                byte[] cmd = new byte[] { 0xAA, 0x88, 0x01, 0x01 };
                _serialPort.Write(cmd, 0, cmd.Length);
                Log(IsChinese ? "[升级] 💾 已向 Pico 2 发送固件升级指令，设备将重启进入 U 盘模式..." : "[Upgrade] 💾 Sent BOOTSEL command. Pico 2 will reboot into U-disk mode...");
                WriteLogToFile("BOOTSEL", "Sent BOOTSEL firmware upgrade command to Pico 2.");
            }
            catch (Exception ex)
            {
                Log($"{(IsChinese ? "发送升级指令失败" : "Failed to send BOOTSEL command")}: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ToggleLanguage()
        {
            IsChinese = !IsChinese;
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            IsLightMode = !IsLightMode;
        }

        // ─────────────────────────────────────────────
        // Connection Handlers
        // ─────────────────────────────────────────────
        private async Task ConnectAsync()
        {
            if (string.IsNullOrWhiteSpace(PushKey))
            {
                Log(IsChinese ? "[拒绝] 链路连接失败。上报 API 推送密钥为空！" : "[Error] Telemetry connection denied. API Push Key is empty!");
                return;
            }

            try
            {
                _resolvedPushEndpoint = NormalizePushEndpoint(ServerUrl);
                SaveSettings();
                Log(IsChinese
                    ? $"[系统] 云端上报地址已确认：{_resolvedPushEndpoint}"
                    : $"[System] Cloud push endpoint ready: {_resolvedPushEndpoint}");
            }
            catch (Exception ex)
            {
                _resolvedPushEndpoint = null;
                Log(IsChinese
                    ? $"[拒绝] 云端服务器地址无效：{ex.Message}"
                    : $"[Error] Invalid cloud server URL: {ex.Message}");
                WriteLogToFile("CONFIG_ERROR", $"Invalid cloud server URL: {ex.Message}");
                return;
            }

            // Cancel any pending auto-reconnection task
            _reconnectCts?.Cancel();
            _reconnectCts = null;
            StabilityCountdownSeconds = 0;

            PrepareHealthCheckedRecovery();

            Log(IsChinese ? "[系统] 正在建立安全数据遥测链路..." : "[System] Establishing secure telemetry link...");

            IsConnected = true;
            _currentState = ConnectionState.Connecting;

            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(LblConnectTrigger));

            TotalBytesUploaded = 0;
            CurrentThroughputKbps = 0;
            _bytesUploadedInCurrentSecond = 0;
            _bytesSinceLastCloudPushLog = 0;
            _lastCloudPushLogAt = DateTime.MinValue;

            _cts = new CancellationTokenSource();

            // Create thread-safe bounded channel (1MB buffer) for high-throughput 2Mbps entropy stream.
            // Drops the oldest bytes when full to prioritize sending fresh real-time entropy.
            _dataChannel = Channel.CreateBounded<byte>(new BoundedChannelOptions(1048576)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });

            // Start async uploader and reader tasks
            var token = _cts.Token;
            _uploaderTask = Task.Run(() => HttpUploaderWorkerAsync(token), token);
            _serialTask = Task.Run(() => SerialReaderLoopAsync(token), token);
            _metricsTask = Task.Run(() => MetricsTrackerLoopAsync(token), token);
        }

        private async Task DisconnectAsync()
        {
            Log(IsChinese ? "[系统] 正在终止安全数据链路，卸载数据通道中..." : "[System] Terminating secure link, cleaning up streams...");

            // Cancel any pending auto-reconnection task and clear cooldown state
            _reconnectCts?.Cancel();
            _reconnectCts = null;
            StabilityCountdownSeconds = 0;
            _lastKickTime = DateTime.MinValue;
            _resolvedPushEndpoint = null;

            _currentState = ConnectionState.Disconnecting;
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(StatusColor));

            if (_cts != null)
            {
                _cts.Cancel();

                try
                {
                    // Wait for background threads to complete gracefully with a timeout
                    var cleanupTask = Task.WhenAll(
                        _serialTask ?? Task.CompletedTask,
                        _uploaderTask ?? Task.CompletedTask,
                        _metricsTask ?? Task.CompletedTask
                    );
                    await Task.WhenAny(cleanupTask, Task.Delay(2000));
                }
                catch (Exception ex)
                {
                    Log($"[Clean Up] Intercepted thread exit: {ex.Message}");
                }

                _cts.Dispose();
                _cts = null;
            }

            CloseSerialPort();

            IsConnected = false;
            _currentState = ConnectionState.Disconnected;

            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(LblConnectTrigger));

            CurrentThroughputKbps = 0;
            _dataChannel = null;

            Log(IsChinese ? "[系统] 数据连接已完全离线。" : "[System] Secure link offline.");
        }

        private async Task HandleKickedAsync()
        {
            if (_currentState == ConnectionState.Kicked || _currentState == ConnectionState.Disconnecting)
            {
                return; // Already handling kick/cooldown. Prevent duplicate triggers from interrupting active timer.
            }

            Log(IsChinese
                ? "⚠️ [冲突互踢] 检测到此推送密钥已在另一台设备上绑定激活！当前设备已被强制下线。"
                : "⚠️ [Key Preempted] This push key is active on another device! Current device has been kicked offline.");

            WriteLogToFile("MUTUAL_KICK", "Kicked offline due to push key preemption.");

            _currentState = ConnectionState.Disconnecting;
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(StatusColor));

            if (_cts != null)
            {
                _cts.Cancel();

                try
                {
                    var cleanupTask = Task.WhenAll(
                        _serialTask ?? Task.CompletedTask,
                        _uploaderTask ?? Task.CompletedTask,
                        _metricsTask ?? Task.CompletedTask
                    );
                    await Task.WhenAny(cleanupTask, Task.Delay(2000));
                }
                catch (Exception ex)
                {
                    Log($"[Clean Up] Intercepted thread exit: {ex.Message}");
                }

                _cts.Dispose();
                _cts = null;
            }

            CloseSerialPort();

            IsConnected = false;
            _currentState = ConnectionState.Kicked;
            IsKickedOffline = true;
            IsObservingHealth = false;

            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(LblConnectTrigger));

            CurrentThroughputKbps = 0;
            _dataChannel = null;


            var now = DateTime.UtcNow;
            bool isRepeated = _lastKickTime != DateTime.MinValue && (now - _lastKickTime) <= TimeSpan.FromMinutes(5);
            _lastKickTime = now;

            _reconnectCts?.Cancel();
            _reconnectCts = new CancellationTokenSource();

            if (isRepeated)
            {
                StabilityCountdownSeconds = 180;
                Log(IsChinese
                    ? "⚠️ [稳定冷却] 检测到重复被踢下线！为保证链路稳定，系统将冷却等待 3 分钟后重新自动尝试上线..."
                    : "⚠️ [Stability Cooldown] Repeated kick-offs detected! Waiting 3 minutes before automatic reconnection...");
            }
            else
            {
                StabilityCountdownSeconds = 5;
                Log(IsChinese
                    ? "⚠️ [自动重连] 系统将在 5 秒后自动尝试重新上线上报数据..."
                    : "⚠️ [Auto-Reconnect] System will automatically reconnect in 5 seconds...");
            }

            var reconnectToken = _reconnectCts.Token;
            _ = Task.Run(() => AutoReconnectWorkerAsync(reconnectToken), reconnectToken);
        }

        private async Task AutoReconnectWorkerAsync(CancellationToken token)
        {
            try
            {
                while (StabilityCountdownSeconds > 0)
                {
                    await Task.Delay(1000, token);
                    StabilityCountdownSeconds--;
                }


                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (token.IsCancellationRequested) return;
                    Log(IsChinese
                        ? "[自动重连] 冷却结束，正在自动激活数据链路重新上报数据..."
                        : "[Auto-Reconnect] Cooldown finished, automatically activating telemetry link and resuming data report...");
                    PrepareHealthCheckedRecovery();
                    await ConnectAsync();
                });
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                Log($"[Auto-Reconnect Error] {ex.Message}");
            }
        }




        private static ushort Crc16Ccitt(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;
            foreach (byte value in data)
            {
                crc ^= (ushort)(value << 8);
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x8000) != 0
                        ? (ushort)((crc << 1) ^ 0x1021)
                        : (ushort)(crc << 1);
                }
            }
            return crc;
        }

        private async Task SerialReaderLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    string targetPort = SelectedPort ?? "AUTO";
                    if (targetPort.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
                    {
                        var ports = SerialPort.GetPortNames();
                        if (ports.Length > 0)
                        {
                            targetPort = ports[0];
                            Log(IsChinese ? $"[串口] 自动搜寻到硬件端口: {targetPort}" : $"[Serial] Auto-detected hardware port: {targetPort}");
                        }
                        else
                        {
                            Log(IsChinese ? "[串口等待] 正在轮询等待虚拟 COM 端口接入... 2秒后重试" : "[Serial Wait] Scanning for virtual COM port... Retrying in 2s.");
                            await Task.Delay(2000, cancellationToken);
                            continue;
                        }
                    }

                    Log(IsChinese ? $"[串口] 正在以 {SelectedBaudRate} 波特率初始化 {targetPort}..." : $"[Serial] Initializing {targetPort} at {SelectedBaudRate} Baud...");
                    _serialPort = new SerialPort(targetPort, SelectedBaudRate)
                    {
                        ReadTimeout  = SerialPort.InfiniteTimeout,
                        WriteTimeout = 5000,
                        DtrEnable    = true,
                        RtsEnable    = true,

                        ReadBufferSize  = 262144,
                        WriteBufferSize = 16384,

                        ReceivedBytesThreshold = 1
                    };

                    _serialPort.Open();


                    _currentState = ConnectionState.LinkActive;
                    OnPropertyChanged(nameof(ConnectionStatusText));
                    OnPropertyChanged(nameof(StatusColor));

                    Log(IsChinese ? $"[串口] 量子随机数链路激活于端口 {targetPort}!" : $"[Serial] Quantum Link active on port {targetPort}!");




                    byte[] buffer = new byte[65536];
                    ParserState parserState = ParserState.FindSync;
                    byte pktType = 0;
                    byte pktLen = 0;
                    byte[] payloadBuf = new byte[256];
                    byte[] crcBuf = new byte[257];
                    int payloadBytesRead = 0;
                    ushort pktCrcReceived = 0;
                    bool dataFlowDetected = false;
                    var dataReadySignal = new SemaphoreSlim(0, 1);
                    DateTime lastDataReceivedTime = DateTime.UtcNow;


                    SerialDataReceivedEventHandler onDataReceived = (s, e) =>
                    {
                        lastDataReceivedTime = DateTime.UtcNow;
                        if (dataReadySignal.CurrentCount == 0)
                            dataReadySignal.Release();
                    };
                    _serialPort.DataReceived += onDataReceived;

                    try
                    {
                        while (!cancellationToken.IsCancellationRequested && _serialPort.IsOpen)
                        {

                            await dataReadySignal.WaitAsync(500, cancellationToken);

                            if (dataFlowDetected && (DateTime.UtcNow - lastDataReceivedTime).TotalSeconds > 2)
                            {
                                throw new TimeoutException(IsChinese ? "串口数据流停滞超过2秒" : "Serial stream stalled for >2 seconds");
                            }

                            while (_serialPort.IsOpen && _serialPort.BytesToRead > 0)
                            {
                                int bytesRead = _serialPort.Read(buffer, 0, Math.Min(buffer.Length, _serialPort.BytesToRead));
                                if (bytesRead <= 0) break;

                                if (!dataFlowDetected)
                                {
                                    dataFlowDetected = true;
                                    Log(IsChinese ? "[串口] ★ 成功检测到物理数据流，解析器启动 ★" : "[Serial] ★ Physical data flow detected, parser engaging ★");
                                }


                                var writer = _dataChannel?.Writer;
                                for (int i = 0; i < bytesRead; i++)
                                {
                                    byte b = buffer[i];
                                    switch (parserState)
                                    {
                                        case ParserState.FindSync:
                                            if (b == 0xAA)
                                                parserState = ParserState.ReadType;
                                            break;

                                        case ParserState.ReadType:
                                            pktType = b;
                                            parserState = ParserState.ReadLength;
                                            break;

                                        case ParserState.ReadLength:
                                            pktLen = b;

                                            bool isValidLen = pktType switch
                                            {
                                                0x01 => (pktLen == 48),
                                                0x02 => (pktLen == 1),
                                                0x03 => (pktLen == 10),
                                                0xE0 => (pktLen == 1),
                                                _ => false
                                            };

                                            if (!isValidLen)
                                            {
                                                if (pktType == 0x01 || pktType == 0x02 || pktType == 0x03 || pktType == 0xE0) { Log($"[同步校验异常] 收到类型 0x{pktType:X2}，但其长度 {pktLen} 与预期不符！"); }

                                                parserState = ParserState.FindSync;
                                            }
                                            else if (pktLen == 0)
                                            {
                                                payloadBytesRead = 0;
                                                parserState = ParserState.ReadCrcLo;
                                            }
                                            else
                                            {
                                                payloadBytesRead = 0;
                                                parserState = ParserState.ReadPayload;
                                            }
                                            break;

                                        case ParserState.ReadPayload:
                                            payloadBuf[payloadBytesRead++] = b;
                                            if (payloadBytesRead == pktLen)
                                            {
                                                parserState = ParserState.ReadCrcLo;
                                            }
                                            break;

                                        case ParserState.ReadCrcLo:
                                            pktCrcReceived = b;
                                            parserState = ParserState.ReadCrcHi;
                                            break;

                                        case ParserState.ReadCrcHi:
                                            pktCrcReceived |= (ushort)(b << 8);
                                            crcBuf[0] = pktType;
                                            crcBuf[1] = pktLen;
                                            if (pktLen > 0)
                                            {
                                                Array.Copy(payloadBuf, 0, crcBuf, 2, pktLen);
                                            }

                                            ushort pktCrcExpected = Crc16Ccitt(crcBuf.AsSpan(0, 2 + pktLen));
                                            if (pktCrcReceived == pktCrcExpected)
                                            {
                                                byte[] completePayload = pktLen == 0 ? Array.Empty<byte>() : new byte[pktLen];
                                                if (pktLen > 0)
                                                {
                                                    Array.Copy(payloadBuf, completePayload, pktLen);
                                                }
                                                await ProcessPacketAsync(pktType, pktLen, completePayload, cancellationToken);
                                            }
                                            else
                                            {
                                                Log(IsChinese
                                                    ? $"[CRC异常] 丢弃损坏帧: type=0x{pktType:X2}, len={pktLen}, got=0x{pktCrcReceived:X4}, expected=0x{pktCrcExpected:X4}"
                                                    : $"[CRC Error] Dropped corrupt frame: type=0x{pktType:X2}, len={pktLen}, got=0x{pktCrcReceived:X4}, expected=0x{pktCrcExpected:X4}");
                                            }
                                            parserState = ParserState.FindSync;
                                            break;
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        _serialPort.DataReceived -= onDataReceived;
                        dataReadySignal.Dispose();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Log($"[Serial Error] {(IsChinese ? "物理数据链路断开" : "Link severed")}: {ex.Message}");
                        WriteLogToFile("SERIAL_ERROR", $"Link severed: {ex.Message}");
                        _currentState = ConnectionState.LinkReconnecting;
                        OnPropertyChanged(nameof(ConnectionStatusText));
                        OnPropertyChanged(nameof(StatusColor));

                        CloseSerialPort();

                        Log(IsChinese ? "[串口] 正在启动硬件自愈模块，3秒后自动尝试重连..." : "[Serial] Re-engaging hardware auto-recovery in 3s...");
                        await Task.Delay(3000, cancellationToken);
                    }
                }
            }

            CloseSerialPort();
        }

        private async Task ProcessPacketAsync(byte type, byte length, byte[] payload, CancellationToken cancellationToken)
        {
            switch (type)
            {
                case 0x01:
                    if (IsUploadPausedByError && !IsObservingHealth)
                    {

                        return;
                    }

                    if (_currentState == ConnectionState.QqmError || _currentState == ConnectionState.QqmWarning || _currentState == ConnectionState.LinkReconnecting)
                    {
                        _currentState = ConnectionState.LinkActive;
                        OnPropertyChanged(nameof(ConnectionStatusText));
                        OnPropertyChanged(nameof(StatusColor));
                    }

                    var writer = _dataChannel?.Writer;
                    if (writer != null)
                    {


                        for (int i = 0; i < length; i++)
                        {
                            if (!writer.TryWrite(payload[i]))
                            {

                                await writer.WriteAsync(payload[i], cancellationToken);
                            }
                        }
                    }
                    break;

                case 0x02:
                    if (length > 0)
                    {
                        byte warnCode = payload[0];
                        string warnMsg = warnCode switch
                        {
                            0x01 => "WARN_LOW_H (Shannon entropy is low)",
                            0x02 => "WARN_BIT_BIAS (Bit bias is too large)",
                            _ => $"Unknown warning code: 0x{warnCode:X2}"
                        };

                        _currentState = ConnectionState.QqmWarning;
                        OnPropertyChanged(nameof(ConnectionStatusText));
                        OnPropertyChanged(nameof(StatusColor));

                        Log(IsChinese ? $"[警告] 硬件警报 (代码 0x{warnCode:X2}): {warnMsg}" : $"[Warning] Hardware Alert (Code 0x{warnCode:X2}): {warnMsg}");
                        WriteLogToFile("WARNING", $"Code 0x{warnCode:X2}: {warnMsg}");
                    }
                    break;

                case 0x03:
                    if (length >= 10)
                    {
                        int entropyMilli = (payload[0] << 8) | payload[1];
                        double entropy = entropyMilli / 1000.0;

                        int[] bitFreqs = new int[4];
                        for (int b = 0; b < 4; b++)
                        {
                            bitFreqs[b] = Math.Clamp((int)payload[2 + b], 0, 100);
                        }

                        int rctFails = payload[7];
                        int aptFails = payload[8];
                        int statusVal = payload[9];
                        string statusStr = statusVal switch
                        {
                            0 => "OK",
                            1 => "WARN",
                            2 => "FAIL",
                            _ => "UNKNOWN"
                        };

                        lock (_telemetryLock)
                        {
                            _latestTelemetry = new
                            {
                                entropy = entropy,
                                bitFreqs = bitFreqs,
                                rctFails = rctFails,
                                aptFails = aptFails,
                                status = statusStr
                            };
                        }

                        Log(IsChinese
                            ? $"[遥测] 香农熵: {entropy:F3} bits | 位偏置: b0={bitFreqs[0]}% b1={bitFreqs[1]}% | RCT累计失败: {rctFails} | APT累计失败: {aptFails}"
                            : $"[Telemetry] Shannon H: {entropy:F3} bits | Bit Freqs: b0={bitFreqs[0]}% b1={bitFreqs[1]}% | RCT Fails: {rctFails} | APT Fails: {aptFails}");
                    }
                    break;

                case 0xE0:
                    if (length > 0)
                    {
                        byte errCode = payload[0];
                        string errMsg = errCode switch
                        {
                            0x01 => "ERR_QQM_FAIL (QQM failed: insufficient quantum purity)",
                            0x05 => "ERR_VL53_I2C (VL53L1X I2C no response)",
                            0x06 => "ERR_VL53_TIMEOUT (VL53L1X data ready timeout)",
                            0x07 => "ERR_VL53_SAT (VL53L1X saturation/abnormal)",
                            0x08 => "ERR_VL53_STUCK (VL53L1X stuck)",
                            _ => $"Unknown error code: 0x{errCode:X2}"
                        };

                        _currentState = ConnectionState.QqmError;
                        OnPropertyChanged(nameof(ConnectionStatusText));
                        OnPropertyChanged(nameof(StatusColor));

                        if (!IsUploadPausedByError)
                        {
                            IsUploadPausedByError = true;
                            Log(IsChinese ? $"[异常] ⚠️ 硬件故障 (代码 0x{errCode:X2}): {errMsg}。已暂停云端上报！" : $"[Exception] ⚠️ Hardware Failure (Code 0x{errCode:X2}): {errMsg}. Cloud uploads suspended!");
                        }
                        WriteLogToFile("ERROR", $"Code 0x{errCode:X2}: {errMsg}");
                    }
                    break;

                default:
                    Log($"[Protocol] Received unknown packet type: 0x{type:X2}");
                    break;
            }
        }

        private void WriteLogToFile(string level, string message)
        {
            try
            {
                string logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!System.IO.Directory.Exists(logDir))
                {
                    System.IO.Directory.CreateDirectory(logDir);
                }
                CleanupPreviousWeekLogs(logDir);
                string logFile = System.IO.Path.Combine(logDir, $"QnEvt_{DateTime.Now:yyyyMMdd}.log");
                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                System.IO.File.AppendAllText(logFile, logLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

        private static void CleanupPreviousWeekLogs(string? logDir = null)
        {
            try
            {
                DateTime today = DateTime.Today;
                if (_lastLogCleanupDate == today)
                {
                    return;
                }

                logDir ??= System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!System.IO.Directory.Exists(logDir))
                {
                    _lastLogCleanupDate = today;
                    return;
                }

                int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                DateTime currentWeekStart = today.AddDays(-daysSinceMonday);

                foreach (var file in System.IO.Directory.EnumerateFiles(logDir, "QnEvt_*.log", System.IO.SearchOption.TopDirectoryOnly))
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                    if (!fileName.StartsWith("QnEvt_", StringComparison.Ordinal) || fileName.Length != 14)
                    {
                        continue;
                    }

                    string datePart = fileName.Substring(6, 8);
                    if (!DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var logDate))
                    {
                        continue;
                    }

                    if (logDate.Date < currentWeekStart)
                    {
                        System.IO.File.Delete(file);
                    }
                }

                _lastLogCleanupDate = today;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clean old log files: {ex.Message}");
            }
        }

        public void SaveSettings()
        {
            try
            {
                System.IO.Directory.CreateDirectory(SettingsDirectory);

                var settings = new ClientSettings
                {
                    ServerUrl = ServerUrl,
                    SelectedPort = SelectedPort,
                    SelectedBaudRate = SelectedBaudRate,
                    IsChinese = IsChinese,
                    IsLightMode = IsLightMode,
                    ProtectedPushKey = ProtectSecret(PushKey)
                };

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                WriteLogToFile("CONFIG_SAVE_ERROR", ex.Message);
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (!System.IO.File.Exists(SettingsFilePath))
                {
                    return;
                }

                string json = System.IO.File.ReadAllText(SettingsFilePath, Encoding.UTF8);
                var settings = JsonSerializer.Deserialize<ClientSettings>(json);
                if (settings == null)
                {
                    return;
                }

                ServerUrl = string.IsNullOrWhiteSpace(settings.ServerUrl)
                    ? DefaultPushEndpoint
                    : settings.ServerUrl;
                PushKey = UnprotectSecret(settings.ProtectedPushKey);
                IsChinese = settings.IsChinese;
                IsLightMode = settings.IsLightMode;

                if (AvailableBaudRates.Contains(settings.SelectedBaudRate))
                {
                    SelectedBaudRate = settings.SelectedBaudRate;
                }

                if (!string.IsNullOrWhiteSpace(settings.SelectedPort) &&
                    AvailablePorts.Contains(settings.SelectedPort))
                {
                    SelectedPort = settings.SelectedPort;
                }
            }
            catch (Exception ex)
            {
                WriteLogToFile("CONFIG_LOAD_ERROR", ex.Message);
            }
        }

        private static string ProtectSecret(string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                return "";
            }

            byte[] plainBytes = Encoding.UTF8.GetBytes(secret);
            byte[] protectedBytes = OperatingSystem.IsWindows()
                ? ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser)
                : plainBytes;
            return Convert.ToBase64String(protectedBytes);
        }

        private static string UnprotectSecret(string? protectedSecret)
        {
            if (string.IsNullOrWhiteSpace(protectedSecret))
            {
                return "";
            }

            byte[] protectedBytes = Convert.FromBase64String(protectedSecret);
            byte[] plainBytes = OperatingSystem.IsWindows()
                ? ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser)
                : protectedBytes;
            return Encoding.UTF8.GetString(plainBytes);
        }

        private sealed class ClientSettings
        {
            public string? ServerUrl { get; set; }
            public string? ProtectedPushKey { get; set; }
            public string? SelectedPort { get; set; }
            public int SelectedBaudRate { get; set; } = 2000000;
            public bool IsChinese { get; set; }
            public bool IsLightMode { get; set; }
        }

        private static string NormalizePushEndpoint(string? serverUrl)
        {
            string value = (serverUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Cloud server URL is empty.");
            }

            if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                value = "https://" + value;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("Use a valid http(s) URL.");
            }

            var builder = new UriBuilder(uri);
            if (builder.Scheme == Uri.UriSchemeHttp && !IsLocalHost(builder.Host))
            {
                builder.Scheme = Uri.UriSchemeHttps;
                if (builder.Port == 80)
                {
                    builder.Port = -1;
                }
            }

            string path = (builder.Path ?? string.Empty).Trim('/');
            if (string.IsNullOrWhiteSpace(path))
            {
                builder.Path = "api/push-random";
            }

            return builder.Uri.ToString();
        }

        private static bool IsLocalHost(string host)
        {
            return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("::1", StringComparison.OrdinalIgnoreCase);
        }

        private async Task HttpUploaderWorkerAsync(CancellationToken cancellationToken)
        {
            byte[] batchBuffer = new byte[UploadBatchSize];
            int currentBatchCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var reader = _dataChannel?.Reader;
                    if (reader == null)
                    {
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }

                    if (await reader.WaitToReadAsync(cancellationToken))
                    {
                        while (reader.TryRead(out byte b))
                        {
                            batchBuffer[currentBatchCount++] = b;

                            if (currentBatchCount == UploadBatchSize)
                            {
                                byte[] payload = new byte[UploadBatchSize];
                                Array.Copy(batchBuffer, payload, UploadBatchSize);
                                currentBatchCount = 0;

                                await UploadDataAsync(payload, cancellationToken);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Log($"[Upload Error] {(IsChinese ? "数据合并批上传发生异常" : "Aggregation stream exception")}: {ex.Message}");
                        WriteLogToFile("UPLOAD_ERROR", $"Aggregation stream exception: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
        }

        private async Task UploadDataAsync(byte[] rawBytes, CancellationToken cancellationToken)
        {
            if (IsUploadPausedByError && !IsObservingHealth)
            {
                Log(IsChinese ? "⚠️ 硬件处于异常状态，放弃上报当前批次。" : "⚠️ Hardware in error state, dropping current telemetry batch.");
                return;
            }

            _uploadSequence++;
            long currentSeq = _uploadSequence;


            bool fipsPassed = true;
            string fipsReason = "";

            if (_lastUploadedBlock != null && _lastUploadedBlock.Length == rawBytes.Length)
            {
                bool isDuplicate = true;
                for (int i = 0; i < rawBytes.Length; i++)
                {
                    if (rawBytes[i] != _lastUploadedBlock[i])
                    {
                        isDuplicate = false;
                        break;
                    }
                }
                if (isDuplicate)
                {
                    fipsPassed = false;
                    fipsReason = IsChinese ? "检测到数据块完全重复（FIPS 140-3 连续性校验失败）" : "Detected identical block duplicate (FIPS 140-3 continuous check failed)";
                }
            }

            if (fipsPassed && rawBytes.Length > 1)
            {
                bool isAllSame = true;
                for (int i = 1; i < rawBytes.Length; i++)
                {
                    if (rawBytes[i] != rawBytes[0])
                    {
                        isAllSame = false;
                        break;
                    }
                }
                if (isAllSame)
                {
                    fipsPassed = false;
                    fipsReason = IsChinese ? "检测到卡死状态（所有字节相同，FIPS 140-3 卡死校验失败）" : "Detected static/stuck stream (all bytes identical, FIPS 140-3 stuck check failed)";
                }
            }

            if (!fipsPassed)
            {
                IsFipsAlarm = true;
                IsUploadPausedByError = true; // Suspend uploads to prevent packet flow flooding
                IsObservingHealth = false;
                FipsAlarmMessage = fipsReason;
                Log($"[FIPS 140-3 ALARM] {fipsReason}");
                Log(IsChinese ? "⚠️ 安全防御机制已挂起，已终止向云端服务器上传故障数据！" : "⚠️ Secure fail-safe engaged: suspended cloud data transmission.");
                WriteLogToFile("FIPS_ALARM", fipsReason);


                System.Security.Cryptography.CryptographicOperations.ZeroMemory(rawBytes);

                return;
            }


            _lastUploadedBlock = new byte[rawBytes.Length];
            Array.Copy(rawBytes, _lastUploadedBlock, rawBytes.Length);


            if (IsObservingHealth)
            {
                Log(IsChinese
                    ? $"[安检通过] ✓ 成功拦截校验第 1 批数据（{rawBytes.Length} 字节），FIPS 检验合格！已确认无卡死/重复故障，自动开启云端上报..."
                    : $"[Security Passed] ✓ Verified 1st block ({rawBytes.Length} bytes) passing FIPS test. Health check passed, starting upload...");
                IsObservingHealth = false;
                IsUploadPausedByError = false;
                IsFipsAlarm = false;
                FipsAlarmMessage = "";

                // Clear the temporary buffer to protect memory
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(rawBytes);
                return;
            }

            string hexData = Convert.ToHexString(rawBytes);

            string targetUrl;
            try
            {
                targetUrl = _resolvedPushEndpoint ?? NormalizePushEndpoint(ServerUrl);
            }
            catch (Exception ex)
            {
                Log(IsChinese
                    ? $"[配置错误] 云端服务器地址无效，当前批次未上报：{ex.Message}"
                    : $"[Config Error] Invalid cloud server URL; skipped this batch: {ex.Message}");
                WriteLogToFile("CONFIG_ERROR", $"Invalid cloud server URL during upload: {ex.Message}");
                return;
            }

            const int maxRetries = 3;
            const int initialDelayMs = 1000;
            bool uploadSucceeded = false;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Post, targetUrl))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PushKey);

                        object? currentTelemetry = null;
                        lock (_telemetryLock)
                        {
                            currentTelemetry = _latestTelemetry;
                        }

                        var payload = new
                        {
                            data = hexData,
                            format = "hex",
                            devStatus = "ONLINE",
                            clientId = ClientUniqueId,
                            telemetry = currentTelemetry,
                            seq = currentSeq
                        };

                        string jsonString = JsonSerializer.Serialize(payload);
                        request.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                        var response = await HttpClient.SendAsync(request, cancellationToken);

                        if (response.IsSuccessStatusCode)
                        {
                            TotalBytesUploaded += rawBytes.Length;
                            _bytesUploadedInCurrentSecond += rawBytes.Length;
                            LogCloudPushSuccess(rawBytes.Length);

                            // Successful upload: clear NetworkError status if active
                            if (_currentState == ConnectionState.NetworkError)
                            {
                                _currentState = ConnectionState.LinkActive;
                                OnPropertyChanged(nameof(ConnectionStatusText));
                                OnPropertyChanged(nameof(StatusColor));
                            }

                            uploadSucceeded = true;
                            break; // Success! Break out of the retry loop
                        }
                        else
                        {
                            string errorMsg = await response.Content.ReadAsStringAsync();
                            Log($"[Server Rejection] Code: {response.StatusCode}. Reason: {errorMsg}");
                            WriteLogToFile("SERVER_REJECT", $"Code: {response.StatusCode}. Reason: {errorMsg}");


                            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest &&
                                errorMsg != null &&
                                errorMsg.Contains("FIPS", StringComparison.OrdinalIgnoreCase))
                            {
                                IsFipsAlarm = true;
                                IsObservingHealth = false;
                                FipsAlarmMessage = IsChinese
                                    ? $"云端拦截 FIPS 告警: {errorMsg}"
                                    : $"Cloud-side FIPS Alarm: {errorMsg}";

                                IsUploadPausedByError = true;

                                Log(IsChinese
                                    ? "⚠️ 检测到云端拦截的 FIPS 140-3 告警，已触发客户端物理熔断！"
                                    : "⚠️ Detected cloud-intercepted FIPS 140-3 alarm. Triggered client-side physical suspension!");

                                WriteLogToFile("CLOUD_FIPS_ALARM", errorMsg);
                                break;
                            }


                            if (response.StatusCode == System.Net.HttpStatusCode.Conflict &&
                                errorMsg != null &&
                                errorMsg.Contains("DEVICE_KICKED", StringComparison.OrdinalIgnoreCase))
                            {
                                _ = Task.Run(() => HandleKickedAsync());
                                break;
                            }


                            if (attempt < maxRetries)
                            {
                                int delay = initialDelayMs * attempt;
                                Log(IsChinese
                                    ? $"[网络重试] ☁️ 服务器返回异常代码 {response.StatusCode}。正在进行第 {attempt} 次重试（{delay}ms 后）..."
                                    : $"[Network Retry] ☁️ Server returned {response.StatusCode}. Retrying attempt {attempt} in {delay}ms...");
                                await Task.Delay(delay, cancellationToken);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries)
                    {
                        int delay = initialDelayMs * attempt;
                        Log(IsChinese
                            ? $"[网络重试] ⚠️ 网络连接异常 ({ex.Message})。正在进行第 {attempt} 次重试（{delay}ms 后）..."
                            : $"[Network Retry] ⚠️ Connection anomaly ({ex.Message}). Retrying attempt {attempt} in {delay}ms...");
                        await Task.Delay(delay, cancellationToken);
                    }
                    else
                    {

                        Log($"[Network Offline] {(IsChinese ? "云端上报服务器连接失败" : "Cloud server unreachable")}: {ex.Message}");
                        WriteLogToFile("NETWORK_OFFLINE", $"Cloud server unreachable: {ex.Message}");
                    }
                }
            }


            if (!uploadSucceeded && !IsUploadPausedByError && !IsFipsAlarm)
            {
                if (_currentState == ConnectionState.LinkActive || _currentState == ConnectionState.QqmWarning || _currentState == ConnectionState.Connecting)
                {
                    _currentState = ConnectionState.NetworkError;
                    OnPropertyChanged(nameof(ConnectionStatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        private async Task MetricsTrackerLoopAsync(CancellationToken cancellationToken)
        {
            _bytesUploadedWindow.Clear();
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);

                _bytesUploadedWindow.Enqueue(_bytesUploadedInCurrentSecond);
                _bytesUploadedInCurrentSecond = 0;

                if (_bytesUploadedWindow.Count > 5)
                {
                    _bytesUploadedWindow.Dequeue();
                }

                long sum = 0;
                foreach (var val in _bytesUploadedWindow)
                {
                    sum += val;
                }
                double avgBytes = _bytesUploadedWindow.Count > 0 ? (double)sum / _bytesUploadedWindow.Count : 0.0;


                double kbps = (avgBytes * 8.0) / 1024.0;
                CurrentThroughputKbps = Math.Round(kbps, 2);
            }
        }

        private void LogCloudPushSuccess(int bytesUploaded)
        {
            _bytesSinceLastCloudPushLog += bytesUploaded;
            var now = DateTime.UtcNow;
            if (_lastCloudPushLogAt != DateTime.MinValue &&
                (now - _lastCloudPushLogAt).TotalSeconds < 5)
            {
                return;
            }

            long reportedBytes = _bytesSinceLastCloudPushLog;
            _bytesSinceLastCloudPushLog = 0;
            _lastCloudPushLogAt = now;

            Log(IsChinese
                ? $"[Cloud Push] 已向服务器上报 {reportedBytes} 字节量子熵。代码：200 OK"
                : $"[Cloud Push] Broadcasted {reportedBytes} bytes entropy. Code: 200 OK");
        }




        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string line = $"[{timestamp}] {message}\n";


            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                TerminalLogs += line;


                var lines = TerminalLogs.Split('\n');
                if (lines.Length > 100)
                {
                    TerminalLogs = string.Join('\n', lines, lines.Length - 100, 100);
                }
            });
        }

        private void CloseSerialPort()
        {
            try
            {
                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }
            catch { }
        }
    }
}
