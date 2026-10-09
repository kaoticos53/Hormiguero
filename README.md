# AntSim — Hormiguero simulado con NEAT (C# / .NET)

Simulación realista de uno o varios hormigueros con hormigas controladas por redes neuronales
que evolucionan con **NEAT** (SharpNEAT 4.1.0) y un GA paralelo de parámetros corporales.
Núcleo de simulación puro en .NET, sin dependencia de motores: evoluciona *headless* a máxima
velocidad, y un **visor gráfico en tiempo real (OpenTK/OpenGL)** muestra el hormiguero vivo —
incluida la opción de ver actuar a un cerebro evolucionado.

## Arquitectura

```
┌─────────────────────┐     ┌──────────────────────┐     ┌────────────────────┐
│  AntSim.Core        │ ←── │  AntSim.Evolution    │ ──► │  AntSim.Headless   │
│  mundo 2D toral,    │     │  SharpNEAT 4.1.0:    │     │  CLI: demo / bench │
│  feromonas, hash    │     │  fitness por colonia,│     │  evolve / evaluate │
│  espacial, hormigas │     │  GA corporal, export │     │  CSV + checkpoints │
│  (determinista)     │     │  JSON del campeón    │     │                    │
└─────────────────────┘     └──────────────────────┘     └────────────────────┘
          ▲
          ├── AntSim.Viewer      OpenTK/OpenGL en tiempo real
          └── unity/AntSimViewer Unity 6: consume champion.json + replay.json
```

**Decisiones clave**

- **Núcleo puro .NET 10, determinista**: timestep fijo, PRNG sembrado (xoshiro), mismo config +
  especie + semilla ⇒ misma trayectoria bit a bit (testado). La evolución corre sin UI a ~2.400
  ticks/s por núcleo.
- **Fitness por colonia, no por hormiga**: un genoma NEAT por casta (obrera), compartido por las
  ~150 hermanas de la colonia. El fitness es la comida entregada al nido por episodio — realista
  (las obreras son hermanas) y mucho menos ruidoso que fitness individual.
- **GA corporal en paralelo**: velocidad, alcance sensorial, tasa de deposición y longevidad
  evolucionan como *hill-climber (1+λ)* junto a los cerebros NEAT; la plasticidad online
  (habitación de sensibilidad al rastro) se hereda con el genoma colonial.
- **Reflejos cableados, estrategia aprendida**: coger comida al contacto y soltarla en el nido son
  reflejos (como en hormigas reales); NEAT aprende movimiento, seguimiento de rastros y deposición
  (3 salidas: giro, velocidad, feromona).
- **Especies = datos**: `SpeciesCatalog` define canales de feromona, cerebro base y estado de
  implementación. La hormiga negra (*Lasius niger*) es la especie de validación; fuego, ejército
  y cortadora de hojas están definidas para fases posteriores.

## Visor gráfico en tiempo real

`AntSim.Viewer` (OpenTK 4.9 / OpenGL 3.3) renderiza la simulación viva: rastros de feromona
como mapa de calor (verde = comida, azul = hogar), pilas de comida que encogen al agotarse,
nido y hormigas orientadas (naranja = cargando comida).

```bash
./view.sh                                   # línea base, semilla 1  (Windows: view.bat)
./view.sh seed=42 brain=output/run/champion.neat   # ver un cerebro evolucionado en vivo
./view.sh --release paused=1                # arrancar en pausa
```

| Tecla | Acción |
|-------|--------|
| `ESPACIO` | Pausar / reanudar |
| `↑` / `↓` | Velocidad ×1 … ×16 |
| `R` | Reiniciar con nueva semilla |
| `F` | Mostrar/ocultar rastros de feromona |
| `ESC` | Salir |

Opciones: `species=`, `seed=`, `brain=` (champion.neat), `paused=1`, `size=900`,
y `smoke=N` (auto-test: N fotogramas y salida). El visor no toca el núcleo: lee el estado
público de `SimWorld` cada fotograma, y la velocidad ×16 sigue siendo fluida.

### Proyecto Unity 6

También se incluye `unity/AntSimViewer`, un proyecto Unity 6 sin dependencias de terceros. Abre esa
carpeta en Unity Hub y pulsa **Play** incluso desde una escena vacía: `ViewerBootstrap` crea la
cámara, el controlador y el renderer automáticamente. El proyecto puede cargar directamente:

- `Assets/StreamingAssets/champion.json`: cerebro NEAT exportado, cuerpo y plasticidad.
- `Assets/StreamingAssets/replay.json`: replay con frames interpolados, comida restante y feromonas
  visualizadas cuando se genera desde la CLI.

Para crear una escena persistente usa **AntSim > Create Viewer Scene**. Las instrucciones completas,
controles, builds de escritorio y limitaciones de StreamingAssets están en
[`unity/AntSimViewer/README.md`](unity/AntSimViewer/README.md). La validación del proyecto Unity
requiere el Editor; la solución .NET y el visor OpenTK sí se validan automáticamente desde CLI.

## Uso

Scripts de compilación + ejecución (compilan la solución y lanzan la CLI; sin argumentos
ejecutan un episodio `demo`; `--release` compila optimizado, recomendado para evoluciones largas):

```bash
./run.sh                              # bash / Git Bash
run.bat                               # Windows (cmd)
./run.sh --release evolve gens=100 popsize=128 out=output/run
```

Alternativamente, con dotnet directamente:

```bash
# Simulación de demostración con el cereal codificado a mano (línea base)
dotnet run --project src/AntSim.Headless -- demo ticks=4000 seed=1 replay=replay.json

# Benchmark del núcleo
dotnet run --project src/AntSim.Headless -- bench runs=3 ticks=4000

# Evolución NEAT + GA corporal (escribe fitness.csv y checkpoints cada 10 generaciones)
dotnet run --project src/AntSim.Headless -- evolve gens=100 popsize=128 episodes=2 seed=12345 out=output/run

# Evaluar el campeón en el carril de validación (semillas que la evolución nunca vio)
dotnet run --project src/AntSim.Headless -- evaluate path=output/run/champion.neat lane=val runseed=12345 episodes=6

# Reproducir el número de entrenamiento del campeón (exactamente las semillas de selección)
dotnet run --project src/AntSim.Headless -- evaluate path=output/run/champion.neat lane=train runseed=12345 episodes=2
```

Opciones de `evolve`: `gens`, `popsize`, `speciescount`, `episodes` (episodios por genoma),
`ticks` (por episodio), `workers` (hormigas por colonia), `seed`, `food=near|far`
(curriculo: comida cercana al nido durante la evolución, por defecto `near`), `valepisodes`
(episodios de validación, por defecto 3) y `valinterval` (cada cuántas generaciones se informa,
por defecto 5; la última siempre).

Opciones de `evaluate`: `path`, `lane=val|train|raw` (por defecto `val`), `runseed` (semilla de la
corrida cuya carril se quiere reproducir), `episodes`, `ticks`, `food=near|far`, `species` y
`seed` (solo con `lane=raw`). `evaluate` carga el cuerpo evolucionado de `champion.json`: medir un
campeón con el cuerpo por defecto no mide a ese campeón.

**Salidas de `evolve`** (en `out/`): `fitness.csv` (historial por generación, escrito en streaming;
incluye la columna `validationFitness`), `champion.neat` (genoma nativo SharpNEAT, reutilizable con
`evaluate`) y `champion.json` (export autocontenido: conexiones, pesos, parámetros corporales y
plasticidad — formato pensado para el visor Unity).

## Carriles de semillas: entrenamiento vs validación

La selección —fitness NEAT y GA corporal por igual— optimiza **solo** el carril de entrenamiento
(`seed × 1000 + índice de episodio`). El carril de validación vive mil millones por encima
(`SeedSpaces`) y únicamente se **lee**, de modo que la diferencia entre ambos mide generalización y
no suerte en dos mundos concretos. `evolve` imprime ambos carriles al arrancar (para poder
reproducir el run) y `fitness.csv` lleva la columna `validationFitness`.

Medición del campeón de `output/run6` (`seed=424242`, entrenado con curriculo `food=near`), a 4.000
ticks y 150 obreras:

| carril | comida | episodios | entregado/ep | % del techo | fitness/ep |
|---|---|---|---|---|---|
| train | near (120–220) | 2 — los de selección | 360,0 | 100 % | 378,0 |
| val | near | 3 | 356,7 | 99,1 % | 374,7 |
| train | far (250–350) | 6 | 323,8 | 90,0 % | 340,7 |
| val | far | 6 | 334,3 | 92,9 % | 351,7 |

La fila 1 reproduce el best de la generación 120 de `run6` (378,0 × 2 episodios = **756**) desde el
CLI, lo que fija el contrato de los carriles. Informar la validación cuesta lo medido: ~670 ms por
generación (≈5 % de una generación de 64 genomas × 2 episodios de 4.000 ticks con 150 obreras en 16
núcleos). El carril de validación puntúa *igual o mejor* que el
de entrenamiento: este campeón no tiene optimismo de semillas, generaliza. Lo que sí aparece es lo
contrario — el fitness de entrenamiento está **saturado** (100 % del techo en los mundos `near`), así
que no puede distinguir entre dos colonias perfectas, y la dispersión entre mundos (268–359
entregados) supera cualquier diferencia train/val. Para que la métrica vuelva a discriminar hay que
endurecer la tarea (mundo mayor, comida más lejos, coste de cargar) antes que añadir generaciones.

## Resultados de validación (24 generaciones, pop 64, 2 episodios, 2.000 ticks)

```
gen 1    best 11.35   mean 8.40     ← población inicial aleatoria
gen 19   best 20.50   mean 10.82    ← NEAT descubre la entrega de comida
gen 20   best 24.15   bodyScore 82  ← el GA corporal adopta un cuerpo mejor
gen 24   best 57.60   mean 27.14    ← co-evolución cerebro+cuerpo despegando
```

Línea base codificada a mano (mismo mundo): ~120–230 unidades de comida/episodio de 4.000 ticks.
NEAT parte de ~6 y necesita cientos de generaciones (pop 128, `episodes=2`) para alcanzarla —
la maquinaria de co-evolución ya está validada.

## Diseño de la simulación

- **Campos de feromona** en rejillas toroidales (evaporación exponencial cada tick, difusión
  4-vecina cada N ticks, saturación por celda). Las hormigas muestrean con 3 "antenas"
  (izquierda/centro/derecha) por canal.
- **Sensores NEAT (14 + bias)**: muestras de rastro de comida y de hogar ×3 antenas, sin/cos del
  acimut al nido y a la comida visible, nivel de energía, indicador de carga, ruido uniforme.
- **Mundo toroidal** 800×800 con hash espacial para consultas de vecinos (O(n)); 6 pilas de comida
  a 250–350 unidades del nido (curriculo de evolución: 120–220).
- **Replay determinista**: `demo replay=…` exporta fotogramas muestreados de toda la colonia
  (posiciones, rumbo, carga, comida restante y estadísticas) en JSON para los visores.

## Tests (27)

`dotnet test` — determinismo bit a bit (misma semilla ⇒ mismo estado; distinta semilla ⇒ divergencia),
invariantes de feromonas (conservación de masa en difusión toroidal, saturación, evaporación),
consultas del hash espacial (sin falsos negativos, envoltura toroidal), umbral de la línea base,
tests estructurales del cableado SharpNEAT (esquema E/S, decodificación, evaluador), el contrato de
los carriles de semillas (disjunción train/val para cualquier corrida, layout histórico intacto) y
que el fitness del evaluador sea reproducible desde el carril de entrenamiento, con la validación
fuera de muestra informada por el runner, y que **la evolución entera sea bit-reproducible** (dos
corridas del mismo seed dan el mismo historial).

**Nota sobre determinismo**: la simulación y **también la evolución** son bit-a-bit reproducibles.
La especiación k-means de SharpNEAT corría en paralelo y reducía flotantes en orden dependiente del
planificador; ahora corre en un solo hilo (`AntEvolutionExperiment`) y la evolución deja de divergir
entre corridas idénticas. La evaluación de genomas sigue en paralelo: es determinista porque el
fitness de cada genoma es independiente y cada episodio ya era reproducible. El historial de cada
corrida queda en `fitness.csv`.

## Hoja de ruta

1. ✅ **MVP**: núcleo determinista, línea base codificada, NEAT por casta + GA corporal, CLI headless.
2. **Escalar evolución**: pop 128–256, `episodes=3`, 100–300 generaciones hasta batir la línea base;
   ampliar el curriculo (distancia de comida creciente por generaciones).
3. **Dinámica de colonia**: reina, cría (brood), muertes y reposición; trofalaxis.
4. **Segunda especie + competición**: hormiga de fuego (alarma + aguijón), ejército (raids sin nido
   fijo), cortadora (huertos de hongo) — cada una como configuración + mecánicas nuevas.
5. ✅ **Visores**: OpenTK en tiempo real y proyecto Unity 6 listo para abrir; ambos consumen
   `champion.json` y `replay.json`, con replay interpolado, mapa de feromonas y controles de live view.

## Estructura

```
src/AntSim.Core         Simulación pura: mundo, feromonas, hormigas, cerebros, especies
src/AntSim.Evolution    SharpNEAT: esquema de evaluación, experimento, GA corporal, export JSON
src/AntSim.Headless     CLI: demo / bench / evolve / evaluate
src/AntSim.Viewer       Visor gráfico en tiempo real (OpenTK/OpenGL)
unity/AntSimViewer       Proyecto Unity 6 + scripts de live/replay
tools/replay_map.py      Mapas ASCII de densidad para analizar replays
tests/AntSim.Tests      27 tests xUnit (determinismo, invariantes, umbrales, cableado NEAT, carriles)
```
