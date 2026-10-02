using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StopwatchOverlay.Platform.Linux;

/// <summary>
/// Ensures single-instance execution on Linux using a lock file and Unix domain socket IPC.
/// </summary>
public sealed class LinuxSingleInstanceService : ISingleInstanceService
{
    private FileStream? _lockStream;
    private Socket? _serverSocket;
    private CancellationTokenSource? _cts;
    private Action? _onShowExistingRequested;

    private static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StopwatchOverlay");

    private static string LockFilePath => Path.Combine(AppDataDir, "instance.lock");
    private static string SocketFilePath => Path.Combine(AppDataDir, "instance.sock");

    public bool TryAcquireSingleInstance(Action onShowExistingRequested)
    {
        _onShowExistingRequested = onShowExistingRequested;

        try
        {
            Directory.CreateDirectory(AppDataDir);

            _lockStream = new FileStream(
                LockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            StartSocketListener();
            return true;
        }
        catch (IOException)
        {
            // Another instance holds the lock file.
            return false;
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "LinuxSingleInstanceService.TryAcquireSingleInstance");
            return true;
        }
    }

    public void SignalExistingInstance()
    {
        try
        {
            if (!File.Exists(SocketFilePath)) return;

            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(SocketFilePath);
            client.Connect(endpoint);
            client.Send(Encoding.UTF8.GetBytes("SHOW\n"));
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "LinuxSingleInstanceService.SignalExistingInstance");
        }
    }

    private void StartSocketListener()
    {
        try
        {
            if (File.Exists(SocketFilePath))
            {
                File.Delete(SocketFilePath);
            }

            _serverSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(SocketFilePath);
            _serverSocket.Bind(endpoint);
            _serverSocket.Listen(5);

            _cts = new CancellationTokenSource();
            _ = AcceptConnectionsAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "LinuxSingleInstanceService.StartSocketListener");
        }
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _serverSocket != null)
        {
            try
            {
                using Socket client = await _serverSocket.AcceptAsync(cancellationToken).ConfigureAwait(false);
                byte[] buffer = new byte[64];
                int read = await client.ReceiveAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
                if (read > 0)
                {
                    _onShowExistingRequested?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                CrashLogger.LogRecoverable(ex, "LinuxSingleInstanceService.AcceptConnections");
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_serverSocket != null)
        {
            try
            {
                _serverSocket.Close();
                _serverSocket.Dispose();
            }
            catch { }
            _serverSocket = null;
        }

        try
        {
            if (File.Exists(SocketFilePath))
            {
                File.Delete(SocketFilePath);
            }
        }
        catch { }

        if (_lockStream != null)
        {
            try
            {
                _lockStream.Dispose();
            }
            catch { }
            _lockStream = null;
        }

        try
        {
            if (File.Exists(LockFilePath))
            {
                File.Delete(LockFilePath);
            }
        }
        catch { }
    }
}
