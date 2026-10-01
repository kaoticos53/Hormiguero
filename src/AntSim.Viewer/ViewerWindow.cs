using System.Diagnostics;
using AntSim.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace AntSim.Viewer;

/// <summary>
/// Renders a live SimWorld with OpenGL: pheromone trails as a colorized heatmap texture,
/// food piles and the nest as circles, ants as oriented triangles. The simulation core is
/// stepped from UpdateFrame; rendering is stateless over the world's public state.
/// </summary>
public sealed class ViewerWindow : GameWindow
{
    private readonly WorldConfig _config;
    private readonly SpeciesDefinition _species;
    private readonly IAntBrainFactory _brainFactory;
    private readonly string _brainLabel;
    private readonly ulong _baseSeed;
    private readonly int _smokeFrames;

    private SimWorld _world;
    private bool _paused;
    private int _speed = 1;          // simulation steps per rendered frame
    private bool _showTrails = true;
    private ulong _restartCount;

    // GL resources.
    private int _solidProgram, _trailProgram;
    private int _solidVao, _solidVbo;
    private int _trailVao, _trailVbo, _trailTexture;
    private int _solidProjLoc;

    // Reused CPU-side buffers (no per-frame allocations).
    private readonly float[] _verts = new float[1 << 16]; // pos(2) + color(3) per vertex
    private int _vertCount;
    private float[] _trailTexels = [];    // RG pairs: food, home

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastTitleUpdateMs;
    private int _framesRendered;
    private int _fpsFrames;
    private int _fps;
    private long _fpsWindowStartMs;

    private const string SolidVertexSrc = """
        #version 330 core
        layout(location=0) in vec2 aPos;
        layout(location=1) in vec3 aColor;
        uniform mat4 uProj;
        out vec3 vColor;
        void main() { gl_Position = uProj * vec4(aPos, 0.0, 1.0); vColor = aColor; }
        """;

    private const string SolidFragmentSrc = """
        #version 330 core
        in vec3 vColor;
        out vec4 oColor;
        void main() { oColor = vec4(vColor, 1.0); }
        """;

    private const string TrailVertexSrc = """
        #version 330 core
        layout(location=0) in vec2 aPos;
        layout(location=1) in vec2 aUv;
        out vec2 vUv;
        void main() { gl_Position = vec4(aPos, 0.0, 1.0); vUv = aUv; }
        """;

    private const string TrailFragmentSrc = """
        #version 330 core
        in vec2 vUv;
        out vec4 oColor;
        uniform sampler2D uTex;
        void main() {
            vec2 t = texture(uTex, vUv).rg;              // r = food trail, g = home trail
            float a = max(t.r, t.g);
            if (a < 0.015) { oColor = vec4(0.0); return; }
            vec3 col = t.r * vec3(0.25, 1.0, 0.35)      // food trail  -> green
                     + t.g * vec3(0.30, 0.60, 1.0);      // home trail  -> blue
            oColor = vec4(col, min(1.0, a * 1.3) * 0.8);
        }
        """;

    public ViewerWindow(
        GameWindowSettings gws, NativeWindowSettings nws,
        WorldConfig config, SpeciesDefinition species, IAntBrainFactory brainFactory,
        string brainLabel, ulong seed, SimWorld initialWorld, bool startPaused, int smokeFrames)
        : base(gws, nws)
    {
        _config = config;
        _species = species;
        _brainFactory = brainFactory;
        _brainLabel = brainLabel;
        _baseSeed = seed;
        _smokeFrames = smokeFrames;
        _paused = startPaused;
        _world = initialWorld;
    }

    // ------------------------------------------------------------- lifecycle

    protected override void OnLoad()
    {
        base.OnLoad();
        VSync = VSyncMode.On;

        GL.ClearColor(0.055f, 0.065f, 0.085f, 1f);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _solidProgram = BuildProgram(SolidVertexSrc, SolidFragmentSrc);
        _trailProgram = BuildProgram(TrailVertexSrc, TrailFragmentSrc);
        _solidProjLoc = GL.GetUniformLocation(_solidProgram, "uProj");

        // Dynamic buffer for all solid geometry (rebuilt every frame on the CPU).
        _solidVao = GL.GenVertexArray();
        _solidVbo = GL.GenBuffer();
        GL.BindVertexArray(_solidVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _solidVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, _verts.Length * sizeof(float), nint.Zero, BufferUsageHint.DynamicDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 2 * sizeof(float));

        // Static fullscreen quad for the pheromone overlay (clip-space positions + uvs).
        float[] quad =
        [
            // pos        uv
            -1f, -1f,     0f, 0f,
             1f, -1f,     1f, 0f,
             1f,  1f,     1f, 1f,
            -1f, -1f,     0f, 0f,
             1f,  1f,     1f, 1f,
            -1f,  1f,     0f, 1f,
        ];
        _trailVao = GL.GenVertexArray();
        _trailVbo = GL.GenBuffer();
        GL.BindVertexArray(_trailVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _trailVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, quad.Length * sizeof(float), quad, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

        // RG float texture: R = food trail, G = home trail (one texel per field cell).
        _trailTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _trailTexture);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        int cols = _world.FoodTrailField.Cols, rows = _world.FoodTrailField.Rows;
        _trailTexels = new float[cols * rows * 2];
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rg32f, cols, rows, 0,
            PixelFormat.Rg, PixelType.Float, nint.Zero);

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        UpdateTitle();
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        if (!IsFocused) { /* keep simulating anyway — the colony never sleeps */ }
        if (!_paused)
        {
            for (int i = 0; i < _speed; i++)
                _world.Step();
        }

        _fpsFrames++;
        long now = _clock.ElapsedMilliseconds;
        if (now - _fpsWindowStartMs >= 500)
        {
            _fps = (int)(_fpsFrames * 1000 / Math.Max(1, now - _fpsWindowStartMs));
            _fpsFrames = 0;
            _fpsWindowStartMs = now;
        }
        if (now - _lastTitleUpdateMs >= 250)
        {
            UpdateTitle();
            _lastTitleUpdateMs = now;
        }
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        if (_showTrails)
            DrawTrails();
        DrawSolids();

        SwapBuffers();

        if (_smokeFrames > 0 && ++_framesRendered >= _smokeFrames)
            Close();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Keys.Escape:
                Close();
                break;
            case Keys.Space:
                _paused = !_paused;
                UpdateTitle();
                break;
            case Keys.Up:
                _speed = Math.Min(16, _speed * 2);
                UpdateTitle();
                break;
            case Keys.Down:
                _speed = Math.Max(1, _speed / 2);
                UpdateTitle();
                break;
            case Keys.R:
                _restartCount++;
                _world = SimWorld.CreateSeeded(_config, _species, _brainFactory, seed: _baseSeed + _restartCount * 7);
                UpdateTitle();
                break;
            case Keys.F:
                _showTrails = !_showTrails;
                break;
        }
    }

    // -------------------------------------------------------------- drawing

    private void DrawTrails()
    {
        var food = _world.FoodTrailField;
        var home = _world.HomeTrailField;
        int n = food.Values.Length;

        for (int i = 0; i < n; i++)
        {
            _trailTexels[i * 2] = MathF.Min(1f, food.Values[i] / food.MaxLevel);
            _trailTexels[i * 2 + 1] = MathF.Min(1f, home.Values[i] / home.MaxLevel);
        }

        GL.UseProgram(_trailProgram);
        GL.BindTexture(TextureTarget.Texture2D, _trailTexture);
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, food.Cols, food.Rows,
            PixelFormat.Rg, PixelType.Float, _trailTexels);
        GL.Uniform1(GL.GetUniformLocation(_trailProgram, "uTex"), 0);
        GL.BindVertexArray(_trailVao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    private void DrawSolids()
    {
        _vertCount = 0;

        // Food piles, sized by remaining amount.
        var sources = _world.FoodSources;
        for (int i = 0; i < sources.Count; i++)
        {
            var f = sources[i];
            if (f.Amount <= 0f) continue;
            float radius = 2f + 10f * MathF.Min(1f, f.Amount / _config.FoodPerSource);
            AddCircle(f.X, f.Y, radius, 0.35f, 0.9f, 0.4f, 20);
        }

        // Nest.
        AddCircle(_world.Nest.X, _world.Nest.Y, _world.Nest.Radius, 0.85f, 0.5f, 0.25f, 36);
        AddCircle(_world.Nest.X, _world.Nest.Y, _world.Nest.Radius * 0.55f, 0.45f, 0.25f, 0.12f, 36);

        // Ants: oriented triangles; carriers glow orange.
        var ants = _world.Ants;
        for (int i = 0; i < ants.Count; i++)
        {
            var ant = ants[i];
            if (!ant.Alive) continue;

            float hx = MathF.Cos(ant.Heading), hy = MathF.Sin(ant.Heading);
            float px = -hy, py = hx;

            float tipX = ant.X + hx * 2.8f, tipY = ant.Y + hy * 2.8f;
            float b1X = ant.X - hx * 1.8f + px * 1.5f, b1Y = ant.Y - hy * 1.8f + py * 1.5f;
            float b2X = ant.X - hx * 1.8f - px * 1.5f, b2Y = ant.Y - hy * 1.8f - py * 1.5f;

            if (ant.FoodCarried > 0f)
                AddTri(tipX, tipY, b1X, b1Y, b2X, b2Y, 1.0f, 0.75f, 0.15f);
            else
                AddTri(tipX, tipY, b1X, b1Y, b2X, b2Y, 0.88f, 0.88f, 0.82f);
        }

        if (_vertCount > 0)
        {
            GL.UseProgram(_solidProgram);
            var proj = Matrix4.CreateOrthographicOffCenter(0f, _config.Width, 0f, _config.Height, -1f, 1f);
            GL.UniformMatrix4(_solidProjLoc, false, ref proj);

            GL.BindVertexArray(_solidVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _solidVbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, nint.Zero, _vertCount * sizeof(float), _verts);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _vertCount / 5);
        }
    }

    private void AddTri(float x0, float y0, float x1, float y1, float x2, float y2, float r, float g, float b)
    {
        if (_vertCount + 15 > _verts.Length) return;
        AddVertex(x0, y0, r, g, b);
        AddVertex(x1, y1, r, g, b);
        AddVertex(x2, y2, r, g, b);
    }

    private void AddCircle(float cx, float cy, float radius, float r, float g, float b, int segments)
    {
        const float Tau = MathF.PI * 2f;
        for (int i = 0; i < segments; i++)
        {
            float a0 = Tau * i / segments, a1 = Tau * (i + 1) / segments;
            if (_vertCount + 15 > _verts.Length) return;
            AddVertex(cx, cy, r, g, b);
            AddVertex(cx + MathF.Cos(a0) * radius, cy + MathF.Sin(a0) * radius, r, g, b);
            AddVertex(cx + MathF.Cos(a1) * radius, cy + MathF.Sin(a1) * radius, r, g, b);
        }
    }

    private void AddVertex(float x, float y, float r, float g, float b)
    {
        _verts[_vertCount++] = x;
        _verts[_vertCount++] = y;
        _verts[_vertCount++] = r;
        _verts[_vertCount++] = g;
        _verts[_vertCount++] = b;
    }

    // ---------------------------------------------------------------- misc

    private void UpdateTitle()
    {
        int alive = 0;
        var ants = _world.Ants;
        for (int i = 0; i < ants.Count; i++)
            if (ants[i].Alive) alive++;

        Title = $"AntSim | {_species.Id} | {_brainLabel} | tick {_world.Tick} | food {_world.FoodDelivered} | alive {alive}" +
                $" | {_fps} fps | {(_paused ? "PAUSED" : $"x{_speed}")}" +
                " | SPACE pause · UP/DOWN speed · R restart · F trails · ESC exit";
    }

    private static int BuildProgram(string vertexSrc, string fragmentSrc)
    {
        int vs = CompileShader(ShaderType.VertexShader, vertexSrc);
        int fs = CompileShader(ShaderType.FragmentShader, fragmentSrc);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vs);
        GL.AttachShader(program, fs);
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int ok);
        if (ok == 0)
            throw new InvalidOperationException($"Shader link error: {GL.GetProgramInfoLog(program)}");

        GL.DetachShader(program, vs);
        GL.DetachShader(program, fs);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);
        return program;
    }

    private static int CompileShader(ShaderType type, string src)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, src);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0)
            throw new InvalidOperationException($"{type} compile error: {GL.GetShaderInfoLog(shader)}");
        return shader;
    }
}
