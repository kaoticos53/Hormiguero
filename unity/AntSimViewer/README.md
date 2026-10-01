# AntSim Viewer for Unity

Este directorio contiene un proyecto Unity listo para abrir que visualiza la simulación de AntSim.
No usa paquetes de terceros: el render se apoya en el pipeline incorporado y `GL`, por lo que se
puede convertir después a prefabs, VFX Graph o DOTS/Burst sin cambiar el formato de datos.

## Requisitos

- Unity **6.0 / 6000.0.32f1** o una versión 6000.x compatible.
- Módulo de plataforma que quieras usar (Windows, macOS o Linux).
- El proyecto .NET raíz solo es necesario para generar nuevos campeones y reproducciones.

## Abrir y ejecutar

1. Abre Unity Hub y selecciona **Add > Add project from disk**.
2. Elige esta carpeta: `unity/AntSimViewer`.
3. Abre una escena vacía (por ejemplo, `File > New Scene > Empty Scene`) y pulsa **Play**.
4. `ViewerBootstrap` crea automáticamente la cámara, el controlador y el renderer; no hace falta
   preparar GameObjects manualmente.
5. La primera ejecución abre `Assets/StreamingAssets/replay.json` si existe. Si no existe, inicia
   una simulación en vivo con el cerebro de `champion.json`.

También puedes crear una escena persistente mediante **AntSim > Create Viewer Scene** y guardarla
como escena inicial de tu build. El menú genera el YAML usando Unity, evitando errores de escenas
hechas a mano.

## Archivos de datos

Los archivos se buscan en `Application.streamingAssetsPath`:

- `Assets/StreamingAssets/champion.json`: export autocontenido de `evolve`, con conexiones NEAT,
  cuerpo y plasticidad.
- `Assets/StreamingAssets/replay.json`: replay muestreado por `demo replay=...`, con configuración,
  posiciones, rumbo, carga, comida inicial/restante, semilla y estadísticas por frame. El replay es
  autosuficiente y puede haberse producido con el baseline o con otro campeón.

Para actualizar los datos desde la raíz del repositorio:

```bash
# Campeón producido por una evolución
cp output/run6/champion.json unity/AntSimViewer/Assets/StreamingAssets/champion.json

# Replay con el cerebro campeón; junto al .neat debe existir el .json del mismo nombre
./run.sh demo ticks=4000 seed=42 brain=output/run6/champion.neat \
  replay=unity/AntSimViewer/Assets/StreamingAssets/replay.json
```

En Windows, el equivalente es:

```bat
run.bat demo ticks=4000 seed=42 brain=output\run6\champion.neat replay=unity\AntSimViewer\Assets\StreamingAssets\replay.json
```

El exportador escribe PascalCase (`InputCount`, `Frames`, `FoodSources`, etc.), igual que los DTOs
serializables de `Scripts/Data/AntSimData.cs`. No edites los nombres de los campos del JSON.

## Modos y controles

- `Auto`: reproduce `replay.json` si existe; si no, ejecuta modo Live.
- `Live`: simula el mundo en tiempo real con `champion.json`; si falta, usa el cerebro baseline.
- `Replay`: reproduce las posiciones grabadas e interpola los frames, incluidos los cruces del borde
toroidal.

| Tecla | Acción |
|---|---|
| `Space` | Pausar / reanudar |
| `Up` / `Down` | Aumentar / reducir velocidad |
| `R` | Reiniciar replay o live con otra semilla |
| `B` | Alternar campeón / baseline en modo Live |
| `M` | Alternar Live / Replay |
| `T` | Mostrar / ocultar feromonas |
| `Esc` | Salir del Play Mode o de la aplicación |

El HUD muestra modo, tick, comida entregada, hormigas vivas y velocidad. Las hormigas naranjas
llevan comida; el mapa verde representa `FoodTrail` y el azul `HomeTrail`.

## Estructura

```text
Assets/AntSimViewer/Scripts/
  AntSimViewerController.cs   carga datos, modos, controles y HUD
  ViewerBootstrap.cs           arranque sin escena preparada
  View/ViewerRenderer.cs       cámara y render de mundo/feromonas
  ReplayPlayer.cs              reproducción interpolada y toroidal
  Data/AntSimData.cs           DTOs compatibles con JsonUtility
  Neural/NeuralNetRuntime.cs   evaluación del campeón cíclico NEAT
  Sim/                         port Unity del mundo y del baseline
Assets/StreamingAssets/        champion.json y replay.json de ejemplo
Packages/manifest.json         solo paquetes oficiales mínimos
```

## Crear una build

1. Abre **File > Build Profiles**.
2. Selecciona la plataforma y pulsa **Switch Platform**.
3. Añade la escena creada por **AntSim > Create Viewer Scene** a `Scenes In Build`, o usa una escena
   vacía: el bootstrap funciona en ambos casos.
4. Pulsa **Build and Run**.

`StreamingAssets` se copia automáticamente al lado de la build. En Android/WebGL, el acceso a
`StreamingAssets` puede requerir una carga asíncrona mediante `UnityWebRequest`; el adaptador actual
está orientado a escritorio y se mantiene deliberadamente sencillo para poder depurarlo con archivos
locales.

## Limitaciones conocidas

- El runtime reproduce el núcleo de hormigas negras implementado en .NET. Las especies de fuego,
  ejército y cortadora siguen siendo futuras extensiones del modelo.
- La representación actual es inmediata y está pensada como viewer funcional/prototipo. Para cientos
  de miles de hormigas conviene sustituirla por `Graphics.DrawMeshInstanced`, Entities Graphics o
  compute shaders.
- La validación automática disponible en este checkout cubre el exportador .NET; la compilación del
  proyecto Unity debe hacerse desde Unity Editor porque `UnityEngine` no forma parte del SDK .NET.
