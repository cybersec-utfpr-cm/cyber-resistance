using Godot;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MinimalisticTelnet;

public partial class TerminalController : Node
{
        [Signal]
        public delegate void OutputReceivedWithArgumentEventHandler(
                string output
        );

        [Signal]
        public delegate void ConnectionStartedEventHandler();

        [Signal]
        public delegate void ConnectionDelayedEventHandler();

        [Signal]
        public delegate void ConnectionSucceededEventHandler();

        [Signal]
        public delegate void ConnectionFailedEventHandler(string message);

        public string host { get; set; } = "127.0.0.1";
        public int port { get; set; } = 5000;
        public string username { get; set; } = "player";
        public string password { get; set; } = "player";
        public int loginTimeoutMs { get; set; } = 1000;
        public int maxAttempts { get; set; } = 10;
        public int retryDelayMs { get; set; } = 750;
        public int slowWarningAttempt { get; set; } = 4;
        public int totalTimeoutMs { get; set; } = 15000;

        private TelnetConnection telnet;
        private CancellationTokenSource readCts;
        private readonly CancellationTokenSource lifetimeCts = new();

        private Task connectionTask;
        private Task telnetTask;

        private readonly ConcurrentQueue<string> commandQueue = new();

        private volatile bool isConnected;
        private volatile bool isConnecting;

        public void StartConnection()
        {
                if (isConnected || isConnecting)
                {
                        Log.Info(
                                "TerminalController: tentativa duplicada ignorada."
                        );
                        return;
                }

                isConnecting = true;
                connectionTask = ConnectWithRetry();
        }

        private async Task ConnectWithRetry()
        {
                CallDeferred(
                        MethodName.EmitSignal,
                        SignalName.ConnectionStarted
                );

                Log.Info(
                        $"TerminalController: conectando em {host}:{port}."
                );

                int attemptsLimit = Math.Max(1, maxAttempts);
                int retryDelay = Math.Max(0, retryDelayMs);
                int warningAttempt = Math.Clamp(
                        slowWarningAttempt,
                        1,
                        attemptsLimit
                );

                using var timeoutCts =
                        CancellationTokenSource.CreateLinkedTokenSource(
                                lifetimeCts.Token
                        );

                timeoutCts.CancelAfter(
                        Math.Max(loginTimeoutMs, totalTimeoutMs)
                );

                CancellationToken token = timeoutCts.Token;

                try
                {
                        for (
                                int attempt = 1;
                                attempt <= attemptsLimit;
                                attempt++
                        )
                        {
                                try
                                {
                                        Log.Info(
                                                $"TerminalController: tentativa " +
                                                $"{attempt}/{attemptsLimit}."
                                        );

                                        TelnetConnection connectedTelnet =
                                                await Task.Run(
                                                        () =>
                                                        {
                                                                var candidate =
                                                                        new TelnetConnection(
                                                                                host,
                                                                                port
                                                                        );

                                                                candidate.Login(
                                                                        username,
                                                                        password,
                                                                        loginTimeoutMs
                                                                );

                                                                return candidate;
                                                        },
                                                        token
                                                );

                                        token.ThrowIfCancellationRequested();

                                        telnet = connectedTelnet;
                                        isConnected = true;

                                        readCts?.Cancel();
                                        readCts?.Dispose();

                                        readCts =
                                                CancellationTokenSource
                                                        .CreateLinkedTokenSource(
                                                                lifetimeCts.Token
                                                        );

                                        telnetTask = Task.Run(
                                                () => RunTelnetAsync(
                                                        readCts.Token
                                                )
                                        );

                                        Log.Info(
                                                "TerminalController: conexão estabelecida."
                                        );

                                        CallDeferred(
                                                MethodName.EmitSignal,
                                                SignalName.ConnectionSucceeded
                                        );

                                        return;
                                }
                                catch (OperationCanceledException)
                                {
                                        if (lifetimeCts.IsCancellationRequested)
                                                return;

                                        break;
                                }
                                catch (Exception exception)
                                {
                                        Log.Error(
                                                $"TerminalController: tentativa " +
                                                $"{attempt} falhou: " +
                                                exception.Message
                                        );
                                }

                                if (attempt == warningAttempt)
                                {
                                        CallDeferred(
                                                MethodName.EmitSignal,
                                                SignalName.ConnectionDelayed
                                        );
                                }

                                if (attempt < attemptsLimit)
                                {
                                        await Task.Delay(
                                                retryDelay,
                                                token
                                        );
                                }
                        }

                        if (!lifetimeCts.IsCancellationRequested)
                        {
                                EmitConnectionFailure(
                                        "Não foi possível iniciar o terminal. " +
                                        "Verifique o Docker e tente novamente."
                                );
                        }
                }
                catch (OperationCanceledException)
                {
                        if (!lifetimeCts.IsCancellationRequested)
                        {
                                EmitConnectionFailure(
                                        "O terminal não respondeu dentro do " +
                                        "tempo esperado. Tente novamente."
                                );
                        }
                }
                finally
                {
                        isConnecting = false;
                        connectionTask = null;
                }
        }

        private async Task RunTelnetAsync(CancellationToken token)
        {
                try
                {
                        while (!token.IsCancellationRequested)
                        {
                                while (
                                        commandQueue.TryDequeue(
                                                out string command
                                        )
                                )
                                {
                                        Log.Info(
                                                "TerminalController: comando enviado."
                                        );
                                        telnet.WriteLine(command);
                                }

                                string output = telnet.Read();

                                if (!string.IsNullOrEmpty(output))
                                {
                                        CallDeferred(
                                                MethodName.EmitSignal,
                                                SignalName
                                                        .OutputReceivedWithArgument,
                                                output
                                        );
                                }

                                await Task.Delay(10, token);
                        }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                        Log.Error(
                                $"TerminalController: conexão encerrada: " +
                                exception.Message
                        );

                        CallDeferred(
                                MethodName.EmitSignal,
                                SignalName.OutputReceivedWithArgument,
                                "\nConexão encerrada.\n"
                        );

                        if (!lifetimeCts.IsCancellationRequested)
                        {
                                EmitConnectionFailure(
                                        "A conexão com o terminal foi encerrada. " +
                                        "Tente novamente."
                                );
                        }
                }
                finally
                {
                        isConnected = false;
                }
        }

        private void EmitConnectionFailure(string message)
        {
                Log.Error(
                        $"TerminalController: falha definitiva. {message}"
                );

                CallDeferred(
                        MethodName.EmitSignal,
                        SignalName.ConnectionFailed,
                        message
                );
        }

        public void SendCommand(string command)
        {
                if (!isConnected)
                {
                        Log.Error(
                                "TerminalController: comando ignorado sem conexão."
                        );
                        return;
                }

                commandQueue.Enqueue(command);
        }

        public override async void _ExitTree()
        {
                lifetimeCts.Cancel();
                readCts?.Cancel();

                try
                {
                        if (connectionTask != null)
                                await connectionTask;

                        if (telnetTask != null)
                                await telnetTask;
                }
                catch
                {
                }
                finally
                {
                        readCts?.Dispose();
                        lifetimeCts.Dispose();
                }
        }
}
