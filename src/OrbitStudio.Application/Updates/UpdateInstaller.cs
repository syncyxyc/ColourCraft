using System.Diagnostics;
using System.Text;

namespace OrbitStudio.Application.Updates;

/// <summary>
/// Ersetzt die laufende EXE durch eine heruntergeladene Version. Das Programm kann sich nicht selbst überschreiben, solange
/// es läuft. Deshalb schreibt <see cref="WriteScript"/> ein kleines Skript, das wartet, bis der Prozess beendet ist,
/// die Datei ersetzt (mit Wiederholungen, falls ein Virenscanner sie kurz sperrt) und die neue Version startet.
/// </summary>
public static class UpdateInstaller
{
    public const string ScriptName = "install-update.cmd";

    /// <summary>Schreibt das Skript nach <paramref name="scriptPath"/>. Pfade mit Nicht-ASCII-Zeichen, Anführungszeichen, % oder Zeilenumbrüchen werden abgewiesen.</summary>
    public static void WriteScript(string scriptPath, int processId, string newFile, string targetFile)
    {
        foreach (var path in new[] { newFile, targetFile, scriptPath })
        {
            if (path.Any(c => c > 127) || path.IndexOfAny(['"', '%', '\r', '\n']) >= 0)
            {
                throw new ArgumentException("Der Pfad enthält Sonderzeichen (Umlaute, Anführungszeichen oder %), die die Aktualisierung nicht unterstützt.", nameof(newFile));
            }
        }

        var script = new StringBuilder();
        script.AppendLine("@echo off");
        script.AppendLine("setlocal");
        script.AppendLine($"set \"PID={processId}\"");
        script.AppendLine($"set \"NEW={newFile}\"");
        script.AppendLine($"set \"TARGET={targetFile}\"");
        script.AppendLine(":wait");
        script.AppendLine("tasklist /FI \"PID eq %PID%\" 2>nul | find \"%PID%\" >nul");
        script.AppendLine("if not errorlevel 1 (");
        script.AppendLine("  ping -n 2 127.0.0.1 >nul");
        script.AppendLine("  goto wait");
        script.AppendLine(")");
        script.AppendLine("set /a TRIES=0");
        script.AppendLine(":move");
        script.AppendLine("move /Y \"%NEW%\" \"%TARGET%\" >nul 2>&1");
        script.AppendLine("if errorlevel 1 (");
        script.AppendLine("  set /a TRIES+=1");
        script.AppendLine("  if %TRIES% GEQ 30 exit /b 1");
        script.AppendLine("  ping -n 2 127.0.0.1 >nul");
        script.AppendLine("  goto move");
        script.AppendLine(")");
        script.AppendLine("start \"\" \"%TARGET%\"");
        script.AppendLine("(goto) 2>nul & del \"%~f0\"");

        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        File.WriteAllText(scriptPath, script.ToString(), Encoding.ASCII);
    }

    /// <summary>Startet das Skript losgelöst vom Programm (es läuft weiter, wenn das Programm beendet wird).</summary>
    public static void Start(string scriptPath)
    {
        var info = new ProcessStartInfo("cmd.exe", $"/c \"\"{scriptPath}\"\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
        };
        Process.Start(info);
    }
}
