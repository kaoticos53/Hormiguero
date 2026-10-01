using System;
using UnityEngine;

namespace AntSimViewer
{
    /// <summary>
    /// Lightweight built-in-pipeline renderer. It deliberately uses GL primitives instead of
    /// prefabs/shaders so the project works immediately after opening the generated scene.
    /// </summary>
    [RequireComponent(typeof(AntSimViewerController))]
    public sealed class ViewerRenderer : MonoBehaviour
    {
        public Color Background = new Color(0.025f, 0.035f, 0.05f, 1f);
        public Color FoodTrailColor = new Color(0.15f, 0.95f, 0.25f, 0.72f);
        public Color HomeTrailColor = new Color(0.15f, 0.45f, 1f, 0.68f);
        public Color NestColor = new Color(0.95f, 0.75f, 0.18f, 1f);
        public Color FoodColor = new Color(1f, 0.52f, 0.12f, 1f);
        public Color AntColor = new Color(0.84f, 0.84f, 0.9f, 1f);
        public Color CarryingAntColor = new Color(1f, 0.55f, 0.08f, 1f);

        AntSimViewerController _controller;
        Material _material;
        Camera _camera;
        float _lastWidth;
        float _lastHeight;

        void Awake()
        {
            _controller = GetComponent<AntSimViewerController>();
            _material = CreateMaterial();
            EnsureCamera();
        }

        void LateUpdate()
        {
            EnsureCamera();
            if (_camera == null) return;
            float width = CurrentWidth();
            float height = CurrentHeight();
            if (width != _lastWidth || height != _lastHeight)
            {
                _camera.transform.position = new Vector3(width * 0.5f, height * 0.5f, -10f);
                _camera.orthographicSize = height * 0.5f;
                _camera.aspect = width / Mathf.Max(1f, height);
                _lastWidth = width;
                _lastHeight = height;
            }
        }

        void EnsureCamera()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null)
            {
                GameObject cameraObject = new GameObject("AntSim Camera");
                _camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Background;
        }

        void OnRenderObject()
        {
            if (_controller == null || _material == null) return;
            _material.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
            DrawScene();
            GL.PopMatrix();
        }

        void DrawScene()
        {
            float width = CurrentWidth();
            float height = CurrentHeight();
            float sx = Screen.width / Mathf.Max(1f, width);
            float sy = Screen.height / Mathf.Max(1f, height);

            if (_controller.ShowTrails && _controller.World != null)
            {
                DrawField(_controller.World.FoodTrailField, FoodTrailColor, width, height, sx, sy);
                DrawField(_controller.World.HomeTrailField, HomeTrailColor, width, height, sx, sy);
            }

            if (_controller.ActiveMode == ViewerMode.Replay && _controller.Replay != null)
                DrawReplay(_controller.Replay, width, height, sx, sy);
            else if (_controller.World != null)
                DrawLive(_controller.World, width, height, sx, sy);
        }

        void DrawLive(SimWorldRuntime world, float width, float height, float sx, float sy)
        {
            DrawNest(world.Nest.X, world.Nest.Y, world.Nest.Radius, sx, sy);
            for (int i = 0; i < world.Food.Count; i++)
            {
                FoodRuntime food = world.Food[i];
                DrawFood(food.X, food.Y, food.Amount, world.Config.FoodPerSource, sx, sy);
            }
            for (int i = 0; i < world.Ants.Count; i++)
            {
                AntRuntime ant = world.Ants[i];
                if (ant.Alive) DrawAnt(ant.X, ant.Y, ant.Heading, ant.FoodCarried > 0f, sx, sy);
            }
        }

        void DrawReplay(ReplayPlayer replay, float width, float height, float sx, float sy)
        {
            ReplayDataFile data = replay.Data;
            if (data.Config != null)
                DrawNest(data.Config.Width * 0.5f, data.Config.Height * 0.5f, data.Config.NestRadius, sx, sy);

            if (data.FoodSources != null)
            {
                float max = data.Config != null ? data.Config.FoodPerSource : 60f;
                for (int i = 0; i < data.FoodSources.Length; i++)
                {
                    ReplayFoodData food = data.FoodSources[i];
                    float amount = replay.GetFoodRemaining(i);
                    if (amount < 0f) amount = food.InitialAmount > 0f ? food.InitialAmount : max;
                    DrawFood(food.X, food.Y, amount, max, sx, sy);
                }
            }

            for (int i = 0; i < replay.AntCount; i++)
            {
                DrawAnt(replay.GetX(i), replay.GetY(i), replay.GetHeading(i), replay.IsCarrying(i), sx, sy);
            }
        }

        void DrawField(PheromoneFieldRuntime field, Color color, float width, float height, float sx, float sy)
        {
            float[] values = field.Values();
            float cellWidth = width / field.Cols;
            float cellHeight = height / field.Rows;
            GL.Begin(GL.QUADS);
            for (int row = 0; row < field.Rows; row++)
            {
                for (int col = 0; col < field.Cols; col++)
                {
                    float value = Mathf.Clamp01(values[row * field.Cols + col] / Mathf.Max(0.0001f, field.MaxLevel));
                    // Square-root contrast exposes faint trails while preserving hot corridors.
                    float alpha = Mathf.Sqrt(value) * color.a;
                    if (alpha < 0.01f) continue;
                    GL.Color(new Color(color.r, color.g, color.b, alpha));
                    float x = col * cellWidth * sx;
                    float y = row * cellHeight * sy;
                    GL.Vertex3(x, y, 0f);
                    GL.Vertex3(x + cellWidth * sx + 1f, y, 0f);
                    GL.Vertex3(x + cellWidth * sx + 1f, y + cellHeight * sy + 1f, 0f);
                    GL.Vertex3(x, y + cellHeight * sy + 1f, 0f);
                }
            }
            GL.End();
        }

        void DrawNest(float x, float y, float radius, float sx, float sy)
        {
            DrawCircle(x * sx, y * sy, radius * Mathf.Min(sx, sy), NestColor, 24, false);
            DrawCircle(x * sx, y * sy, Mathf.Max(2f, radius * 0.18f * Mathf.Min(sx, sy)), NestColor, 12, true);
        }

        void DrawFood(float x, float y, float amount, float maximum, float sx, float sy)
        {
            if (amount <= 0f) return;
            float ratio = Mathf.Clamp01(amount / Mathf.Max(0.001f, maximum));
            float radius = 4f + 11f * Mathf.Sqrt(ratio);
            DrawCircle(x * sx, y * sy, radius * Mathf.Min(sx, sy), FoodColor, 16, true);
        }

        void DrawAnt(float x, float y, float heading, bool carrying, float sx, float sy)
        {
            float scale = Mathf.Min(sx, sy);
            float length = 7f * scale;
            float width = 3.5f * scale;
            Vector2 p = new Vector2(x * sx, y * sy);
            Vector2 direction = new Vector2(Mathf.Cos(heading), -Mathf.Sin(heading));
            Vector2 side = new Vector2(-direction.y, direction.x);
            GL.Begin(GL.TRIANGLES);
            GL.Color(carrying ? CarryingAntColor : AntColor);
            GL.Vertex3(p.x + direction.x * length, p.y + direction.y * length, 0f);
            GL.Vertex3(p.x - direction.x * length * 0.65f + side.x * width, p.y - direction.y * length * 0.65f + side.y * width, 0f);
            GL.Vertex3(p.x - direction.x * length * 0.65f - side.x * width, p.y - direction.y * length * 0.65f - side.y * width, 0f);
            GL.End();
        }

        void DrawCircle(float x, float y, float radius, Color color, int segments, bool filled)
        {
            GL.Begin(filled ? GL.TRIANGLE_FAN : GL.LINE_STRIP);
            GL.Color(color);
            if (filled) GL.Vertex3(x, y, 0f);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                GL.Vertex3(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius, 0f);
            }
            GL.End();
        }

        float CurrentWidth()
        {
            if (_controller.World != null) return _controller.World.Config.Width;
            if (_controller.Replay != null && _controller.Replay.Data.Config != null) return _controller.Replay.Data.Config.Width;
            return 800f;
        }

        float CurrentHeight()
        {
            if (_controller.World != null) return _controller.World.Config.Height;
            if (_controller.Replay != null && _controller.Replay.Data.Config != null) return _controller.Replay.Data.Config.Height;
            return 800f;
        }

        static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return null;
            Material material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            material.SetInt("_ZWrite", 0);
            return material;
        }
    }
}
