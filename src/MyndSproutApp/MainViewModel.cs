//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.
using MyndSprout;
using MyndSproutApp.Actions;
using MyndSproutApp.Services;
using Microsoft.Identity.Client;
using Microsoft.Win32;
using SqlContain;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;               // for Clipboard
using System.Windows.Input;

namespace MyndSproutApp
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        // ==== Logging config ====
        private readonly object _logLock = new();
        private readonly List<string> _log = new();     // all log lines (capped)
        private int _visibleLogCount = 20;               // how many recent lines to show
        private int _maxLogEntries = 20000;              // hard cap
        private int _maxlogLength = 50000;
        private bool _isRunning;
        private int _maxEpochs = 10;
        private bool _createNewDatabase = true;
        private string? _serverConnectionString; // optional when creating new DB
        private string _databaseName = "Bootstrap14";
        private string? _connectionString;       // used when CreateNewDatabase == false
        private string _prompt = "";
        private string _output = "";
        private bool _useIsComplete = false;
        public event PropertyChangedEventHandler? PropertyChanged;
        private readonly FilePromptService _fileService = new();
        private string? _promptFilePath;
        private bool _naturalLanguageResponse = false;
        private CancellationTokenSource? _cts;
        private bool _keepEpisodics = true;
        private bool _useSearch = false;
        private bool _containServer = false;
        private bool _containDatabase = true;
        private bool _queryOnly = false;
        private int _currentEpoch = 0;
        private int _sessionStartingEpoch = 1;
        private string? _sandBoxFolder;
        private SqlAgent? _currentAgent;

        public ICommand ImportFolderCommand { get; }
        public ICommand ExportDataCommand { get; }
        public ICommand ExportSchemaCommand { get; }
        public ICommand OpenPromptCommand { get; }
        public ICommand SavePromptCommand { get; }
        public ICommand SavePromptAsCommand { get; }
        public ICommand ClearLogExceptLastCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand CopyLogCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand ExportSchemaXmlCommand { get; }
        public ICommand BrowseSandBoxFolderCommand { get; }
        public ICommand UpdatePromptCommand { get; }

        public MainViewModel()
        {
            StartCommand = new RelayCommand(async _ => await StartAsync(), _ => IsNotRunning);
            StopCommand = new RelayCommand(_ => Stop(), _ => IsRunning);
            CopyLogCommand = new RelayCommand(_ => CopyLog(), _ => true);
            ClearLogCommand = new RelayCommand(_ => ClearLog(), _ => true);
            ImportFolderCommand = new RelayCommand(async _ => await DoImportFolderAsync(), _ => IsNotRunning);
            ExportDataCommand = new RelayCommand(async _ => await DoExportDataAsync(), _ => IsNotRunning);
            ExportSchemaCommand = new RelayCommand(async _ => await DoExportSchemaAsync(), _ => IsNotRunning);
            OpenPromptCommand = new RelayCommand(async _ => await _fileService.OpenPromptAsync(this, Log), _ => IsNotRunning);
            SavePromptCommand = new RelayCommand(async _ => await _fileService.SavePromptAsync(this, Log), _ => true);
            SavePromptAsCommand = new RelayCommand(async _ => await _fileService.SavePromptAsAsync(this, Log), _ => true);
            ClearLogExceptLastCommand = new RelayCommand(_ => ClearLogExceptLast(), _ => true);
            ExportSchemaXmlCommand = new RelayCommand(async _ => await DoExportSchemaXmlAsync(), _ => IsNotRunning);
            BrowseSandBoxFolderCommand = new RelayCommand(_ => BrowseSandBoxFolder(), _ => IsNotRunning);
            UpdatePromptCommand = new RelayCommand(_ => UpdatePrompt(), _ => IsRunning);
        }

        // Reactive props
        public string? SandBoxFolder
        {
            get => _sandBoxFolder;
            set { _sandBoxFolder = value; OnPropertyChanged(); }
        }

        public int CurrentEpoch
        {
            get => _currentEpoch;
            private set { _currentEpoch = value; OnPropertyChanged(); }
        }

        public string? PromptFilePath
        {
            get => _promptFilePath;
            set { _promptFilePath = value; OnPropertyChanged(); }
        }

        public bool NaturalLanguageResponse
        {
            get => _naturalLanguageResponse;
            set { _naturalLanguageResponse = value; OnPropertyChanged(); }
        }

        public bool KeepEpisodics
        {
            get => _keepEpisodics;
            set { _keepEpisodics = value; OnPropertyChanged(); }
        }

        public bool UseSearch
        {
            get => _useSearch;
            set { _useSearch = value; OnPropertyChanged(); }
        }

        public bool ContainServer
        {
            get => _containServer;
            set { _containServer = value; OnPropertyChanged(); }
        }

        public bool ContainDatabase
        {
            get => _containDatabase;
            set { _containDatabase = value; OnPropertyChanged(); }
        }

        public bool QueryOnly
        {
            get => _queryOnly;
            set { _queryOnly = value; OnPropertyChanged(); }
        }

        public bool UseIsComplete
        {
            get => _useIsComplete;
            set { _useIsComplete = value; OnPropertyChanged(); }
        }

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (_isRunning == value) return;
                _isRunning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotRunning));

                // Tell WPF to re-evaluate all ICommand.CanExecute
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsNotRunning => !IsRunning;

        public int MaxEpochs
        {
            get => _maxEpochs;
            set { _maxEpochs = Math.Max(1, value); OnPropertyChanged(); }
        }

        public bool CreateNewDatabase
        {
            get => _createNewDatabase;
            set { _createNewDatabase = value; OnPropertyChanged(); OnPropertyChanged(nameof(NotCreateNewDatabase)); }
        }
        public bool NotCreateNewDatabase => !CreateNewDatabase;

        public string? ServerConnectionString
        {
            get => _serverConnectionString;
            set { _serverConnectionString = value; OnPropertyChanged(); }
        }

        public string DatabaseName
        {
            get => _databaseName;
            set { _databaseName = value; OnPropertyChanged(); }
        }

        public string? ConnectionString
        {
            get => _connectionString;
            set { _connectionString = value; OnPropertyChanged(); }
        }

        public string Prompt
        {
            get => _prompt;
            set { _prompt = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Joined text of the last VisibleLogCount log lines.
        /// </summary>
        public string Output
        {
            get => _output;
            private set { _output = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// How many recent lines are shown in Output. Default 20.
        /// </summary>
        public int VisibleLogCount
        {
            get => _visibleLogCount;
            set
            {
                var v = Math.Clamp(value, 1, 10000);
                if (_visibleLogCount != v)
                {
                    _visibleLogCount = v;
                    OnPropertyChanged();
                    // Recompute Output when the window changes
                    RefreshVisibleOutput();
                }
            }
        }

        private void BrowseSandBoxFolder()
        {
            try
            {
                var owner = Application.Current?.MainWindow;
                string? folder = owner == null ? null : FolderDialog.SelectFolder(owner, SandBoxFolder);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    SandBoxFolder = folder;
                    Log($"SandBoxFolder set: {folder}");
                }
                else
                {
                    Log("SandBoxFolder: selection canceled.");
                }
            }
            catch (Exception ex)
            {
                Log("SandBoxFolder error: " + ex.Message);
            }
        }

        private async Task DoExportSchemaXmlAsync()
        {
            try
            {
                var action = new MyndSproutApp.Actions.ExportSchemaXmlAction(Log);
                await action.RunAsync(this);
            }
            catch (Exception ex)
            {
                Log("ExportSchemaXml error: " + ex.Message);
            }
        }


        /// <summary>
        /// Maximum total lines stored in memory (cap). Default 20,000.
        /// </summary>
        public int MaxLogEntries
        {
            get => _maxLogEntries;
            set
            {
                var v = Math.Clamp(value, 100, 2_000_000);
                if (_maxLogEntries != v)
                {
                    _maxLogEntries = v;
                    OnPropertyChanged();
                    EnforceCap();
                    RefreshVisibleOutput();
                }
            }
        }

        private async Task StartAsync()
        {
            if (IsRunning) return;

            ClearLog(); // fresh run view
            CurrentEpoch = 0; // reset epoch display for new run
            _sessionStartingEpoch = 1;
            IsRunning = true;
            _cts = new CancellationTokenSource();

            try
            {
                SqlAgent agent;

                if (CreateNewDatabase)
                {
                    var dbName = string.IsNullOrWhiteSpace(DatabaseName) ? "AgenticDb" : DatabaseName;
                    if (!string.IsNullOrWhiteSpace(ServerConnectionString))
                    {
                        agent = await AgentFactory.CreateAgentAsync(ServerConnectionString!, dbName, MaxEpochs);
                        Log($"Created/connected DB '{dbName}' using provided server connection string.");
                    }
                    else
                    {
                        agent = await AgentFactory.CreateAgentWithDefaultServerAsync(dbName, MaxEpochs);
                        Log($"Created/connected DB '{dbName}' using default LocalDB server.");
                    }
                }
                else
                {
                    var conn = BuildExistingDbConnectionString();
                    agent = await AgentFactory.CreateAgentFromConnectionStringAsync(conn, MaxEpochs);
                    var dbShown = string.IsNullOrWhiteSpace(DatabaseName) ? "(unspecified)" : DatabaseName;
                    Log($"Connected to existing database '{dbShown}' using {(string.IsNullOrWhiteSpace(ConnectionString) ? "auto-generated" : "provided")} connection string.");
                }
                agent.UseIsComplete = UseIsComplete;
                agent.NaturalLanguageResponse = NaturalLanguageResponse;
                agent.QueryOnly = QueryOnly;
                agent.UseSearch = UseSearch;
                agent.KeepEpisodics = KeepEpisodics;
                if (ContainServer || ContainDatabase)
                {
                    try
                    {
                        var opts = BuildHardenerOptions();

                        Log($"SqlContain: hardening scope = {opts.Scope}, server = '{opts.Server}', database = '{opts.Database}'.");
                        var rc = await Hardener.RunAsync(opts, Log);
                        if (rc != 0)
                        {
                            Log("SqlContain hardening failed (non-zero exit). Aborting start.");
                            return; // Do not run the agent if user asked to contain and it failed
                        }
                        Log("SqlContain hardening complete.");
                    }
                    catch (Exception ex)
                    {
                        Log("SqlContain error: " + ex.Message);
                        return; // Fail closed if requested containment cannot be established
                    }
                }
                _currentAgent = agent;
                string finalContext = await agent.RunAsync(Prompt ?? string.Empty, Log, _cts.Token);
                Log(finalContext);
            }
            catch (OperationCanceledException)
            {
                Log("Agent canceled.");
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message);
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
                _currentAgent = null;
            }
        }

        private string BuildExistingDbConnectionString()
        {
            // 1) If the user provided a full connection string, use it as-is.
            if (!string.IsNullOrWhiteSpace(ConnectionString))
                return ConnectionString!;

            // Choose a db name (fallback to AgenticDb if empty)
            var db = string.IsNullOrWhiteSpace(DatabaseName) ? "AgenticDb" : DatabaseName;

            // 2) If they provided a server-level connection string, attach Database=...
            if (!string.IsNullOrWhiteSpace(ServerConnectionString))
            {
                var s = ServerConnectionString!.Trim();

                // Only append Database= if not already present
                if (s.IndexOf("Database=", StringComparison.OrdinalIgnoreCase) < 0 &&
                    s.IndexOf("Initial Catalog=", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    if (!s.EndsWith(";")) s += ";";
                    s += $"Database={db};";
                }
                return s;
            }

            // 3) Default: LocalDB + Trusted_Connection for developer convenience
            return $"Server=(localdb)\\MSSQLLocalDB;Database={db};Trusted_Connection=True;";
        }

        private void Stop() => _cts?.Cancel();

        private void UpdatePrompt()
    	{
    		if (_currentAgent == null || !IsRunning)
    		{
    			Log("Cannot update prompt: Agent is not running.");
    			return;
    		}
    		_currentAgent.UpdatePrompt(this.Prompt);
    		Log("--- UPDATE PROMPT signal sent to agent. It will be applied at the start of the next epoch. ---");
    	}

    /// <summary>
    /// Thread-safe append to the log with capping and visible refresh.
    /// Safe to call from background threads.
    /// </summary>
    private void Log(string s)
        {
            if (string.IsNullOrEmpty(s)) return;

            TryUpdateSessionStartFromLogLine(s);
            // Detect "Epoch N starting."
            TryUpdateEpochFromLogLine(s);

            lock (_logLock)
            {
                _log.Add(s);
                EnforceCap_NoLock();
            }

            if (Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                RefreshVisibleOutput();
            }
            else
            {
                Application.Current?.Dispatcher?.Invoke(RefreshVisibleOutput);
            }
        }

        private void TryUpdateSessionStartFromLogLine(string line)
        {
            var m = Regex.Match(line ?? string.Empty, @"^Agent resumed\. Starting at absolute epoch (\d+)\.", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n))
            {
                _sessionStartingEpoch = n;
            }
        }

        private void TryUpdateEpochFromLogLine(string line)
        {
            var m = Regex.Match(line ?? string.Empty, @"^Epoch\s+(\d+)\s+starting\.", RegexOptions.IgnoreCase);
            if (!m.Success) return;

            if (int.TryParse(m.Groups[1].Value, out int absoluteEpoch))
            {
                int sessionEpoch = absoluteEpoch - _sessionStartingEpoch + 1;
                if (Application.Current?.Dispatcher?.CheckAccess() == true)
                    CurrentEpoch = sessionEpoch;
                else
                    Application.Current?.Dispatcher?.Invoke(() => CurrentEpoch = sessionEpoch);
            }
        }

        private void RefreshVisibleOutput()
        {
            // join only the last N items
            string result;
            lock (_logLock)
            {
                int count = _log.Count;
                int take = Math.Min(_visibleLogCount, count);
                if (take <= 0)
                {
                    result = string.Empty;
                }
                else
                {
                    var sb = new StringBuilder();
                    int start = count - take;
                    for (int i = start; i < count; i++)
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(_log[i]);
                    }
                    result = sb.ToString();
                }
            }

            string unescaped = System.Net.WebUtility.HtmlDecode(result);
            if (result.Length <= _maxlogLength)
                Output = unescaped;
            else
                Output = unescaped.Substring(unescaped.Length - _maxlogLength, _maxlogLength);
        }

        private void CopyLog()
        {
            string all;
            lock (_logLock)
            {
                all = string.Join(Environment.NewLine, _log);
            }
            try
            {
                Clipboard.SetText(Common.WrapInTags(all, "Log"));
                // give a small UI hint in the log
                Log($"[Copied {(_log.Count)} line(s) to clipboard]");
            }
            catch (Exception ex)
            {
                Log("Copy failed: " + ex.Message);
            }
        }

        private void ClearLog()
        {
            lock (_logLock)
            {
                _log.Clear();
            }
            RefreshVisibleOutput();
        }

        private void ClearLogExceptLast()
        {
            lock (_logLock)
            {
                if (_log.Count > 1)
                {
                    // Keep the last line only
                    var last = _log[^1].Trim();
                    _log.Clear();
                    _log.Add(last);
                }
                // If 0 or 1, nothing to do
            }
            RefreshVisibleOutput();
        }

        private void EnforceCap()
        {
            lock (_logLock) EnforceCap_NoLock();
        }

        private void EnforceCap_NoLock()
        {
            int over = _log.Count - _maxLogEntries;
            if (over > 0)
            {
                _log.RemoveRange(0, over);
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private async Task DoImportFolderAsync()
        {
            try
            {
                var owner = Application.Current?.MainWindow;
                string? folder = owner == null ? null : FolderDialog.SelectFolder(owner);
                if (string.IsNullOrWhiteSpace(folder))
                {
                    Log("ImportFolder: canceled.");
                    return;
                }

                // Ask user if they want to truncate existing table first (optional simple prompt)
                // Keep it simple: default false; change to true if you want a MessageBox prompt.
                bool truncateFirst = false;

                var action = new ImportFolderAction(Log);
                await action.RunAsync(this, folder, truncateFirst);
            }
            catch (Exception ex)
            {
                Log("ImportFolder error: " + ex.Message);
            }
        }

        private async Task DoExportSchemaAsync()
        {
            try
            {
                var action = new ExportSchemaAction(Log);
                await action.RunAsync(this);
            }
            catch (Exception ex)
            {
                Log("ExportSchema error: " + ex.Message);
            }
        }

        private async Task DoExportDataAsync()
        {
            try
            {
                var action = new ExportDataAction(Log);
                await action.RunAsync(this, includeEmptyTables: true);
            }
            catch (Exception ex)
            {
                Log("ExportData error: " + ex.Message);
            }
        }

        private HardenerOptions BuildHardenerOptions()
        {
            // Reuse the same connection-string derivation you use to open/export/etc.
            var connStr = ConnectionStringHelper.BuildFromViewModel(this);
            var csb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connStr);

            // Choose scope from the two checkboxes
            Scope scope = (ContainServer, ContainDatabase) switch
            {
                (true, true) => Scope.Both,
                (true, false) => Scope.Instance,
                (false, true) => Scope.Database,
                _ => Scope.Database // default (shouldn't be used if both are false)
            };

            return new HardenerOptions
            {
                Server = csb.DataSource ?? "",
                Auth = csb.IntegratedSecurity ? "Trusted" : "Sql",
                User = csb.UserID ?? "",
                Password = csb.Password ?? "",
                Database = string.IsNullOrWhiteSpace(csb.InitialCatalog) ? (DatabaseName ?? "") : csb.InitialCatalog,
                Scope = scope,
                Firewall = false,            // not requested here
                SqlServrPath = null ,         // optional; leave unset unless you need firewall config
                AllowSkippedDeny = true,
                AllowMissingTrigger = true,
            };
        }

    }
}


