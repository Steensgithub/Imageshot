using System;
using System.Collections.Generic;
using System.Globalization;

namespace ImageShot
{
    // Oversættelser: dansk, engelsk, tysk, spansk.
    // Brug: L.T("nøgle") eller L.T("nøgle", arg0, arg1 …) med {0}, {1} i teksten.
    static class L
    {
        public static readonly string[] Languages = { "da", "en", "de", "es" };
        public static readonly string[] LanguageNames = { "Dansk", "English", "Deutsch", "Español" };
        static int idx = 1; // engelsk indtil Init

        public static string Current { get { return Languages[idx]; } }

        // setting: "auto" eller en sprogkode. "auto" følger Windows' sprog (engelsk, hvis sproget ikke findes).
        public static void Init(string setting)
        {
            string code = setting;
            if (string.IsNullOrEmpty(code) || code == "auto")
                code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            int i = Array.IndexOf(Languages, code);
            idx = i >= 0 ? i : 1;
        }

        public static string T(string key, params object[] args)
        {
            string[] v;
            string s = D.TryGetValue(key, out v) ? (v[idx] ?? v[1]) : key;
            if (args != null && args.Length > 0)
            {
                try { s = string.Format(CultureInfo.CurrentCulture, s, args); } catch (FormatException) { }
            }
            return s;
        }

        public static IEnumerable<KeyValuePair<string, string[]>> All { get { return D; } }

        //                                               dansk, engelsk, tysk, spansk
        static readonly Dictionary<string, string[]> D = new Dictionary<string, string[]>
        {
            // --- Optagelsestyper
            ["mode.region"] = new[] { "Markér område", "Select area", "Bereich auswählen", "Seleccionar área" },
            ["mode.window"] = new[] { "Aktivt vindue", "Active window", "Aktives Fenster", "Ventana activa" },
            ["mode.screen"] = new[] { "Hele skærmen", "Full screen", "Ganzer Bildschirm", "Pantalla completa" },
            ["mode.all"] = new[] { "Alle skærme", "All screens", "Alle Bildschirme", "Todas las pantallas" },
            ["mode.cancel"] = new[] { "Annullér", "Cancel", "Abbrechen", "Cancelar" },

            // --- Menu ved uret
            ["menu.folder"] = new[] { "Åbn mappen med skærmbilleder", "Open screenshot folder", "Screenshot-Ordner öffnen", "Abrir carpeta de capturas" },
            ["menu.history"] = new[] { "Seneste", "Recent", "Zuletzt", "Recientes" },
            ["menu.delay"] = new[] { "Med forsinkelse", "With delay", "Mit Verzögerung", "Con retraso" },
            ["menu.delaySec"] = new[] { "{0} sekunder", "{0} seconds", "{0} Sekunden", "{0} segundos" },
            ["menu.log"] = new[] { "Åbn logfil (fejlfinding)", "Open log file (troubleshooting)", "Protokoll öffnen (Fehlersuche)", "Abrir registro (diagnóstico)" },
            ["menu.settings"] = new[] { "Indstillinger…", "Settings…", "Einstellungen…", "Configuración…" },
            ["menu.about"] = new[] { "Om ImageShot", "About ImageShot", "Über ImageShot", "Acerca de ImageShot" },
            ["menu.quit"] = new[] { "Afslut", "Exit", "Beenden", "Salir" },

            // --- Beskeder
            ["toast.hotkeysTaken"] = new[] { "Disse genveje bruges allerede af Windows eller et andet program: {0}. Vælg andre under Indstillinger.", "These shortcuts are already used by Windows or another program: {0}. Choose others in Settings.", "Diese Tastenkürzel werden bereits von Windows oder einem anderen Programm verwendet: {0}. Wählen Sie andere in den Einstellungen.", "Estos atajos ya los usa Windows u otro programa: {0}. Elige otros en Configuración." },
            ["toast.blackScreen"] = new[] { "En skærm kunne ikke fotograferes og blev sort. Prøv at slå HDR fra (Win+Alt+B) – eller send logfilen (menuen ved uret → Åbn logfil).", "A screen could not be captured and came out black. Try turning HDR off (Win+Alt+B) – or send the log file (tray menu → Open log file).", "Ein Bildschirm konnte nicht aufgenommen werden und ist schwarz. Schalten Sie HDR aus (Win+Alt+B) – oder senden Sie die Protokolldatei (Menü im Infobereich → Protokoll öffnen).", "No se pudo capturar una pantalla y salió negra. Prueba a desactivar HDR (Win+Alt+B) o envía el registro (menú de la bandeja → Abrir registro)." },
            ["toast.emptyArea"] = new[] { "Området var tomt – prøv igen.", "The area was empty – try again.", "Der Bereich war leer – bitte erneut versuchen.", "El área estaba vacía; inténtalo de nuevo." },
            ["toast.captureFailed"] = new[] { "Skærmbilledet mislykkedes: {0}", "The screenshot failed: {0}", "Der Screenshot ist fehlgeschlagen: {0}", "La captura falló: {0}" },
            ["toast.copied"] = new[] { "Kopieret til udklipsholderen", "Copied to clipboard", "In die Zwischenablage kopiert", "Copiado al portapapeles" },
            ["toast.saved"] = new[] { "Gemt: {0}", "Saved: {0}", "Gespeichert: {0}", "Guardado: {0}" },
            ["toast.clickToOpen"] = new[] { "Klik for at åbne", "Click to open", "Zum Öffnen klicken", "Haz clic para abrir" },
            ["toast.textCopied"] = new[] { "Tekst kopieret ({0} tegn)", "Text copied ({0} characters)", "Text kopiert ({0} Zeichen)", "Texto copiado ({0} caracteres)" },
            ["toast.noText"] = new[] { "Der blev ikke fundet tekst i billedet", "No text was found in the image", "Im Bild wurde kein Text gefunden", "No se encontró texto en la imagen" },
            ["toast.logFailed"] = new[] { "Logfilen kunne ikke åbnes: {0}", "The log file could not be opened: {0}", "Die Protokolldatei konnte nicht geöffnet werden: {0}", "No se pudo abrir el registro: {0}" },
            ["toast.folderFailed"] = new[] { "Mappen kunne ikke åbnes: {0}", "The folder could not be opened: {0}", "Der Ordner konnte nicht geöffnet werden: {0}", "No se pudo abrir la carpeta: {0}" },
            ["toast.updateAvailable"] = new[] { "ImageShot {0} er klar. Klik for at opdatere.", "ImageShot {0} is available. Click to update.", "ImageShot {0} ist verfügbar. Zum Aktualisieren klicken.", "ImageShot {0} está disponible. Haz clic para actualizar." },
            ["error.generic"] = new[] { "Der opstod en fejl: {0}", "An error occurred: {0}", "Ein Fehler ist aufgetreten: {0}", "Se produjo un error: {0}" },

            // --- Filnavne og billedtekster
            ["file.prefix"] = new[] { "ImageShot", "ImageShot", "ImageShot", "ImageShot" },
            ["dialog.filter"] = new[] { "PNG-billede (*.png)|*.png|JPEG-billede (*.jpg)|*.jpg", "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg", "PNG-Bild (*.png)|*.png|JPEG-Bild (*.jpg)|*.jpg", "Imagen PNG (*.png)|*.png|Imagen JPEG (*.jpg)|*.jpg" },
            ["pixels"] = new[] { "{0} × {1} pixels", "{0} × {1} pixels", "{0} × {1} Pixel", "{0} × {1} píxeles" },

            // --- Overlejring (Markér område)
            ["overlay.title"] = new[] { "ImageShot – markér område", "ImageShot – select area", "ImageShot – Bereich auswählen", "ImageShot – seleccionar área" },
            ["overlay.hint"] = new[] { "Træk for at markere   ·   Klik på et vindue for at tage det   ·   Piletaster finjusterer   ·   Højreklik annullerer", "Drag to select   ·   Click a window to capture it   ·   Arrow keys fine-tune   ·   Right-click cancels", "Ziehen zum Auswählen   ·   Fenster anklicken, um es aufzunehmen   ·   Pfeiltasten zur Feinjustierung   ·   Rechtsklick bricht ab", "Arrastra para seleccionar   ·   Haz clic en una ventana para capturarla   ·   Flechas para ajustar   ·   Clic derecho cancela" },
            ["overlay.scrollHint"] = new[] { "Skub mod kanten for at scrolle", "Push against the edge to scroll", "Zum Scrollen an den Rand schieben", "Empuja contra el borde para desplazar" },
            ["overlay.lost"] = new[] { "Kunne ikke følge med – slip for at tage billedet", "Could not keep up – release to take the screenshot", "Konnte nicht folgen – loslassen, um aufzunehmen", "No se pudo seguir; suelta para hacer la captura" },
            ["overlay.stuck"] = new[] { "Programmet kan ikke scrolles herfra – brug musehjulet", "This program can't be scrolled from here – use the mouse wheel", "Dieses Programm lässt sich von hier nicht scrollen – Mausrad verwenden", "Este programa no se puede desplazar desde aquí; usa la rueda del ratón" },
            ["overlay.release"] = new[] { "Slip for at tage billedet", "Release to take the screenshot", "Loslassen zum Aufnehmen", "Suelta para hacer la captura" },
            ["overlay.escCancels"] = new[] { "Esc annullerer", "Esc cancels", "Esc bricht ab", "Esc cancela" },

            // --- Udklipsholder / editor
            ["preview.title"] = new[] { "ImageShot – udklipsholder", "ImageShot – clipboard", "ImageShot – Zwischenablage", "ImageShot – portapapeles" },
            ["preview.copy"] = new[] { "Kopiér", "Copy", "Kopieren", "Copiar" },
            ["preview.crop"] = new[] { "Tilskær", "Crop", "Zuschneiden", "Recortar" },
            ["preview.applyCrop"] = new[] { "Anvend tilskæring", "Apply crop", "Zuschnitt anwenden", "Aplicar recorte" },
            ["preview.undo"] = new[] { "Fortryd", "Undo", "Rückgängig", "Deshacer" },
            ["preview.redo"] = new[] { "Annullér fortryd", "Redo", "Wiederholen", "Rehacer" },
            ["preview.save"] = new[] { "Gem", "Save", "Speichern", "Guardar" },
            ["preview.saveAs"] = new[] { "Gem som…", "Save as…", "Speichern unter…", "Guardar como…" },
            ["preview.openFolder"] = new[] { "Åbn mappe", "Open folder", "Ordner öffnen", "Abrir carpeta" },
            ["preview.close"] = new[] { "Luk", "Close", "Schließen", "Cerrar" },
            ["preview.copyText"] = new[] { "Kopiér tekst", "Copy text", "Text kopieren", "Copiar texto" },
            ["preview.pin"] = new[] { "Fastgør", "Pin", "Anheften", "Fijar" },
            ["preview.autoClose"] = new[] { "Luk automatisk, når billedet er kopieret eller gemt", "Close automatically after copying or saving", "Nach dem Kopieren oder Speichern automatisch schließen", "Cerrar automáticamente al copiar o guardar" },
            ["preview.cropHint"] = new[] { "Træk en ramme om det, du vil beholde. Kanter og hjørner kan justeres bagefter.  ·  Esc annullerer", "Drag a frame around what you want to keep. Edges and corners can be adjusted afterwards.  ·  Esc cancels", "Ziehen Sie einen Rahmen um den Bereich, den Sie behalten möchten. Kanten und Ecken lassen sich danach anpassen.  ·  Esc bricht ab", "Arrastra un marco alrededor de lo que quieres conservar. Luego puedes ajustar bordes y esquinas.  ·  Esc cancela" },
            ["preview.cropSel"] = new[] { "{0} × {1} pixels markeret  ·  Enter anvender  ·  Esc annullerer", "{0} × {1} pixels selected  ·  Enter applies  ·  Esc cancels", "{0} × {1} Pixel ausgewählt  ·  Enter übernimmt  ·  Esc bricht ab", "{0} × {1} píxeles seleccionados  ·  Enter aplica  ·  Esc cancela" },
            ["preview.edited"] = new[] { "redigeret", "edited", "bearbeitet", "editado" },
            ["preview.copied"] = new[] { "✓ Kopieret til udklipsholderen", "✓ Copied to clipboard", "✓ In die Zwischenablage kopiert", "✓ Copiado al portapapeles" },
            ["preview.saved"] = new[] { "✓ Gemt: {0}", "✓ Saved: {0}", "✓ Gespeichert: {0}", "✓ Guardado: {0}" },
            ["preview.copyFailed"] = new[] { "Kunne ikke kopiere: {0}", "Could not copy: {0}", "Kopieren fehlgeschlagen: {0}", "No se pudo copiar: {0}" },
            ["preview.saveFailed"] = new[] { "Kunne ikke gemme: {0}", "Could not save: {0}", "Speichern fehlgeschlagen: {0}", "No se pudo guardar: {0}" },

            // --- Tegneværktøjer
            ["tool.select"] = new[] { "Vælg", "Select", "Auswählen", "Seleccionar" },
            ["tool.arrow"] = new[] { "Pil", "Arrow", "Pfeil", "Flecha" },
            ["tool.rect"] = new[] { "Firkant", "Rectangle", "Rechteck", "Rectángulo" },
            ["tool.ellipse"] = new[] { "Cirkel", "Ellipse", "Ellipse", "Elipse" },
            ["tool.highlight"] = new[] { "Overstreg", "Highlight", "Markieren", "Resaltar" },
            ["tool.text"] = new[] { "Tekst", "Text", "Text", "Texto" },
            ["tool.step"] = new[] { "Trin", "Step", "Schritt", "Paso" },
            ["tool.blur"] = new[] { "Slør", "Blur", "Weichzeichnen", "Difuminar" },
            ["tool.pixelate"] = new[] { "Pixelér", "Pixelate", "Verpixeln", "Pixelar" },
            ["tool.color"] = new[] { "Farve", "Colour", "Farbe", "Color" },
            ["tool.delete"] = new[] { "Slet", "Delete", "Löschen", "Eliminar" },
            ["tool.textPrompt"] = new[] { "Skriv tekst og tryk Enter", "Type text and press Enter", "Text eingeben und Enter drücken", "Escribe el texto y pulsa Enter" },

            // --- Kvalitet
            ["q.smallest"] = new[] { "Mindste fil", "Smallest file", "Kleinste Datei", "Archivo más pequeño" },
            ["q.small"] = new[] { "Lille", "Small", "Klein", "Pequeño" },
            ["q.balanced"] = new[] { "Balanceret", "Balanced", "Ausgewogen", "Equilibrado" },
            ["q.high"] = new[] { "Høj", "High", "Hoch", "Alto" },
            ["q.highest"] = new[] { "Højeste opløsning", "Highest resolution", "Höchste Auflösung", "Máxima resolución" },
            ["q.detail"] = new[] { "{0} · {1} % af skærmens opløsning · {2}", "{0} · {1}% of screen resolution · {2}", "{0} · {1} % der Bildschirmauflösung · {2}", "{0} · {1} % de la resolución de pantalla · {2}" },
            ["q.lossless"] = new[] { "tabsfri", "lossless", "verlustfrei", "sin pérdida" },
            ["q.quality"] = new[] { "kvalitet {0}", "quality {0}", "Qualität {0}", "calidad {0}" },

            // --- Genveje
            ["hk.none"] = new[] { "(ingen)", "(none)", "(keine)", "(ninguno)" },
            ["hk.needModifier"] = new[] { "Brug Ctrl, Alt eller Shift med tasten", "Use Ctrl, Alt or Shift with the key", "Strg, Alt oder Umschalt mit der Taste verwenden", "Usa Ctrl, Alt o Mayús con la tecla" },

            // --- Indstillinger
            ["set.title"] = new[] { "ImageShot – indstillinger", "ImageShot – settings", "ImageShot – Einstellungen", "ImageShot – configuración" },
            ["set.intro"] = new[] { "ImageShot kører nu ved uret nederst til højre (tryk evt. på ^ for at se ikonet). Tryk på PrtScn i et hvilket som helst program – så kan du vælge område, vindue eller hele skærmen.", "ImageShot is now running next to the clock at the bottom right (click ^ to see the icon). Press PrtScn in any program to choose an area, a window or the whole screen.", "ImageShot läuft jetzt neben der Uhr unten rechts (ggf. auf ^ klicken, um das Symbol zu sehen). Drücken Sie in jedem Programm Druck, um einen Bereich, ein Fenster oder den ganzen Bildschirm zu wählen.", "ImageShot ya se está ejecutando junto al reloj, abajo a la derecha (pulsa ^ para ver el icono). Pulsa ImprPant en cualquier programa para elegir un área, una ventana o toda la pantalla." },
            ["set.hotkeys"] = new[] { "Genveje – virker i alle programmer", "Shortcuts – work in every program", "Tastenkürzel – funktionieren in jedem Programm", "Atajos: funcionan en cualquier programa" },
            ["set.hkRegion"] = new[] { "Markér / vælg type", "Select / choose type", "Auswählen / Art wählen", "Seleccionar / elegir tipo" },
            ["set.hkNote"] = new[] { "Den første genvej viser en værktøjslinje, hvor du vælger område, vindue eller skærm. Klik i et felt og tryk den nye tastekombination. Delete fjerner genvejen.", "The first shortcut shows a toolbar where you choose area, window or screen. Click a field and press the new key combination. Delete removes the shortcut.", "Das erste Kürzel zeigt eine Symbolleiste zur Wahl von Bereich, Fenster oder Bildschirm. Feld anklicken und neue Tastenkombination drücken. Entf entfernt das Kürzel.", "El primer atajo muestra una barra donde eliges área, ventana o pantalla. Haz clic en un campo y pulsa la nueva combinación. Supr elimina el atajo." },
            ["set.image"] = new[] { "Billede", "Image", "Bild", "Imagen" },
            ["set.resolution"] = new[] { "Opløsning", "Resolution", "Auflösung", "Resolución" },
            ["set.format"] = new[] { "Filformat", "File format", "Dateiformat", "Formato" },
            ["set.png"] = new[] { "PNG – tabsfri, skarp tekst", "PNG – lossless, sharp text", "PNG – verlustfrei, scharfer Text", "PNG: sin pérdida, texto nítido" },
            ["set.jpeg"] = new[] { "JPEG – små filer", "JPEG – small files", "JPEG – kleine Dateien", "JPEG: archivos pequeños" },
            ["set.after"] = new[] { "Efter optagelse", "After capture", "Nach der Aufnahme", "Después de capturar" },
            ["set.afterShow"] = new[] { "Åbn redigering – vælg kopiér eller gem", "Open editor – choose copy or save", "Editor öffnen – Kopieren oder Speichern wählen", "Abrir editor: elegir copiar o guardar" },
            ["set.afterCopy"] = new[] { "Kopiér direkte til udklipsholderen", "Copy straight to clipboard", "Direkt in die Zwischenablage kopieren", "Copiar directamente al portapapeles" },
            ["set.afterSave"] = new[] { "Gem direkte som fil", "Save straight to file", "Direkt als Datei speichern", "Guardar directamente como archivo" },
            ["set.autoClose"] = new[] { "Luk redigeringen automatisk, når billedet er kopieret eller gemt", "Close the editor automatically after copying or saving", "Editor nach dem Kopieren oder Speichern automatisch schließen", "Cerrar el editor automáticamente al copiar o guardar" },
            ["set.folder"] = new[] { "Gem i mappe", "Save to folder", "Speichern in", "Guardar en" },
            ["set.fileName"] = new[] { "Filnavn", "File name", "Dateiname", "Nombre de archivo" },
            ["set.fileNameNote"] = new[] { "Felter: {date} {time} {title} {app} {counter}. Brug \\ for undermapper, fx {app}\\{date}", "Fields: {date} {time} {title} {app} {counter}. Use \\ for subfolders, e.g. {app}\\{date}", "Felder: {date} {time} {title} {app} {counter}. \\ für Unterordner, z. B. {app}\\{date}", "Campos: {date} {time} {title} {app} {counter}. Usa \\ para subcarpetas, p. ej. {app}\\{date}" },
            ["set.browse"] = new[] { "Vælg…", "Browse…", "Durchsuchen…", "Examinar…" },
            ["set.browseTitle"] = new[] { "Vælg mappen, hvor skærmbilleder skal gemmes", "Choose the folder where screenshots are saved", "Ordner für Screenshots wählen", "Elige la carpeta donde se guardan las capturas" },
            ["set.other"] = new[] { "Andet", "Other", "Sonstiges", "Otros" },
            ["set.language"] = new[] { "Sprog", "Language", "Sprache", "Idioma" },
            ["set.langAuto"] = new[] { "Samme som Windows", "Same as Windows", "Wie Windows", "Igual que Windows" },
            ["set.theme"] = new[] { "Udseende", "Appearance", "Darstellung", "Apariencia" },
            ["set.themeAuto"] = new[] { "Samme som Windows", "Same as Windows", "Wie Windows", "Igual que Windows" },
            ["set.themeLight"] = new[] { "Lyst", "Light", "Hell", "Claro" },
            ["set.themeDark"] = new[] { "Mørkt", "Dark", "Dunkel", "Oscuro" },
            ["set.startup"] = new[] { "Start automatisk sammen med Windows", "Start automatically with Windows", "Automatisch mit Windows starten", "Iniciar automáticamente con Windows" },
            ["set.updates"] = new[] { "Søg automatisk efter opdateringer", "Check for updates automatically", "Automatisch nach Updates suchen", "Buscar actualizaciones automáticamente" },
            ["set.history"] = new[] { "Gem historik over de seneste billeder", "Keep a history of recent screenshots", "Verlauf der letzten Screenshots speichern", "Guardar historial de capturas recientes" },
            ["set.alwaysDx"] = new[] { "Brug altid spiltilstand (DirectX-optagelse)", "Always use game mode (DirectX capture)", "Immer Spielmodus verwenden (DirectX-Aufnahme)", "Usar siempre el modo juego (captura DirectX)" },
            ["set.alwaysDxNote"] = new[] { "Normalt slår spiltilstanden selv til, når et billede ellers ville blive sort (fx spil i fuldskærm eller HDR).", "Game mode normally switches on by itself when a screenshot would otherwise be black (e.g. full-screen games or HDR).", "Der Spielmodus schaltet sich normalerweise selbst ein, wenn ein Bild sonst schwarz wäre (z. B. Vollbildspiele oder HDR).", "El modo juego se activa solo cuando una captura saldría negra (p. ej., juegos a pantalla completa o HDR)." },
            ["set.save"] = new[] { "Gem", "Save", "Speichern", "Guardar" },
            ["set.cancel"] = new[] { "Annullér", "Cancel", "Abbrechen", "Cancelar" },
            ["set.dupHotkey"] = new[] { "Den samme genvej ({0}) er brugt to gange.", "The same shortcut ({0}) is used twice.", "Dasselbe Tastenkürzel ({0}) wird zweimal verwendet.", "El mismo atajo ({0}) se usa dos veces." },
            ["set.badFolder"] = new[] { "Mappen kan ikke bruges: {0}", "The folder cannot be used: {0}", "Der Ordner kann nicht verwendet werden: {0}", "No se puede usar la carpeta: {0}" },
            ["set.fullPath"] = new[] { "Angiv en fuld sti, fx C:\\Users\\dig\\Pictures\\ImageShot.", "Enter a full path, e.g. C:\\Users\\you\\Pictures\\ImageShot.", "Geben Sie einen vollständigen Pfad an, z. B. C:\\Users\\Sie\\Pictures\\ImageShot.", "Indica una ruta completa, p. ej. C:\\Users\\tú\\Pictures\\ImageShot." },
            ["set.hotkeysTaken"] = new[] { "Disse genveje bruges allerede af Windows eller et andet program:\n\n{0}\n\nVælg en anden tastekombination.", "These shortcuts are already used by Windows or another program:\n\n{0}\n\nChoose another key combination.", "Diese Tastenkürzel werden bereits von Windows oder einem anderen Programm verwendet:\n\n{0}\n\nWählen Sie eine andere Kombination.", "Estos atajos ya los usa Windows u otro programa:\n\n{0}\n\nElige otra combinación." },

            // --- Windows 11: PrtScn åbner Skærmklip
            ["snip.title"] = new[] { "PrtScn bruges af Windows", "PrtScn is used by Windows", "Druck wird von Windows verwendet", "Windows usa ImprPant" },
            ["snip.text"] = new[] { "Windows er sat til at åbne Skærmklip, når du trykker PrtScn. ImageShot kan overtage tasten, så PrtScn åbner ImageShot i stedet.\n\nVil du det?", "Windows is set to open the Snipping Tool when you press PrtScn. ImageShot can take over the key so PrtScn opens ImageShot instead.\n\nDo you want that?", "Windows öffnet beim Drücken von Druck das Snipping Tool. ImageShot kann die Taste übernehmen, sodass Druck stattdessen ImageShot öffnet.\n\nMöchten Sie das?", "Windows abre Recortes al pulsar ImprPant. ImageShot puede encargarse de la tecla para que ImprPant abra ImageShot.\n\n¿Quieres hacerlo?" },

            // --- Om
            ["ocr.unavailable"] = new[] { "Tekstgenkendelse findes ikke på denne pc (kræver Windows 10/11 med et installeret sprog).", "Text recognition is not available on this PC (requires Windows 10/11 with an installed language).", "Texterkennung ist auf diesem PC nicht verfügbar (erfordert Windows 10/11 mit installierter Sprache).", "El reconocimiento de texto no está disponible en este equipo (requiere Windows 10/11 con un idioma instalado)." },
            ["ocr.failed"] = new[] { "Tekstgenkendelse mislykkedes: {0}", "Text recognition failed: {0}", "Texterkennung fehlgeschlagen: {0}", "Error en el reconocimiento de texto: {0}" },
            ["pin.opened"] = new[] { "Billedet er fastgjort øverst på skærmen. Træk for at flytte, musehjul zoomer, Esc lukker.", "The image is pinned on top. Drag to move, scroll to zoom, Esc closes.", "Das Bild ist oben angeheftet. Ziehen zum Verschieben, Mausrad zoomt, Esc schließt.", "La imagen está fijada encima. Arrastra para moverla, la rueda hace zoom, Esc cierra." },
            ["pin.opacity"] = new[] { "Gennemsigtighed", "Opacity", "Deckkraft", "Opacidad" },
            ["hist.empty"] = new[] { "(ingen endnu)", "(none yet)", "(noch keine)", "(ninguna todavía)" },
            ["hist.openFolder"] = new[] { "Åbn historikmappen", "Open history folder", "Verlaufsordner öffnen", "Abrir carpeta del historial" },
            ["hist.clear"] = new[] { "Ryd historik", "Clear history", "Verlauf löschen", "Borrar historial" },
            ["delay.countdown"] = new[] { "Skærmbillede om {0} …", "Screenshot in {0} …", "Screenshot in {0} …", "Captura en {0} …" },
            ["about.title"] = new[] { "Om ImageShot", "About ImageShot", "Über ImageShot", "Acerca de ImageShot" },
            ["about.version"] = new[] { "Version {0}", "Version {0}", "Version {0}", "Versión {0}" },
            ["about.tagline"] = new[] { "Skærmbilleder i alle programmer – med scroll, tegneværktøjer og tekstgenkendelse.", "Screenshots in every program – with scrolling capture, annotation tools and text recognition.", "Screenshots in jedem Programm – mit Scroll-Aufnahme, Zeichenwerkzeugen und Texterkennung.", "Capturas en cualquier programa, con desplazamiento, herramientas de anotación y reconocimiento de texto." },
            ["about.data"] = new[] { "Indstillinger og logfiler: {0}", "Settings and logs: {0}", "Einstellungen und Protokolle: {0}", "Configuración y registros: {0}" },
            ["about.checkUpdates"] = new[] { "Søg efter opdateringer", "Check for updates", "Nach Updates suchen", "Buscar actualizaciones" },
            ["about.upToDate"] = new[] { "Du har den nyeste version.", "You have the latest version.", "Sie haben die neueste Version.", "Tienes la versión más reciente." },
            ["about.openLogs"] = new[] { "Åbn logmappe", "Open log folder", "Protokollordner öffnen", "Abrir carpeta de registros" },
        };
    }
}
