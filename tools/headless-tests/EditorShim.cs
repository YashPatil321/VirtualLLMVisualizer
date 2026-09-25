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
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public struct Scene { }

    public static class EditorSceneManager
    {
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() => true;
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => new Scene();
        public static Scene GetActiveScene() => new Scene();
        public static void MarkSceneDirty(Scene s) { }
        public static bool SaveScene(Scene s, string path) => true;
    }
}
