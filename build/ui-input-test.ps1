<#
.SYNOPSIS
    Prüft die Leinwand mit echten Windows-Eingaben (Mausrad, mittlere Maustaste, Tastatur).
    Startet OrbitStudio.exe mit isoliertem Datenordner und einem erzeugten Testbild, sendet Eingaben
    über SendInput und liest Zoom und Scrollposition per UI Automation aus.
    Ergebnis: artifacts/testresults/ui-input-test.txt und ui-input-test.png
.NOTES
    Benötigt eine interaktive, entsperrte Windows-Sitzung. Während des Tests Maus und Tastatur nicht benutzen.
#>
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')

. (Join-Path $PSScriptRoot 'common.ps1')
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class NativeInput {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, int data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    public const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004;
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public const uint WHEEL = 0x0800, MIDDLEDOWN = 0x0020, MIDDLEUP = 0x0040;
}
'@

$results = Join-Path $Artifacts 'testresults'
New-Item -ItemType Directory -Force $results | Out-Null
$work = Join-Path $env:TEMP ('orbit-ui-input-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $work | Out-Null
$lines = New-Object System.Collections.Generic.List[string]
$failed = $false
function Check([string]$name, [bool]$ok) {
    $script:lines.Add(($(if ($ok) { 'PASS' } else { 'FAIL' })) + ' ' + $name)
    if (-not $ok) { $script:failed = $true }
}

# Testbild 1600×1000 erzeugen (größer als das Fenster, damit Verschieben möglich ist)
$imagePath = Join-Path $work 'eingabe.png'
$bmp = New-Object System.Drawing.Bitmap 1600, 1000
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::SteelBlue)
$g.FillRectangle([System.Drawing.Brushes]::Orange, 200, 150, 600, 400)
$g.Dispose(); $bmp.Save($imagePath, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()

$exe = Join-Path $RepoRoot "src/OrbitStudio.App/bin/$Configuration/net10.0-windows/OrbitStudio.exe"
$process = Start-Process $exe -ArgumentList "--data-dir `"$work\data`" `"$imagePath`"" -PassThru
try {
    $deadline = (Get-Date).AddSeconds(30)
    while ($process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200; $process.Refresh() }
    $hwnd = $process.MainWindowHandle
    Check 'Fenster gestartet' ($hwnd -ne 0)
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    $textCond = New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty), ([System.Windows.Automation.ControlType]::Text)

    function Get-ZoomText {
        foreach ($t in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $textCond)) {
            if ($t.Current.Name -like 'Zoom:*') { return $t.Current.Name }
        }
        return $null
    }
    function Get-ZoomValue {
        $text = Get-ZoomText
        if ($text -match 'Zoom:\s*([\d,\.]+)') { return [double]::Parse($Matches[1], [Globalization.CultureInfo]'de-DE') }
        return $null
    }
    function Get-HorizontalScroll {
        $cond = New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::NameProperty), 'Horizontal scrollen'
        $bar = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($null -eq $bar) { return $null }
        return $bar.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value
    }

    # Warten, bis das Bild geladen ist
    $deadline = (Get-Date).AddSeconds(30)
    while ($null -eq (Get-ZoomValue) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
    $initial = Get-ZoomValue
    Check "Bild per Kommandozeile geöffnet (Zoom $initial %)" ($null -ne $initial)

    $rect = New-Object NativeInput+RECT
    [NativeInput]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $cx = [int](($rect.Left + $rect.Right) / 2); $cy = [int](($rect.Top + $rect.Bottom) / 2)

    # Sicherheitsregel: Eingaben nur senden, wenn ORBIT Studio das Vordergrundfenster ist.
    # Sonst würden Tastenkürzel in einem fremden Programm landen.
    function Test-Foreground { return [NativeInput]::GetForegroundWindow() -eq $hwnd }
    function Assert-Foreground([string]$step) {
        if (-not (Test-Foreground)) { throw "Fenster nicht im Vordergrund vor Schritt '$step' – Test abgebrochen, keine Eingaben gesendet." }
    }
    $deadline = (Get-Date).AddSeconds(10)
    while (-not (Test-Foreground) -and (Get-Date) -lt $deadline) {
        [NativeInput]::SetForegroundWindow($hwnd) | Out-Null
        Start-Sleep -Milliseconds 300
        if (Test-Foreground) { break }
        # Windows verweigert die Aktivierung manchmal (Vordergrundsperre). Dann auf die eigene Titelleiste klicken –
        # aber nur, wenn dort nachweislich ORBIT Studio liegt und kein anderes Fenster darüber.
        $title = New-Object NativeInput+POINT
        $title.X = $rect.Left + 300; $title.Y = $rect.Top + 12
        $hit = [NativeInput]::GetAncestor([NativeInput]::WindowFromPoint($title), 2) # GA_ROOT
        if ($hit -eq $hwnd) {
            [NativeInput]::SetCursorPos($title.X, $title.Y) | Out-Null
            [NativeInput]::mouse_event([NativeInput]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
            [NativeInput]::mouse_event([NativeInput]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 300
        }
    }
    Check 'Fenster im Vordergrund' (Test-Foreground)
    Assert-Foreground 'Start'
    Start-Sleep -Milliseconds 700 # erste Darstellung abschließen lassen

    # 1) Mausrad nach oben: drei Raststufen = drei Zoomstufen größer
    [NativeInput]::SetCursorPos($cx, $cy) | Out-Null
    Start-Sleep -Milliseconds 100
    Assert-Foreground 'Mausrad'
    for ($i = 0; $i -lt 3; $i++) { [NativeInput]::mouse_event([NativeInput]::WHEEL, 0, 0, 120, [UIntPtr]::Zero); Start-Sleep -Milliseconds 150 }
    Start-Sleep -Milliseconds 300
    $afterWheel = Get-ZoomValue
    Check "Mausrad vergrößert ($initial % → $afterWheel %)" ($afterWheel -gt $initial)

    # 2) Mittlere Maustaste ziehen: horizontale Scrollposition ändert sich
    $scrollBefore = Get-HorizontalScroll
    Assert-Foreground 'Ziehen'
    [NativeInput]::mouse_event([NativeInput]::MIDDLEDOWN, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 10; $step++) { [NativeInput]::SetCursorPos($cx - 20 * $step, $cy - 10 * $step) | Out-Null; Start-Sleep -Milliseconds 30 }
    [NativeInput]::mouse_event([NativeInput]::MIDDLEUP, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
    $scrollAfter = Get-HorizontalScroll
    Check "Ziehen mit mittlerer Maustaste verschiebt (Scroll $([math]::Round($scrollBefore)) → $([math]::Round($scrollAfter)))" ($scrollAfter -gt $scrollBefore)

    # 3) Mausrad nach unten verkleinert
    Assert-Foreground 'Mausrad unten'
    [NativeInput]::mouse_event([NativeInput]::WHEEL, 0, 0, -120, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
    $afterWheelDown = Get-ZoomValue
    Check "Mausrad verkleinert ($afterWheel % → $afterWheelDown %)" ($afterWheelDown -lt $afterWheel)

    # 4) Tastatur: erst zwei Stufen vergrößern, dann Strg+0 = 100 %
    Assert-Foreground 'Mausrad oben'
    for ($i = 0; $i -lt 2; $i++) { [NativeInput]::mouse_event([NativeInput]::WHEEL, 0, 0, 120, [UIntPtr]::Zero); Start-Sleep -Milliseconds 150 }
    Start-Sleep -Milliseconds 300
    $beforeKey = Get-ZoomValue
    Assert-Foreground 'Strg+0'
    [System.Windows.Forms.SendKeys]::SendWait('^0')
    Start-Sleep -Milliseconds 300
    Check "Strg+0 setzt 100 % ($beforeKey % → $(Get-ZoomValue) %)" (($beforeKey -ne 100) -and ((Get-ZoomValue) -eq 100))

    # 5) Tastatur: Strg+B = Bild anpassen (Bild ist größer als das Fenster → gleicher Wert wie beim Öffnen)
    Assert-Foreground 'Strg+B'
    [System.Windows.Forms.SendKeys]::SendWait('^b')
    Start-Sleep -Milliseconds 300
    Check "Strg+B passt das Bild ein ($(Get-ZoomValue) %, beim Öffnen $initial %)" ((Get-ZoomValue) -eq $initial)

    # 6) Malen mit echter Maus: Taste B (Pinsel), Strich ziehen, Strg+Z – Statusleiste meldet „Rückgängig: Pinsel"
    function Get-StatusMessage {
        foreach ($t in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $textCond)) {
            if ($t.Current.Name -like 'Rückgängig:*' -or $t.Current.Name -like 'Bereit*' -or $t.Current.Name -like 'Geöffnet:*') { return $t.Current.Name }
        }
        return $null
    }
    Assert-Foreground 'Taste B'
    [System.Windows.Forms.SendKeys]::SendWait('b')
    Start-Sleep -Milliseconds 200
    [NativeInput]::SetCursorPos($cx - 100, $cy) | Out-Null
    Start-Sleep -Milliseconds 100
    Assert-Foreground 'Pinselstrich'
    [NativeInput]::mouse_event([NativeInput]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 10; $step++) { [NativeInput]::SetCursorPos($cx - 100 + 20 * $step, $cy + 5 * $step) | Out-Null; Start-Sleep -Milliseconds 30 }
    [NativeInput]::mouse_event([NativeInput]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
    Assert-Foreground 'Strg+Z'
    [System.Windows.Forms.SendKeys]::SendWait('^z')
    Start-Sleep -Milliseconds 300
    $status = Get-StatusMessage
    Check "Pinselstrich mit echter Maus und Strg+Z ($status)" ($status -like 'Rückgängig: Pinsel*')

    # Bildschirmfoto des Fensters
    [NativeInput]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $shot = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
    $gs = [System.Drawing.Graphics]::FromImage($shot)
    $gs.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $shot.Size)
    $gs.Dispose(); $shot.Save((Join-Path $results 'ui-input-test.png'), [System.Drawing.Imaging.ImageFormat]::Png); $shot.Dispose()
}
catch {
    Check ('Ausnahme: ' + $_.Exception.Message) $false
}
finally {
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null; if (-not $process.WaitForExit(10000)) { $process.Kill() } }
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

$lines.Add($(if ($failed) { 'ERGEBNIS: FEHLGESCHLAGEN' } else { 'ERGEBNIS: BESTANDEN' }))
$lines | Set-Content (Join-Path $results 'ui-input-test.txt') -Encoding UTF8
$lines | ForEach-Object { Write-Host $_ }
if ($failed) { exit 1 }
