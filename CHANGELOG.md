# Änderungen

## 0.9.0

Erste öffentliche Version zum internen Testen.

### Hinzugefügt
- **Updates:** Hilfe › Nach Updates suchen prüft auf Knopfdruck das neueste GitHub-Release, lädt eine neuere
  Programmdatei, prüft ihre SHA-256-Prüfsumme und installiert sie nach dem Beenden des Programms.
- **Strichmuster:** Linien lassen sich biegen: Klick auf die Linie setzt einen Punkt, Ziehen biegt die Linie
  (bis zu 16 Punkte). Rechtsklick entfernt einen inneren Punkt.
- **Farben und Effekte:** Helligkeit/Kontrast, Farbton/Sättigung, Tonwertkorrektur, Farbkanäle, Schwarzweiß,
  Invertieren, Sepia, Gaußscher und einfacher Weichzeichner, Schärfen, Rauschen, Rauschreduktion,
  Kantenerkennung, Schlagschatten, Verpixeln. Effekte laufen im Hintergrund und lassen sich mit Esc abbrechen.
- **Projektformat `.orbit`:** Speichern, Speichern unter, Kopie speichern und Öffnen mit Ebenen, Gruppen,
  Masken und Eigenschaften. Beschädigte oder veränderte Dateien werden mit Meldung abgelehnt.
- **Rückfrage beim Schließen:** Speichern, Nicht speichern oder Abbrechen.

### Vorbereitet (noch ohne Oberfläche)
- Farbkurven-, Docking- und Themen-Modell, Hilfslinien/Raster/Lineale, Tastenkürzel-Profile, Plugin-API
  (Commits M8–M10). Diese Teile sind getestet, aber noch nicht im Menü nutzbar.

### Bekannt
- Die Programmdatei ist nicht signiert. Windows-SmartScreen kann beim ersten Start warnen (siehe Benutzerhandbuch).
