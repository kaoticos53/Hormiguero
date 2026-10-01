using System;
using System.IO;
using UnityEngine;

namespace AntSimViewer
{
    public enum ViewerMode
    {
        Auto = 0,
        Live = 1,
        Replay = 2,
    }

    /// <summary>
    /// Main Unity entry point. Auto mode opens the bundled replay when available; press M to
    /// switch to live simulation. Live mode reads champion.json and applies its brain, body,
    /// and plasticity parameters. Replay mode reads replay.json and interpolates sampled frames.
    /// </summary>
    public sealed class AntSimViewerController : MonoBehaviour
    {
        [Header("Mode")]
        public ViewerMode Mode = ViewerMode.Auto;

        [Header("StreamingAssets files")]
        public string BrainFile = "champion";
        public string ReplayFile = "replay";

        [Header("Live simulation")]
        public long Seed = 1;
        public int EpisodeTicks = 4000;
        [Range(1, 64)] public int Speed = 4;
        public bool StartPaused;

        [Header("Display")]
        public bool ShowTrails = true;
        public bool AutoRestart;

        SimWorldRuntime _world;
        ReplayPlayer _replay;
        IAntBrainRuntime _brain;
        BrainData _brainData;
        AntBodyData _body;
        PlasticityData _plasticity;
        string _brainLabel = "baseline";
        bool _paused;
        bool _useChampion = true;
        bool _episodeLogged;
        ViewerRenderer _renderer;

        public SimWorldRuntime World { get { return _world; } }
        public ReplayPlayer Replay { get { return _replay; } }
        public bool Paused { get { return _paused; } }
        public string BrainLabel { get { return _brainLabel; } }
        public ViewerMode ActiveMode { get { return _replay != null ? ViewerMode.Replay : ViewerMode.Live; } }

        void Start()
        {
            _renderer = GetComponent<ViewerRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<ViewerRenderer>();

            if (Mode == ViewerMode.Auto)
            {
                Mode = File.Exists(Path.Combine(Application.streamingAssetsPath, ReplayFile + ".json"))
                    ? ViewerMode.Replay : ViewerMode.Live;
            }

            if (Mode == ViewerMode.Replay) LoadReplay();
            else StartLive();
        }

        void Update()
        {
            HandleInput();

            if (_replay != null)
            {
                _replay.Paused = _paused;
                _replay.Advance(Time.unscaledDeltaTime, Speed);
                return;
            }

            if (_world == null || _paused) return;
            for (int i = 0; i < Mathf.Max(1, Speed); i++) _world.Step();

            if (AutoRestart && _world.Tick >= _world.Config.EpisodeTicks)
            {
                StartLive();
            }
            else if (!_episodeLogged && _world.Tick >= _world.Config.EpisodeTicks)
            {
                _episodeLogged = true;
                Debug.Log(string.Format("[AntSim] Episode complete: delivered {0}, picked up {1}.",
                    _world.FoodDelivered, _world.FoodPickedUp));
            }
        }

        void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _paused = !_paused;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow)) Speed = Mathf.Min(64, Speed * 2);
            if (Input.GetKeyDown(KeyCode.DownArrow)) Speed = Mathf.Max(1, Speed / 2);

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (_replay != null)
                {
                    _replay.Restart();
                    _paused = false;
                }
                else StartLive(true);
            }

            if (Input.GetKeyDown(KeyCode.B) && _replay == null)
            {
                _useChampion = !_useChampion;
                StartLive();
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                if (_replay != null)
                {
                    Mode = ViewerMode.Live;
                    StartLive();
                }
                else
                {
                    Mode = ViewerMode.Replay;
                    LoadReplay();
                }
            }

            if (Input.GetKeyDown(KeyCode.T)) ShowTrails = !ShowTrails;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        void StartLive()
        {
            StartLive(false);
        }

        void StartLive(bool incrementSeed)
        {
            if (incrementSeed) Seed += 7;
            _replay = null;
            _episodeLogged = false;
            _paused = StartPaused;
            _brainData = null;
            _body = new AntBodyData();
            _plasticity = new PlasticityData();

            if (_useChampion)
            {
                _brainData = LoadBrainData();
                if (_brainData != null)
                {
                    _brain = new NeatBrainRuntime(_brainData);
                    if (_brainData.Body != null) _body = _brainData.Body;
                    if (_brainData.Plasticity != null) _plasticity = _brainData.Plasticity;
                    int connections = _brainData.Connections != null ? _brainData.Connections.Length : 0;
                    _brainLabel = BrainFile + " (" + connections + " conns)";
                }
                else
                {
                    _brain = new BaselineForagerBrainRuntime();
                    _brainLabel = "baseline (champion.json not found)";
                }
            }
            else
            {
                _brain = new BaselineForagerBrainRuntime();
                _brainLabel = "baseline (hand-coded)";
            }

            WorldConfigData config = new WorldConfigData();
            config.EpisodeTicks = EpisodeTicks;
            _world = new SimWorldRuntime(config, _body, _plasticity, _brain, (ulong)Math.Max(0L, Seed));
        }

        BrainData LoadBrainData()
        {
            if (string.IsNullOrEmpty(BrainFile)) return null;
            string path = Path.Combine(Application.streamingAssetsPath, BrainFile + ".json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("[AntSim] champion.json not found at " + path);
                return null;
            }

            try
            {
                return JsonUtility.FromJson<BrainData>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError("[AntSim] Could not parse champion.json: " + e.Message);
                return null;
            }
        }

        void LoadReplay()
        {
            _world = null;
            _replay = null;
            _paused = false;

            string path = Path.Combine(Application.streamingAssetsPath, ReplayFile + ".json");
            if (!File.Exists(path))
            {
                Debug.LogError("[AntSim] replay.json not found at " + path);
                return;
            }

            try
            {
                ReplayDataFile data = JsonUtility.FromJson<ReplayDataFile>(File.ReadAllText(path));
                _replay = new ReplayPlayer(data);
                if (_replay.FrameCount == 0)
                {
                    Debug.LogError("[AntSim] Replay contains no frames.");
                    _replay = null;
                    return;
                }
                Debug.Log(string.Format("[AntSim] Replay loaded: {0} frames, {1} ants, delivered {2}.",
                    _replay.FrameCount, _replay.AntCount, data.FoodDelivered));
            }
            catch (Exception e)
            {
                Debug.LogError("[AntSim] Could not parse replay.json: " + e.Message);
            }
        }

        void OnGUI()
        {
            if (_replay == null && _world == null) return;

            string mode = _replay != null ? "REPLAY" : "LIVE";
            string state = _paused ? "PAUSED" : "RUNNING";
            string stats;
            if (_replay != null)
            {
                stats = string.Format("AntSim  |  {0}  |  tick {1}  |  delivered {2}  |  alive {3}  |  x{4}  |  {5}",
                    mode, _replay.Tick, _replay.Delivered, _replay.AliveCount, Speed, state);
            }
            else
            {
                int alive = 0;
                float food = 0f;
                for (int i = 0; i < _world.Ants.Count; i++) if (_world.Ants[i].Alive) alive++;
                for (int i = 0; i < _world.Food.Count; i++) food += _world.Food[i].Amount;
                stats = string.Format("AntSim  |  {0}  |  {1}  |  tick {2}/{3}  |  delivered {4}  |  food left {5:F0}  |  alive {6}  |  x{7}",
                    mode, _brainLabel, _world.Tick, _world.Config.EpisodeTicks, _world.FoodDelivered, food, alive, Speed);
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(12, 10, Screen.width - 24, 24), stats);
            GUI.Label(new Rect(12, 32, Screen.width - 24, 24),
                "SPACE pause  |  UP/DOWN speed  |  R restart  |  B brain/baseline  |  M live/replay  |  T trails  |  ESC quit");
        }
    }
}