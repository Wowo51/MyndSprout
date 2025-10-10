//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;               // for Clipboard
using System.Windows.Input;
using MyndSprout;
using System.IO;
using Microsoft.Win32;
using MyndSproutApp.Actions;
using MyndSproutApp.Services;
using SqlContain;

public sealed class AgentViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; private set { _isRunning = value; Raise(nameof(IsRunning)); Raise(nameof(CanStart)); Raise(nameof(CanStop)); } }

    public bool CanStart => !IsRunning;
    public bool CanStop => IsRunning;

    private CancellationTokenSource? _cts;
    private SqlAgent? _agent;
    private readonly Action<string> _log;

    public AgentViewModel(Action<string> log) => _log = log;

    public async Task StartAsync(SqlAgent agent, string prompt)
    {
        if (IsRunning) return;
        _agent = agent;
        _cts = new CancellationTokenSource();
        IsRunning = true;

        try
        {
            // await guarantees finally runs even on exceptions/cancel/early returns
            var result = await _agent.RunAsync(prompt, _log, _cts.Token).ConfigureAwait(false);
            _log(result);
        }
        catch (OperationCanceledException)
        {
            _log("Agent canceled.");
        }
        catch (Exception ex)
        {
            _log("Agent crashed: " + ex.Message);
        }
        finally
        {
            IsRunning = false;          // <-- THIS reliably re-enables Start, disables Stop
            _cts?.Dispose();
            _cts = null;
            _agent = null;
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
    }
}

