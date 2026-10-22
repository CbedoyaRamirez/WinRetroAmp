# WinRetro Amp

Un homenaje a los reproductores de MP3 clÃ¡sicos con skins de finales de los 90 (estilo 2.x), hecho con .NET 10, WPF y NAudio. Tiene la misma disposiciÃ³n y el mismo comportamiento: reproductor principal de 275Ã—116, ecualizador de 10 bandas y lista de reproducciÃ³n, ventanas que se acoplan entre sÃ­, modo sombra, doble tamaÃ±o y compatibilidad con skins clÃ¡sicos `.wsz`.

> **Sobre la marca:** el nombre, el icono, el emblema y el skin por defecto ("Graphite") son **originales** y se dibujan en cÃ³digo (`DefaultSkinPainter`). No se incluye ningÃºn bitmap, logotipo, nombre ni sonido de otro reproductor. Lo que sÃ­ se respeta es el **formato** de los skins clÃ¡sicos (nombres de archivo y coordenadas de los sprites), asÃ­ cada persona puede cargar los skins que ya tenga.

## Funciones

**ReproducciÃ³n**
- MP3, AAC/M4A, WMA, FLAC, OGG Vorbis, WAV y AIFF (Media Foundation, NVorbis).
- Radios de Internet HTTP/Shoutcast en MP3, con el tÃ­tulo de la canciÃ³n leÃ­do de los metadatos ICY.
- Reproducir, pausar, detener, anterior y siguiente, avanzar y retroceder, aleatorio (todas las pistas suenan una vez antes de repetir) y repetir.
- Volumen y balance, elecciÃ³n del dispositivo de salida y teclas multimedia del teclado en todo el sistema.

**Ventana principal**
- Tiempo transcurrido o restante (clic sobre los dÃ­gitos) y parpadeo en pausa.
- Analizador de espectro (barras gruesas o finas, con picos y caÃ­da configurable), osciloscopio o apagado (clic sobre el visualizador).
- TÃ­tulo que se desplaza (se puede arrastrar), kbps, kHz y mono/estÃ©reo.
- Barra lateral O A I D V: opciones, siempre visible, informaciÃ³n del archivo, doble tamaÃ±o y visualizaciÃ³n.
- Modo sombra (275Ã—14) con minitiempo, minivisualizador, minibarra de posiciÃ³n y controles.

**Ecualizador**
- 10 bandas (60 Hz a 16 kHz, Â±12 dB) mÃ¡s preamplificador, ON/AUTO y curva de respuesta.
- Presets incluidos y propios: guardar, borrar y cargar. Importa y exporta `.eqf` y bibliotecas `.q1`.
- AUTO: guarda y aplica un preset para cada archivo.
- Doble clic en un control lo devuelve a 0 dB; la rueda del ratÃ³n lo ajusta.

**Lista de reproducciÃ³n**
- Redimensionable en pasos de 25Ã—29 px, con colores y fuente del skin (`pledit.txt`).
- SelecciÃ³n con clic, Ctrl, MayÃºs y Ctrl+A; arrastrar para reordenar; soltar archivos y carpetas desde el Explorador.
- MenÃºs emergentes ADD, REM, SEL, MISC y LIST, como el original: aÃ±adir archivos, carpeta o URL; quitar duplicados o archivos que ya no existen; recortar; ordenar; invertir; aleatorizar; exportar a HTML.
- Abre y guarda M3U, M3U8 y PLS. Muestra el tiempo total y el de la selecciÃ³n, y tiene minicontroles y modo sombra.
- **Saltar a archivo** (J): bÃºsqueda por palabras.
- **InformaciÃ³n del archivo** (Alt+3): datos tÃ©cnicos y ediciÃ³n de etiquetas (tÃ­tulo, artista, Ã¡lbum, aÃ±o, pista, gÃ©nero, comentario).

**Ventanas y sistema**
- Imanes a 10 px entre ventanas y bordes de la pantalla; al mover la principal se mueven las acopladas.
- Doble tamaÃ±o (Ctrl+D) y pÃ­xeles siempre nÃ­tidos con cualquier escala de Windows.
- Siempre visible, icono en la bandeja del sistema y opciÃ³n de minimizar a la bandeja.
- Una sola instancia: al abrir canciones desde el Explorador se envÃ­an a la ventana que ya estÃ¡ abierta (`/ADD` para encolar).
- AsociaciÃ³n de archivos por usuario (sin permisos de administrador) desde Preferencias.
- Recuerda posiciones, skin, EQ, volumen, modos y la Ãºltima lista (`%APPDATA%\WinRetroAmp`). Opcionalmente reanuda donde lo dejaste.

## Skins

MenÃº principal (clic derecho) â†’ **Skins** â†’ *Load skin...*. Se copia a `%APPDATA%\WinRetroAmp\Skins` y queda en la lista. Se leen `main`, `cbuttons`, `titlebar`, `shufrep`, `posbar`, `volume`, `balance`, `monoster`, `playpaus`, `numbers`/`nums_ex`, `text`, `eqmain`, `eq_ex` y `pledit` (BMP o PNG), ademÃ¡s de `viscolor.txt` y `pledit.txt`. Los nombres no distinguen mayÃºsculas, pueden estar en una subcarpeta del zip, y lo que falte se toma del skin por defecto.

*Export default skin as .wsz...* guarda el skin Graphite como plantilla para crear el tuyo (tambiÃ©n estÃ¡ en `skins/WinRetroAmp-Graphite.wsz`).

## Atajos de teclado

| Tecla | AcciÃ³n | Tecla | AcciÃ³n |
|---|---|---|---|
| Z X C V B | Anterior, reproducir, pausa, detener, siguiente | â† / â†’ | âˆ’5 s / +5 s (con Ctrl, Â±1 min) |
| â†‘ / â†“ | Volumen | Num 1 / Num 3 | âˆ’10 / +10 pistas |
| L, MayÃºs+L, Ctrl+L | Abrir archivo, carpeta o URL | J | Saltar a archivo |
| S / R | Aleatorio / repetir | Alt+3 | InformaciÃ³n del archivo |
| Alt+E / Alt+G | Lista / ecualizador | Ctrl+D | Doble tamaÃ±o |
| Ctrl+A | Siempre visible (en la lista: seleccionar todo) | Ctrl+T | Tiempo transcurrido o restante |
| Ctrl+W | Modo sombra (Ctrl+MayÃºs+W lista, Ctrl+Alt+W EQ) | Ctrl+P | Preferencias |
| Supr, Intro, Alt+â†‘/â†“ | Lista: quitar, reproducir, mover | Alt+S | Cargar skin |

## Arquitectura

```
src/WinRetroAmp.Core   Sin interfaz: motor de audio (NAudio 3), EQ biquad, FFT, radio ICY, lista,
                       M3U/PLS, EQF, etiquetas (TagLibSharp), skins (.wsz), ajustes
src/WinRetroAmp        WPF: ventanas dibujadas con sprites (PixelImage â†’ WriteableBitmap, escala entera
                       con NearestNeighbor), acoplamiento, menÃºs, diÃ¡logos, bandeja, teclas multimedia
tests/                 xUnit: 22 pruebas (EQ Â±12 dB, FFT, lista, aleatorio, formatos, skins, etiquetas)
tools/GenerateAssets   Genera app.ico y el .wsz del skin por defecto a partir del cÃ³digo
```

Cadena de audio: `lector â†’ estÃ©reo â†’ ecualizador â†’ analizador â†’ volumen/balance â†’ WaveOut`.

## Compilar y ejecutar

Requisitos: Windows 10/11 y el SDK de .NET 10.

```powershell
dotnet run --project src/WinRetroAmp            # ejecutar
dotnet test                                     # pruebas
dotnet run --project tools/GenerateAssets -- .  # regenerar icono y skin por defecto

# Un Ãºnico .exe autocontenido (no necesita .NET instalado):
dotnet publish src/WinRetroAmp -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o artifacts/publish
```

Para un modo portÃ¡til o para hacer pruebas sin tocar tu perfil, define `WINRETROAMP_DATA` con la carpeta donde quieres guardar ajustes, lista y skins.

## Fuera de alcance (v1)

Plugins nativos (`in_`, `out_`, `dsp_`, `vis_`, `gen_`), MilkDrop/AVS, biblioteca multimedia, skins modernos `.wal`, CD de audio, fundido entre canciones, formas de ventana `region.txt`, cursores de skin y radios en AAC (solo MP3).
