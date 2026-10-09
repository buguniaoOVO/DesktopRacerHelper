using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopRacerHelper;

internal sealed class MainForm : Form
{
	private static readonly TimeSpan BackgroundShotInterval = TimeSpan.FromMilliseconds(900.0);

	private static readonly object RuntimeLogLock = new object();

	private readonly AppSettings _settings = AppSettings.Load();

	private readonly GameApiClient _gameApi = new GameApiClient();

	private readonly MarketPriceService _marketPrices = new MarketPriceService();

	private readonly ChestLog _chestLog = ChestLog.Load();

	private readonly List<string> _log = new List<string>();

	private readonly Dictionary<string, Button> _navButtons = new Dictionary<string, Button>();

	private readonly Dictionary<string, string> _navKeys = new Dictionary<string, string>();

	private readonly Panel _pageHost = new Panel();

	private readonly Label _footer = new Label();

	private readonly Label _footerCredits = new Label();

	private readonly Label _connection = new Label();

	private readonly Label _ocrLabel = new Label();

	private readonly Label _headerTitle = new Label();

	private readonly Label _brandName = new Label();

	private readonly Label _brandSub = new Label();

	private readonly Button _reconnect = new Button();

	private readonly Label _topAutomationState = new Label();

	private readonly Button _topLaunchButton = new Button();

	private readonly Button _topStartButton = new Button();

	private readonly Button _topStopButton = new Button();

	private readonly Label _guardTitle = new Label();

	private readonly Label _guardText = new Label();

	private readonly NotifyIcon _tray = new NotifyIcon();

	private readonly System.Windows.Forms.Timer _autoScan = new System.Windows.Forms.Timer();

	private readonly System.Windows.Forms.Timer _manualChestStatsTimer = new System.Windows.Forms.Timer();

	private readonly CancellationTokenSource _manualChestStatsCts = new CancellationTokenSource();

	private CancellationTokenSource _runCts = new CancellationTokenSource();

	private readonly SemaphoreSlim _gameOwnership = new SemaphoreSlim(1, 1);

	private bool _executionEnabled;

	private bool _manualRunRunning;

	private bool _manualChestStatsRunning;

	private string _lastManualChestStatsError = "";

	private bool _initializing = true;

	private bool _initialized;

	private bool _initializationRunning;

	private string _initializationError = "";

	private string _currentTaskKey = "task.initializing";

	private string _nextTaskKey = "task.verifyEnvironment";

	private PluginAutomationEngine? _automation;

	private long _lastGameApiEventSequence;

	private int _lastGameApiEventPid;

	private DateTime _lastGameApiInventoryRefresh = DateTime.MinValue;

	private ProgressBar _progressBar = new ProgressBar();

	private Label _progressLabel = new Label();

	private Label _currentTaskLabel = new Label();

	private Label _nextTaskLabel = new Label();

	private Label _executionStatusLabel = new Label();

	private Label _executionDetailLabel = new Label();

	private Button _stopButton = new Button();

	private Button _chestImmediateButton = new Button();

	private Button _warehouseImmediateButton = new Button();

	private BridgeServer? _bridge;

	private GameFingerprint? _envFingerprint;

	private Label? _envGame;

	private Label? _envEngine;

	private Label? _envHash;

	private Label? _envBridge;

	private Label? _envVerify;

	private Label? _envPlugin;

	private Native.WindowInfo? _window;

	private WarehouseScan? _lastScan = InventoryCache.Load();

	private bool _autoRunning;

	private readonly System.Windows.Forms.Timer _watchdog = new System.Windows.Forms.Timer();

	private bool _watchdogRunning;

	private int _antiDropRestartCount;

	private DateTime _watchdogWaitingSince = DateTime.MinValue;

	private DateTime _lastGameFeedbackAt = DateTime.MinValue;

	private int _sendFailureStreak;

	private int _watchdogProbeFailures;

	private DateTime _nextApiProbeLogAt = DateTime.MinValue;

	private string _pluginDeployedHash = "";

	private bool _steamLaunchPending;

	private bool _steamLaunchSyncRunning;

	private DateTime _nextSteamLaunchAttemptAt = DateTime.MinValue;

	private string _pluginRuntimeVersion = "";

	private int _pluginRuntimeProtocol;

	private bool _warehouseIconScanAttempted;

	private DateTime _lastChestCheck = DateTime.MinValue;

	private DateTime _lastWarehouseScan = DateTime.MinValue;

	private DateTime _lastRepairCheck = DateTime.MinValue;

	private string _lastAutoScanError = "";

	private int _autoScanFailures;

	private string _lastAutoScanFailure = "";

	private DateTime _nextAutoScanFailureLog = DateTime.MinValue;

	private string _activePage = "overview";

	private bool _reallyExit;

	private bool _busy;

	private NumericUpDown _warehouseMinutes = new NumericUpDown();

	private NumericUpDown _warehouseThreshold = new NumericUpDown();

	private NumericUpDown _chestMinutes = new NumericUpDown();

	private NumericUpDown _repairMinutes = new NumericUpDown();

	private NumericUpDown _repairThreshold = new NumericUpDown();

	private NumericUpDown _delayMin = new NumericUpDown();

	private NumericUpDown _delayMax = new NumericUpDown();

	private Label _slotLabel = new Label();

	private FlowLayoutPanel _itemList = new FlowLayoutPanel();

	private Button _warehouseFilterButton = new Button();

	private ComboBox _warehouseSortBox = new ComboBox();

	private Button _refreshWarehouseButton = new Button();

	private string _warehouseFilterMode = "all";

	private string _warehouseSortMode = "game";

	private readonly Dictionary<string, Label> _warehousePriceLabels = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);

	private Button _queryAllPricesButton = new Button();

	private Label _warehousePriceStatus = new Label();

	private Label _chestStatus = new Label();

	private string _chestStatusKey = "chest.statusIdle";

	private Color _chestStatusColor = Theme.Muted;

	private Panel _statsContent = new Panel();

	private Button _statsChestTab = new Button();

	private Button _statsTradeTab = new Button();

	private Label _statsChestSummary = new Label();

	private Label _statsTradeSummary = new Label();

	private Label _stats24hValue = new Label();

	private DataGridView _statsQualityGrid = new DataGridView();

	private DataGridView _statsTradeGrid = new DataGridView();

	private string _statsTab = "chest";

	private bool _statsRefreshing;

	private readonly System.Windows.Forms.Timer _statsTimer = new System.Windows.Forms.Timer();

	private DataGridView _qualityGrid = new DataGridView();

	private TextBox _logView = new TextBox();

	private ComboBox _languageBox = new ComboBox();

	private ComboBox _closeBox = new ComboBox();

	private CheckBox _waitReconnect = new CheckBox();

	private CheckBox _antiDropEnable = new CheckBox();

	private NumericUpDown _antiDropMinutes = new NumericUpDown();

	private TextBox _gamePathBox = new TextBox();

	private CheckBox _collectAfterSort = new CheckBox();

	private CheckBox _sortAfterChestClaim = new CheckBox();

	private CheckBox _repairEnable = new CheckBox();

	private Button _repairImmediateButton = new Button();

	private readonly System.Windows.Forms.Timer _poll = new System.Windows.Forms.Timer();

	private static string AppVersion => typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

	private PluginAutomationEngine Engine => _automation ?? (_automation = new PluginAutomationEngine(_gameApi, EngineMessage, () => _settings, SetProgress, StoreInventory, _gameOwnership));

	internal MainForm()
	{
		L.Lang = (string.Equals(_settings.Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh");
		if (BackgroundCapture.Destination != null)
		{
			_settings.DryRun = true;
			_settings.AutoRunEnabled = false;
			_settings.AntiDropEnabled = false;
			_settings.CollectSupplyBoxesAfterSort = false;
			_settings.SortAfterSupplyBoxClaim = false;
		}
		Icon = LoadAppIcon();
		ApplyFormText();
		StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new Size(1040, 720);
		Size = new Size(1180, 800);
		BackColor = Theme.Surface;
		ForeColor = Theme.Text;
		Font = Theme.UiFont("Microsoft YaHei UI", 9f);
		BuildShell();
		BuildTray();
		ActionGate.Shared.Settings = () => _settings;
		ShowPage("overview");
		Log(string.Format(L.T("log.started"), AppVersion));
		string loadWarning = _chestLog.LoadWarning;
		if (loadWarning != null && loadWarning.Length > 0)
		{
			Log(loadWarning);
		}
		string lastSaveError = _chestLog.LastSaveError;
		if (lastSaveError != null && lastSaveError.Length > 0)
		{
			Log("统计数据保存失败：" + lastSaveError);
		}
		Log("游戏数据读取方式：游戏内接口；不使用截图和 OCR。");
		_poll.Interval = 1000;
		_poll.Tick += async (object? _, EventArgs _) =>
		{
			await PollForGameAsync();
		};
		_autoScan.Interval = 2000;
		_autoScan.Tick += async (object? _, EventArgs _) =>
		{
			await AutoScanAsync();
		};
		_manualChestStatsTimer.Interval = 1000;
		_manualChestStatsTimer.Tick += async (object? _, EventArgs _) =>
		{
			await SyncGameApiEventsAsync();
		};
		_manualChestStatsTimer.Start();
		_statsTimer.Interval = 60000;
		_statsTimer.Tick += (object? _, EventArgs _) =>
		{
			RefreshLast24HourCount();
		};
		_statsTimer.Start();
		_watchdog.Tick += async (object? _, EventArgs _) =>
		{
			await WatchdogTickAsync();
		};
		SyncWatchdog();
		Shown += async (object? _, EventArgs _) =>
		{
			if (BackgroundCapture.Destination == null)
			{
				await InitializeAssistantAsync();
			}
		};
		if (BackgroundCapture.Destination != null)
		{
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			Location = new Point(-32000, -32000);
			BackgroundCapture.State = BackgroundCapture.Mode.Requested;
			Shown += async (object? _, EventArgs _) =>
			{
				await RunBackgroundCaptureAsync();
			};
		}
	}

	private void SetTaskState(string currentKey, string nextKey)
	{
		_currentTaskKey = currentKey;
		_nextTaskKey = nextKey;
		RefreshTaskLabels();
	}

	private void RefreshTaskLabels()
	{
		if (!_currentTaskLabel.IsDisposed)
		{
			_currentTaskLabel.Text = L.T("overview.currentTask") + "：" + L.T(_currentTaskKey);
		}
		if (!_nextTaskLabel.IsDisposed)
		{
			_nextTaskLabel.Text = L.T("overview.nextTask") + "：" + L.T(_nextTaskKey);
		}
		RefreshAutomationChrome();
	}

	private void RefreshAutomationChrome()
	{
		if (!_topStartButton.IsDisposed)
		{
			_topStartButton.Text = L.T("header.startAll");
		}
		if (!_topStopButton.IsDisposed)
		{
			_topStopButton.Text = L.T("header.stopAll");
		}
		if (!_reconnect.IsDisposed)
		{
			_reconnect.Text = L.T("header.reconnect");
		}
		if (!_topAutomationState.IsDisposed)
		{
			if (_initializing)
			{
				_topAutomationState.Text = "●  " + L.T("header.automationInitializing");
				_topAutomationState.ForeColor = Theme.Warning;
			}
			else if (_manualRunRunning)
			{
				_topAutomationState.Text = "●  " + L.T("header.automationRunning");
				_topAutomationState.ForeColor = Theme.Accent;
			}
			else if (_executionEnabled)
			{
				_topAutomationState.Text = "●  " + L.T("header.automationOn");
				_topAutomationState.ForeColor = Theme.Good;
			}
			else if (_autoScan.Enabled)
			{
				_topAutomationState.Text = "●  " + L.T("header.automationMonitoring");
				_topAutomationState.ForeColor = Theme.Accent;
			}
			else
			{
				_topAutomationState.Text = "●  " + L.T("header.automationOff");
				_topAutomationState.ForeColor = Theme.Muted;
			}
		}
		bool enabled = _initialized && !_initializing && (object)_window != null && !_executionEnabled && !_manualRunRunning && !_autoRunning && !(_automation?.IsRunning ?? false);
		if (!_topStartButton.IsDisposed)
		{
			_topStartButton.Enabled = enabled;
		}
		bool enabled2 = _initialized && !_initializing && (object)_window != null && !_manualRunRunning && !_autoRunning && !(_automation?.IsRunning ?? false);
		if (!_chestImmediateButton.IsDisposed)
		{
			_chestImmediateButton.Enabled = enabled2;
		}
		if (!_warehouseImmediateButton.IsDisposed)
		{
			_warehouseImmediateButton.Enabled = enabled2;
		}
		if (!_repairImmediateButton.IsDisposed)
		{
			_repairImmediateButton.Enabled = enabled2;
		}
		bool enabled3 = _executionEnabled || _autoScan.Enabled || _manualRunRunning;
		if (!_topStopButton.IsDisposed)
		{
			_topStopButton.Enabled = enabled3;
		}
		if (!_stopButton.IsDisposed)
		{
			_stopButton.Enabled = enabled3;
		}
		RefreshExecutionStatus();
	}

	private void RefreshExecutionStatus()
	{
		if (_executionStatusLabel.IsDisposed || _executionDetailLabel.IsDisposed)
		{
			return;
		}
		if (_initializing)
		{
			_executionStatusLabel.Text = "●  " + L.T("overview.stateInitializing");
			_executionStatusLabel.ForeColor = Theme.Warning;
			_executionDetailLabel.Text = L.T("overview.stateDetailInitializing");
			return;
		}
		if (!_initialized)
		{
			_executionStatusLabel.Text = "●  " + L.T("overview.stateInitFailed");
			_executionStatusLabel.ForeColor = Theme.Warning;
			_executionDetailLabel.Text = ((_initializationError.Length > 0) ? _initializationError : L.T("overview.stateDetailInitFailed"));
			return;
		}
		if (_currentTaskKey == "task.paused")
		{
			_executionStatusLabel.Text = "●  " + L.T("overview.statePaused");
			_executionStatusLabel.ForeColor = Theme.Warning;
			_executionDetailLabel.Text = ((_lastAutoScanError.Length > 0) ? _lastAutoScanError : L.T("overview.stateDetailPaused"));
			return;
		}
		if (_executionEnabled || _manualRunRunning)
		{
			PluginAutomationEngine automation = _automation;
			if (automation != null && automation.IsRunning)
			{
				_executionStatusLabel.Text = "●  " + L.T("overview.stateRunning");
				_executionStatusLabel.ForeColor = Theme.Accent;
				_executionDetailLabel.Text = L.T("overview.currentTask") + "：" + L.T(_currentTaskKey);
				return;
			}
		}
		if (!_executionEnabled && !_autoScan.Enabled && !_manualRunRunning)
		{
			_executionStatusLabel.Text = "●  " + L.T("overview.stateOff");
			_executionStatusLabel.ForeColor = Theme.Muted;
			_executionDetailLabel.Text = L.T("overview.stateDetailOff");
		}
		else if (_executionEnabled)
		{
			_executionStatusLabel.Text = "●  " + L.T("overview.stateMonitoring");
			_executionStatusLabel.ForeColor = Theme.Good;
			_executionDetailLabel.Text = L.T("overview.stateDetailMonitoring");
		}
		else if ((object)_window != null)
		{
			_executionStatusLabel.Text = "●  " + L.T("overview.stateReady");
			_executionStatusLabel.ForeColor = Theme.Good;
			_executionDetailLabel.Text = string.Format(L.T("overview.stateDetailReady"), _window.Title);
		}
		else
		{
			_executionStatusLabel.Text = "●  " + L.T("overview.stateWaiting");
			_executionStatusLabel.ForeColor = Theme.Muted;
			_executionDetailLabel.Text = L.T("overview.stateDetailWaiting");
		}
	}

	private void EngineMessage(string message)
	{
		if (InvokeRequired)
		{
			InvokeOnUi(() =>
			{
				EngineMessage(message);
			});
			return;
		}
		Log(message);
		if (message.Contains("仓库达到阈值", StringComparison.Ordinal))
		{
			SetTaskState("task.collect", "task.checkParts");
		}
		else if (message.Contains("收藏第", StringComparison.Ordinal))
		{
			SetTaskState("task.collect", "task.checkParts");
		}
		else if (message.Contains("没有兼容的零件", StringComparison.Ordinal))
		{
			SetTaskState("task.checkParts", "task.checkParts");
		}
		else if (message.Contains("领取一个补给箱", StringComparison.Ordinal))
		{
			SetTaskState("task.chest", "task.waitResult");
		}
		else if (message.Contains("补给箱检查", StringComparison.Ordinal))
		{
			SetTaskState("task.chest", "task.batch");
		}
		else if (message.Contains("车况检查", StringComparison.Ordinal) || message.Contains("维修已安排", StringComparison.Ordinal) || message.Contains("安排维修", StringComparison.Ordinal) || message.Contains("车况良好", StringComparison.Ordinal))
		{
			SetTaskState("task.repair", "task.waitCycle");
		}
		else if (message.Contains("补给箱已点击", StringComparison.Ordinal) || message.Contains("已发送投入", StringComparison.Ordinal))
		{
			SetTaskState("task.waitResult", "task.chest");
		}
		else if (message.Contains("补给箱提示物品栏已满", StringComparison.Ordinal))
		{
			SetTaskState("task.batch", "task.chest");
		}
		else if (message.Contains("按动作分组批量整理", StringComparison.Ordinal))
		{
			SetTaskState("task.batch", "task.chest");
		}
		else if (message.Contains("已提交批量", StringComparison.Ordinal))
		{
			SetTaskState("task.waitResult", "task.batch");
		}
		else if (message.Contains("批量拆解确认", StringComparison.Ordinal) || message.Contains("批量出售确认", StringComparison.Ordinal))
		{
			SetTaskState("task.batch", "task.chest");
		}
		else if (message.Contains("仓库扫描", StringComparison.Ordinal) || message.Contains("读取仓库", StringComparison.Ordinal))
		{
			SetTaskState("task.readInventory", "task.chest");
		}
		else if (message.Contains("本轮已完成", StringComparison.Ordinal))
		{
			SetTaskState("task.complete", "task.waitCycle");
		}
		else if (message.Contains("暂停", StringComparison.Ordinal))
		{
			_lastAutoScanError = message;
			SetTaskState("task.paused", "task.waitCycle");
		}
	}

	private async Task InitializeAssistantAsync()
	{
		if (_initializationRunning)
		{
			return;
		}
		if (_executionEnabled)
		{
			StopAutomation(persistOff: false);
		}
		_poll.Stop();
		_autoScan.Stop();
		_initializationRunning = true;
		_initializing = true;
		_initialized = false;
		_initializationError = "";
		_window = null;
		_reconnect.Enabled = false;
		SetTaskState("task.initializing", "task.verifyEnvironment");
		UpdateConnection();
		Log(L.T("log.initStarted"));
		try
		{
			await Task.Yield();
			string location = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "DesktopRacerHelper.exe");
			string path = Path.Combine(AppContext.BaseDirectory, "Assets", "game-plugin", "helper_bridge.gd");
			if (!File.Exists(location) || !File.Exists(path))
			{
				throw new InvalidOperationException("助手组件或必要资源文件缺失。");
			}
			if (Environment.Version.Major < 8)
			{
				throw new InvalidOperationException("助手运行时版本低于 .NET 8。");
			}
			SetTaskState("task.verifyEnvironment", "task.connect");
			string text = ResolveGameSourcePath();
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidOperationException("没有找到游戏路径。请在设置中确认游戏路径，或先运行一次环境检测。");
			}
			string path2 = PluginInstall.GetSourceExecutablePath(text);
			GameFingerprint fingerprint = await Task.Run(() => Deployment.Inspect(path2));
			if (fingerprint == null)
			{
				throw new InvalidOperationException(L.T("verify.gameMissing"));
			}
			_envFingerprint = fingerprint;
			if (!string.Equals(fingerprint.Engine, "Godot", StringComparison.OrdinalIgnoreCase) || !fingerprint.Suffix.StartsWith("4.7.1", StringComparison.Ordinal))
			{
				throw new InvalidOperationException(L.T("verify.engineChanged"));
			}
			if (Deployment.Verify(fingerprint).Length > 0)
			{
				DeployResult deployResult = Deployment.Deploy(fingerprint, _bridge?.Port ?? 0, L.T("env.bridgeMode"));
				if (!deployResult.Ok)
				{
					throw new InvalidOperationException("重建游戏环境记录失败：" + deployResult.Message);
				}
				Log("环境记录缺失或与游戏文件不一致，已按已保存的游戏路径重新部署环境记录。");
			}
			_settings.GameExePath = fingerprint.ExecutablePath;
			try
			{
				_settings.Save();
			}
			catch (Exception ex)
			{
				Log("保存游戏路径失败：" + ex.Message);
			}
			SetTaskState("task.verifyEnvironment", "task.connect");
			(bool, string) tuple = await EnsurePluginAsync(path2);
			if (!tuple.Item1)
			{
				throw new InvalidOperationException(tuple.Item2);
			}
			if (!StartBridge())
			{
				throw new InvalidOperationException("本地桥接服务启动失败。");
			}
			_initializing = false;
			_initialized = true;
			LocateGame();
			Log(string.Format(L.T("log.initReady"), fingerprint.Suffix, AppVersion));
			SetTaskState(((object)_window == null) ? "task.waitGame" : "task.ready", "task.startAutomation");
			_poll.Start();
			RefreshAutomationChrome();
			RefreshEnvironment();
			SyncWatchdog();
			if (_settings.AutoRunEnabled)
			{
				await ResumeMonitoringAsync();
			}
		}
		catch (Exception ex2)
		{
			_initializing = false;
			_initialized = false;
			_initializationError = ex2.Message;
			_window = null;
			_poll.Stop();
			_autoScan.Stop();
			_bridge?.Dispose();
			_bridge = null;
			UpdateConnection();
			SetTaskState("task.initFailed", "task.openEnvironment");
			Log("初始化失败，尚未连接游戏：" + ex2.Message);
		}
		finally
		{
			_initializationRunning = false;
			if (!_reconnect.IsDisposed)
			{
				_reconnect.Enabled = true;
			}
			RefreshEnvironment();
		}
	}

	private async Task PollForGameAsync()
	{
		if (!_initialized || _initializationRunning)
		{
			return;
		}
		Native.WindowInfo candidate = GameReader.FindGameWindow();
		GameApiClient.SetExpectedProcessId((object)candidate == null ? 0 : candidate.ProcessId);
		GameApiRuntimeStatus pluginStatus = GameApiClient.ReadRuntimeStatus((object)candidate == null ? 0 : candidate.ProcessId);
		if ((object)_window != null)
		{
			if ((object)candidate != null && candidate.ProcessId == _window.ProcessId && (object)pluginStatus != null && pluginStatus.HostPid == candidate.ProcessId && pluginStatus.KernelReady)
			{
				UpdateConnection();
				return;
			}
			_window = null;
		}
		if ((object)candidate == null)
		{
			UpdateConnection();
			TryCompleteSteamLaunchPatch();
			return;
		}
		if ((object)pluginStatus == null || pluginStatus.HostPid != candidate.ProcessId || !pluginStatus.KernelReady)
		{
			UpdateConnection();
			return;
		}
		_initializationRunning = true;
		_initializing = true;
		_reconnect.Enabled = false;
		SetTaskState("task.verifyEnvironment", "task.connect");
		UpdateConnection();
		try
		{
			string text = GameProcessPath();
			string sourcePath = ((text == null) ? null : PluginInstall.GetSourceExecutablePath(text));
			GameFingerprint gameFingerprint = ((sourcePath != null) ? (await Task.Run(() => Deployment.Inspect(sourcePath))) : null);
			GameFingerprint fingerprint = gameFingerprint;
			if (fingerprint == null)
			{
				throw new InvalidOperationException(L.T("verify.gameMissing"));
			}
			string text2 = Deployment.Verify(fingerprint);
			if (text2.Length > 0)
			{
				throw new InvalidOperationException(L.T(text2));
			}
			try
			{
				await _gameApi.PingAsync();
			}
			catch (Exception ex) when (IsTransientApiProbeFailure(ex))
			{
				_window = null;
				_initializing = false;
				_initializationError = "";
				UpdateConnection();
				if (_currentTaskKey != "task.waitGame")
				{
					SetTaskState("task.waitGame", "task.connect");
				}
				if (DateTime.Now >= _nextApiProbeLogAt)
				{
					Log("游戏接口正在启动，助手会继续检测连接：" + ex.Message);
					_nextApiProbeLogAt = DateTime.Now.AddSeconds(15.0);
				}
				return;
			}
			_envFingerprint = fingerprint;
			_window = candidate;
			_initializing = false;
			_nextApiProbeLogAt = DateTime.MinValue;
			UpdateConnection();
			NoteGameFeedback();
			Log($"游戏内 API 已连接：进程 {pluginStatus.HostPid}，端口 127.0.0.1:{pluginStatus.Port}，服务端状态 {pluginStatus.ConnectionState}。");
			SetTaskState("task.ready", "task.startAutomation");
		}
		catch (Exception ex2)
		{
			_window = null;
			_initialized = false;
			_initializing = false;
			_initializationError = ex2.Message;
			_poll.Stop();
			_autoScan.Stop();
			_bridge?.Dispose();
			_bridge = null;
			UpdateConnection();
			SetTaskState("task.initFailed", "task.openEnvironment");
			Log("重新连接前校验失败，已阻止连接：" + ex2.Message);
		}
		finally
		{
			_initializationRunning = false;
			if (!_reconnect.IsDisposed)
			{
				_reconnect.Enabled = true;
			}
		}
	}

	private static bool IsTransientApiProbeFailure(Exception error)
	{
		if (error is IOException || error is TimeoutException || error is InvalidOperationException || error is OperationCanceledException || error is SocketException || error is JsonException)
		{
			return true;
		}
		return false;
	}

	private void RecordChestOpen(List<ItemEntry> items, int openedCount)
	{
		if (InvokeRequired)
		{
			InvokeOnUi(() =>
			{
				RecordChestOpen(items, openedCount);
			});
			return;
		}
		NoteGameFeedback();
		_chestLog.RecordChestOpen(items, openedCount);
		string lastSaveError = _chestLog.LastSaveError;
		if (lastSaveError != null && lastSaveError.Length > 0)
		{
			Log("统计数据尚未保存；助手会保留当前记录并在后续变更时重试：" + lastSaveError);
		}
		if (_activePage == "chest")
		{
			RefreshStatisticsView();
		}
	}

	private async Task AutoScanAsync()
	{
		if (!_initialized || _initializing || _autoRunning || _manualRunRunning || _manualChestStatsRunning || _busy)
		{
			return;
		}
		PluginAutomationEngine automation = _automation;
		if ((automation != null && automation.IsRunning) || (object)_window == null)
		{
			return;
		}
		_autoRunning = true;
		RefreshAutomationChrome();
		try
		{
			if (!_executionEnabled)
			{
				if (!(DateTime.Now - _lastWarehouseScan < TimeSpan.FromMinutes(Math.Max(1, _settings.WarehouseCheckMinutes))))
				{
					StoreInventory(await Engine.RefreshAsync(_window, CancellationToken.None));
					NoteGameFeedback();
					_lastWarehouseScan = DateTime.Now;
					_lastAutoScanError = "";
					SetTaskState("task.ready", "task.startAutomation");
				}
				return;
			}
			DateTime now = DateTime.Now;
			bool chestDue = now - _lastChestCheck >= TimeSpan.FromMinutes(Math.Max(1, _settings.ChestCheckMinutes));
			bool warehouseDue = now - _lastWarehouseScan >= TimeSpan.FromMinutes(Math.Max(1, _settings.WarehouseCheckMinutes));
			bool repairDue = _settings.RepairEnabled && now - _lastRepairCheck >= TimeSpan.FromMinutes(Math.Max(1, _settings.RepairCheckMinutes));
			if (!chestDue && !warehouseDue)
			{
				if (repairDue)
				{
					await Engine.CheckAndRepairAsync(_window, _settings.RepairThresholdPercent, _runCts.Token);
					NoteGameFeedback();
					_lastRepairCheck = DateTime.Now;
					_lastAutoScanError = "";
				}
				return;
			}
			await Engine.RunAsync(_window, chestDue, warehouseDue, forceSort: false, _runCts.Token);
			NoteGameFeedback();
			if (chestDue)
			{
				_lastChestCheck = DateTime.Now;
			}
			if (warehouseDue)
			{
				_lastWarehouseScan = DateTime.Now;
			}
			if (repairDue)
			{
				await Engine.CheckAndRepairAsync(_window, _settings.RepairThresholdPercent, _runCts.Token);
				NoteGameFeedback();
				_lastRepairCheck = DateTime.Now;
			}
			_lastAutoScanError = "";
			if (_autoScanFailures > 0)
			{
				_autoScanFailures = 0;
				Log("后台读取已恢复，自动监控继续。");
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex2)
		{
			string text = (_executionEnabled ? "自动执行等待游戏 API：" : "后台仓库刷新等待游戏 API：") + ex2.Message;
			_autoScanFailures++;
			if (IsConnectionFailure(ex2.Message))
			{
				_sendFailureStreak++;
			}
			if (_autoScanFailures < 3)
			{
				if (!string.Equals(_lastAutoScanFailure, text, StringComparison.Ordinal))
				{
					Log(text + "；将在下一轮检查自动重试。");
				}
				_lastAutoScanFailure = text;
				_lastAutoScanError = text;
				SetTaskState("task.waitCycle", _executionEnabled ? "task.chest" : "task.startAutomation");
				return;
			}
			SetTaskState("task.waitCycle", _executionEnabled ? "task.chest" : "task.startAutomation");
			if (!string.Equals(_lastAutoScanError, text, StringComparison.Ordinal) || DateTime.Now >= _nextAutoScanFailureLog)
			{
				Log(text + $"；已连续 {_autoScanFailures} 次失败，仍在后台重试，恢复可读后会自动继续。");
				_nextAutoScanFailureLog = DateTime.Now.AddMinutes(1.0);
			}
			_lastAutoScanError = text;
		}
		finally
		{
			_autoRunning = false;
			RefreshAutomationChrome();
		}
	}

	private void StoreInventory(WarehouseScan scan)
	{
		if (InvokeRequired)
		{
			InvokeOnUi(() =>
			{
				StoreInventory(scan);
			});
		}
		else
		{
			if (scan.SlotTotal <= 0 || (scan.SlotUsed > 0 && scan.Items.Count == 0))
			{
				return;
			}
			bool num = (object)_lastScan == null || _lastScan.SlotUsed != scan.SlotUsed || _lastScan.SlotTotal != scan.SlotTotal || _lastScan.Complete != scan.Complete || _lastScan.CanScroll != scan.CanScroll || !_lastScan.Items.Select((ItemEntry i) => (Name: i.Name, Quality: i.Quality, Count: i.Count)).SequenceEqual(scan.Items.Select((ItemEntry i) => (Name: i.Name, Quality: i.Quality, Count: i.Count))) || (_lastScan.Items.Count == scan.Items.Count && _lastScan.Items.Zip(scan.Items).Any(((ItemEntry First, ItemEntry Second) pair) =>
			{
				if (pair.First.IconPng == null)
				{
					byte[] iconPng = pair.Second.IconPng;
					if (iconPng != null)
					{
						return iconPng.Length > 0;
					}
					return false;
				}
				return false;
			}));
			_lastScan = scan;
			if (num)
			{
				try
				{
					InventoryCache.Save(scan);
				}
				catch (Exception ex)
				{
					Log("仓库缓存保存失败：" + ex.Message);
				}
				if (_activePage == "warehouse")
				{
					RenderWarehouse();
				}
			}
		}
	}

	private async Task SyncGameApiEventsAsync()
	{
		if (BackgroundCapture.Destination != null)
		{
			return;
		}
		if (_manualChestStatsRunning || !_initialized || _initializing)
		{
			return;
		}
		_manualChestStatsRunning = true;
		using CancellationTokenSource attemptCts = CancellationTokenSource.CreateLinkedTokenSource(_manualChestStatsCts.Token);
		attemptCts.CancelAfter(TimeSpan.FromSeconds(12.0));
		try
		{
			GameApiRuntimeStatus status = _gameApi.RuntimeStatus;
			if ((object)status == null || !status.KernelReady)
			{
				if (_activePage == "chest")
				{
					UpdateChestMonitorStatus("chest.statusNoGame", Theme.Muted);
				}
				return;
			}
			if (_lastGameApiEventPid != status.HostPid)
			{
				_lastGameApiEventPid = status.HostPid;
				_lastGameApiEventSequence = 0L;
			}
			IReadOnlyList<GameApiEvent> source = await _gameApi.GetEventsAsync(_lastGameApiEventSequence, attemptCts.Token);
			bool flag = false;
			int syncedEventCount = 0;
			foreach (GameApiEvent item in source.OrderBy((GameApiEvent value) => value.Sequence))
			{
				_lastGameApiEventSequence = Math.Max(_lastGameApiEventSequence, item.Sequence);
				ItemEntry itemEntry = ParseGameApiReward(item.Item);
				if (_chestLog.RecordChestOpen(new _003C_003Ez__ReadOnlySingleElementList<ItemEntry>(itemEntry), 1, item.EventId, persist: false).Count != 0)
				{
					flag = true;
					syncedEventCount++;
					NoteGameFeedback();
				}
			}
			if (flag)
			{
				_chestLog.Save();
				string lastSaveError = _chestLog.LastSaveError;
				if (lastSaveError != null && lastSaveError.Length > 0)
				{
					Log("统计数据尚未保存；助手会保留当前记录并在后续变更时重试：" + lastSaveError);
				}
				Log(string.Format(L.T("log.gameEventsSynced"), syncedEventCount));
				if (_activePage == "chest")
				{
					RefreshStatisticsView();
				}
			}
			if ((object)_window != null && status.Connected && (flag || DateTime.Now - _lastGameApiInventoryRefresh >= TimeSpan.FromSeconds(10.0)))
			{
				StoreInventory((await Engine.RefreshManualStatsSnapshotAsync(_window, attemptCts.Token)).Item1);
				NoteGameFeedback();
				_lastGameApiInventoryRefresh = DateTime.Now;
			}
			if (_activePage == "chest")
			{
				UpdateChestMonitorStatus(status.Connected ? "chest.statusWatching" : "chest.statusIdle", status.Connected ? Theme.Good : Theme.Warning);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex2)
		{
			if (IsConnectionFailure(ex2.Message))
			{
				_sendFailureStreak++;
			}
			if (!string.Equals(_lastManualChestStatsError, ex2.Message, StringComparison.Ordinal))
			{
				Log("游戏开箱事件同步稍后重试：" + ex2.Message);
			}
			_lastManualChestStatsError = ex2.Message;
			if (_activePage == "chest")
			{
				UpdateChestMonitorStatus("chest.statusIdle", Theme.Warning);
			}
		}
		finally
		{
			_manualChestStatsRunning = false;
		}
	}

	private static ItemEntry ParseGameApiReward(JsonElement value)
	{
		if (value.ValueKind != JsonValueKind.Object)
		{
			return new ItemEntry("未知物品", Theme.Unknown.Name, 0, 0, 1, 0f, 0f, Selected: false, NameReliable: false);
		}
		string text = GameApiClient.GetString(value, "name").Trim();
		string text2 = GameApiClient.GetString(value, "rarity_id", "common");
		string text3 = ((!GameVision.IsConsumable(text)) ? (text2 switch
		{
			"common" => "普通", 
			"uncommon" => "优秀", 
			"rare" => "稀有", 
			"epic" => "史诗", 
			"legendary" => "传说", 
			"mythic" => "神话", 
			"exotic" => "异域", 
			_ => Theme.Unknown.Name, 
		}) : Theme.Consumable.Name);
		string text4 = text3;
		bool flag = GameApiClient.GetBool(value, "steam_item") || GameApiClient.GetBool(value, "marketable") || GameVision.IsMarketable(text);
		if (flag && !GameVision.IsMarketable(text))
		{
			text += " [M]";
		}
		if (text.Length == 0)
		{
			text = "未知物品";
		}
		return new ItemEntry(text, text4, 0, 0, Math.Max(1, GameApiClient.GetInt(value, "count", 1)), 0f, 0f, Selected: false, text != "未知物品")
		{
			Id = GameApiClient.GetString(value, "id"),
			RarityId = text2,
			SlotId = GameApiClient.GetString(value, "slot_id"),
			ArchetypeId = GameApiClient.GetString(value, "archetype_id"),
			IsConsumable = (text4 == Theme.Consumable.Name),
			IsMarketable = flag
		};
	}

	private void UpdateChestMonitorStatus(string key, Color color)
	{
		if (InvokeRequired)
		{
			InvokeOnUi(() =>
			{
				UpdateChestMonitorStatus(key, color);
			});
			return;
		}
		_chestStatusKey = key;
		_chestStatusColor = color;
		if (!_chestStatus.IsDisposed)
		{
			_chestStatus.Text = L.T(key);
			_chestStatus.ForeColor = color;
		}
	}

	private void RefreshLast24HourCount()
	{
		if (!(_activePage != "chest") && !_stats24hValue.IsDisposed)
		{
			_stats24hValue.Text = _chestLog.OpenedLast24Hours.ToString();
		}
	}

	private bool StartBridge()
	{
		if (_bridge != null)
		{
			return true;
		}
		try
		{
			_bridge = new BridgeServer(HandleBridgeRequest);
			_bridge.Publish();
			Log($"桥接服务已启动：127.0.0.1:{_bridge.Port}");
			return true;
		}
		catch (Exception ex)
		{
			_bridge = null;
			Log("桥接服务启动失败：" + ex.Message);
			return false;
		}
	}

	private (int, string) HandleBridgeRequest(string path)
	{
		if (!(path == "/api/ping"))
		{
			if (path == "/api/status")
			{
				return (200, StatusPayload());
			}
			return (404, "NOT_FOUND");
		}
		return (200, $"SUCCESS|instance_id={_bridge?.InstanceId}\nport={_bridge?.Port}\n");
	}

	private string StatusPayload()
	{
		EnvironmentRecord environmentRecord = Deployment.LoadRecord();
		List<string> list = new List<string>
		{
			"SUCCESS",
			"game_running=" + (((object)_window == null) ? "0" : "1"),
			"game_title=" + _window?.Title,
			"engine=" + (_envFingerprint?.Engine ?? environmentRecord?.Engine ?? "unknown"),
			"exe_sha256=" + (_envFingerprint?.Sha256 ?? environmentRecord?.ExeSha256 ?? ""),
			"deployed=" + ((environmentRecord == null) ? "0" : "1")
		};
		string text = Deployment.Verify(_envFingerprint);
		list.Add("verify=" + ((text != null && text.Length > 0) ? text : "ok"));
		list.Add("warehouse_slots=" + (((object)_lastScan == null) ? "" : $"{_lastScan.SlotUsed}/{_lastScan.SlotTotal}"));
		list.Add("warehouse_items=" + (_lastScan?.Items.Count.ToString() ?? "0"));
		list.Add("chest_records=" + _chestLog.Records.Count);
		list.Add("chests_opened_total=" + _chestLog.TotalOpened);
		list.Add("chests_opened_24h=" + _chestLog.OpenedLast24Hours);
		list.Add("tradable_m_records=" + _chestLog.TradeRecords.Count);
		List<string> values = list;
		return string.Join("\n", values) + "\n";
	}

	private void ApplyFormText()
	{
		Text = L.T("app.title");
		if (_headerTitle != null)
		{
			_headerTitle.Text = L.T("header.title");
		}
		if (_brandSub != null)
		{
			_brandSub.Text = L.T("app.subtitle");
		}
		if (_reconnect != null)
		{
			_reconnect.Text = L.T("header.reconnect");
		}
	}

	private static Icon LoadAppIcon()
	{
		string text = Path.Combine(AppContext.BaseDirectory, "Assets", "appicon.ico");
		if (File.Exists(text))
		{
			try
			{
				return new Icon(text);
			}
			catch
			{
			}
		}
		return SystemIcons.Application;
	}

	private void BuildTray()
	{
		_tray.Icon = Icon;
		_tray.Text = L.T("app.title");
		_tray.Visible = true;
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.Items.Add(L.T("tray.open"), null, (object? _, EventArgs _) =>
		{
			RestoreFromTray();
		});
		contextMenuStrip.Items.Add(L.T("tray.exit"), null, (object? _, EventArgs _) =>
		{
			_reallyExit = true;
			Close();
		});
		_tray.ContextMenuStrip = contextMenuStrip;
		_tray.DoubleClick += (object? _, EventArgs _) =>
		{
			RestoreFromTray();
		};
	}

	private void RestoreFromTray()
	{
		Show();
		WindowState = FormWindowState.Normal;
		Native.Focus(Handle);
		Activate();
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		if (!_reallyExit && _settings.CloseToTray && e.CloseReason == CloseReason.UserClosing)
		{
			e.Cancel = true;
			Hide();
			_tray.BalloonTipTitle = L.T("app.title");
			_tray.BalloonTipText = L.T("tray.hint");
			_tray.ShowBalloonTip(2000);
			return;
		}
		StopAutomation(persistOff: false);
		_autoScan.Stop();
		_poll.Stop();
		_statsTimer.Stop();
		_manualChestStatsTimer.Stop();
		_watchdog.Stop();
		_manualChestStatsCts.Cancel();
		_tray.Visible = false;
		_bridge?.Dispose();
		base.OnFormClosing(e);
	}

	private void BuildShell()
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1,
			BackColor = Theme.Surface
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 248f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		Controls.Add(tableLayoutPanel);
		Panel sidebar = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.Surface,
			Padding = new Padding(16, 16, 16, 14)
		};
		sidebar.Paint += (object? _, PaintEventArgs e) =>
		{
			using Pen pen = new Pen(Theme.Border);
			e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
		};
		tableLayoutPanel.Controls.Add(sidebar, 0, 0);
		Panel panel = new Panel
		{
			Dock = DockStyle.Top,
			Height = 76
		};
		panel.Controls.Add(new PictureBox
		{
			Image = Fit(LoadAssetOrNull("appicon.png"), 46),
			SizeMode = PictureBoxSizeMode.Zoom,
			Location = new Point(0, 6),
			Size = new Size(46, 46),
			BackColor = Color.Transparent
		});
		panel.Controls.Add(new Label
		{
			Text = "Desktop Racer",
			ForeColor = Theme.Text,
			Font = Theme.UiFont(Font.FontFamily, 12.5f, FontStyle.Bold),
			AutoSize = true,
			Location = new Point(54, 12)
		});
		_brandSub.Text = L.T("app.subtitle");
		_brandSub.ForeColor = Theme.Muted;
		_brandSub.AutoSize = true;
		_brandSub.Location = new Point(55, 38);
		panel.Controls.Add(_brandSub);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			AutoSize = true,
			BackColor = Theme.Surface,
			Padding = new Padding(0, 10, 0, 0)
		};
		sidebar.Controls.Add(flowLayoutPanel);
		(string, string)[] array = new (string, string)[7]
		{
			("nav.overview", "dashboard"),
			("nav.warehouse", "warehouse"),
			("nav.chest", "chart"),
			("nav.quality", "sliders"),
			("nav.settings", "gear"),
			("nav.environment", "shield"),
			("nav.log", "log")
		};
		for (int num = 0; num < array.Length; num++)
		{
			(string, string) tuple = array[num];
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			Button button = new Button
			{
				Text = L.B(item),
				Width = 216,
				Height = 46,
				FlatStyle = FlatStyle.Flat,
				TextAlign = ContentAlignment.MiddleRight,
				BackColor = Theme.Surface,
				ForeColor = Theme.Muted,
				Font = Theme.UiFont(Font.FontFamily, 9.5f),
				Cursor = Cursors.Hand,
				Margin = new Padding(0, 2, 0, 2),
				Image = LineIcons.Create(item2, 24, Theme.Muted),
				ImageAlign = ContentAlignment.MiddleLeft,
				Padding = new Padding(14, 0, 16, 0),
				TextImageRelation = TextImageRelation.ImageBeforeText
			};
			button.FlatAppearance.BorderSize = 0;
			button.FlatAppearance.MouseOverBackColor = Theme.SurfaceAlt;
			string page = item.Substring("nav.".Length);
			button.Click += (object? _, EventArgs _) =>
			{
				ShowPage(page);
			};
			_navButtons[page] = button;
			_navKeys[page] = item2;
			flowLayoutPanel.Controls.Add(button);
		}
		sidebar.Controls.Add(panel);
		Panel panel2 = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 138,
			BackColor = Theme.SurfaceAlt,
			Padding = new Padding(12, 10, 12, 10)
		};
		_guardTitle.Text = L.B("guard.title");
		_guardTitle.ForeColor = Theme.Text;
		_guardTitle.Font = Theme.UiFont(Font.FontFamily, 9.5f, FontStyle.Bold);
		_guardTitle.AutoSize = true;
		_guardTitle.Location = new Point(12, 10);
		panel2.Controls.Add(_guardTitle);
		_guardText.ForeColor = Theme.Muted;
		_guardText.AutoSize = false;
		_guardText.Size = new Size(214, 92);
		_guardText.Location = new Point(12, 34);
		panel2.Controls.Add(_guardText);
		sidebar.Controls.Add(panel2);
		RefreshGuardText();
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 3,
			BackColor = Theme.PageBg
		};
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 1, 0);
		Panel header = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.Surface
		};
		header.Paint += (object? _, PaintEventArgs e) =>
		{
			using Pen pen = new Pen(Theme.Border);
			e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
		};
		tableLayoutPanel2.Controls.Add(header, 0, 0);
		_headerTitle.Text = L.B("header.title");
		_headerTitle.ForeColor = Theme.Text;
		_headerTitle.Font = Theme.UiFont(Font.FontFamily, 14f, FontStyle.Bold);
		_headerTitle.AutoSize = true;
		_headerTitle.Location = new Point(24, 11);
		header.Controls.Add(_headerTitle);
		_connection.AutoSize = false;
		_connection.AutoEllipsis = true;
		_connection.Font = Theme.UiFont(Font.FontFamily, 9.5f, FontStyle.Bold);
		header.Controls.Add(_connection);
		_ocrLabel.AutoSize = false;
		_ocrLabel.AutoEllipsis = true;
		_ocrLabel.ForeColor = Theme.Muted;
		_ocrLabel.Font = Theme.UiFont(Font.FontFamily, 8.5f);
		header.Controls.Add(_ocrLabel);
		_reconnect.Text = L.T("header.reconnect");
		CopyButtonStyle(_reconnect, MakeButton("", primary: false));
		_reconnect.Size = new Size(126, 30);
		_reconnect.Click += async (object? _, EventArgs _) =>
		{
			await InitializeAssistantAsync();
		};
		header.Controls.Add(_reconnect);
		_topAutomationState.AutoSize = false;
		_topAutomationState.TextAlign = ContentAlignment.MiddleRight;
		_topAutomationState.Font = Theme.UiFont(Font.FontFamily, 9f, FontStyle.Bold);
		header.Controls.Add(_topAutomationState);
		_topLaunchButton.Text = L.T("header.launchGame");
		CopyButtonStyle(_topLaunchButton, MakeButton("", primary: false));
		_topLaunchButton.Click += async (object? _, EventArgs _) =>
		{
			await LaunchGameAsync();
		};
		header.Controls.Add(_topLaunchButton);
		_topStartButton.Text = L.T("header.startAll");
		CopyButtonStyle(_topStartButton, MakeButton("", primary: true));
		_topStartButton.Click += async (object? _, EventArgs _) =>
		{
			await StartMonitoringAsync();
		};
		header.Controls.Add(_topStartButton);
		_topStopButton.Text = L.T("header.stopAll");
		CopyButtonStyle(_topStopButton, MakeButton("", primary: false));
		_topStopButton.Click += (object? _, EventArgs _) =>
		{
			StopAllAutomation();
		};
		header.Controls.Add(_topStopButton);
		header.Resize += (object? _, EventArgs _) =>
		{
			LayoutHeader(header);
		};
		LayoutHeader(header);
		_pageHost.Dock = DockStyle.Fill;
		_pageHost.BackColor = Theme.PageBg;
		_pageHost.Padding = new Padding(20, 14, 20, 12);
		tableLayoutPanel2.Controls.Add(_pageHost, 0, 1);
		TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1,
			BackColor = Theme.Surface
		};
		tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190f));
		tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		_footer.ForeColor = Theme.Muted;
		_footer.Dock = DockStyle.Fill;
		_footer.TextAlign = ContentAlignment.MiddleLeft;
		_footer.Padding = new Padding(24, 0, 0, 0);
		_footer.BackColor = Theme.Surface;
		tableLayoutPanel3.Controls.Add(_footer, 0, 0);
		_footerCredits.ForeColor = Theme.Muted;
		_footerCredits.Dock = DockStyle.Fill;
		_footerCredits.TextAlign = ContentAlignment.MiddleRight;
		_footerCredits.Padding = new Padding(0, 0, 18, 0);
		_footerCredits.BackColor = Theme.Surface;
		tableLayoutPanel3.Controls.Add(_footerCredits, 1, 0);
		tableLayoutPanel2.Controls.Add(tableLayoutPanel3, 0, 2);
		UpdateConnection();
		RefreshAutomationChrome();
	}

	private void LayoutHeader(Panel header)
	{
		int width = header.ClientSize.Width;
		if (width >= 160)
		{
			int num = width - 24;
			_reconnect.SetBounds(num - 126, 12, 126, 30);
			num = _reconnect.Left - 8;
			_topStopButton.SetBounds(num - 100, 12, 100, 30);
			num = _topStopButton.Left - 8;
			_topStartButton.SetBounds(num - 100, 12, 100, 30);
			num = _topStartButton.Left - 8;
			_topLaunchButton.SetBounds(num - 100, 12, 100, 30);
			_connection.SetBounds(26, 46, Math.Max(120, width - 52), 21);
			_ocrLabel.SetBounds(26, 70, Math.Max(120, width - 272), 18);
			_topAutomationState.SetBounds(Math.Max(26, width - 196), 70, 172, 18);
		}
	}

	private void RefreshGuardText()
	{
		List<string> list = new List<string>
		{
			string.Format(L.T("guard.delay"), _settings.DelayMinSeconds, _settings.DelayMaxSeconds),
			L.T("guard.reconnect"),
			L.T("guard.skip")
		};
		list.Add(_settings.AntiDropEnabled ? string.Format(L.T("guard.antiDrop"), _settings.AntiDropCheckMinutes) : L.T("guard.antiDropOff"));
		_guardText.Text = string.Join(Environment.NewLine, list);
	}

	private void SyncWatchdog()
	{
		_watchdog.Interval = Math.Clamp(_settings.AntiDropCheckMinutes, 1, 60) * 60000;
		if (_settings.AntiDropEnabled)
		{
			_watchdog.Start();
		}
		else
		{
			_watchdog.Stop();
		}
	}

	private async Task<(bool Ok, string Message)> EnsurePluginAsync(string gameExecutable)
	{
		if (PluginInstall.BundledHash == null)
		{
			return (Ok: false, Message: "游戏接口资源缺失：Assets/game-plugin。");
		}
		PluginInstall.EnsureResult ensureResult = await Task.Run(() => PluginInstall.Ensure(gameExecutable));
		if (!ensureResult.Ok)
		{
			return (Ok: false, Message: "游戏环境部署失败：" + ensureResult.Message);
		}
		_pluginDeployedHash = PluginInstall.DeployedHash(Path.GetDirectoryName(PluginInstall.GetSourceExecutablePath(gameExecutable))) ?? "";
		_steamLaunchPending = !ensureResult.SteamLaunchReady;
		Log(ensureResult.Changed ? $"游戏接口已部署：v{"1.0.2"}（协议 {3}）。" : $"游戏接口与助手一致：v{"1.0.2"}（协议 {3}）。");
		Log(ensureResult.Message);
		Native.WindowInfo windowInfo = GameReader.FindGameWindow();
		GameApiClient.SetExpectedProcessId((object)windowInfo == null ? 0 : windowInfo.ProcessId);
		GameApiRuntimeStatus gameApiRuntimeStatus = GameApiClient.ReadRuntimeStatus((object)windowInfo == null ? 0 : windowInfo.ProcessId);
		if ((object)gameApiRuntimeStatus != null && (object)windowInfo != null && gameApiRuntimeStatus.HostPid == windowInfo.ProcessId)
		{
			if (gameApiRuntimeStatus.Protocol != 3 || gameApiRuntimeStatus.PluginVersion != "1.0.2")
			{
				return (Ok: false, Message: $"游戏内 API 版本不一致（v{gameApiRuntimeStatus.PluginVersion}/协议 {gameApiRuntimeStatus.Protocol}，助手 v{"1.0.2"}/协议 {3}）。");
			}
			_pluginRuntimeVersion = gameApiRuntimeStatus.PluginVersion;
			_pluginRuntimeProtocol = gameApiRuntimeStatus.Protocol;
			Log($"游戏内 API 已加载：v{gameApiRuntimeStatus.PluginVersion}（协议 {gameApiRuntimeStatus.Protocol}），进程 {gameApiRuntimeStatus.HostPid}，游戏连接 {gameApiRuntimeStatus.ConnectionState}。");
		}
		else
		{
			Log(ensureResult.SteamLaunchReady
				? "Steam 游戏环境已就绪；可以启动游戏。"
				: "游戏环境已准备；当前游戏退出后，助手会完成 Steam 启动接入。");
		}
		return (Ok: true, Message: "plugin.deployed");
	}

	private void TryCompleteSteamLaunchPatch()
	{
		if (!_initialized || _initializing || !_steamLaunchPending || _steamLaunchSyncRunning || DateTime.Now < _nextSteamLaunchAttemptAt)
		{
			return;
		}
		string executable = _envFingerprint?.ExecutablePath ?? _settings.GameExePath;
		if (string.IsNullOrWhiteSpace(executable))
		{
			return;
		}
		_steamLaunchSyncRunning = true;
		_nextSteamLaunchAttemptAt = DateTime.Now.AddSeconds(15.0);
		_ = CompleteSteamLaunchPatchAsync(executable);
	}

	private async Task CompleteSteamLaunchPatchAsync(string executable)
	{
		try
		{
			PluginInstall.EnsureResult result = await Task.Run(() => PluginInstall.Ensure(executable));
			if (result.Ok)
			{
				_steamLaunchPending = !result.SteamLaunchReady;
				if (result.SteamLaunchReady)
				{
					Log(L.T("log.steamLaunchReady"));
				}
				else
				{
					Log(L.T("log.steamLaunchPending"));
				}
			}
			else
			{
				Log("Steam 启动接入失败：" + result.Message);
			}
		}
		catch (Exception ex)
		{
			Log("Steam 启动接入失败：" + ex.Message);
		}
		finally
		{
			_steamLaunchSyncRunning = false;
			UpdateConnection();
			RefreshEnvironment();
		}
	}

	private void NoteGameFeedback()
	{
		_lastGameFeedbackAt = DateTime.Now;
		_sendFailureStreak = 0;
	}

	private static bool IsConnectionFailure(string message)
	{
		if (!message.Contains("API", StringComparison.OrdinalIgnoreCase) && !message.Contains("游戏内核尚未连接", StringComparison.Ordinal) && !message.Contains("提前关闭", StringComparison.Ordinal))
		{
			return message.Contains("等待游戏内 API 返回结果超时", StringComparison.Ordinal);
		}
		return true;
	}

	private void LocateGame()
	{
		if (!_initialized)
		{
			_window = null;
			UpdateConnection();
			return;
		}
		Native.WindowInfo windowInfo = GameReader.FindGameWindow();
		GameApiClient.SetExpectedProcessId((object)windowInfo == null ? 0 : windowInfo.ProcessId);
		GameApiRuntimeStatus runtimeStatus = GameApiClient.ReadRuntimeStatus((object)windowInfo == null ? 0 : windowInfo.ProcessId);
		_window = (((object)windowInfo != null && (object)runtimeStatus != null && runtimeStatus.HostPid == windowInfo.ProcessId && runtimeStatus.KernelReady) ? windowInfo : null);
		UpdateConnection();
		Log(((object)_window == null) ? "等待游戏接口连接。" : $"已连接游戏接口：进程 {_window.ProcessId}，端口 127.0.0.1:{runtimeStatus.Port}。");
	}

	private void UpdateConnection()
	{
		Native.WindowInfo windowInfo = GameReader.FindGameWindow();
		GameApiClient.SetExpectedProcessId((object)windowInfo == null ? 0 : windowInfo.ProcessId);
		GameApiRuntimeStatus runtimeStatus = GameApiClient.ReadRuntimeStatus((object)windowInfo == null ? 0 : windowInfo.ProcessId);
		bool flag = (object)windowInfo != null && (object)runtimeStatus != null && runtimeStatus.HostPid == windowInfo.ProcessId && runtimeStatus.KernelReady;
		if (!_topLaunchButton.IsDisposed)
		{
			_topLaunchButton.Text = L.T("header.launchGame");
		}
		if (_initializing)
		{
			_connection.Text = "●  " + L.T("header.initializing");
			_connection.ForeColor = Theme.Warning;
		}
		else if (!_initialized)
		{
			_connection.Text = "●  " + L.T("header.initFailed");
			_connection.ForeColor = Theme.Warning;
		}
		else if ((object)windowInfo == null)
		{
			_connection.Text = "●  " + L.T("header.noGame");
			_connection.ForeColor = Theme.Muted;
		}
		else if ((object)windowInfo != null && !flag && _steamLaunchPending)
		{
			_connection.Text = "●  " + L.T("header.originalGame");
			_connection.ForeColor = Theme.Warning;
		}
		else if (!flag)
		{
			_connection.Text = "●  " + L.T("header.waitingPlugin");
			_connection.ForeColor = Theme.Warning;
		}
		else if ((object)_window == null)
		{
			_connection.Text = "●  " + L.T("header.checkingPlugin");
			_connection.ForeColor = Theme.Warning;
		}
		else if (!runtimeStatus.Connected)
		{
			_connection.Text = "●  " + string.Format(L.T("header.pluginConnected"), DescribeGameConnectionState(runtimeStatus.ConnectionState));
			_connection.ForeColor = Theme.Warning;
		}
		else
		{
			_connection.Text = "●  " + string.Format(L.T("header.gameConnected"), windowInfo.Title);
			_connection.ForeColor = Theme.Good;
		}
		Label ocrLabel = _ocrLabel;
		string text;
		if (!_initialized && !_initializing && _initializationError.Length > 0)
		{
			text = _initializationError;
		}
		else if ((object)windowInfo == null)
		{
			text = L.T("header.noGameDetail");
		}
		else if ((object)windowInfo != null && !flag && _steamLaunchPending)
		{
			text = L.T("header.originalGameDetail");
		}
		else
		{
			text = ((!flag) ? L.T("header.heartbeatWaiting") : string.Format(L.T("header.apiDetails"), runtimeStatus.PluginVersion, runtimeStatus.Protocol, DateTimeOffset.FromUnixTimeMilliseconds(runtimeStatus.HeartbeatUnixMs).ToLocalTime().ToString("HH:mm:ss"), runtimeStatus.Port));
		}
		ocrLabel.Text = text;
		_footer.Text = AppSettings.SettingsPath;
		_footerCredits.Text = "v" + AppVersion + " · by Awan";
		RefreshAutomationChrome();
	}

	private static string DescribeGameConnectionState(string state)
	{
		string text = state.ToLowerInvariant() switch
		{
			"connecting" => "header.gameConnecting", 
			"reconnecting" => "header.gameReconnecting", 
			"offline" => "header.gameOffline", 
			_ => "", 
		};
		if (text.Length <= 0)
		{
			return string.Format(L.T("header.gameStateUnknown"), state);
		}
		return L.T(text);
	}

	private void ShowPage(string page, bool forceRebuild = false)
	{
		if (!forceRebuild && page == _activePage && _pageHost.Controls.Count > 0)
		{
			return;
		}
		_activePage = page;
		foreach (var (text2, button2) in _navButtons)
		{
			bool flag = text2 == page;
			button2.BackColor = (flag ? Theme.AccentSoft : Theme.Surface);
			button2.ForeColor = (flag ? Theme.Accent : Theme.Muted);
			button2.Font = Theme.UiFont(Font.FontFamily, 9.5f, flag ? FontStyle.Bold : FontStyle.Regular);
			button2.Image?.Dispose();
			button2.Image = LineIcons.Create(_navKeys[text2], 24, flag ? Theme.Accent : Theme.Muted);
		}
		Control[] oldPages = _pageHost.Controls.Cast<Control>().ToArray();
		_pageHost.Controls.Clear();
		foreach (Control oldPage in oldPages)
		{
			oldPage.Dispose();
		}
		if (page == null)
		{
			return;
		}
		switch (page.Length)
		{
		case 8:
			switch (page[0])
			{
			case 'o':
				if (page == "overview")
				{
					BuildOverview();
				}
				break;
			case 's':
				if (page == "settings")
				{
					BuildSettings();
				}
				break;
			}
			break;
		case 9:
			if (page == "warehouse")
			{
				BuildWarehouse();
			}
			break;
		case 5:
			if (page == "chest")
			{
				BuildChest();
			}
			break;
		case 7:
			if (page == "quality")
			{
				BuildQuality();
			}
			break;
		case 11:
			if (page == "environment")
			{
				BuildEnvironment();
			}
			break;
		case 3:
			if (page == "log")
			{
				BuildLog();
			}
			break;
		case 4:
		case 6:
		case 10:
			break;
		}
	}

	private void BuildOverview()
	{
		_progressBar = new ProgressBar();
		_progressLabel = new Label();
		_currentTaskLabel = new Label();
		_nextTaskLabel = new Label();
		_executionStatusLabel = new Label();
		_executionDetailLabel = new Label();
		FlowLayoutPanel flowLayoutPanel = MakePage();
		flowLayoutPanel.Controls.Add(MakeHeading(L.T("overview.title"), L.T("overview.desc")));
		Panel panel = MakeCard(180);
		panel.Controls.Add(MakeCardTitle(L.T("overview.status"), 18, 14));
		_executionStatusLabel.ForeColor = Theme.Good;
		_executionStatusLabel.Font = Theme.UiFont(Font.FontFamily, 10.5f, FontStyle.Bold);
		_executionStatusLabel.AutoSize = true;
		_executionStatusLabel.Location = new Point(20, 40);
		panel.Controls.Add(_executionStatusLabel);
		_executionDetailLabel.ForeColor = Theme.Muted;
		_executionDetailLabel.AutoSize = false;
		_executionDetailLabel.AutoEllipsis = true;
		_executionDetailLabel.Size = new Size(Math.Max(360, panel.Width - 340), 18);
		_executionDetailLabel.Location = new Point(20, 60);
		panel.Controls.Add(_executionDetailLabel);
		_currentTaskLabel.ForeColor = Theme.Text;
		_currentTaskLabel.AutoSize = false;
		_currentTaskLabel.Size = new Size(Math.Max(360, panel.Width - 340), 20);
		_currentTaskLabel.Location = new Point(20, 80);
		panel.Controls.Add(_currentTaskLabel);
		_nextTaskLabel.ForeColor = Theme.Muted;
		_nextTaskLabel.AutoSize = false;
		_nextTaskLabel.Size = new Size(Math.Max(360, panel.Width - 340), 20);
		_nextTaskLabel.Location = new Point(20, 100);
		panel.Controls.Add(_nextTaskLabel);
		RefreshTaskLabels();
		_progressLabel.Text = (_executionEnabled ? L.T("run.monitoring") : L.T("run.ready"));
		_progressLabel.ForeColor = Theme.Muted;
		_progressLabel.AutoSize = true;
		_progressLabel.Location = new Point(20, 150);
		panel.Controls.Add(_progressLabel);
		_progressBar.Location = new Point(20, 126);
		_progressBar.Size = new Size(Math.Max(360, panel.Width - 340), 14);
		_progressBar.Style = ProgressBarStyle.Continuous;
		panel.Controls.Add(_progressBar);
		_stopButton = MakeButton(L.T("run.stop"), primary: false);
		_stopButton.SetBounds(panel.Width - 160, 72, 130, 32);
		_stopButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		_stopButton.TabStop = false;
		_stopButton.Click += (object? _, EventArgs _) =>
		{
			StopAutomation();
		};
		panel.Controls.Add(_stopButton);
		flowLayoutPanel.Controls.Add(panel);
		RefreshAutomationChrome();
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			Height = 154,
			ColumnCount = 4,
			RowCount = 1,
			BackColor = Theme.PageBg,
			Margin = new Padding(0, 0, 0, 12)
		};
		for (int num = 0; num < 4; num++)
		{
			tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
		}
		tableLayoutPanel.Controls.Add(MakeStatCard(L.T("overview.chestCheck"), string.Format(L.T("overview.everyMinutes"), _settings.ChestCheckMinutes), L.T("overview.chestNote"), null, L.T("overview.runNow"), RunImmediateChestCheckAsync, (Button value) =>
		{
			_chestImmediateButton = value;
		}), 0, 0);
		tableLayoutPanel.Controls.Add(MakeStatCard(L.T("overview.whCheck"), string.Format(L.T("overview.everyMinutes"), _settings.WarehouseCheckMinutes), string.Format(L.T("overview.whNote"), _settings.WarehouseThresholdPercent), null, L.T("overview.runNow"), RunImmediateWarehouseCheckAsync, (Button value) =>
		{
			_warehouseImmediateButton = value;
		}), 1, 0);
		tableLayoutPanel.Controls.Add(MakeStatCard(L.T("overview.records"), _chestLog.TotalOpened.ToString(), L.T("overview.recordsNote")), 2, 0);
		tableLayoutPanel.Controls.Add(MakeStatCard(L.T("repair.title"), string.Format(L.T("overview.everyMinutes"), _settings.RepairCheckMinutes), string.Format(L.T("overview.repairNote"), _settings.RepairThresholdPercent), null, L.T("overview.runNow"), RunImmediateRepairCheckAsync, (Button value) =>
		{
			_repairImmediateButton = value;
		}), 3, 0);
		flowLayoutPanel.Controls.Add(tableLayoutPanel);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			Height = 442,
			ColumnCount = 1,
			RowCount = 2,
			BackColor = Theme.PageBg,
			Margin = new Padding(0, 0, 0, 12)
		};
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 170f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 272f));
		Panel panel2 = MakeCard(260);
		panel2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		panel2.Controls.Add(MakeCardTitle(L.T("overview.summary"), 18, 16));
		int num2 = 52;
		foreach (QualityRule qualityRule in _settings.QualityRules)
		{
			List<string> list = new List<string>();
			if (qualityRule.Favorite)
			{
				list.Add(L.T("quality.favorite"));
			}
			if (qualityRule.Dismantle)
			{
				list.Add(L.T("quality.dismantle"));
			}
			if (qualityRule.Sell)
			{
				list.Add(L.T("quality.sell"));
			}
			string text = ((list.Count == 0) ? L.T("overview.none") : string.Join(" · ", list));
			panel2.Controls.Add(new Label
			{
				Text = "■  " + L.T("quality." + QualityKey(qualityRule.Quality)),
				ForeColor = Theme.ColorOf(qualityRule.Quality),
				AutoSize = true,
				Location = new Point(20, num2),
				Font = Theme.UiFont(Font.FontFamily, 9.5f, FontStyle.Bold)
			});
			panel2.Controls.Add(new Label
			{
				Text = text,
				ForeColor = Theme.Muted,
				AutoSize = true,
				Location = new Point(150, num2)
			});
			num2 += 29;
		}
		tableLayoutPanel2.Controls.Add(panel2, 0, 1);
		Panel panel3 = MakeCard(158);
		panel3.Controls.Add(MakeCardTitle(L.T("overview.disclaimerTitle"), 18, 13));
		Panel links = new Panel
		{
			Size = new Size(292, 40),
			Location = new Point(panel3.Width - 310, 6),
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			BackColor = Theme.Surface
		};
		links.Paint += (object? _, PaintEventArgs e) =>
		{
			using Pen pen = new Pen(Color.FromArgb(235, 166, 178));
			e.Graphics.DrawRectangle(pen, 0, 0, links.Width - 1, links.Height - 1);
		};
		Button button = MakeDisclaimerLinkButton(L.T("overview.disclaimerGithub"), "github", Color.FromArgb(28, 31, 35), Theme.Text, 90, () =>
		{
			OpenExternalUrl("https://github.com/buguniaoOVO/DesktopRacerHelper/releases/latest");
		});
		button.Location = new Point(7, 5);
		links.Controls.Add(button);
		Button button2 = MakeDisclaimerLinkButton(L.T("overview.disclaimerQQ"), "qq", Color.FromArgb(45, 108, 176), Theme.Text, 80, () =>
		{
			OpenExternalUrl("https://qm.qq.com/q/a4N90riKrY");
		});
		button2.Location = new Point(103, 5);
		links.Controls.Add(button2);
		Button button3 = MakeDisclaimerLinkButton(L.T("overview.disclaimerSponsor"), "heart", Color.FromArgb(226, 73, 82), Color.FromArgb(205, 58, 68), 80, () =>
		{
			OpenExternalUrl("https://afdian.com/a/Awan0v0?utm_source=copylink&utm_medium=link");
		});
		button3.Location = new Point(189, 5);
		links.Controls.Add(button3);
		panel3.Controls.Add(links);
		Color foreColor = Color.FromArgb(190, 55, 55);
		panel3.Controls.Add(new Label
		{
			Text = L.T("overview.disclaimerFree"),
			ForeColor = foreColor,
			AutoEllipsis = true,
			Size = new Size(panel3.Width - 36, 18),
			Location = new Point(18, 48),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		});
		panel3.Controls.Add(new Label
		{
			Text = L.T("overview.disclaimerOfficial"),
			ForeColor = foreColor,
			AutoEllipsis = true,
			Size = new Size(panel3.Width - 36, 18),
			Location = new Point(18, 70),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		});
		panel3.Controls.Add(new Label
		{
			Text = L.T("overview.disclaimerRisk"),
			ForeColor = foreColor,
			AutoEllipsis = true,
			Size = new Size(panel3.Width - 36, 18),
			Location = new Point(18, 92),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		});
		panel3.Controls.Add(new Label
		{
			Text = L.T("overview.disclaimerGroup"),
			ForeColor = Theme.Muted,
			AutoSize = true,
			Location = new Point(18, 123)
		});
		tableLayoutPanel2.Controls.Add(panel3, 0, 0);
		flowLayoutPanel.Controls.Add(tableLayoutPanel2);
		_pageHost.Controls.Add(flowLayoutPanel);
		RefreshAutomationChrome();
	}

	private static string QualityKey(string quality)
	{
		return quality switch
		{
			"普通" => "common", 
			"优秀" => "uncommon", 
			"稀有" => "rare", 
			"史诗" => "epic", 
			"传说" => "legendary", 
			"神话" => "mythic", 
			"异域" => "exotic", 
			_ => "mythic", 
		};
	}

	private void UpdateWarehouseFilterButton()
	{
		if (!_warehouseFilterButton.IsDisposed)
		{
			bool flag = _warehouseFilterMode == "marketable";
			_warehouseFilterButton.BackColor = (flag ? Theme.AccentSoft : Theme.Surface);
			_warehouseFilterButton.ForeColor = (flag ? Theme.Accent : Theme.Text);
			_warehouseFilterButton.FlatAppearance.BorderColor = (flag ? Theme.Accent : Theme.BorderStrong);
		}
	}

	private void BuildWarehouse()
	{
		_slotLabel = new Label();
		_itemList = new FlowLayoutPanel();
		_warehousePriceStatus = new Label();
		_warehouseMinutes = new NumericUpDown();
		_warehouseThreshold = new NumericUpDown();
		FlowLayoutPanel flowLayoutPanel = MakePage();
		flowLayoutPanel.Controls.Add(MakeHeading(L.T("warehouse.title"), L.T("warehouse.desc")));
		Panel panel = MakeCard(334);
		panel.Controls.Add(MakeCardTitle(L.T("warehouse.items"), 18, 14));
		_warehousePriceStatus.Text = L.T("warehouse.priceCacheNote");
		_warehousePriceStatus.ForeColor = Theme.Muted;
		_warehousePriceStatus.Font = Theme.UiFont(Font.FontFamily, 8.5f);
		_warehousePriceStatus.AutoSize = true;
		_warehousePriceStatus.BackColor = Theme.SurfaceAlt;
		_warehousePriceStatus.Padding = new Padding(6, 2, 6, 2);
		_warehousePriceStatus.Location = new Point(148, 14);
		panel.Controls.Add(_warehousePriceStatus);
		_warehouseFilterButton = MakeButton(L.T("warehouse.filterTradable"), primary: false);
		_warehouseFilterButton.SetBounds(18, 42, 110, 32);
		_warehouseFilterButton.Click += (object? _, EventArgs _) =>
		{
			_warehouseFilterMode = ((_warehouseFilterMode == "marketable") ? "all" : "marketable");
			UpdateWarehouseFilterButton();
			RenderWarehouse();
		};
		panel.Controls.Add(_warehouseFilterButton);
		UpdateWarehouseFilterButton();
		Label label = MakeFieldLabel(L.T("warehouse.sortLabel"), 142, 49);
		label.Size = new Size(32, 20);
		label.Font = Theme.UiFont(Font.FontFamily, 9.5f);
		label.TextAlign = ContentAlignment.MiddleLeft;
		panel.Controls.Add(label);
		_warehouseSortBox = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			FlatStyle = FlatStyle.Standard,
			Font = Theme.UiFont(Font.FontFamily, 9.5f),
			Size = new Size(160, 32),
			Location = new Point(178, 42),
			Anchor = (AnchorStyles.Top | AnchorStyles.Right),
			BackColor = Theme.Surface,
			ForeColor = Theme.Text
		};
		_warehouseSortBox.Items.AddRange(new object[4]
		{
			L.T("warehouse.sortGame"),
			L.T("warehouse.sortQuality"),
			L.T("warehouse.sortPriceDesc"),
			L.T("warehouse.sortPriceAsc")
		});
		ComboBox warehouseSortBox = _warehouseSortBox;
		warehouseSortBox.SelectedIndex = _warehouseSortMode switch
		{
			"quality" => 1, 
			"price-desc" => 2, 
			"price-asc" => 3, 
			_ => 0, 
		};
		_warehouseSortBox.SelectedIndexChanged += (object? _, EventArgs _) =>
		{
			_warehouseSortMode = _warehouseSortBox.SelectedIndex switch
			{
				1 => "quality", 
				2 => "price-desc", 
				3 => "price-asc", 
				_ => "game", 
			};
			RenderWarehouse();
		};
		panel.Controls.Add(_warehouseSortBox);
		_refreshWarehouseButton = MakeButton(L.T("warehouse.read"), primary: true);
		_refreshWarehouseButton.SetBounds(panel.Width - 246, 42, 110, 32);
		_refreshWarehouseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		_refreshWarehouseButton.Click += async (object? _, EventArgs _) =>
		{
			await ReadWarehouseAsync();
		};
		panel.Controls.Add(_refreshWarehouseButton);
		_queryAllPricesButton = MakeButton(L.T("warehouse.priceAll"), primary: false);
		_queryAllPricesButton.SetBounds(panel.Width - 128, 42, 110, 32);
		_queryAllPricesButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		_queryAllPricesButton.Click += async (object? _, EventArgs _) =>
		{
			await QueryAllPricesAsync();
		};
		panel.Controls.Add(_queryAllPricesButton);
		_itemList = new FlowLayoutPanel
		{
			Location = new Point(18, 80),
			Size = new Size(panel.Width - 40, 238),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			AutoScroll = true,
			BackColor = Theme.Surface,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = true
		};
		_itemList.Controls.Add(new Label
		{
			Text = L.T("warehouse.empty"),
			ForeColor = Theme.Muted,
			AutoSize = true
		});
		panel.Controls.Add(_itemList);
		flowLayoutPanel.Controls.Add(panel);
		Panel panel2 = MakeCard(214);
		panel2.Controls.Add(MakeCardTitle(L.T("warehouse.capacity"), 18, 14));
		_slotLabel.Text = L.T("warehouse.slotsUnread");
		_slotLabel.ForeColor = Theme.Text;
		_slotLabel.Font = Theme.UiFont(Font.FontFamily, 14f, FontStyle.Bold);
		_slotLabel.AutoSize = true;
		_slotLabel.Location = new Point(20, 44);
		panel2.Controls.Add(_slotLabel);
		panel2.Controls.Add(MakeFieldLabel(L.T("warehouse.intervalLabel"), 20, 82));
		ConfigureNumber(_warehouseMinutes, 1, 240, _settings.WarehouseCheckMinutes, 20, 104, 88);
		panel2.Controls.Add(_warehouseMinutes);
		panel2.Controls.Add(MakeFieldLabel(L.T("warehouse.thresholdLabel"), 132, 82));
		ConfigureNumber(_warehouseThreshold, 10, 100, _settings.WarehouseThresholdPercent, 132, 104, 88);
		panel2.Controls.Add(_warehouseThreshold);
		Button button = MakeButton(L.T("warehouse.save"), primary: false);
		button.SetBounds(20, 164, 120, 30);
		button.Click += (object? _, EventArgs _) =>
		{
			SaveWarehouseSettings();
		};
		panel2.Controls.Add(button);
		flowLayoutPanel.Controls.Add(panel2);
		_pageHost.Controls.Add(flowLayoutPanel);
		RenderWarehouse();
		if (!_warehouseIconScanAttempted && !_busy)
		{
			PluginAutomationEngine engine = Engine;
			if (engine != null && !engine.IsRunning && (object)_window != null && ((object)_lastScan == null || _lastScan.Items.Any((ItemEntry item) => item.IconPng == null)))
			{
				_ = ReadWarehouseAsync();
			}
		}
	}

	private void BuildChest()
	{
		FlowLayoutPanel flowLayoutPanel = MakePage();
		flowLayoutPanel.Controls.Add(MakeHeading(L.T("chest.title"), L.T("chest.desc")));
		Panel panel = new Panel
		{
			Height = 38,
			Width = Math.Max(660, _pageHost.ClientSize.Width - 52),
			BackColor = Theme.PageBg,
			Margin = new Padding(0, 0, 0, 8)
		};
		_statsChestTab = MakeButton(L.T("stats.tabChest"), primary: false);
		_statsChestTab.SetBounds(0, 1, 92, 34);
		_statsChestTab.Click += (object? _, EventArgs _) =>
		{
			_statsTab = "chest";
			RefreshStatisticsView();
		};
		panel.Controls.Add(_statsChestTab);
		_statsTradeTab = MakeButton(L.T("stats.tabTrade"), primary: false);
		_statsTradeTab.SetBounds(100, 1, 112, 34);
		_statsTradeTab.Click += (object? _, EventArgs _) =>
		{
			_statsTab = "trade";
			RefreshStatisticsView();
		};
		panel.Controls.Add(_statsTradeTab);
		Button button = MakeButton(L.T("stats.clear"), primary: false);
		button.SetBounds(panel.Width - 128, 1, 120, 34);
		button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		button.Click += (object? _, EventArgs _) =>
		{
			if (_manualChestStatsRunning)
			{
				Log("手动开箱统计正在同步，请稍后再清空。");
			}
			else if (MessageBox.Show(L.T("stats.clearConfirm"), L.T("stats.clear"), MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) == DialogResult.OK)
			{
				_chestLog.Clear();
				RefreshStatisticsView();
				string lastSaveError = _chestLog.LastSaveError;
				if (lastSaveError != null && lastSaveError.Length > 0)
				{
					Log("统计已清空但保存失败：" + lastSaveError);
				}
				else
				{
					Log(L.T("log.recordsCleared"));
				}
			}
		};
		panel.Controls.Add(button);
		flowLayoutPanel.Controls.Add(panel);
		_statsContent = new Panel
		{
			Height = 520,
			Width = Math.Max(660, _pageHost.ClientSize.Width - 52),
			BackColor = Theme.PageBg,
			Margin = new Padding(0)
		};
		flowLayoutPanel.Controls.Add(_statsContent);
		_pageHost.Controls.Add(flowLayoutPanel);
		RefreshStatisticsView();
	}

	private void RefreshStatisticsView()
	{
		if (IsDisposed || Disposing)
		{
			return;
		}
		if (InvokeRequired)
		{
			InvokeOnUi(RefreshStatisticsView);
			return;
		}
		Panel statsContent = _statsContent;
		if (statsContent.IsDisposed || statsContent.Disposing || _activePage != "chest" || _statsChestTab.IsDisposed || _statsTradeTab.IsDisposed || _statsRefreshing)
		{
			return;
		}
		_statsRefreshing = true;
		statsContent.SuspendLayout();
		try
		{
			_statsChestTab.BackColor = ((_statsTab == "chest") ? Theme.AccentSoft : Theme.Surface);
			_statsChestTab.ForeColor = ((_statsTab == "chest") ? Theme.Accent : Theme.Muted);
			_statsTradeTab.BackColor = ((_statsTab == "trade") ? Theme.AccentSoft : Theme.Surface);
			_statsTradeTab.ForeColor = ((_statsTab == "trade") ? Theme.Accent : Theme.Muted);
			foreach (Control item in statsContent.Controls.Cast<Control>().ToList())
			{
				statsContent.Controls.Remove(item);
				item.Dispose();
			}
			if (_statsTab == "trade")
			{
				BuildTradeStatistics();
			}
			else
			{
				BuildChestStatistics();
			}
		}
		catch (ObjectDisposedException ex)
		{
			Log("统计页面切换时跳过已释放控件的刷新：" + ex.Message);
		}
		finally
		{
			if (!statsContent.IsDisposed && !statsContent.Disposing)
			{
				statsContent.ResumeLayout(performLayout: true);
			}
			_statsRefreshing = false;
		}
	}

	private void BuildChestStatistics()
	{
		_chestStatus = new Label();
		_chestStatus.Text = L.T(_chestStatusKey);
		_chestStatus.ForeColor = _chestStatusColor;
		_chestStatus.Font = Theme.UiFont(Font.FontFamily, 9.5f, FontStyle.Bold);
		_chestStatus.AutoSize = true;
		_chestStatus.Location = new Point(4, 8);
		_statsContent.Controls.Add(_chestStatus);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Location = new Point(0, 40),
			Size = new Size(_statsContent.Width, 112),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			ColumnCount = 2,
			RowCount = 1,
			BackColor = Theme.PageBg
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel.Controls.Add(MakeStatCard(L.T("stats.opened24h"), _chestLog.OpenedLast24Hours.ToString(), L.T("stats.opened24hNote"), (Label valueLabel) =>
		{
			_stats24hValue = valueLabel;
		}), 0, 0);
		tableLayoutPanel.Controls.Add(MakeStatCard(L.T("stats.openedTotal"), _chestLog.TotalOpened.ToString(), L.T("stats.openedTotalNote")), 1, 0);
		_statsContent.Controls.Add(tableLayoutPanel);
		Panel panel = MakeCard(340);
		panel.Location = new Point(0, 160);
		panel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		panel.Controls.Add(MakeCardTitle(L.T("stats.qualityTitle"), 18, 14));
		_statsChestSummary = new Label();
		_statsChestSummary.Text = string.Format(L.T("stats.dropTotal"), _chestLog.TotalDrops);
		_statsChestSummary.ForeColor = Theme.Muted;
		_statsChestSummary.AutoSize = true;
		_statsChestSummary.Location = new Point(180, 17);
		panel.Controls.Add(_statsChestSummary);
		_statsQualityGrid = CreateStatisticsGrid();
		_statsQualityGrid.ColumnHeadersHeight = 32;
		_statsQualityGrid.RowTemplate.Height = 29;
		_statsQualityGrid.Location = new Point(18, 48);
		_statsQualityGrid.Size = new Size(panel.Width - 36, 276);
		_statsQualityGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		_statsQualityGrid.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "Quality",
			HeaderText = L.T("stats.quality"),
			ReadOnly = true,
			FillWeight = 40f
		});
		_statsQualityGrid.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "Count",
			HeaderText = L.T("stats.quantity"),
			ReadOnly = true,
			FillWeight = 30f
		});
		_statsQualityGrid.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "Probability",
			HeaderText = L.T("stats.probability"),
			ReadOnly = true,
			FillWeight = 30f
		});
		foreach (QualityStyle item in new QualityStyle[1] { Theme.Consumable }.Concat(Theme.Qualities))
		{
			long valueOrDefault = _chestLog.QualityTotals.GetValueOrDefault(item.Name);
			double num = ((_chestLog.TotalDrops == 0L) ? 0.0 : ((double)valueOrDefault * 100.0 / (double)_chestLog.TotalDrops));
			int index = _statsQualityGrid.Rows.Add(item.Name, valueOrDefault.ToString(), num.ToString("0.0") + "%");
			_statsQualityGrid.Rows[index].Cells[0].Style.ForeColor = item.Color;
			_statsQualityGrid.Rows[index].Cells[0].Style.SelectionForeColor = item.Color;
		}
		panel.Controls.Add(_statsQualityGrid);
		_statsContent.Controls.Add(panel);
	}

	private void BuildTradeStatistics()
	{
		int count = _chestLog.TradeRecords.Count;
		int num = Math.Min(count, 10);
		int num2 = ((count != 0) ? (36 + num * 34) : 0);
		Panel panel = MakeCard((count == 0) ? 112 : (48 + num2 + 18));
		panel.Location = new Point(0, 0);
		panel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		panel.Controls.Add(MakeCardTitle(L.T("stats.tradeTitle"), 18, 14));
		_statsTradeSummary = new Label();
		_statsTradeSummary.Text = ((_chestLog.TradeRecords.Count == 0) ? L.T("stats.tradeEmpty") : string.Format(L.T("stats.tradeCount"), _chestLog.TradeRecords.Count));
		_statsTradeSummary.ForeColor = Theme.Muted;
		_statsTradeSummary.AutoSize = true;
		_statsTradeSummary.Location = new Point(132, 17);
		panel.Controls.Add(_statsTradeSummary);
		if (count == 0)
		{
			panel.Controls.Add(new Label
			{
				Text = L.T("stats.tradeHint"),
				ForeColor = Theme.Muted,
				AutoSize = true,
				Location = new Point(18, 54)
			});
			_statsContent.Controls.Add(panel);
			return;
		}
		_statsTradeGrid = CreateStatisticsGrid();
		_statsTradeGrid.Location = new Point(18, 48);
		_statsTradeGrid.Size = new Size(panel.Width - 36, num2);
		_statsTradeGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		_statsTradeGrid.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "Name",
			HeaderText = L.T("stats.itemName"),
			ReadOnly = true,
			FillWeight = 40f
		});
		_statsTradeGrid.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "Quality",
			HeaderText = L.T("stats.quality"),
			ReadOnly = true,
			FillWeight = 20f
		});
		_statsTradeGrid.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "Count",
			HeaderText = L.T("stats.quantity"),
			ReadOnly = true,
			FillWeight = 15f
		});
		_statsTradeGrid.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "Time",
			HeaderText = L.T("stats.time"),
			ReadOnly = true,
			FillWeight = 25f
		});
		foreach (ChestRecord tradeRecord in _chestLog.TradeRecords)
		{
			int index = _statsTradeGrid.Rows.Add(tradeRecord.Name, tradeRecord.Quality, tradeRecord.Count.ToString(), tradeRecord.Time.ToString("yyyy-MM-dd HH:mm:ss"));
			Color color = Theme.ColorOf(tradeRecord.Quality);
			_statsTradeGrid.Rows[index].Cells[0].Style.ForeColor = color;
			_statsTradeGrid.Rows[index].Cells[0].Style.SelectionForeColor = color;
			_statsTradeGrid.Rows[index].Cells[1].Style.ForeColor = color;
			_statsTradeGrid.Rows[index].Cells[1].Style.SelectionForeColor = color;
		}
		panel.Controls.Add(_statsTradeGrid);
		_statsContent.Controls.Add(panel);
	}

	private DataGridView CreateStatisticsGrid()
	{
		DataGridView dataGridView = new DataGridView();
		dataGridView.BackgroundColor = Theme.Surface;
		dataGridView.GridColor = Theme.Border;
		dataGridView.BorderStyle = BorderStyle.None;
		dataGridView.RowHeadersVisible = false;
		dataGridView.AllowUserToAddRows = false;
		dataGridView.AllowUserToDeleteRows = false;
		dataGridView.AllowUserToResizeRows = false;
		dataGridView.ReadOnly = true;
		dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
		dataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
		dataGridView.ColumnHeadersHeight = 36;
		dataGridView.RowTemplate.Height = 34;
		dataGridView.EnableHeadersVisualStyles = false;
		dataGridView.ColumnHeadersDefaultCellStyle.BackColor = Theme.SurfaceAlt;
		dataGridView.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Muted;
		dataGridView.ColumnHeadersDefaultCellStyle.Font = Theme.UiFont(Font.FontFamily, 9.5f, FontStyle.Bold);
		dataGridView.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.SurfaceAlt;
		dataGridView.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.Muted;
		dataGridView.DefaultCellStyle.BackColor = Theme.Surface;
		dataGridView.DefaultCellStyle.ForeColor = Theme.Text;
		dataGridView.DefaultCellStyle.SelectionBackColor = Theme.AccentSoft;
		dataGridView.DefaultCellStyle.SelectionForeColor = Theme.Text;
		dataGridView.AlternatingRowsDefaultCellStyle.BackColor = Theme.SurfaceAlt;
		dataGridView.DefaultCellStyle.Font = Theme.UiFont(Font.FontFamily, 9.5f);
		return dataGridView;
	}

	private void BuildQuality()
	{
		FlowLayoutPanel flowLayoutPanel = MakePage();
		flowLayoutPanel.Controls.Add(MakeHeading(L.T("quality.title"), L.T("quality.desc")));
		int num = 38 + _settings.QualityRules.Count * 44;
		int num2 = 52 + num + 12;
		Panel panel = MakeCard(num2 + 48);
		panel.Controls.Add(MakeCardTitle(L.T("quality.table"), 18, 16));
		_qualityGrid = new DataGridView
		{
			Location = new Point(18, 52),
			Size = new Size(panel.Width - 36, num),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			BackgroundColor = Theme.Surface,
			GridColor = Theme.Border,
			BorderStyle = BorderStyle.None,
			RowHeadersVisible = false,
			AllowUserToAddRows = false,
			AllowUserToDeleteRows = false,
			AllowUserToResizeRows = false,
			AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
			SelectionMode = DataGridViewSelectionMode.CellSelect,
			ColumnHeadersHeight = 38,
			EnableHeadersVisualStyles = false
		};
		_qualityGrid.RowTemplate.Height = 44;
		_qualityGrid.ColumnHeadersDefaultCellStyle.BackColor = Theme.SurfaceAlt;
		_qualityGrid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Muted;
		_qualityGrid.ColumnHeadersDefaultCellStyle.Font = Theme.UiFont(Font.FontFamily, 9.5f, FontStyle.Bold);
		_qualityGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.SurfaceAlt;
		_qualityGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.Muted;
		_qualityGrid.DefaultCellStyle.BackColor = Theme.Surface;
		_qualityGrid.DefaultCellStyle.ForeColor = Theme.Text;
		_qualityGrid.DefaultCellStyle.SelectionBackColor = Theme.AccentSoft;
		_qualityGrid.DefaultCellStyle.SelectionForeColor = Theme.Text;
		_qualityGrid.DefaultCellStyle.Font = Theme.UiFont(Font.FontFamily, 10f, FontStyle.Bold);
		_qualityGrid.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "Quality",
			HeaderText = L.T("quality.columnQuality"),
			ReadOnly = true,
			FillWeight = 30f
		});
		_qualityGrid.Columns.Add(new DataGridViewCheckBoxColumn
		{
			Name = "Favorite",
			HeaderText = L.T("quality.favorite"),
			FillWeight = 18f
		});
		_qualityGrid.Columns.Add(new DataGridViewCheckBoxColumn
		{
			Name = "Dismantle",
			HeaderText = L.T("quality.dismantle"),
			FillWeight = 18f
		});
		_qualityGrid.Columns.Add(new DataGridViewCheckBoxColumn
		{
			Name = "Sell",
			HeaderText = L.T("quality.sell"),
			FillWeight = 18f
		});
		foreach (QualityRule qualityRule in _settings.QualityRules)
		{
			int index = _qualityGrid.Rows.Add(QualityLabel(qualityRule.Quality), qualityRule.Favorite, qualityRule.Dismantle, qualityRule.Sell);
			_qualityGrid.Rows[index].Cells["Quality"].Style.ForeColor = Theme.ColorOf(qualityRule.Quality);
		}
		_qualityGrid.CurrentCellDirtyStateChanged += (object? _, EventArgs _) =>
		{
			if (_qualityGrid.IsCurrentCellDirty)
			{
				_qualityGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
			}
		};
		_qualityGrid.CellValueChanged += (object? _, DataGridViewCellEventArgs e) =>
		{
			EnforceExclusive(e.RowIndex, e.ColumnIndex);
		};
		panel.Controls.Add(_qualityGrid);
		panel.Controls.Add(new Label
		{
			Text = L.T("quality.mSkip"),
			ForeColor = Theme.Muted,
			AutoSize = true,
			Location = new Point(18, num2 + 8)
		});
		Button button = MakeButton(L.T("quality.save"), primary: true);
		button.SetBounds(panel.Width - 162, num2, 144, 32);
		button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		button.Click += (object? _, EventArgs _) =>
		{
			SaveQualitySettings();
		};
		panel.Controls.Add(button);
		flowLayoutPanel.Controls.Add(panel);
		_pageHost.Controls.Add(flowLayoutPanel);
	}

	private static string QualityLabel(string quality)
	{
		return quality switch
		{
			"普通" => L.T("quality.common"), 
			"优秀" => L.T("quality.uncommon"), 
			"稀有" => L.T("quality.rare"), 
			"史诗" => L.T("quality.epic"), 
			"传说" => L.T("quality.legendary"), 
			"神话" => L.T("quality.mythic"), 
			"异域" => L.T("quality.exotic"), 
			_ => L.T("quality.mythic"), 
		};
	}

	private void BuildSettings()
	{
		_delayMin = new NumericUpDown();
		_delayMax = new NumericUpDown();
		_waitReconnect = new CheckBox();
		_chestMinutes = new NumericUpDown();
		_antiDropEnable = new CheckBox();
		_antiDropMinutes = new NumericUpDown();
		_repairEnable = new CheckBox();
		_repairMinutes = new NumericUpDown();
		_repairThreshold = new NumericUpDown();
		FlowLayoutPanel flowLayoutPanel = MakePage();
		flowLayoutPanel.Controls.Add(MakeHeading(L.T("settings.title"), L.T("settings.desc")));
		Panel panel = MakeCard(120);
		panel.Controls.Add(MakeCardTitle(L.T("settings.language"), 18, 16));
		_languageBox = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Location = new Point(20, 48),
			Size = new Size(240, 30),
			BackColor = Theme.Surface,
			ForeColor = Theme.Text,
			FlatStyle = FlatStyle.Flat,
			Font = Theme.UiFont(Font.FontFamily, 10f)
		};
		_languageBox.Items.Add("中文 / Chinese");
		_languageBox.Items.Add("English / 英文");
		_languageBox.SelectedIndex = ((L.Lang == "en") ? 1 : 0);
		panel.Controls.Add(_languageBox);
		panel.Controls.Add(new Label
		{
			Text = L.T("settings.languageNote"),
			ForeColor = Theme.Muted,
			AutoSize = true,
			Location = new Point(276, 52)
		});
		flowLayoutPanel.Controls.Add(panel);
		Panel panel2 = MakeCard(140);
		panel2.Controls.Add(MakeCardTitle(L.T("settings.close"), 18, 16));
		_closeBox = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Location = new Point(20, 48),
			Size = new Size(300, 30),
			BackColor = Theme.Surface,
			ForeColor = Theme.Text,
			FlatStyle = FlatStyle.Flat,
			Font = Theme.UiFont(Font.FontFamily, 10f)
		};
		_closeBox.Items.Add(L.T("settings.closeExit"));
		_closeBox.Items.Add(L.T("settings.closeTray"));
		_closeBox.SelectedIndex = (_settings.CloseToTray ? 1 : 0);
		panel2.Controls.Add(_closeBox);
		panel2.Controls.Add(new Label
		{
			Text = L.T("settings.closeNote"),
			ForeColor = Theme.Muted,
			AutoSize = false,
			AutoEllipsis = true,
			Size = new Size(panel2.Width - 40, 40),
			Location = new Point(20, 86),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		});
		flowLayoutPanel.Controls.Add(panel2);
		Panel panel3 = MakeCard(160);
		panel3.Controls.Add(MakeCardTitle(L.T("settings.pacing"), 18, 16));
		panel3.Controls.Add(MakeFieldLabel(L.T("settings.delayLabel"), 20, 52));
		ConfigureNumber(_delayMin, 2, 60, _settings.DelayMinSeconds, 20, 74, 74);
		panel3.Controls.Add(_delayMin);
		panel3.Controls.Add(new Label
		{
			Text = "–",
			ForeColor = Theme.Muted,
			AutoSize = true,
			Location = new Point(100, 78)
		});
		ConfigureNumber(_delayMax, 2, 60, _settings.DelayMaxSeconds, 118, 74, 74);
		panel3.Controls.Add(_delayMax);
		_waitReconnect.Text = L.T("settings.reconnect");
		_waitReconnect.Checked = _settings.WaitForReconnect;
		_waitReconnect.AutoSize = true;
		_waitReconnect.ForeColor = Theme.Text;
		_waitReconnect.Location = new Point(220, 76);
		panel3.Controls.Add(_waitReconnect);
		Button button = MakeButton(L.T("settings.save"), primary: true);
		button.SetBounds(20, 116, 130, 30);
		button.Click += (object? _, EventArgs _) =>
		{
			SaveSettings();
		};
		panel3.Controls.Add(button);
		flowLayoutPanel.Controls.Add(panel3);
		Panel panel4 = MakeCard(174);
		panel4.Controls.Add(MakeCardTitle(L.T("settings.chestSchedule"), 18, 16));
		panel4.Controls.Add(MakeFieldLabel(L.T("chest.intervalLabel"), 20, 52));
		ConfigureNumber(_chestMinutes, 1, 240, _settings.ChestCheckMinutes, 20, 74, 88);
		panel4.Controls.Add(_chestMinutes);
		Button button2 = MakeButton(L.T("chest.save"), primary: false);
		button2.SetBounds(132, 72, 120, 30);
		button2.Click += (object? _, EventArgs _) =>
		{
			SaveChestSettings();
		};
		panel4.Controls.Add(button2);
		_sortAfterChestClaim = new CheckBox
		{
			Text = L.T("settings.sortAfterChestClaim"),
			Checked = _settings.SortAfterSupplyBoxClaim,
			AutoSize = true,
			ForeColor = Theme.Text,
			Location = new Point(20, 110)
		};
		panel4.Controls.Add(_sortAfterChestClaim);
		_collectAfterSort = new CheckBox
		{
			Text = L.T("settings.collectAfterSort"),
			Checked = _settings.CollectSupplyBoxesAfterSort,
			AutoSize = true,
			ForeColor = Theme.Text,
			Location = new Point(20, 138)
		};
		panel4.Controls.Add(_collectAfterSort);
		flowLayoutPanel.Controls.Add(panel4);
		Panel panel5 = MakeCard(262);
		panel5.Controls.Add(MakeCardTitle(L.T("settings.antiDrop"), 18, 16));
		_antiDropEnable.Text = L.T("settings.antiDropEnable");
		_antiDropEnable.Checked = _settings.AntiDropEnabled;
		_antiDropEnable.AutoSize = true;
		_antiDropEnable.ForeColor = Theme.Text;
		_antiDropEnable.Location = new Point(20, 50);
		panel5.Controls.Add(_antiDropEnable);
		panel5.Controls.Add(MakeFieldLabel(L.T("settings.antiDropInterval"), 20, 82));
		ConfigureNumber(_antiDropMinutes, 1, 60, (_settings.AntiDropCheckMinutes <= 0) ? 3 : _settings.AntiDropCheckMinutes, 20, 104, 88);
		panel5.Controls.Add(_antiDropMinutes);
		panel5.Controls.Add(MakeFieldLabel(L.T("settings.gamePathLabel"), 20, 140));
		_gamePathBox = new TextBox
		{
			Text = _settings.GameExePath,
			Location = new Point(20, 162),
			Size = new Size(panel5.Width - 160, 26),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			BackColor = Theme.Surface,
			ForeColor = Theme.Text,
			BorderStyle = BorderStyle.FixedSingle
		};
		panel5.Controls.Add(_gamePathBox);
		Button button3 = MakeButton(L.T("settings.gamePathBrowse"), primary: false);
		button3.SetBounds(panel5.Width - 130, 161, 110, 28);
		button3.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		button3.Click += (object? _, EventArgs _) =>
		{
			BrowseForGame();
		};
		panel5.Controls.Add(button3);
		Button button4 = MakeButton(L.T("settings.save"), primary: true);
		button4.SetBounds(20, 198, 130, 28);
		button4.Click += (object? _, EventArgs _) =>
		{
			SaveAntiDropSettings();
		};
		panel5.Controls.Add(button4);
		panel5.Controls.Add(new Label
		{
			Text = L.T("settings.antiDropNote"),
			ForeColor = Theme.Muted,
			AutoSize = false,
			AutoEllipsis = true,
			Size = new Size(panel5.Width - 180, 22),
			Location = new Point(170, 202),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		});
		flowLayoutPanel.Controls.Add(panel5);
		Panel panel6 = MakeCard(214);
		panel6.Controls.Add(MakeCardTitle(L.T("repair.title"), 18, 16));
		_repairEnable.Text = L.T("repair.enable");
		_repairEnable.Checked = _settings.RepairEnabled;
		_repairEnable.AutoSize = true;
		_repairEnable.ForeColor = Theme.Text;
		_repairEnable.Location = new Point(20, 50);
		panel6.Controls.Add(_repairEnable);
		panel6.Controls.Add(MakeFieldLabel(L.T("repair.intervalLabel"), 20, 82));
		ConfigureNumber(_repairMinutes, 1, 240, _settings.RepairCheckMinutes, 20, 104, 88);
		panel6.Controls.Add(_repairMinutes);
		panel6.Controls.Add(MakeFieldLabel(L.T("repair.thresholdLabel"), 152, 82));
		ConfigureNumber(_repairThreshold, 10, 100, _settings.RepairThresholdPercent, 152, 104, 88);
		panel6.Controls.Add(_repairThreshold);
		Button button5 = MakeButton(L.T("repair.save"), primary: false);
		button5.SetBounds(20, 144, 148, 30);
		button5.Click += (object? _, EventArgs _) =>
		{
			SaveRepairSettings();
		};
		panel6.Controls.Add(button5);
		panel6.Controls.Add(new Label
		{
			Text = L.T("repair.note"),
			ForeColor = Theme.Muted,
			AutoSize = false,
			AutoEllipsis = true,
			Size = new Size(panel6.Width - 40, 22),
			Location = new Point(20, 184),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		});
		flowLayoutPanel.Controls.Add(panel6);
		_pageHost.Controls.Add(flowLayoutPanel);
	}

	private void BuildEnvironment()
	{
		FlowLayoutPanel flowLayoutPanel = MakePage();
		flowLayoutPanel.Controls.Add(MakeHeading(L.T("env.title"), L.T("env.desc")));
		Panel panel = MakeCard(300);
		panel.Controls.Add(MakeCardTitle(L.T("env.detect"), 18, 16));
		_envGame = MakeValueLabel(20, 50);
		_envEngine = MakeValueLabel(20, 84);
		_envHash = MakeValueLabel(20, 118);
		_envBridge = MakeValueLabel(20, 152);
		_envVerify = MakeValueLabel(20, 186);
		_envPlugin = MakeValueLabel(20, 220);
		Label[] array = new Label[6] { _envGame, _envEngine, _envHash, _envBridge, _envVerify, _envPlugin };
		foreach (Label obj in array)
		{
			obj.AutoSize = false;
			obj.AutoEllipsis = true;
			obj.Size = new Size(panel.Width - 40, 22);
			obj.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		}
		panel.Controls.Add(_envGame);
		panel.Controls.Add(_envEngine);
		panel.Controls.Add(_envHash);
		panel.Controls.Add(_envBridge);
		panel.Controls.Add(_envVerify);
		panel.Controls.Add(_envPlugin);
		Button button = MakeButton(L.T("env.detect"), primary: true);
		button.SetBounds(20, 254, 118, 30);
		button.Click += (object? _, EventArgs _) =>
		{
			DetectEnvironment();
		};
		panel.Controls.Add(button);
		Button button2 = MakeButton(L.T("env.deploy"), primary: false);
		button2.SetBounds(148, 254, 118, 30);
		button2.Click += async (object? _, EventArgs _) =>
		{
			await DeployEnvironmentAsync();
		};
		panel.Controls.Add(button2);
		Button button3 = MakeButton(L.T("env.purge"), primary: false);
		button3.SetBounds(276, 254, 138, 30);
		button3.Click += (object? _, EventArgs _) =>
		{
			PurgeEnvironment();
		};
		panel.Controls.Add(button3);
		flowLayoutPanel.Controls.Add(panel);
		Panel panel2 = MakeCard(150);
		panel2.Controls.Add(MakeCardTitle(L.T("env.pluginTitle"), 18, 16));
		panel2.Controls.Add(new Label
		{
			Text = L.T("env.pluginNote"),
			ForeColor = Theme.Muted,
			AutoSize = false,
			AutoEllipsis = true,
			Size = new Size(panel2.Width - 40, 86),
			Location = new Point(20, 48),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		});
		flowLayoutPanel.Controls.Add(panel2);
		_pageHost.Controls.Add(flowLayoutPanel);
		RefreshEnvironment();
	}

	private Label MakeValueLabel(int x, int y)
	{
		return new Label
		{
			ForeColor = Theme.Text,
			AutoSize = true,
			Location = new Point(x, y),
			Font = Theme.UiFont(Font.FontFamily, 9.5f)
		};
	}

	private void DetectEnvironment()
	{
		_envFingerprint = null;
		string text = ResolveGameSourcePath();
		if (text == null)
		{
			Log(L.T("log.gameNotFound"));
			RefreshEnvironment();
			return;
		}
		_envFingerprint = Deployment.Inspect(text);
		if (_envFingerprint == null)
		{
			Log(L.T("log.gameNotFound"));
			RefreshEnvironment();
		}
		else
		{
			Log(string.Format(L.T("log.detectGame"), _envFingerprint.ExecutablePath, _envFingerprint.Engine));
			RefreshEnvironment();
		}
	}

	private string? ResolveGameSourcePath()
	{
		EnvironmentRecord environmentRecord = Deployment.LoadRecord();
		string[] array = new string[4]
		{
			GameProcessPath(),
			_envFingerprint?.ExecutablePath,
			_settings.GameExePath,
			environmentRecord?.GamePath
		};
		foreach (string text in array)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				continue;
			}
			try
			{
				string sourceExecutablePath = PluginInstall.GetSourceExecutablePath(text);
				if (File.Exists(sourceExecutablePath))
				{
					return Path.GetFullPath(sourceExecutablePath);
				}
			}
			catch
			{
			}
		}
		return null;
	}

	private async Task DeployEnvironmentAsync()
	{
		if (_envFingerprint == null)
		{
			DetectEnvironment();
		}
		if (_envFingerprint == null)
		{
			Log("一键部署未执行：设置中的游戏路径不存在，请检查设置页的游戏路径。");
			MessageBox.Show("未能定位游戏文件。请检查设置页中的游戏路径，然后重试部署。", L.T("msg.saveTitle"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		DeployResult deployResult = Deployment.Deploy(_envFingerprint, _bridge?.Port ?? 0, L.T("env.bridgeMode"));
		if (deployResult.Ok)
		{
			_settings.GameExePath = _envFingerprint.ExecutablePath;
			try
			{
				_settings.Save();
			}
			catch (Exception ex)
			{
				Log("保存游戏路径失败：" + ex.Message);
			}
			PluginInstall.EnsureResult ensureResult = await Task.Run(() => PluginInstall.Ensure(_envFingerprint.ExecutablePath));
			if (!ensureResult.Ok)
			{
				MessageBox.Show("游戏环境部署失败：" + ensureResult.Message, L.T("msg.saveTitle"), MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
			Log(string.Format(L.T("log.deployed"), _bridge?.Port ?? 0));
			Log(ensureResult.Changed ? $"游戏接口已部署：v{"1.0.2"}（协议 {3}）。" : "游戏接口已是最新版本。");
			Log(L.T(ensureResult.SteamLaunchReady ? "log.steamLaunchReady" : "log.steamLaunchPending"));
			MessageBox.Show(L.T("deploy.done"), L.T("msg.doneTitle"), MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			await InitializeAssistantAsync();
		}
		else
		{
			MessageBox.Show(string.Format(L.T("msg.saveFailed"), deployResult.Message), L.T("msg.saveTitle"), MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		RefreshEnvironment();
	}

	private void PurgeEnvironment()
	{
		if (MessageBox.Show(L.T("env.purgeConfirm"), L.T("env.purgeTitle"), MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) != DialogResult.OK)
		{
			return;
		}
		string text = ResolveGameSourcePath();
		if (!string.IsNullOrWhiteSpace(text))
		{
			PluginInstall.EnsureResult ensureResult = PluginInstall.Remove(text);
			if (!ensureResult.Ok)
			{
				Log("清除游戏内插件失败，环境记录已保留以便重试：" + ensureResult.Message);
				return;
			}
			Log("已清除游戏接口环境；Steam 游戏文件已恢复。");
		}
		DeployResult deployResult = Deployment.Purge();
		if (deployResult.Ok)
		{
			Log(L.T("log.purged"));
		}
		else
		{
			Log("清除助手环境记录失败：" + deployResult.Message);
		}
		_envFingerprint = (string.IsNullOrWhiteSpace(text) ? null : Deployment.Inspect(text));
		RefreshEnvironment();
	}

	private static string? GameProcessPath()
	{
		Native.WindowInfo windowInfo = GameReader.FindGameWindow();
		if ((object)windowInfo == null)
		{
			return null;
		}
		try
		{
			using Process process = Process.GetProcessById(windowInfo.ProcessId);
			string text = process.MainModule?.FileName;
			if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
			{
				return text;
			}
		}
		catch
		{
		}
		try
		{
			Process[] processes = Process.GetProcesses();
			foreach (Process process2 in processes)
			{
				try
				{
					if (process2.ProcessName.Contains("Racer", StringComparison.OrdinalIgnoreCase))
					{
						string text2 = process2.MainModule?.FileName;
						if (!string.IsNullOrWhiteSpace(text2) && File.Exists(text2))
						{
							return text2;
						}
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private void RefreshEnvironment()
	{
		if (_envGame == null || _envGame.IsDisposed)
		{
			return;
		}
		EnvironmentRecord environmentRecord = Deployment.LoadRecord();
		if (_envEngine != null && _envHash != null && _envBridge != null && _envVerify != null)
		{
			_envGame.Text = L.T("env.gameLabel") + "  " + ((_envFingerprint == null) ? L.T("env.notDetected") : _envFingerprint.ExecutablePath);
			_envEngine.Text = L.T("env.engineLabel") + "  " + ((_envFingerprint != null) ? (_envFingerprint.Engine + (string.IsNullOrEmpty(_envFingerprint.Suffix) ? "" : (" " + _envFingerprint.Suffix))) : (environmentRecord?.Engine ?? L.T("env.notDetected")));
			Label? envHash = _envHash;
			string text = L.T("env.hashLabel");
			string text2;
			if (_envFingerprint != null)
			{
				text2 = _envFingerprint.Sha256.Substring(0, 16);
			}
			else
			{
				text2 = ((environmentRecord == null) ? L.T("env.notDetected") : environmentRecord.ExeSha256.Substring(0, 16));
			}
			envHash.Text = text + "  " + text2;
			_envBridge.Text = L.T("env.bridgeLabel") + "  " + ((_bridge == null) ? L.T("env.stopped") : $"127.0.0.1:{_bridge.Port}  ({L.T("env.running")})");
			string text3 = Deployment.Verify(_envFingerprint);
			_envVerify.Text = L.T("env.statusLabel") + "  " + ((text3.Length == 0) ? L.T("verify.ok") : L.T(text3));
			_envVerify.ForeColor = ((text3.Length == 0) ? Theme.Good : Theme.Warning);
			if (_envPlugin != null && !_envPlugin.IsDisposed)
			{
				string text4 = _envFingerprint?.ExecutablePath ?? environmentRecord?.GamePath;
				string text5 = (string.IsNullOrWhiteSpace(text4) ? null : PluginInstall.GetSourceExecutablePath(text4));
				string bundledHash = PluginInstall.BundledHash;
				string text6 = (string.IsNullOrWhiteSpace(text5) ? null : PluginInstall.DeployedHash(Path.GetDirectoryName(text5)));
				PluginInstall.PluginStatus pluginStatus = PluginInstall.ReadRuntimeStatus();
				bool flag = bundledHash != null && text6 != null && string.Equals(bundledHash, text6, StringComparison.OrdinalIgnoreCase);
				bool flag2 = (object)pluginStatus != null && pluginStatus.Protocol == 3 && pluginStatus.Version == "1.0.2";
				bool steamReady = flag && !string.IsNullOrWhiteSpace(text4) && PluginInstall.IsSteamLaunchReady(text4);
				_steamLaunchPending = flag && !steamReady;
				_envPlugin.Text = L.T("env.pluginLabel") + "  " + $"v{"1.0.2"} / 协议 {3}  ·  " + (flag ? "游戏接口已部署" : "游戏接口待部署") + (flag ? "  ·  " + L.T(steamReady ? "env.steamReady" : "env.steamPending") : "") + (((object)pluginStatus == null) ? "  ·  游戏 API 未连接" : (flag2 ? $"  ·  进程 {pluginStatus.HostPid} / {pluginStatus.ConnectionState}" : "  ·  版本不一致"));
				_envPlugin.ForeColor = ((flag && steamReady && (((object)pluginStatus == null) | flag2)) ? Theme.Good : Theme.Warning);
			}
		}
	}

	private async Task StartMonitoringAsync()
	{
		if (!_initialized || _initializing)
		{
			Log("初始化尚未通过，暂不启动自动监控。");
		}
		else
		{
			if (_executionEnabled || _manualRunRunning || Engine.IsRunning)
			{
				return;
			}
			if ((object)_window == null)
			{
				await PollForGameAsync();
			}
			if ((object)_window == null)
			{
				Log(L.T("log.gameNotFound"));
				return;
			}
			string text = Deployment.Verify(_envFingerprint);
			if (text.Length <= 0)
			{
				_runCts.Dispose();
				_runCts = new CancellationTokenSource();
				_lastAutoScanError = "";
				_executionEnabled = true;
				PersistAutoRun(enabled: true);
				_autoScan.Start();
				_lastChestCheck = DateTime.Now;
				_lastWarehouseScan = DateTime.Now;
				_lastRepairCheck = DateTime.Now;
				ActiveControl = null;
				SetTaskState("task.readInventory", "task.waitCycle");
				if (!_progressLabel.IsDisposed)
				{
					_progressLabel.Text = L.T("run.monitorStarting");
				}
				RefreshAutomationChrome();
				try
				{
					await Engine.RunAsync(_window, chestDue: false, warehouseDue: true, forceSort: false, _runCts.Token);
					_lastWarehouseScan = DateTime.Now;
					_lastAutoScanError = "";
					SetTaskState("task.waitCycle", "task.chest");
					return;
				}
				catch (OperationCanceledException)
				{
					Log("首次监控检查已停止。");
					return;
				}
				catch (Exception ex2)
				{
					_lastAutoScanError = "首次监控检查暂未成功：" + ex2.Message;
					SetTaskState("task.waitCycle", "task.chest");
					Log(_lastAutoScanError + "；后台会继续按周期重试。");
					return;
				}
				finally
				{
					RefreshAutomationChrome();
				}
			}
			Log(L.T(text));
		}
	}

	private Task RunImmediateChestCheckAsync()
	{
		return RunImmediateCheckAsync(chestCheck: true);
	}

	private Task RunImmediateWarehouseCheckAsync()
	{
		return RunImmediateCheckAsync(chestCheck: false);
	}

	private async Task RunImmediateRepairCheckAsync()
	{
		if (!_initialized || _initializing)
		{
			Log("初始化尚未通过，暂不能立即执行。");
			return;
		}
		if (_manualRunRunning || _autoRunning || Engine.IsRunning)
		{
			Log("已有游戏操作正在执行；本次立即执行未排队。");
			return;
		}
		if ((object)_window == null)
		{
			await PollForGameAsync();
		}
		if ((object)_window == null)
		{
			Log(L.T("log.gameNotFound"));
			return;
		}
		string text = Deployment.Verify(_envFingerprint);
		if (text.Length > 0)
		{
			Log(L.T(text));
			return;
		}
		if (!_executionEnabled)
		{
			_runCts.Dispose();
			_runCts = new CancellationTokenSource();
		}
		_manualRunRunning = true;
		_lastAutoScanError = "";
		ActiveControl = null;
		SetTaskState("task.repair", "task.waitCycle");
		RefreshAutomationChrome();
		bool failed = false;
		try
		{
			await Engine.CheckAndRepairAsync(_window, _settings.RepairThresholdPercent, _runCts.Token);
			_lastRepairCheck = DateTime.Now;
		}
		catch (OperationCanceledException)
		{
			Log("已停止本次立即执行。");
		}
		catch (Exception ex2)
		{
			failed = true;
			_lastAutoScanError = "立即执行暂停：" + ex2.Message;
			SetTaskState("task.paused", "task.startAutomation");
			Log(_lastAutoScanError);
		}
		finally
		{
			_manualRunRunning = false;
			if (!failed)
			{
				SetTaskState(_executionEnabled ? "task.waitCycle" : "task.ready", _executionEnabled ? "task.waitCycle" : "task.startAutomation");
			}
			if (!_progressLabel.IsDisposed)
			{
				_progressLabel.Text = (_executionEnabled ? L.T("run.monitoring") : L.T("run.ready"));
			}
			RefreshAutomationChrome();
		}
	}

	private async Task RunImmediateCheckAsync(bool chestCheck)
	{
		if (!_initialized || _initializing)
		{
			Log("初始化尚未通过，暂不能立即执行。");
			return;
		}
		if (_manualRunRunning || _autoRunning || Engine.IsRunning)
		{
			Log("已有游戏操作正在执行；本次立即执行未排队。");
			return;
		}
		if ((object)_window == null)
		{
			await PollForGameAsync();
		}
		if ((object)_window == null)
		{
			Log(L.T("log.gameNotFound"));
			return;
		}
		string text = Deployment.Verify(_envFingerprint);
		if (text.Length > 0)
		{
			Log(L.T(text));
			return;
		}
		if (!_executionEnabled)
		{
			_runCts.Dispose();
			_runCts = new CancellationTokenSource();
		}
		CancellationToken token = _runCts.Token;
		_manualRunRunning = true;
		_lastAutoScanError = "";
		ActiveControl = null;
		SetTaskState(chestCheck ? "task.chest" : "task.readInventory", chestCheck ? "task.waitResult" : "task.collect");
		RefreshAutomationChrome();
		bool completed = false;
		bool failed = false;
		try
		{
			await Engine.RunAsync(_window, chestCheck, !chestCheck, !chestCheck, token);
			if (chestCheck)
			{
				_lastChestCheck = DateTime.Now;
			}
			else
			{
				_lastWarehouseScan = DateTime.Now;
			}
			completed = true;
		}
		catch (OperationCanceledException)
		{
			Log("已停止本次立即执行。");
		}
		catch (Exception ex2)
		{
			failed = true;
			_lastAutoScanError = "立即执行暂停：" + ex2.Message;
			SetTaskState("task.paused", "task.startAutomation");
			Log(_lastAutoScanError);
		}
		finally
		{
			_manualRunRunning = false;
			if (failed)
			{
				SetTaskState("task.paused", "task.startAutomation");
			}
			else if (!_executionEnabled)
			{
				SetTaskState(completed ? "task.complete" : "task.ready", completed ? "task.waitCycle" : "task.startAutomation");
			}
			else
			{
				SetTaskState("task.waitCycle", "task.chest");
			}
			if (!_progressLabel.IsDisposed)
			{
				_progressLabel.Text = (_executionEnabled ? L.T("run.monitoring") : L.T("run.ready"));
			}
			RefreshAutomationChrome();
		}
	}

	private void StopAutomation()
	{
		StopAutomation(persistOff: true);
	}

	private void StopAutomation(bool persistOff)
	{
		_executionEnabled = false;
		_runCts.Cancel();
		if (persistOff)
		{
			PersistAutoRun(enabled: false);
		}
		_lastAutoScanError = "";
		SetTaskState("task.paused", "task.startAutomation");
		if (!_progressLabel.IsDisposed)
		{
			_progressLabel.Text = "停止中…";
		}
	}

	private void StopAllAutomation()
	{
		_autoScan.Stop();
		StopAutomation();
		SetTaskState("task.ready", "task.startAutomation");
		if (!_progressLabel.IsDisposed)
		{
			_progressLabel.Text = L.T("run.ready");
		}
		RefreshAutomationChrome();
	}

	private void PersistAutoRun(bool enabled)
	{
		if (_settings.AutoRunEnabled == enabled)
		{
			return;
		}
		_settings.AutoRunEnabled = enabled;
		try
		{
			_settings.Save();
		}
		catch
		{
		}
	}

	private async Task ResumeMonitoringAsync()
	{
		if (_initialized && !_initializing && !_executionEnabled)
		{
			if ((object)_window == null)
			{
				await PollForGameAsync().ConfigureAwait(continueOnCapturedContext: true);
			}
			if ((object)_window == null)
			{
				Log("上次自动监控为开启状态，但暂未找到游戏窗口；连接后会自动恢复。");
			}
			else if (!_executionEnabled)
			{
				await StartMonitoringAsync().ConfigureAwait(continueOnCapturedContext: true);
			}
		}
	}

	private string? ResolveGameExecutable()
	{
		string[] array = new string[2]
		{
			_settings.GameExePath,
			Deployment.LoadRecord()?.GamePath
		};
		foreach (string text in array)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				continue;
			}
			string sourceExecutablePath = PluginInstall.GetSourceExecutablePath(text);
			if (File.Exists(sourceExecutablePath))
			{
				string text2 = PluginInstall.HelperExecutablePath(sourceExecutablePath);
				if (File.Exists(text2))
				{
					return text2;
				}
			}
		}
		string text3 = GameProcessPath();
		if (!string.IsNullOrWhiteSpace(text3))
		{
			string text4 = PluginInstall.HelperExecutablePath(PluginInstall.GetSourceExecutablePath(text3));
			if (File.Exists(text4))
			{
				return text4;
			}
		}
		array = new string[2] { "D:\\SteamLibrary\\steamapps\\common", "C:\\Program Files (x86)\\Steam\\steamapps\\common" };
		for (int i = 0; i < array.Length; i++)
		{
			string text5 = Path.Combine(array[i], "Desk Top Racer", "Desk Top Racer.exe");
			if (File.Exists(text5))
			{
				string text6 = PluginInstall.HelperExecutablePath(text5);
				if (File.Exists(text6))
				{
					return text6;
				}
			}
		}
		return null;
	}

	private async Task LaunchGameAsync()
	{
		Native.WindowInfo windowInfo = GameReader.FindGameWindow();
		GameApiClient.SetExpectedProcessId((object)windowInfo == null ? 0 : windowInfo.ProcessId);
		if ((object)windowInfo != null)
		{
			for (int attempt = 0; attempt < 12; attempt++)
			{
				GameApiRuntimeStatus status = GameApiClient.ReadRuntimeStatus(windowInfo.ProcessId);
				if (status != null && status.HostPid == windowInfo.ProcessId && status.KernelReady)
				{
					Log(L.T("log.gameAlreadyRunning"));
					return;
				}
				await Task.Delay(500);
			}
			Log(_steamLaunchPending ? L.T("log.gameRestartForApi") : L.T("log.gameApiStillWaiting"));
			return;
		}
		try
		{
			await StartGameThroughSteamAsync();
		}
		catch (Exception ex2)
		{
			Log(string.Format(L.T("log.launchFailed"), ex2.Message));
		}
	}

	private async Task StartGameThroughSteamAsync()
	{
		string helperExecutable = ResolveGameExecutable();
		if (helperExecutable == null)
		{
			throw new FileNotFoundException("未找到游戏环境；请在环境页检测游戏并部署环境。");
		}
		string sourceExecutable = PluginInstall.GetSourceExecutablePath(helperExecutable);
		PluginInstall.EnsureResult ensure = await Task.Run(() => PluginInstall.Ensure(sourceExecutable));
		if (!ensure.Ok)
		{
			throw new InvalidOperationException(ensure.Message);
		}
		_steamLaunchPending = !ensure.SteamLaunchReady;
		Log(ensure.Message);
		if (!PluginInstall.RestoreOriginalSteamLibrary(sourceExecutable, out string restoreMessage))
		{
			throw new InvalidOperationException(restoreMessage);
		}
		if (ensure.SteamLaunchReady)
		{
			string steamUri = $"steam://run/{_settings.SteamAppId}";
			Log(string.Format(L.T("log.launchingGame"), _settings.SteamAppId));
			Process.Start(new ProcessStartInfo(steamUri)
			{
				UseShellExecute = true
			});
		}
		else
		{
			throw new InvalidOperationException("Steam 游戏接口环境尚未就绪；退出游戏后重新部署环境即可完成接入。");
		}
		Log(L.T("log.gameLaunched"));
		_poll.Start();
		if (_settings.AntiDropEnabled)
		{
			_watchdog.Start();
		}
	}

	private async Task WatchdogTickAsync()
	{
		if (!_settings.AntiDropEnabled || _watchdogRunning || !_initialized || _initializing)
		{
			return;
		}
		_watchdogRunning = true;
		try
		{
			if (_autoRunning || _manualRunRunning || _busy)
			{
				return;
			}
			PluginAutomationEngine automation = _automation;
			if (automation != null && automation.IsRunning)
			{
				return;
			}
			var probe = await ProbeConnectionAsync();
			if (probe.PluginResponsive && !probe.GameOnline)
			{
				// A healthy plugin can report while the server session recovers; retain this game process and recheck its state.
				_sendFailureStreak = 0;
				_watchdogProbeFailures = 0;
				if (_watchdogWaitingSince == DateTime.MinValue)
				{
					_watchdogWaitingSince = DateTime.Now;
					Log(string.Format(L.T("log.antiDropServerPending"), DescribeGameConnectionState(probe.GameState)));
				}
				return;
			}
			if (probe.Failure == null)
			{
				_sendFailureStreak = 0;
				_watchdogProbeFailures = 0;
				if (_watchdogWaitingSince != DateTime.MinValue)
				{
					_watchdogWaitingSince = DateTime.MinValue;
					Log(L.T("log.antiDropRestored"));
					if (_settings.AutoRunEnabled && !_executionEnabled)
					{
						await ResumeMonitoringAsync();
					}
				}
			}
			else
			{
				if (!probe.PluginResponsive && GameReader.FindGameWindow() != null)
				{
					_watchdogProbeFailures = 0;
					Log(L.T("log.antiDropApiPending"));
					return;
				}
				_watchdogProbeFailures++;
				if (_watchdogWaitingSince == DateTime.MinValue)
				{
					_watchdogWaitingSince = DateTime.Now;
				}
				if (_watchdogProbeFailures < 2)
				{
					Log(string.Format(L.T("log.antiDropRetrying"), probe.Failure, _settings.AntiDropCheckMinutes));
					return;
				}
				_watchdogProbeFailures = 0;
				Log(string.Format(L.T("log.antiDropDetected"), probe.Failure));
				await RestartGameAsync(probe.Failure);
			}
		}
		catch (Exception ex)
		{
			Log("防掉线检查异常：" + ex.Message);
		}
		finally
		{
			_watchdogRunning = false;
		}
	}

	private async Task<(string? Failure, bool PluginResponsive, bool GameOnline, string GameState)> ProbeConnectionAsync()
	{
		Native.WindowInfo windowInfo = GameReader.FindGameWindow();
		GameApiClient.SetExpectedProcessId((object)windowInfo == null ? 0 : windowInfo.ProcessId);
		if ((object)windowInfo == null)
		{
			return ("未找到游戏窗口", false, false, "");
		}
		if (GameProcessPath() == null)
		{
			return ("游戏进程不存在", false, false, "");
		}
		GameApiRuntimeStatus gameApiRuntimeStatus = GameApiClient.ReadRuntimeStatus(windowInfo.ProcessId);
		if ((object)gameApiRuntimeStatus == null || gameApiRuntimeStatus.HostPid != windowInfo.ProcessId)
		{
			return ("游戏内 API 心跳缺失或进程不匹配", false, false, "");
		}
		try
		{
			await _gameApi.PingAsync();
			JsonElement jsonElement = await _gameApi.GetSnapshotAsync();
			string text = GameApiClient.GetString((jsonElement.ValueKind == JsonValueKind.Object && jsonElement.TryGetProperty("connection", out var value)) ? value : default(JsonElement), "state", "unknown");
			NoteGameFeedback();
			if (!string.Equals(text, "online", StringComparison.OrdinalIgnoreCase))
			{
				return (null, true, false, text);
			}
			if (_sendFailureStreak >= 3)
			{
				return ($"连续 {_sendFailureStreak} 次操作未得到游戏反馈", true, true, text);
			}
			if (_lastGameFeedbackAt != DateTime.MinValue)
			{
				double totalMinutes = (DateTime.Now - _lastGameFeedbackAt).TotalMinutes;
				int num = Math.Max(2, _settings.AntiDropCheckMinutes * 2);
				if (totalMinutes >= (double)num)
				{
					return ($"已有 {totalMinutes:0} 分钟没有得到游戏反馈", true, true, text);
				}
			}
			return (null, true, true, text);
		}
		catch (Exception ex)
		{
			return ("游戏内 API 无响应：" + ex.Message, false, false, "");
		}
	}

	private async Task RestartGameAsync(string reason)
	{
		try
		{
			Process[] processes = Process.GetProcesses();
			foreach (Process process in processes)
			{
				try
				{
					if (process.ProcessName.Contains("Racer", StringComparison.OrdinalIgnoreCase))
					{
						string text = process.MainModule?.FileName;
						if (text == null || text.Contains("Desk Top Racer", StringComparison.OrdinalIgnoreCase))
						{
							process.Kill(entireProcessTree: true);
							process.WaitForExit(8000);
						}
					}
				}
				catch
				{
				}
			}
		}
		catch (Exception ex)
		{
			Log("防掉线结束游戏进程异常：" + ex.Message);
		}
		if (_executionEnabled)
		{
			StopAutomation(persistOff: false);
		}
		_window = null;
		_sendFailureStreak = 0;
		_watchdogProbeFailures = 0;
		_lastGameFeedbackAt = DateTime.MinValue;
		UpdateConnection();
		_antiDropRestartCount++;
		_watchdogWaitingSince = DateTime.Now;
		await Task.Delay(1500);
		try
		{
			Log(string.Format(L.T("log.antiDropRestarting"), _antiDropRestartCount));
			await StartGameThroughSteamAsync();
			await WaitAndReconnectAsync();
		}
		catch (Exception ex2)
		{
			Log(string.Format(L.T("log.launchFailed"), ex2.Message));
		}
	}

	private async Task WaitAndReconnectAsync()
	{
		DateTime deadline = DateTime.Now.AddMinutes(3.0);
		while (DateTime.Now < deadline && (object)GameReader.FindGameWindow() == null)
		{
			await Task.Delay(3000);
		}
		if ((object)GameReader.FindGameWindow() == null)
		{
			Log(L.T("log.gameNotFound"));
			return;
		}
		await Task.Delay(4000);
		await InitializeAssistantAsync();
		if (_initialized && (object)_window != null && !_executionEnabled)
		{
			await Task.Delay(3000);
			await StartMonitoringAsync();
			Log(L.T("log.antiDropAutoStarted"));
		}
	}

	private void SetProgress(int percent)
	{
		if (InvokeRequired)
		{
			InvokeOnUi(() =>
			{
				SetProgress(percent);
			});
		}
		else if (!_progressBar.IsDisposed)
		{
			_progressBar.Value = Math.Clamp(percent, 0, 100);
			_progressLabel.Text = string.Format(L.T("run.progress"), _progressBar.Value);
		}
	}

	private void BuildLog()
	{
		Panel panel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.PageBg
		};
		panel.Controls.Add(MakeHeading(L.T("log.title"), L.T("log.desc")));
		_logView = new TextBox
		{
			Multiline = true,
			ReadOnly = true,
			ScrollBars = ScrollBars.Vertical,
			BackColor = Theme.Surface,
			ForeColor = Theme.Text,
			Font = Theme.UiFont("Cascadia Mono", 9f),
			BorderStyle = BorderStyle.FixedSingle,
			Location = new Point(0, 80),
			Size = new Size(Math.Max(560, _pageHost.ClientSize.Width - 44), Math.Max(240, _pageHost.ClientSize.Height - 90)),
			Anchor = (AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right)
		};
		panel.Controls.Add(_logView);
		Button button = MakeButton(L.T("log.clear"), primary: false);
		button.SetBounds(0, Math.Max(120, panel.ClientSize.Height - 40), 108, 30);
		button.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
		button.Click += (object? _, EventArgs _) =>
		{
			_log.Clear();
			RefreshLog();
		};
		panel.Controls.Add(button);
		_pageHost.Controls.Add(panel);
		RefreshLog();
	}

	private async Task ReadWarehouseAsync()
	{
		if (!_initialized || _initializing)
		{
			Log("初始化通过前不能读取仓库。");
		}
		else
		{
			if (_busy || Engine.IsRunning)
			{
				return;
			}
			if ((object)_window == null)
			{
				await PollForGameAsync();
			}
			if ((object)_window != null)
			{
				_warehouseIconScanAttempted = true;
				_busy = true;
				if (!_refreshWarehouseButton.IsDisposed)
				{
					_refreshWarehouseButton.Enabled = false;
				}
				try
				{
					WarehouseScan warehouseScan = await Engine.RefreshAsync(_window, CancellationToken.None);
					StoreInventory(warehouseScan);
					Log(string.Format(L.T("log.readWarehouse"), warehouseScan.SlotUsed, warehouseScan.SlotTotal, warehouseScan.Items.Count));
					return;
				}
				catch (Exception ex)
				{
					Log(string.Format(L.T("log.readFailed"), ex.Message));
					return;
				}
				finally
				{
					_busy = false;
					if (!_refreshWarehouseButton.IsDisposed)
					{
						_refreshWarehouseButton.Enabled = true;
					}
				}
			}
			Log(L.T("log.gameNotFound"));
		}
	}

	private void RenderWarehouse()
	{
		if (_itemList.IsDisposed || _slotLabel.IsDisposed || _activePage != "warehouse")
		{
			return;
		}
		UpdateWarehouseFilterButton();
		WarehouseScan lastScan = _lastScan;
		foreach (Control item in _itemList.Controls.Cast<Control>().ToList())
		{
			foreach (PictureBox item2 in item.Controls.OfType<PictureBox>().Concat(item.Controls.Cast<Control>().SelectMany((Control child) => child.Controls.OfType<PictureBox>())))
			{
				item2.Image?.Dispose();
			}
			_itemList.Controls.Remove(item);
			item.Dispose();
		}
		if ((object)lastScan == null)
		{
			_slotLabel.Text = L.T("warehouse.slotsUnread");
			return;
		}
		_slotLabel.Text = string.Format(L.T("warehouse.slots"), lastScan.SlotUsed, lastScan.SlotTotal);
		_warehousePriceLabels.Clear();
		IEnumerable<ItemEntry> source = CollapseWarehouseItems(lastScan.Items);
		if (_warehouseFilterMode == "marketable")
		{
			source = source.Where((ItemEntry item) => GameVision.IsMarketable(item.Name));
		}
		foreach (ItemEntry item3 in _warehouseSortMode switch
		{
			"quality" => (IEnumerable<ItemEntry>)(from item in source
				orderby QualityOrder(item.Quality), item.GridRow, item.GridColumn
				select item), 
			"price-desc" => from item in source
				orderby CachedPriceValue(item) ?? double.NegativeInfinity descending, item.GridRow, item.GridColumn
				select item, 
			"price-asc" => from item in source
				orderby CachedPriceValue(item) ?? double.PositiveInfinity, item.GridRow, item.GridColumn
				select item, 
			_ => from item in source
				orderby item.GridRow, item.GridColumn
				select item, 
		})
		{
			int num = Math.Max(320, (_itemList.ClientSize.Width - 36) / 2);
			Color qualityColor = Theme.ColorOf(item3.Quality);
			Panel row = new Panel
			{
				Width = num,
				Height = 58,
				BackColor = Theme.Surface,
				Margin = new Padding(4)
			};
			row.Paint += (object? _, PaintEventArgs e) =>
			{
				using Pen pen = new Pen(qualityColor, 1.5f);
				e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);
			};
			Panel iconFrame = new Panel
			{
				Location = new Point(5, 6),
				Size = new Size(44, 44),
				BackColor = Theme.SurfaceAlt
			};
			iconFrame.Paint += (object? _, PaintEventArgs e) =>
			{
				using Pen pen = new Pen(qualityColor, 2f);
				e.Graphics.DrawRectangle(pen, 1, 1, iconFrame.Width - 3, iconFrame.Height - 3);
			};
			Bitmap bitmap = DecodeItemIcon(item3.IconPng);
			if (bitmap != null)
			{
				iconFrame.Controls.Add(new PictureBox
				{
					Image = bitmap,
					Location = new Point(4, 4),
					Size = new Size(36, 36),
					SizeMode = PictureBoxSizeMode.Zoom,
					BackColor = Color.Transparent
				});
			}
			row.Controls.Add(iconFrame);
			string text = ((item3.Count > 1) ? $"{item3.Name} ×{item3.Count}" : item3.Name);
			row.Controls.Add(new Label
			{
				Text = text,
				ForeColor = qualityColor,
				Font = Theme.UiFont(Font.FontFamily, 9.5f, FontStyle.Bold),
				AutoEllipsis = true,
				Size = new Size(num - 72, 22),
				Location = new Point(56, 7)
			});
			bool flag = GameVision.IsMarketable(item3.Name);
			Label value = new Label
			{
				Text = L.T(flag ? "warehouse.tradable" : "warehouse.nonTradable"),
				ForeColor = qualityColor,
				BackColor = Theme.SurfaceAlt,
				BorderStyle = BorderStyle.FixedSingle,
				TextAlign = ContentAlignment.MiddleCenter,
				Font = Theme.UiFont(Font.FontFamily, 8f, FontStyle.Bold),
				Size = new Size(68, 19),
				Location = new Point(56, 32)
			};
			row.Controls.Add(value);
			Label value2 = new Label
			{
				Text = FormatCachedPrice(item3),
				ForeColor = Theme.Good,
				Font = Theme.UiFont(Font.FontFamily, 8f),
				AutoEllipsis = true,
				TextAlign = ContentAlignment.MiddleLeft,
				Size = new Size(Math.Max(76, num - 140), 20),
				Location = new Point(132, 32)
			};
			row.Controls.Add(value2);
			_warehousePriceLabels[WarehouseItemKey(item3)] = value2;
			_itemList.Controls.Add(row);
		}
	}

	private static List<ItemEntry> CollapseWarehouseItems(IEnumerable<ItemEntry> source)
	{
		return (from item in source.GroupBy(WarehouseItemKey, StringComparer.OrdinalIgnoreCase).Select((IGrouping<string, ItemEntry> @group) =>
			{
				ItemEntry itemEntry = (from item in @group
					orderby item.GridRow, item.GridColumn
					select item).First();
				byte[] iconPng = @group.Select((ItemEntry item) => item.IconPng).FirstOrDefault((byte[] bytes) => bytes != null && bytes.Length > 0);
				return itemEntry with
				{
					Count = @group.Sum((ItemEntry item) => Math.Max(1, item.Count)),
					IconPng = iconPng,
					NameReliable = @group.Any((ItemEntry item) => item.NameReliable)
				};
			})
			orderby item.GridRow, item.GridColumn
			select item).ToList();
	}

	private static string WarehouseItemKey(ItemEntry item)
	{
		string input = Regex.Replace(item.Name.Trim(), "\\s*\\[\\s*M\\s*\\]\\s*$", "", RegexOptions.IgnoreCase);
		input = Regex.Replace(input, "\\s+", " ").ToUpperInvariant();
		return $"{input}|{item.Quality.Trim().ToUpperInvariant()}|{(GameVision.IsMarketable(item.Name) ? "marketable" : "nonmarketable")}";
	}

	private static int QualityOrder(string quality)
	{
		if (string.Equals(quality, Theme.Consumable.Name, StringComparison.Ordinal))
		{
			return -1;
		}
		if (string.Equals(quality, Theme.Unknown.Name, StringComparison.Ordinal))
		{
			return int.MaxValue;
		}
		for (int i = 0; i < Theme.Qualities.Count; i++)
		{
			if (string.Equals(Theme.Qualities[i].Name, quality, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return int.MaxValue;
	}

	private double? CachedPriceValue(ItemEntry item)
	{
		MarketPriceQuote marketPriceQuote = _marketPrices.Cached(item, _settings.MarketCurrencyCode);
		if ((object)marketPriceQuote == null || !marketPriceQuote.Available)
		{
			return null;
		}
		Match match = Regex.Match(string.IsNullOrWhiteSpace(marketPriceQuote.LowestPrice) ? marketPriceQuote.MedianPrice : marketPriceQuote.LowestPrice, "[0-9][0-9,.]*");
		if (!match.Success)
		{
			return null;
		}
		if (!double.TryParse(match.Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result))
		{
			return null;
		}
		return result;
	}

	private static Bitmap? DecodeItemIcon(byte[]? bytes)
	{
		if (bytes == null || bytes.Length == 0)
		{
			return null;
		}
		try
		{
			using MemoryStream stream = new MemoryStream(bytes);
			using Bitmap original = new Bitmap(stream);
			return new Bitmap(original);
		}
		catch
		{
			return null;
		}
	}

	private string FormatCachedPrice(ItemEntry item)
	{
		MarketPriceQuote quote = _marketPrices.Cached(item, _settings.MarketCurrencyCode);
		if (!HasDisplayPrice(quote))
		{
			return "";
		}
		return FormatQuote(quote);
	}

	private static bool HasDisplayPrice(MarketPriceQuote? quote)
	{
		if ((object)quote != null && quote.Available)
		{
			if (string.IsNullOrWhiteSpace(quote.LowestPrice))
			{
				return !string.IsNullOrWhiteSpace(quote.MedianPrice);
			}
			return true;
		}
		return false;
	}

	private async Task QueryAllPricesAsync()
	{
		if (_busy || Engine.IsRunning)
		{
			return;
		}
		if (!_initialized || _initializing)
		{
			Log("初始化通过前不能查询 Steam 价格。");
			return;
		}
		if ((object)_lastScan == null || _lastScan.Items.Count == 0)
		{
			await ReadWarehouseAsync();
			if ((object)_lastScan == null || _lastScan.Items.Count == 0)
			{
				return;
			}
		}
		if (_queryAllPricesButton.IsDisposed)
		{
			return;
		}
		_queryAllPricesButton.Enabled = false;
		List<ItemEntry> items = CollapseWarehouseItems(_lastScan.Items.Where((ItemEntry item) => item.NameReliable && (item.IsMarketable || GameVision.IsMarketable(item.Name))));
		int quotesFound = 0;
		int noListings = 0;
		int requestFailures = 0;
		int loggedFailures = 0;
		try
		{
			if (items.Count == 0)
			{
				if (!_warehousePriceStatus.IsDisposed)
				{
					_warehousePriceStatus.Text = L.T("warehouse.priceNone");
				}
				return;
			}
			for (int index = 0; index < items.Count; index++)
			{
				ItemEntry itemEntry = items[index];
				if (_warehousePriceStatus.IsDisposed)
				{
					break;
				}
				string key = WarehouseItemKey(itemEntry);
				_warehousePriceLabels.TryGetValue(key, out Label label);
				_warehousePriceStatus.Text = string.Format(L.T("warehouse.priceProgress"), index + 1, items.Count, itemEntry.Name);
				try
				{
					MarketPriceQuote quote = await _marketPrices.QueryAsync(itemEntry, _settings);
					if (label != null && !label.IsDisposed)
					{
						label.Text = FormatQuote(quote);
					}
					if (HasDisplayPrice(quote))
					{
						quotesFound++;
					}
					else if (quote.Status.StartsWith("no-listing", StringComparison.Ordinal) || quote.Status == "ok")
					{
						noListings++;
					}
					else
					{
						requestFailures++;
						if (loggedFailures < 3)
						{
							Log("Steam 查价失败：" + itemEntry.Name + "（" + quote.Status + "）");
							loggedFailures++;
						}
					}
				}
				catch (Exception ex)
				{
					requestFailures++;
					if (label != null && !label.IsDisposed)
					{
						label.Text = L.T("warehouse.priceError");
					}
					if (loggedFailures < 3)
					{
						Log("Steam 查价失败：" + itemEntry.Name + "（" + ex.Message + "）");
						loggedFailures++;
					}
				}
				label = null;
			}
			if (!_warehousePriceStatus.IsDisposed)
			{
				_warehousePriceStatus.Text = string.Format(L.T("warehouse.priceDone"), quotesFound, noListings, requestFailures);
			}
		}
		finally
		{
			if (!_queryAllPricesButton.IsDisposed)
			{
				_queryAllPricesButton.Enabled = true;
			}
		}
	}

	private string FormatQuote(MarketPriceQuote quote)
	{
		if (quote.Available && HasDisplayPrice(quote))
		{
			string text = string.Format(L.T("warehouse.priceFound"), quote.LowestPrice, quote.MedianPrice);
			if (!(quote.Status == "ok") || _marketPrices.IsStale(quote))
			{
				return text + " · " + L.T("warehouse.priceStale");
			}
			return text;
		}
		if (quote.Status.StartsWith("rate-limited", StringComparison.Ordinal))
		{
			return L.T("warehouse.priceLimited");
		}
		if (quote.Status.StartsWith("error", StringComparison.Ordinal))
		{
			return L.T("warehouse.priceError");
		}
		return L.T("warehouse.priceNoListing");
	}

	private static string LocalizeNote(string note)
	{
		if (note.StartsWith("note.items:", StringComparison.Ordinal))
		{
			return string.Format(L.T("note.items"), note.Substring("note.items:".Length));
		}
		return L.T(note);
	}

	private void SaveWarehouseSettings()
	{
		_settings.WarehouseCheckMinutes = (int)_warehouseMinutes.Value;
		_settings.WarehouseThresholdPercent = (int)_warehouseThreshold.Value;
		Persist();
	}

	private void SaveChestSettings()
	{
		_settings.ChestCheckMinutes = (int)_chestMinutes.Value;
		_settings.SortAfterSupplyBoxClaim = _sortAfterChestClaim.Checked;
		_settings.CollectSupplyBoxesAfterSort = _collectAfterSort.Checked;
		Persist();
	}

	private void SaveRepairSettings()
	{
		_settings.RepairEnabled = _repairEnable.Checked;
		_settings.RepairCheckMinutes = (int)_repairMinutes.Value;
		_settings.RepairThresholdPercent = (int)_repairThreshold.Value;
		Persist();
	}

	private void SaveQualitySettings()
	{
		try
		{
			_qualityGrid.EndEdit();
			List<QualityRule> list = new List<QualityRule>();
			string[] source = new string[7] { "普通", "优秀", "稀有", "史诗", "传说", "神话", "异域" };
			foreach (DataGridViewRow item in (IEnumerable)_qualityGrid.Rows)
			{
				string label = Convert.ToString(item.Cells["Quality"].Value) ?? "";
				string quality = source.FirstOrDefault((string q) => QualityLabel(q) == label) ?? label;
				list.Add(new QualityRule(quality)
				{
					Favorite = Convert.ToBoolean(item.Cells["Favorite"].Value ?? ((object)false)),
					Dismantle = Convert.ToBoolean(item.Cells["Dismantle"].Value ?? ((object)false)),
					Sell = Convert.ToBoolean(item.Cells["Sell"].Value ?? ((object)false))
				});
			}
			_settings.QualityRules = list;
			Persist();
			MessageBox.Show(L.T("msg.qualitySaved"), L.T("msg.doneTitle"), MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		catch (Exception ex)
		{
			MessageBox.Show(string.Format(L.T("msg.qualitySaveFailed"), ex.Message), L.T("msg.saveTitle"), MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void BrowseForGame()
	{
		using OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = L.T("settings.gamePathLabel"),
			Filter = "Desktop Racer (*.exe)|*.exe|所有文件 (*.*)|*.*",
			CheckFileExists = true
		};
		string text = ResolveGameExecutable();
		if (!string.IsNullOrWhiteSpace(text))
		{
			openFileDialog.InitialDirectory = Path.GetDirectoryName(text);
		}
		if (openFileDialog.ShowDialog(this) == DialogResult.OK)
		{
			_gamePathBox.Text = openFileDialog.FileName;
		}
	}

	private void SaveAntiDropSettings()
	{
		_settings.AntiDropEnabled = _antiDropEnable.Checked;
		_settings.AntiDropCheckMinutes = (int)_antiDropMinutes.Value;
		_settings.GameExePath = _gamePathBox.Text.Trim();
		Persist();
		SyncWatchdog();
		if (_settings.AntiDropEnabled && ResolveGameExecutable() == null)
		{
			Log(L.T("log.gamePathMissing"));
		}
		else if (_settings.AntiDropEnabled)
		{
			Log($"防掉线已启用：每 {_settings.AntiDropCheckMinutes} 分钟检查游戏，异常时自动重启。");
		}
		else
		{
			Log("防掉线已关闭。");
		}
		MessageBox.Show(L.T("settings.saved"), L.T("msg.doneTitle"), MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
	}

	private void SaveSettings()
	{
		if (_delayMin.Value > _delayMax.Value)
		{
			MessageBox.Show(L.T("msg.intervalInvalid"), L.T("msg.intervalTitle"), MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		bool flag = ((_languageBox.SelectedIndex == 1) ? "en" : "zh") != L.Lang;
		_settings.Language = ((_languageBox.SelectedIndex == 1) ? "en" : "zh");
		_settings.CloseToTray = _closeBox.SelectedIndex == 1;
		_settings.DelayMinSeconds = (int)_delayMin.Value;
		_settings.DelayMaxSeconds = (int)_delayMax.Value;
		_settings.WaitForReconnect = _waitReconnect.Checked;
		L.Lang = _settings.Language;
		Persist();
		RefreshGuardText();
		if (flag)
		{
			RebuildForLanguage();
		}
		else
		{
			MessageBox.Show(L.T("settings.saved"), L.T("msg.doneTitle"), MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
	}

	private void RebuildForLanguage()
	{
		ApplyFormText();
		_headerTitle.Text = L.B("header.title");
		foreach (var (text2, button2) in _navButtons)
		{
			button2.Text = L.B("nav." + text2);
		}
		_tray.Text = L.T("app.title");
		ContextMenuStrip contextMenuStrip = _tray.ContextMenuStrip;
		if (contextMenuStrip != null)
		{
			ToolStripItemCollection items = contextMenuStrip.Items;
			if (items != null && items.Count >= 2)
			{
				contextMenuStrip.Items[0].Text = L.T("tray.open");
				contextMenuStrip.Items[1].Text = L.T("tray.exit");
			}
		}
		ShowPage(_activePage, forceRebuild: true);
		UpdateConnection();
	}

	private void EnforceExclusive(int rowIndex, int columnIndex)
	{
		if (rowIndex < 0 || columnIndex < 0)
		{
			return;
		}
		string name = _qualityGrid.Columns[columnIndex].Name;
		bool flag = ((name == "Dismantle" || name == "Sell") ? true : false);
		if (flag && Convert.ToBoolean(_qualityGrid.Rows[rowIndex].Cells[name].Value ?? ((object)false)))
		{
			string columnName = ((name == "Dismantle") ? "Sell" : "Dismantle");
			if (Convert.ToBoolean(_qualityGrid.Rows[rowIndex].Cells[columnName].Value ?? ((object)false)))
			{
				_qualityGrid.Rows[rowIndex].Cells[columnName].Value = false;
			}
		}
	}

	private void Persist()
	{
		try
		{
			_settings.Save();
			Log(L.T("log.settingsSaved"));
			UpdateConnection();
		}
		catch (Exception ex)
		{
			MessageBox.Show(string.Format(L.T("msg.saveFailed"), ex.Message), L.T("msg.saveTitle"), MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private FlowLayoutPanel MakePage()
	{
		FlowLayoutPanel page = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			AutoScroll = true,
			BackColor = Theme.PageBg,
			Padding = new Padding(0, 0, 8, 8)
		};
		page.SizeChanged += (object? _, EventArgs _) =>
		{
			foreach (Control control in page.Controls)
			{
				control.Width = Math.Max(660, page.ClientSize.Width - 30);
			}
		};
		return page;
	}

	private Panel MakeCard(int height)
	{
		Panel card = new CardPanel
		{
			Height = height,
			Width = Math.Max(660, _pageHost.ClientSize.Width - 52),
			BackColor = Theme.PageBg,
			Margin = new Padding(0, 0, 0, 12)
		};
		return card;
	}

	private Panel MakeStatCard(string title, string value, string note, Action<Label>? onValueCreated = null, string? actionText = null, Func<Task>? action = null, Action<Button>? button = null)
	{
		Panel card = new CardPanel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.PageBg,
			Margin = new Padding(0, 0, 12, 0)
		};
		card.Controls.Add(new Label
		{
			Text = title,
			ForeColor = Theme.Muted,
			AutoSize = true,
			Location = new Point(18, 14)
		});
		Label label = new Label
		{
			Text = value,
			ForeColor = Theme.Text,
			Font = Theme.UiFont(Font.FontFamily, 15f, FontStyle.Bold),
			AutoSize = true,
			Location = new Point(18, 40)
		};
		onValueCreated?.Invoke(label);
		card.Controls.Add(label);
		Label value2 = new Label
		{
			Text = note,
			ForeColor = Theme.Muted,
			AutoSize = false,
			AutoEllipsis = true,
			Size = new Size(Math.Max(120, card.Width - 36), 40),
			Location = new Point(18, 76),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		card.Controls.Add(value2);
		if (action != null)
		{
			Button button2 = MakeButton(actionText ?? L.T("overview.runNow"), primary: false);
			button2.SetBounds(18, 116, 120, 28);
			button2.Click += async (object? _, EventArgs _) =>
			{
				await action();
			};
			button?.Invoke(button2);
			card.Controls.Add(button2);
		}
		return card;
	}

	private Label MakeHeading(string title, string subtitle)
	{
		Label label = new Label
		{
			Text = title,
			ForeColor = Theme.Text,
			Font = Theme.UiFont(Font.FontFamily, 13f, FontStyle.Bold),
			AutoSize = false,
			Height = 68,
			Width = Math.Max(660, _pageHost.ClientSize.Width - 52)
		};
		label.Paint += (object? _, PaintEventArgs e) =>
		{
			TextRenderer.DrawText(e.Graphics, subtitle, Theme.UiFont(Font.FontFamily, 9f), new Rectangle(0, 30, label.Width, 34), Theme.Muted, TextFormatFlags.WordBreak);
		};
		return label;
	}

	private Label MakeCardTitle(string text, int x, int y)
	{
		return new Label
		{
			Text = text,
			ForeColor = Theme.Text,
			Font = Theme.UiFont(Font.FontFamily, 10.5f, FontStyle.Bold),
			AutoSize = true,
			Location = new Point(x, y)
		};
	}

	private Label MakeFieldLabel(string text, int x, int y)
	{
		return new Label
		{
			Text = text,
			ForeColor = Theme.Muted,
			AutoSize = true,
			Location = new Point(x, y)
		};
	}

	private static void AddBullets(Control parent, IEnumerable<string> lines, int x, int y, int lineHeight)
	{
		int num = 0;
		foreach (string line in lines)
		{
			parent.Controls.Add(new Label
			{
				Text = "·  " + line,
				ForeColor = Theme.Muted,
				AutoSize = false,
				Size = new Size(420, 24),
				Location = new Point(x, y + num * lineHeight)
			});
			num++;
		}
	}

	private static void ConfigureNumber(NumericUpDown number, int min, int max, int value, int x, int y, int width)
	{
		number.Minimum = min;
		number.Maximum = max;
		number.Value = Math.Clamp(value, min, max);
		number.Location = new Point(x, y);
		number.Size = new Size(width, 28);
		number.BackColor = Theme.Surface;
		number.ForeColor = Theme.Text;
		number.BorderStyle = BorderStyle.FixedSingle;
	}

	private Button MakeButton(string text, bool primary)
	{
		Button button = new Button();
		button.Text = text;
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = (primary ? Theme.Accent : Theme.Surface);
		button.ForeColor = (primary ? Color.White : Theme.Text);
		button.Font = Theme.UiFont(Font.FontFamily, 9.5f, FontStyle.Bold);
		button.Cursor = Cursors.Hand;
		button.UseVisualStyleBackColor = false;
		button.Size = new Size(118, 30);
		button.FlatAppearance.BorderSize = ((!primary) ? 1 : 0);
		button.FlatAppearance.BorderColor = Theme.BorderStrong;
		button.FlatAppearance.MouseOverBackColor = (primary ? Color.FromArgb(50, 128, 245) : Theme.SurfaceAlt);
		return button;
	}

	private static void CopyButtonStyle(Button target, Button source)
	{
		target.FlatStyle = source.FlatStyle;
		target.BackColor = source.BackColor;
		target.ForeColor = source.ForeColor;
		target.Font = source.Font;
		target.Cursor = source.Cursor;
		target.UseVisualStyleBackColor = false;
		target.FlatAppearance.BorderSize = source.FlatAppearance.BorderSize;
		target.FlatAppearance.BorderColor = source.FlatAppearance.BorderColor;
		target.FlatAppearance.MouseOverBackColor = source.FlatAppearance.MouseOverBackColor;
		source.Dispose();
	}

	private static Bitmap? LoadAssetOrNull(string name)
	{
		string text = Path.Combine(AppContext.BaseDirectory, "Assets", name);
		if (!File.Exists(text))
		{
			return null;
		}
		try
		{
			return new Bitmap(text);
		}
		catch
		{
			return null;
		}
	}

	private static Bitmap Fit(Bitmap? source, int size)
	{
		if (source != null)
		{
			return GameReader.Fit(source, size);
		}
		return new Bitmap(size, size);
	}

	private void Log(string message)
	{
		if (IsDisposed || Disposing)
		{
			return;
		}
		if (InvokeRequired)
		{
			InvokeOnUi(() =>
			{
				Log(message);
			});
			return;
		}
		string text = $"[{DateTime.Now:HH:mm:ss}] {message}";
		_log.Add(text);
		if (_log.Count > 400)
		{
			_log.RemoveAt(0);
		}
		if (BackgroundCapture.Destination == null)
		{
			try
			{
				Directory.CreateDirectory(Deployment.Folder);
				lock (RuntimeLogLock)
				{
					File.AppendAllText(Path.Combine(Deployment.Folder, "assistant.log"), text + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				}
			}
			catch
			{
			}
		}
		if (_activePage == "log")
		{
			RefreshLog();
		}
	}

	private bool InvokeOnUi(Action action)
	{
		if (IsDisposed || Disposing)
		{
			return false;
		}
		if (!InvokeRequired)
		{
			action();
			return true;
		}
		if (!IsHandleCreated)
		{
			return false;
		}
		try
		{
			Invoke(action);
			return true;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private void OpenExternalUrl(string url)
	{
		try
		{
			Process.Start(new ProcessStartInfo(url)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			Log("无法打开链接：" + ex.Message);
		}
	}

	private async Task RunBackgroundCaptureAsync()
	{
		if (BackgroundCapture.State != BackgroundCapture.Mode.Requested)
		{
			return;
		}
		BackgroundCapture.State = BackgroundCapture.Mode.Running;
		string destination = BackgroundCapture.Destination ?? "";
		try
		{
			Directory.CreateDirectory(destination);
			UseBackgroundSampleData();
			_initializing = false;
			_initialized = true;
			_initializationError = "";
			LocateGame();
			UpdateConnection();
			RefreshAutomationChrome();
			RefreshEnvironment();
			await Task.Delay(1500);
			foreach (string page in BackgroundCapture.Pages)
			{
				ShowPage(page, forceRebuild: true);
				await Task.Delay(BackgroundShotInterval);
				BackgroundCapture.CaptureControl(this, Path.Combine(destination, page + ".png"));
			}
		}
		catch
		{
		}
		finally
		{
			BackgroundCapture.State = BackgroundCapture.Mode.Finished;
			Close();
		}
	}

	private void UseBackgroundSampleData()
	{
		if ((object)_lastScan == null || _lastScan.Items.Count == 0)
		{
			_lastScan = new WarehouseScan(96, 240, new List<ItemEntry>
			{
				new ItemEntry("DTR Coin [M]", Theme.Consumable.Name, 0, 0, 12)
				{
					IsConsumable = true,
					IsMarketable = true
				},
				new ItemEntry("推进器核心 [M]", Theme.Qualities[4].Name, 0, 1, 2)
				{
					IsMarketable = true
				},
				new ItemEntry("合金装甲板", Theme.Qualities[3].Name, 0, 2, 3),
				new ItemEntry("等离子线圈 [M]", Theme.Qualities[2].Name, 0, 3, 5)
				{
					IsMarketable = true
				},
				new ItemEntry("强化齿轮", Theme.Qualities[1].Name, 1, 0, 9),
				new ItemEntry("废料螺栓", Theme.Qualities[0].Name, 1, 1, 24),
				new ItemEntry("星舰蓝图 [M]", Theme.Qualities[5].Name, 1, 2, 1)
				{
					IsMarketable = true
				},
				new ItemEntry("曲率燃料", Theme.Qualities[2].Name, 1, 3, 7)
			}, "", Complete: true, CanScroll: false);
		}
		if (_chestLog.TotalOpened > 0)
		{
			return;
		}
		_chestLog.TotalOpened = 128;
		_chestLog.QualityTotals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
		{
			[Theme.Qualities[0].Name] = 61,
			[Theme.Qualities[1].Name] = 27,
			[Theme.Qualities[2].Name] = 19,
			[Theme.Qualities[3].Name] = 12,
			[Theme.Qualities[4].Name] = 9
		};
		_chestLog.RecentOpenedAt = Enumerable.Range(0, 18).Select((int index) => DateTime.Now.AddMinutes(-index * 3)).ToList();
		_chestLog.Records = new List<ChestRecord>
		{
			new ChestRecord
			{
				Name = "DTR Coin [M]",
				Quality = Theme.Consumable.Name,
				Count = 12,
				Time = DateTime.Now.AddMinutes(-3.0)
			},
			new ChestRecord
			{
				Name = "等离子线圈 [M]",
				Quality = Theme.Qualities[2].Name,
				Count = 5,
				Time = DateTime.Now.AddMinutes(-11.0)
			},
			new ChestRecord
			{
				Name = "星舰蓝图 [M]",
				Quality = Theme.Qualities[5].Name,
				Count = 1,
				Time = DateTime.Now.AddMinutes(-26.0)
			},
			new ChestRecord
			{
				Name = "强化齿轮",
				Quality = Theme.Qualities[1].Name,
				Count = 9,
				Time = DateTime.Now.AddMinutes(-42.0)
			}
		};
		_chestLog.TradeRecords = new List<ChestRecord>
		{
			new ChestRecord
			{
				Name = "推进器核心 [M]",
				Quality = Theme.Qualities[4].Name,
				Count = 2,
				Time = DateTime.Now.AddMinutes(-18.0)
			},
			new ChestRecord
			{
				Name = "DTR Coin [M]",
				Quality = Theme.Consumable.Name,
				Count = 8,
				Time = DateTime.Now.AddMinutes(-35.0)
			}
		};
	}

	private Button MakeDisclaimerLinkButton(string text, string iconName, Color iconColor, Color textColor, int width, Action action)
	{
		Button button = new Button
		{
			Text = text,
			Size = new Size(width, 28),
			FlatStyle = FlatStyle.Flat,
			BackColor = Theme.Surface,
			ForeColor = textColor,
			Font = Theme.UiFont(Font.FontFamily, 8.5f, FontStyle.Regular),
			Cursor = Cursors.Hand,
			UseVisualStyleBackColor = false,
			Image = LineIcons.Create(iconName, 16, iconColor),
			ImageAlign = ContentAlignment.MiddleLeft,
			TextAlign = ContentAlignment.MiddleCenter,
			TextImageRelation = TextImageRelation.ImageBeforeText,
			Padding = new Padding(6, 0, 2, 0)
		};
		button.FlatAppearance.BorderSize = 1;
		button.FlatAppearance.BorderColor = Theme.BorderStrong;
		button.FlatAppearance.MouseOverBackColor = Theme.SurfaceAlt;
		button.Click += (object? _, EventArgs _) =>
		{
			action();
		};
		button.Disposed += (object? _, EventArgs _) =>
		{
			button.Image?.Dispose();
		};
		return button;
	}

	private void RefreshLog()
	{
		if (!_logView.IsDisposed)
		{
			_logView.Lines = _log.ToArray();
			_logView.SelectionStart = _logView.TextLength;
			_logView.ScrollToCaret();
		}
	}

	private sealed class CardPanel : Panel
	{
		public CardPanel()
		{
			DoubleBuffered = true;
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
			base.OnPaintBackground(e);
			if (ClientSize.Width < 2 || ClientSize.Height < 2)
			{
				return;
			}
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using GraphicsPath path = Theme.Rounded(new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), 10);
			using SolidBrush brush = new SolidBrush(Theme.Surface);
			e.Graphics.FillPath(brush, path);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			if (ClientSize.Width < 2 || ClientSize.Height < 2)
			{
				return;
			}
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using GraphicsPath path = Theme.Rounded(new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), 10);
			using Pen pen = new Pen(Theme.Border);
			e.Graphics.DrawPath(pen, path);
		}
	}
}
