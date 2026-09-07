# AntSim — Hormiguero simulado con NEAT (C# / .NET)

Simulación realista de uno o varios hormigueros con hormigas controladas por redes neuronales
que evolucionan con **NEAT** (SharpNEAT 4.1.0) y un GA paralelo de parámetros corporales.
Núcleo de simulación puro en .NET, sin dependencia de Unity: evoluciona *headless* a máxima
velocidad y cualquier visor (Unity incluido) consume los cerebros exportados en JSON.

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
          └── futuro visor Unity: consume champion.json + replays JSON
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

## Uso

```bash
# Simulación de demostración con el cereal codificado a mano (línea base)
dotnet run --project src/AntSim.Headless -- demo ticks=4000 seed=1 replay=replay.json

# Benchmark del núcleo
dotnet run --project src/AntSim.Headless -- bench runs=3 ticks=4000

# Evolución NEAT + GA corporal (escribe fitness.csv y checkpoints cada 10 generaciones)
dotnet run --project src/AntSim.Headless -- evolve gens=100 popsize=128 episodes=2 seed=12345 out=output/run

# Evaluar el campeón guardado en semillas no vistas
dotnet run --project src/AntSim.Headless -- evaluate path=output/run/champion.neat episodes=3
```

Opciones de `evolve`: `gens`, `popsize`, `speciescount`, `episodes` (episodios por genoma),
`ticks` (por episodio), `workers` (hormigas por colonia), `seed`, `food=near|far`
(curriculo: comida cercana al nido durante la evolución, por defecto `near`).

**Salidas de `evolve`** (en `out/`): `fitness.csv` (historial por generación, escrito en streaming),
`champion.neat` (genoma nativo SharpNEAT, reutilizable con `evaluate`) y `champion.json`
(export autocontenido: conexiones, pesos, parámetros corporales y plasticidad — formato pensado
para el visor Unity).

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
  (posiciones, rumbo, carga) en JSON para el visor.

## Tests (19)

`dotnet test` — determinismo bit a bit (misma semilla ⇒ mismo estado; distinta semilla ⇒ divergencia),
invariantes de feromonas (conservación de masa en difusión toroidal, saturación, evaporación),
consultas del hash espacial (sin falsos negativos, envoltura toroidal), umbral de la línea base y
tests estructurales del cableado SharpNEAT (esquema E/S, decodificación, evaluador).

**Nota sobre determinismo**: la simulación es bit-a-bit reproducible; la *evolución* completa no
lo es exactamente porque la especiación k-means paralelizada de SharpNEAT reduce flotantes en
orden dependiente del planificador de hilos. El historial de cada corrida queda en `fitness.csv`.

## Hoja de ruta

1. ✅ **MVP**: núcleo determinista, línea base codificada, NEAT por casta + GA corporal, CLI headless.
2. **Escalar evolución**: pop 128–256, `episodes=3`, 100–300 generaciones hasta batir la línea base;
   ampliar el curriculo (distancia de comida creciente por generaciones).
3. **Dinámica de colonia**: reina, cría (brood), muertes y reposición; trofalaxis.
4. **Segunda especie + competición**: hormiga de fuego (alarma + aguijón), ejército (raids sin nido
   fijo), cortadora (huertos de hongo) — cada una como configuración + mecánicas nuevas.
5. **Visor Unity**: cargar `champion.json` y `replay.json`; modo interactivo con hormigas vivas
   usando el cerebro exportado.

## Estructura

```
src/AntSim.Core         Simulación pura: mundo, feromonas, hormigas, cerebros, especies
src/AntSim.Evolution    SharpNEAT: esquema de evaluación, experimento, GA corporal, export JSON
src/AntSim.Headless     CLI: demo / bench / evolve / evaluate
tests/AntSim.Tests      19 tests xUnit (determinismo, invariantes, umbrales, cableado NEAT)
```
