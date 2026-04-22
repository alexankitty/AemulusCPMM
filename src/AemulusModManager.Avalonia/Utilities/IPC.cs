using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;

namespace AemulusModManager.Avalonia.Utilities;

public class IPC
{
    private readonly IPCService _ipcService;
    private readonly List<Func<string, Task>> _handlers = new();

    public IPC()
    {
        _ipcService = new IPCService();
        _ipcService.MessageReceived += HandleMessageReceived;
        IPCService.Register(_ipcService);
    }

    public int RegisterMessageHandler(Func<string, Task> handler)
    {
        _handlers.Add(handler);
        return _handlers.Count - 1;
    }

    public int UnregisterMessageHandler(int handlerId)
    {
        if (handlerId >= 0 && handlerId < _handlers.Count)
        {
            _handlers.RemoveAt(handlerId);
            return handlerId;
        }
        return -1;
    }

    private void HandleMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        foreach (var handler in _handlers)
        {
            // Fire and forget
            _ = handler(e.Message);
        }
    }

    public void SendMessage(string message) => _ipcService.SendMessage(message);
}

public class IPCService
{
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    public static void Register(IPCService service)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                using var pipeServer = new NamedPipeServerStream($"{Info.Name}", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipeServer.WaitForConnectionAsync();
                using var reader = new StreamReader(pipeServer);
                string? message = await reader.ReadLineAsync();
                if (!string.IsNullOrEmpty(message))
                    service.MessageReceived?.Invoke(service, new MessageReceivedEventArgs(message));
                // The using block will dispose and recreate the pipe for the next connection
            }
        });
    }

    public void SendMessage(string message)
    {
        using var pipeClient = new NamedPipeClientStream(".", $"{Info.Name}", PipeDirection.Out);
        pipeClient.Connect();
        if(pipeClient.IsConnected)
        {
            using var writer = new StreamWriter(pipeClient) { AutoFlush = true };
            writer.WriteLine(message);
        }
    }
}

public class MessageReceivedEventArgs : EventArgs
{
    public string Message { get; }

    public MessageReceivedEventArgs(string message)
    {
        Message = message;
    }
}