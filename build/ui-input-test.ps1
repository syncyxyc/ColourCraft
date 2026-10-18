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
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
'@
# Alle Koordinaten (Fenster, Maus, UI Automation, Bildschirmfoto) in echten Bildschirmpixeln, auch bei 125–200 % Skalierung.
[NativeInput]::SetProcessDPIAware() | Out-Null

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

    # 7) Abnahme Strichmuster (Spezifikation 1.1): Linie aufziehen, beide Kontrollpunkte mit echter Maus ziehen,
    #    Parameter per Tastatur eingeben, Vorschau auf dem Bildschirm prüfen, „Anwenden" klicken, Strg+Z, Strg+Y.
    function Find-Named([string]$name, $controlType) {
        $byName = New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::NameProperty), $name
        $byType = New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty), $controlType
        return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.AndCondition $byName, $byType))
    }
    function Get-Field([string]$name) {
        $edit = Find-Named $name ([System.Windows.Automation.ControlType]::Edit)
        if ($null -eq $edit) { return $null }
        $text = $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
        return [double]::Parse($text.Replace(',', '.'), [Globalization.CultureInfo]::InvariantCulture)
    }
    function Move-Drag([int]$x0, [int]$y0, [int]$x1, [int]$y1) {
        Assert-Foreground 'Ziehen (Strichmuster)'
        [NativeInput]::SetCursorPos($x0, $y0) | Out-Null
        Start-Sleep -Milliseconds 80
        [NativeInput]::mouse_event([NativeInput]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
        for ($step = 1; $step -le 12; $step++) {
            [NativeInput]::SetCursorPos([int]($x0 + ($x1 - $x0) * $step / 12), [int]($y0 + ($y1 - $y0) * $step / 12)) | Out-Null
            Start-Sleep -Milliseconds 25
        }
        [NativeInput]::mouse_event([NativeInput]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 300
    }
    function Click-Element($element) {
        $r = $element.Current.BoundingRectangle
        Assert-Foreground ('Klick ' + $element.Current.Name)
        [NativeInput]::SetCursorPos([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2)) | Out-Null
        Start-Sleep -Milliseconds 60
        [NativeInput]::mouse_event([NativeInput]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
        [NativeInput]::mouse_event([NativeInput]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 250
    }
    function Set-Field([string]$name, [double]$value) {
        Click-Element (Find-Named $name ([System.Windows.Automation.ControlType]::Edit))
        Assert-Foreground ('Eingabe ' + $name)
        [System.Windows.Forms.SendKeys]::SendWait('^a')
        [System.Windows.Forms.SendKeys]::SendWait($value.ToString([Globalization.CultureInfo]::InvariantCulture))
        [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
        Start-Sleep -Milliseconds 250
    }
    function Get-ScreenColor([int]$x, [int]$y) {
        $pixel = New-Object System.Drawing.Bitmap 1, 1
        $gp = [System.Drawing.Graphics]::FromImage($pixel)
        $gp.CopyFromScreen($x, $y, 0, 0, $pixel.Size)
        $gp.Dispose()
        $color = $pixel.GetPixel(0, 0); $pixel.Dispose()
        return $color
    }
    function Test-Dark($color) { return ($color.R + $color.G + $color.B) -lt 150 }

    Assert-Foreground 'Umschalt+L'
    [System.Windows.Forms.SendKeys]::SendWait('+l')
    Start-Sleep -Milliseconds 300
    $ax = $cx - 200; $ay = $cy - 60; $bx = $cx + 100
    Move-Drag $ax $ay $bx $ay
    $p1 = @((Get-Field 'X1'), (Get-Field 'Y1')); $p2 = @((Get-Field 'X2'), (Get-Field 'Y2'))
    $created = ($null -ne $p1[0]) -and ($p2[0] -gt $p1[0]) -and ([math]::Abs($p2[1] - $p1[1]) -lt 0.01)
    Check "Strichmuster: Linie mit echter Maus aufgezogen ($($p1 -join '; ') → $($p2 -join '; '))" $created
    if (-not $created) { throw 'Strichmuster-Linie wurde nicht angelegt.' }
    $scale = 300 / ($p2[0] - $p1[0]) # Bildschirmpixel je Dokumentpixel

    Move-Drag $ax $ay $ax ($ay + 120)               # Kontrollpunkt 1
    $q1 = @((Get-Field 'X1'), (Get-Field 'Y1'))
    Check "Kontrollpunkt 1 gezogen (Y1 $($p1[1]) → $($q1[1]), erwartet $([math]::Round($p1[1] + 120 / $scale, 2)))" (
        ([math]::Abs($q1[0] - $p1[0]) -lt 0.5) -and ([math]::Abs($q1[1] - ($p1[1] + 120 / $scale)) -lt 0.5) -and ((Get-Field 'X2') -eq $p2[0]))
    Move-Drag $bx $ay ($bx + 40) ($ay + 120)        # Kontrollpunkt 2
    $q2 = @((Get-Field 'X2'), (Get-Field 'Y2'))
    Check "Kontrollpunkt 2 gezogen (X2 $($p2[0]) → $($q2[0]), Y2 $($p2[1]) → $($q2[1]))" (
        ([math]::Abs($q2[0] - ($p2[0] + 40 / $scale)) -lt 0.5) -and ([math]::Abs($q2[1] - ($p2[1] + 120 / $scale)) -lt 0.5) -and ((Get-Field 'Y1') -eq $q1[1]))

    # Parameter per Tastatur (Feld anklicken, Wert tippen, Enter) – gut sichtbar: 16 / 60 / 40 Bildschirmpixel
    $thick = [math]::Ceiling(16 / $scale); $dash = [math]::Ceiling(60 / $scale); $gap = [math]::Ceiling(40 / $scale)
    Set-Field 'Linienstärke' $thick
    Set-Field 'Strichlänge' $dash
    Set-Field 'Strichabstand' $gap
    Check "Parameter per Tastatur übernommen ($thick / $dash / $gap px)" (
        ((Get-Field 'Linienstärke') -eq $thick) -and ((Get-Field 'Strichlänge') -eq $dash) -and ((Get-Field 'Strichabstand') -eq $gap))

    # Vorschau am Bildschirm: Mitte des ersten Strichs dunkel, Mitte der ersten Lücke hell (4 px neben der Hilfslinie)
    $lineY = $ay + 120 + 4
    $dashX = [int]($ax + $dash / 2 * $scale); $gapX = [int]($ax + ($dash + $gap / 2) * $scale)
    $previewDash = Get-ScreenColor $dashX $lineY; $previewGap = Get-ScreenColor $gapX $lineY
    Check "Vorschau folgt den Kontrollpunkten (Strich $previewDash, Lücke $previewGap)" ((Test-Dark $previewDash) -and -not (Test-Dark $previewGap))

    Click-Element (Find-Named 'Anwenden' ([System.Windows.Automation.ControlType]::Button))
    $x1Field = Find-Named 'X1' ([System.Windows.Automation.ControlType]::Edit)
    $appliedDash = Get-ScreenColor $dashX $lineY
    Check "„Anwenden“ übernimmt das Muster (Felder danach gesperrt, Strich $appliedDash)" ((-not $x1Field.Current.IsEnabled) -and (Test-Dark $appliedDash))
    Assert-Foreground 'Strg+Z (Strichmuster)'
    [System.Windows.Forms.SendKeys]::SendWait('^z')
    Start-Sleep -Milliseconds 300
    $status = Get-StatusMessage
    $undoneDash = Get-ScreenColor $dashX $lineY
    Check "Strg+Z nimmt das Muster zurück ($status, Strich $undoneDash)" (($status -like 'Rückgängig: Strichmuster*') -and -not (Test-Dark $undoneDash))
    Assert-Foreground 'Strg+Y (Strichmuster)'
    [System.Windows.Forms.SendKeys]::SendWait('^y')
    Start-Sleep -Milliseconds 300
    $redoneDash = Get-ScreenColor $dashX $lineY
    $redoStatus = $null
    foreach ($t in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $textCond)) { if ($t.Current.Name -like 'Wiederholt:*') { $redoStatus = $t.Current.Name } }
    Check "Strg+Y stellt das Muster wieder her ($redoStatus, Strich $redoneDash)" (($redoStatus -like 'Wiederholt: Strichmuster*') -and (Test-Dark $redoneDash))

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
