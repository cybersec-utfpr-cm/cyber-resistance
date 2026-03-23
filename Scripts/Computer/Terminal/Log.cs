using Godot;

public static class Log
{
    public static bool DebugEnabled { get; set; } = true;

    public static void Info(string message)
    {
        if (DebugEnabled)
            GD.Print($"[INFO] {message}");
    }

    public static void Error(string message)
    {
        GD.PrintErr($"[ERRO] {message}");
    }
}