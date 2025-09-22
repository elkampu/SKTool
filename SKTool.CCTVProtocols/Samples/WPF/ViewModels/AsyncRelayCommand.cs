using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using SKTool.CCTVProtocols.Samples.WPF.Services;

namespace SKTool.CCTVProtocols.Samples.WPF.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task>? _execute;
    private readonly Func<CancellationToken, Task>? _executeWithToken;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _onError;
    private CancellationTokenSource? _cts;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null, Action<Exception>? onError = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onError = onError;
    }

    public AsyncRelayCommand(Func<CancellationToken, Task> executeWithToken, Func<bool>? canExecute = null, Action<Exception>? onError = null)
    {
        _executeWithToken = executeWithToken ?? throw new ArgumentNullException(nameof(executeWithToken));
        _canExecute = canExecute;
        _onError = onError;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        try
        {
            if (_executeWithToken is not null)
            {
                Cancel(); // cancel any prior run
                _cts = new CancellationTokenSource();
                await _executeWithToken(_cts.Token);
            }
            else if (_execute is not null)
            {
                await _execute();
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception ex)
        {
            if (_onError != null) _onError(ex);
            else CameraErrorHandler.Handle(ex);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    public void Cancel()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
    }
}