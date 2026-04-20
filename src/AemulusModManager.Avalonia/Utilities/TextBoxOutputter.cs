using System;
using System.IO;
using System.Text;

namespace AemulusModManager.Avalonia.Utilities;

public class ConsoleWriterEventArgs : EventArgs
{
    public string Value { get; }
    public ConsoleWriterEventArgs(string value)
    {
        Value = value;
    }
}

public class TextBoxOutputter : TextWriter
{
    public StreamWriter? sw;

    public TextBoxOutputter(StreamWriter streamWriter)
    {
        sw = streamWriter;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(string? value)
    {
        if (value == null) return;
        WriteEvent?.Invoke(this, new ConsoleWriterEventArgs(value));
        base.Write(value);
        sw?.Write(value);
    }

    public override void WriteLine(string? value)
    {
        if (value == null) return;
        WriteLineEvent?.Invoke(this, new ConsoleWriterEventArgs(value));
        base.WriteLine(value);
        sw?.WriteLine($"{DateTime.Now} {value}");
    }

    public override void Close()
    {
        if (sw != null)
        {
            sw.Dispose();
            sw = null;
        }
    }

    public event EventHandler<ConsoleWriterEventArgs>? WriteEvent;
    public event EventHandler<ConsoleWriterEventArgs>? WriteLineEvent;
}
