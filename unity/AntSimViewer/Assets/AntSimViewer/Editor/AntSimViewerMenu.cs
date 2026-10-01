#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AntSimViewer.Editor
{
    public static class AntSimViewerMenu
    {
        [MenuItem("AntSim/Create Viewer Scene")]
        public static void CreateViewerScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("AntSim Viewer");
            root.AddComponent<AntSimViewerController>();
            root.AddComponent<ViewerRenderer>();

            string directory = "Assets/AntSimViewer/Scenes";
            if (!AssetDatabase.IsValidFolder(directory))
            {
                AssetDatabase.CreateFolder("Assets/AntSimViewer", "Scenes");
            }

            string path = directory + "/AntSimViewer.unity";
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("AntSim viewer scene created at " + path);
        }

        [MenuItem("AntSim/Open StreamingAssets Folder")]
        public static void OpenStreamingAssetsFolder()
        {
            string path = Application.dataPath + "/StreamingAssets";
            if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }
    }
}
#endif
