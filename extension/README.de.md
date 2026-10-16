# ImageShot – Browsererweiterung

Screenshots für Chrome, Edge, Ecosia und andere Chromium-Browser: **sichtbarer Bereich**, **ausgewählter
Bereich**, **ganze Seite** oder **alle Tabs** aufnehmen – und in die Zwischenablage kopieren oder als Datei speichern.
Die Erweiterung ist Teil von **ImageShot** (Windows-App + Browsererweiterung).

Sprachen: English, Dansk, Deutsch, Español – die Sprache des Browsers wird automatisch verwendet.
Andere Sprachen: [English](README.md) · [Dansk](README.da.md) · [Español](README.es.md)

## Installation

1. Entpacken Sie das ZIP-Archiv, sodass Sie den Ordner `extension` erhalten.
2. Geben Sie `chrome://extensions` in die Adressleiste ein (oder: Menü → Erweiterungen → Erweiterungen verwalten).
3. Schalten Sie oben rechts den **Entwicklermodus** ein.
4. Klicken Sie auf **Entpackte Erweiterung laden** und wählen Sie den Ordner `extension`.
5. Klicken Sie in der Symbolleiste auf das Puzzle-Symbol und heften Sie **ImageShot** an.
   Lassen Sie den Ordner, wo er ist – die Erweiterung wird daraus ausgeführt.

## Verwendung

* **Rechtsklick** auf eine Seite → **ImageShot** → *Sichtbarer Bereich* · *Bereich auswählen* · *Ganze Seite* · *Alle Tabs*
* Oder klicken Sie in der Symbolleiste auf das **ImageShot-Symbol** und wählen Sie dort die Aufnahme.
* **Tastenkürzel:** `Alt+Umschalt+S` (sichtbarer Bereich), `Alt+Umschalt+A` (Bereich auswählen).
  Änderbar unter `chrome://extensions/shortcuts`.
* **Bereich auswählen:** mit der Maus aufziehen. `Esc` oder ein Rechtsklick bricht ab.
  Fahren Sie beim Ziehen an den oberen oder unteren Rand, scrollt die Seite (oder der Scrollbereich der Web-App)
  von selbst, und die Auswahl kann höher als der Bildschirm werden. Das Mausrad funktioniert ebenfalls.
  Die Teile werden zu einem Bild zusammengesetzt.

### Einstellungen (Klick auf das Symbol)

* **Auflösung:** Der Regler bestimmt Bildgröße und Qualität. 70 = 87 % der Bildschirmauflösung, Qualität 85.
* **Dateiformat:** WebP (kleine Dateien), PNG (verlustfrei) oder JPEG.
* **Nach der Aufnahme:**
  * Zwischenablage anzeigen – kopieren oder speichern
  * Direkt in die Zwischenablage kopieren
  * Direkt als Datei speichern
* **Zwischenablage automatisch schließen**, sobald das Bild kopiert oder gespeichert wurde – in den Einstellungen
  und unten im Zwischenablage-Fenster.
* **„Alle Tabs“ umfasst:** die Tabs im aktuellen Fenster oder in allen Fenstern.
* **Ordner in „Downloads“:** z. B. `ImageShot` (Unterordner mit `/` sind erlaubt).
  Dateinamen: `ImageShot JJJJ-MM-TT HH.mm.ss`.

### Zuschneiden

Klicken Sie im Zwischenablage-Fenster auf **Zuschneiden**, ziehen Sie einen Rahmen um den Bereich, den Sie behalten
möchten, und passen Sie Kanten und Ecken an. Ziehen innerhalb des Rahmens verschiebt ihn. `Enter` (oder ein
Doppelklick) übernimmt, `Esc` bricht ab. **Zuschnitt rückgängig** oder `Strg+Z` geht zurück. Zugeschnitten wird
immer aus dem Original – ohne zusätzlichen Qualitätsverlust. „Kopieren“ und „Speichern“ verwenden das zugeschnittene Bild.

## Tipps

* Beim Kopieren landet das Bild immer als PNG in der Zwischenablage, weil die Zwischenablage nur PNG annimmt.
  Gespeicherte Dateien haben das gewählte Format.
* „Direkt in die Zwischenablage kopieren“ zeigt bei *Alle Tabs* stattdessen das Zwischenablage-Fenster,
  weil die Zwischenablage nur ein Bild gleichzeitig aufnehmen kann.
* *Ganze Seite* bei Web-Apps (z. B. Gmail, Saxo, Notion), deren Inhalt in einem Bereich statt im Fenster scrollt:
  ImageShot findet diesen Bereich und scrollt ihn durch. Das Bild enthält oben die Kopfzeile, den gesamten Inhalt
  des Bereichs und unten die Fußzeile. Daneben liegende Bereiche (z. B. ein Menü) erscheinen nur einmal.
* Beim ersten Einsatz von *Alle Tabs* fragt Chrome nach Zugriff auf alle Websites – ImageShot braucht ihn, um auch
  andere Tabs als den aktuellen aufzunehmen. Siehe [PERMISSIONS.md](PERMISSIONS.md) (Englisch).

## Einschränkungen

* Browsereigene Seiten (`chrome://`, Chrome Web Store, andere Erweiterungen) können nicht aufgenommen werden.
  Das Symbol zeigt dann ein rotes „!“ – der Grund steht im Tooltip.
* *Ganze Seite*: Feste Kopfleisten werden nach dem ersten Teil ausgeblendet, damit sie sich nicht wiederholen.
  Sehr lange Seiten werden bei 30.000 px abgeschnitten. WebP ist auf 16.383 px begrenzt; längere Bilder werden als PNG gespeichert.
* Listen, die beim Scrollen nachladen (endlose Feeds), werden nur so weit aufgenommen, wie sie beim Start der
  Aufnahme geladen waren.
* *Alle Tabs* wechselt während der Aufnahme kurz durch die Tabs und kehrt danach zum ursprünglichen Tab zurück.
  Minimierte Fenster können nicht aufgenommen werden.
* Aufnahmen werden nur aufbewahrt, solange das Zwischenablage-Fenster offen ist (höchstens eine Stunde).

## Datenschutz und Berechtigungen

ImageShot funktioniert offline und sendet nichts. Aufnahmen bleiben im lokalen Speicher der Erweiterung, bis das
Zwischenablage-Fenster geschlossen wird. Alle Berechtigungen sind in [PERMISSIONS.md](PERMISSIONS.md) erklärt.

## Entwicklung

Automatische Tests (Playwright) befinden sich in [`tests/`](tests/README.md). Lassen Sie den Ordner `tests/` weg,
wenn Sie die Erweiterung für den Chrome Web Store verpacken.
