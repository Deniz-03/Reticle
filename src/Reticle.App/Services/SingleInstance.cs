using System.IO.Pipes;
using System.IO;

namespace Reticle.App.Services;

/// <summary>Local-session single-instance guard with a small activation pipe.</summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\Reticle.SingleInstance";
    private const string PipeName = "Reticle.Activate";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();
    public bool IsPrimary { get; }
    public event Action? Activated;
    public SingleInstance()
    {
        _mutex = new Mutex(true, MutexName, out bool created);
        IsPrimary = created;
        if (created) _ = ListenAsync(); else SignalExisting();
    }
    private void SignalExisting()
    {
        try { using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out); pipe.Connect(500); pipe.WriteByte(1); }
        catch (TimeoutException) { }
        catch (IOException) { }
    }
    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(_stop.Token);
                var byteBuffer = new byte[1];
                if (await pipe.ReadAsync(byteBuffer, _stop.Token) > 0) Activated?.Invoke();
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }
    }
    public void Dispose() { _stop.Cancel(); if (IsPrimary) _mutex.ReleaseMutex(); _mutex.Dispose(); _stop.Dispose(); }
}
