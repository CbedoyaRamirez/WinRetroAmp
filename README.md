# WinRetro Amp

Un homenaje a los reproductores de MP3 clásicos con skins de finales de los 90 (estilo 2.x), hecho con .NET 10, WPF y NAudio. Tiene la misma disposición y el mismo comportamiento: reproductor principal de 275×116, ecualizador de 10 bandas y lista de reproducción, ventanas que se acoplan entre sí, modo sombra, doble tamaño y compatibilidad con skins clásicos `.wsz`.

<p align="center">
  <img src="docs/screenshots/main-eq-playlist.png" alt="WinRetro Amp: reproductor, ecualizador y lista acoplados" width="275">
</p>

> **Sobre la marca:** el nombre, el icono, el emblema y el skin por defecto ("Graphite") son **originales** y se dibujan en código (`DefaultSkinPainter`). No se incluye ningún bitmap, logotipo, nombre ni sonido de otro reproductor. Lo que sí se respeta es el **formato** de los skins clásicos (nombres de archivo y coordenadas de los sprites), para que cada persona pueda cargar los skins que ya tenga.

## Funciones

**Reproducción**
- MP3, AAC/M4A, WMA, FLAC, OGG Vorbis, WAV y AIFF (Media Foundation, NVorbis).
- Radios de Internet HTTP/Shoutcast en MP3, con el título de la canción leído de los metadatos ICY.
- Reproducir, pausar, detener, anterior y siguiente, avanzar y retroceder, aleatorio (todas las pistas suenan una vez antes de repetir) y repetir.
- Volumen y balance, elección del dispositivo de salida y teclas multimedia del teclado en todo el sistema.

**Ventana principal**
- Tiempo transcurrido o restante (clic sobre los dígitos) y parpadeo en pausa.
- Analizador de espectro (barras gruesas o finas, con picos y caída configurable), osciloscopio o apagado (clic sobre el visualizador).
- Título que se desplaza (se puede arrastrar), kbps, kHz y mono/estéreo.
- Barra lateral O A I D V: opciones, siempre visible, información del archivo, doble tamaño y visualización.
- Modo sombra (275×14) con minitiempo, minivisualizador, minibarra de posición y controles.

**Ecualizador**
- 10 bandas (60 Hz a 16 kHz, ±12 dB) más preamplificador, ON/AUTO y curva de respuesta.
- Presets incluidos y propios: guardar, borrar y cargar. Importa y exporta `.eqf` y bibliotecas `.q1`.
- AUTO: guarda y aplica un preset para cada archivo.
- Doble clic en un control lo devuelve a 0 dB; la rueda del ratón lo ajusta.

**Lista de reproducción**
- Redimensionable en pasos de 25×29 px, con colores y fuente del skin (`pledit.txt`).
- Selección con clic, Ctrl, Mayús y Ctrl+A; arrastrar para reordenar; soltar archivos y carpetas desde el Explorador.
- Menús emergentes ADD, REM, SEL, MISC y LIST, como el original: añadir archivos, carpeta o URL; quitar duplicados o archivos que ya no existen; recortar; ordenar; invertir; aleatorizar; exportar a HTML.
- Abre y guarda M3U, M3U8 y PLS. Muestra el tiempo total y el de la selección, y tiene minicontroles y modo sombra.
- **Saltar a archivo** (J): búsqueda por palabras.
- **Información del archivo** (Alt+3): datos técnicos y edición de etiquetas (título, artista, álbum, año, pista, género, comentario).

**Ventanas y sistema**
- Imanes a 10 px entre ventanas y bordes de la pantalla; al mover la principal se mueven las acopladas.
- Doble tamaño (Ctrl+D) y píxeles siempre nítidos con cualquier escala de Windows.
- Siempre visible, icono en la bandeja del sistema y opción de minimizar a la bandeja.
- Una sola instancia: al abrir canciones desde el Explorador se envían a la ventana que ya está abierta (`/ADD` para encolar).
- Asociación de archivos por usuario (sin permisos de administrador) desde Preferencias.
- Recuerda posiciones, skin, EQ, volumen, modos y la última lista (`%APPDATA%\WinRetroAmp`). Opcionalmente reanuda donde lo dejaste.

## Capturas

| Reproductor, ecualizador y lista acoplados | Doble tamaño (Ctrl+D) con osciloscopio |
|:---:|:---:|
| <img src="docs/screenshots/main-eq-playlist.png" alt="Las tres ventanas acopladas con el skin Graphite" width="275"> | <img src="docs/screenshots/double-size.png" alt="Reproductor y ecualizador a doble tamaño" width="550"> |

<p align="center">
  <img src="docs/screenshots/shade-mode.png" alt="Las tres ventanas en modo sombra" width="275"><br>
  <em>Modo sombra (Ctrl+W) en las tres ventanas</em>
</p>

*Todas las capturas usan el skin por defecto, Graphite, con pistas de demostración generadas.*

## Skins

Incluye siete skins seleccionables directamente en **Skins**: Graphite, Amber (ámbar), Arctic (hielo), Rose (rosa), Copper (cobre), Emerald (esmeralda) y Violet (violeta). Cada variante combina reproductor, ecualizador, lista y visualizador, y se recuerda al reiniciar. Sus archivos `.wsz` están en `skins/`; `tools/GenerateAssets` regenera toda la colección.

Menú principal (clic derecho) → **Skins** → *Load skin...*. Se copia a `%APPDATA%\WinRetroAmp\Skins` y queda en la lista. Se leen `main`, `cbuttons`, `titlebar`, `shufrep`, `posbar`, `volume`, `balance`, `monoster`, `playpaus`, `numbers`/`nums_ex`, `text`, `eqmain`, `eq_ex` y `pledit` (BMP o PNG), además de `viscolor.txt` y `pledit.txt`. Los nombres no distinguen mayúsculas y pueden estar en una subcarpeta del zip; lo que falte se toma del skin por defecto.

*Export default skin as .wsz...* guarda el skin Graphite como plantilla para crear el tuyo (también está en `skins/WinRetroAmp-Graphite.wsz`).

## Atajos de teclado

| Tecla | Acción | Tecla | Acción |
|---|---|---|---|
| Z X C V B | Anterior, reproducir, pausa, detener, siguiente | ← / → | −5 s / +5 s (con Ctrl, ±1 min) |
| ↑ / ↓ | Volumen | Num 1 / Num 3 | −10 / +10 pistas |
| L, Mayús+L, Ctrl+L | Abrir archivo, carpeta o URL | J | Saltar a archivo |
| S / R | Aleatorio / repetir | Alt+3 | Información del archivo |
| Alt+E / Alt+G | Lista / ecualizador | Ctrl+D | Doble tamaño |
| Ctrl+A | Siempre visible (en la lista: seleccionar todo) | Ctrl+T | Tiempo transcurrido o restante |
| Ctrl+W | Modo sombra (Ctrl+Mayús+W lista, Ctrl+Alt+W EQ) | Ctrl+P | Preferencias |
| Supr, Intro, Alt+↑/↓ | Lista: quitar, reproducir, mover | Alt+S | Cargar skin |

## Arquitectura

```
src/WinRetroAmp.Core   Sin interfaz: motor de audio (NAudio 3), EQ biquad, FFT, radio ICY, lista,
                       M3U/PLS, EQF, etiquetas (TagLibSharp), skins (.wsz), ajustes
src/WinRetroAmp        WPF: ventanas dibujadas con sprites (PixelImage → WriteableBitmap, escala entera
                       con NearestNeighbor), acoplamiento, menús, diálogos, bandeja, teclas multimedia
tests/                 xUnit: 22 pruebas (EQ ±12 dB, FFT, lista, aleatorio, formatos, skins, etiquetas)
tools/GenerateAssets   Genera app.ico y el .wsz del skin por defecto a partir del código
```

Cadena de audio: `lector → estéreo → ecualizador → analizador → volumen/balance → WaveOut`.

## Compilar y ejecutar

Requisitos: Windows 10/11 y el SDK de .NET 10.

```powershell
dotnet run --project src/WinRetroAmp            # ejecutar
dotnet test                                     # pruebas
dotnet run --project tools/GenerateAssets -- .  # regenerar icono y skin por defecto

# Un único .exe autocontenido (no necesita .NET instalado):
dotnet publish src/WinRetroAmp -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o artifacts/publish
```

Para un modo portátil o para hacer pruebas sin tocar tu perfil, define `WINRETROAMP_DATA` con la carpeta donde quieres guardar ajustes, lista y skins.

## Fuera de alcance (v1)

Plugins nativos (`in_`, `out_`, `dsp_`, `vis_`, `gen_`), MilkDrop/AVS, biblioteca multimedia, skins modernos `.wal`, CD de audio, fundido entre canciones, formas de ventana `region.txt`, cursores de skin y radios en AAC (solo MP3).
