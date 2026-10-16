# ImageShot – extensión para el navegador

Capturas de pantalla para Chrome, Edge, Ecosia y otros navegadores Chromium: captura el **área visible**, un
**área seleccionada**, la **página completa** o **todas las pestañas** y cópiala al portapapeles o guárdala como archivo.
La extensión forma parte de **ImageShot** (aplicación para Windows + extensión para el navegador).

Idiomas: English, Dansk, Deutsch, Español; se usa automáticamente el idioma del navegador.
Otros idiomas: [English](README.md) · [Dansk](README.da.md) · [Deutsch](README.de.md)

## Instalación

1. Descomprime el archivo ZIP para obtener la carpeta `extension`.
2. Escribe `chrome://extensions` en la barra de direcciones (o: Menú → Extensiones → Gestionar extensiones).
3. Activa el **Modo de desarrollador** (arriba a la derecha).
4. Haz clic en **Cargar descomprimida** y elige la carpeta `extension`.
5. Haz clic en el icono de la pieza de puzle de la barra de herramientas y fija **ImageShot**.
   No muevas la carpeta: la extensión se ejecuta desde ella.

## Uso

* **Haz clic derecho** en una página → **ImageShot** → *Área visible* · *Seleccionar área* · *Página completa* · *Todas las pestañas*
* O haz clic en el **icono de ImageShot** de la barra de herramientas y elige ahí la captura.
* **Atajos de teclado:** `Alt+Mayús+S` (área visible), `Alt+Mayús+A` (seleccionar área).
  Puedes cambiarlos en `chrome://extensions/shortcuts`.
* **Seleccionar área:** arrastra con el ratón. `Esc` o un clic derecho cancela.
  Si llevas el ratón al borde superior o inferior mientras arrastras, la página (o el cuadro desplazable de la
  aplicación web) se desplaza sola y la selección puede ser más alta que la pantalla. La rueda del ratón también
  funciona. Las partes se unen en una sola imagen.

### Ajustes (haz clic en el icono)

* **Resolución:** el control deslizante define el tamaño y la calidad de la imagen. 70 = 87 % de la resolución de
  pantalla, calidad 85.
* **Formato de archivo:** WebP (archivos pequeños), PNG (sin pérdida) o JPEG.
* **Después de capturar:**
  * Mostrar el portapapeles: elegir copiar o guardar
  * Copiar directamente al portapapeles
  * Guardar directamente como archivo
* **Cerrar el portapapeles automáticamente** cuando la imagen se haya copiado o guardado; está en los ajustes y
  en la parte inferior de la ventana del portapapeles.
* **«Todas las pestañas» incluye:** las pestañas de la ventana actual o de todas las ventanas.
* **Carpeta en Descargas:** p. ej. `ImageShot` (se permiten subcarpetas con `/`).
  Los archivos se llaman `ImageShot AAAA-MM-DD HH.mm.ss`.

### Recortar

Haz clic en **Recortar** en la ventana del portapapeles, arrastra un marco alrededor de lo que quieres conservar y
ajusta los bordes y las esquinas. Arrastra dentro del marco para moverlo. `Enter` (o doble clic) aplica y `Esc`
cancela. **Deshacer recorte** o `Ctrl+Z` vuelve atrás. Siempre se recorta a partir del original, sin pérdida de
calidad adicional. Copiar y Guardar usan la imagen recortada.

## Consejos

* Al copiar, la imagen siempre se coloca en el portapapeles como PNG, porque el portapapeles solo admite PNG.
  Los archivos guardados usan el formato elegido.
* «Copiar directamente al portapapeles» con *Todas las pestañas* muestra en su lugar la ventana del portapapeles,
  porque el portapapeles solo puede contener una imagen a la vez.
* *Página completa* en aplicaciones web en las que el contenido se desplaza dentro de un cuadro y no en la ventana
  (p. ej. Gmail, Saxo, Notion): ImageShot encuentra el cuadro y lo recorre. La imagen incluye la barra superior
  arriba, todo el contenido del cuadro y la barra inferior abajo. Las zonas junto al cuadro (p. ej. un menú)
  aparecen una sola vez.
* La primera vez que uses *Todas las pestañas*, Chrome pedirá acceso a todos los sitios web: ImageShot lo necesita
  para capturar pestañas distintas de la actual. Consulta [PERMISSIONS.md](PERMISSIONS.md) (en inglés).

## Limitaciones

* Las páginas propias del navegador (`chrome://`, Chrome Web Store, otras extensiones) no se pueden capturar.
  El icono muestra entonces un «!» rojo; al pasar el ratón por encima se ve el motivo.
* *Página completa*: las barras superiores fijas se ocultan tras la primera parte para que no se repitan. Las
  páginas muy largas se cortan a los 30 000 px. WebP admite como máximo 16 383 px; las imágenes más largas se
  guardan como PNG.
* Las listas que cargan más contenido al desplazarse (feeds infinitos) solo se capturan hasta donde estaban
  cargadas al empezar la captura.
* *Todas las pestañas* cambia brevemente entre las pestañas mientras captura y después vuelve a la pestaña en la
  que estabas. Las ventanas minimizadas no se pueden capturar.
* Las capturas solo se conservan mientras la ventana del portapapeles está abierta (y nunca más de una hora).

## Privacidad y permisos

ImageShot funciona sin conexión y no envía nada a ningún sitio. Las capturas se quedan en el almacenamiento local
de la extensión hasta que se cierra la ventana del portapapeles. Todos los permisos se explican en
[PERMISSIONS.md](PERMISSIONS.md).

## Desarrollo

Las pruebas automáticas (Playwright) están en [`tests/`](tests/README.md). Excluye la carpeta `tests/` al
empaquetar la extensión para Chrome Web Store.
