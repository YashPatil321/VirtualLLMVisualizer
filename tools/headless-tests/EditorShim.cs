// Minimal UnityEditor + extra UnityEngine surface, only so the editor scripts compile.
using System;
using System.Collections.Generic;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MenuItemAttribute : Attribute { public MenuItemAttribute(string path) { } }

    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => null;
        public static void Refresh() { }
        public static void SaveAssets() { }
        public static void CreateAsset(UnityEngine.Object o, string path) { }
        public static bool IsValidFolder(string path) => true;
        public static string CreateFolder(string parent, string name) => "";
    }

    public static class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object o) { }
    }

    public enum InteractionMode { AutomatedAction, UserAction }

    public static class PrefabUtility
    {
        public static UnityEngine.Object InstantiatePrefab(UnityEngine.Object o) => new UnityEngine.GameObject();
        public static UnityEngine.GameObject SaveAsPrefabAssetAndConnect(UnityEngine.GameObject go, string path, InteractionMode mode) => go;
    }

    public enum ModelImporterAnimationType { None, Legacy, Generic, Human }

    public class AssetImporter : UnityEngine.Object { }

    public class ModelImporter : AssetImporter
    {
        public bool generateSecondaryUV, importCameras, importLights, importAnimation, isReadable, preserveHierarchy;
        public ModelImporterAnimationType animationType;
    }

    public class AssetPostprocessor
    {
        public string assetPath = "";
        public AssetImporter assetImporter;
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public enum OpenSceneMode { Single, Additive, AdditiveWithoutLoading }
    public struct Scene { }

    public static class EditorSceneManager
    {
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() => true;
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => new Scene();
        public static Scene GetActiveScene() => new Scene();
        public static void MarkSceneDirty(Scene s) { }
        public static bool SaveScene(Scene s, string path) => true;
        public static Scene OpenScene(string path, OpenSceneMode mode) => new Scene();
    }
}
