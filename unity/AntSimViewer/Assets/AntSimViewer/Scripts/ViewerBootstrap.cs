using UnityEngine;

namespace AntSimViewer
{
    /// <summary>
    /// Creates the viewer root at runtime when the project is opened without a prepared scene.
    /// This keeps the checked-in project usable from an empty Unity scene and avoids fragile YAML.
    /// </summary>
    public static class ViewerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateViewer()
        {
            if (Object.FindObjectOfType<AntSimViewerController>() != null) return;

            GameObject root = new GameObject("AntSim Viewer");
            root.AddComponent<AntSimViewerController>();
            root.AddComponent<ViewerRenderer>();
            Object.DontDestroyOnLoad(root);
        }
    }
}
