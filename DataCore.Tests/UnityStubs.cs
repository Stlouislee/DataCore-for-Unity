// Minimal Unity engine stubs for compiling Runtime source outside Unity.
// These are NOT functional implementations — just enough to satisfy the compiler.

using System;

namespace UnityEngine
{
    // ──────────────────────────────────────────────────────────────
    // Base classes
    // ──────────────────────────────────────────────────────────────

    public partial class Object
    {
        public string name;
        public static void Destroy(Object obj) { }
        public static void Destroy(Object obj, float t) { }
        public static void DestroyImmediate(Object obj) { }
        public static void DestroyImmediate(Object obj, bool allowDestroyingAssets) { }
        public static T Instantiate<T>(T original) where T : Object => default;
        public static T Instantiate<T>(T original, Transform parent) where T : Object => default;
        public override string ToString() => name ?? GetType().Name;
        public static implicit operator bool(Object o) => o != null;
    }

    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform;
    }

    public class Behaviour : Component
    {
        public bool enabled;
    }

    public class MonoBehaviour : Behaviour { }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => default;
    }

    // ──────────────────────────────────────────────────────────────
    // Core types
    // ──────────────────────────────────────────────────────────────

    public class GameObject : Object
    {
        public Transform transform;
        public T GetComponent<T>() => default;
        public T AddComponent<T>() where T : Component => default;
    }

    public class Transform : Component
    {
        public Vector3 position;
        public Vector3 localPosition;
        public Quaternion rotation;
        public Quaternion localRotation;
        public Vector3 localScale = Vector3.one;
        public Transform parent;
        public int childCount => 0;
        public Transform GetChild(int index) => null;
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 one => new(1, 1, 1);
        public static Vector3 up => new(0, 1, 0);
        public static Vector3 forward => new(0, 0, 1);
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new(0, 0);
        public static Vector2 one => new(1, 1);
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public static Quaternion identity => new() { w = 1 };
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) : this(r, g, b, 1f) { }
        public static Color white => new(1, 1, 1, 1);
        public static Color black => new(0, 0, 0, 1);
        public static Color red => new(1, 0, 0, 1);
        public static Color green => new(0, 1, 0, 1);
        public static Color blue => new(0, 0, 1, 1);
        public static Color yellow => new(1, 1, 0, 1);
        public static Color cyan => new(0, 1, 1, 1);
        public static Color magenta => new(1, 0, 1, 1);
        public static Color gray => new(0.5f, 0.5f, 0.5f, 1);
        public static Color clear => new(0, 0, 0, 0);
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
    }

    public struct Bounds
    {
        public Vector3 center;
        public Vector3 size;
    }

    // ──────────────────────────────────────────────────────────────
    // TextAsset (used by CaliforniaHousingDataset.cs)
    // ──────────────────────────────────────────────────────────────

    public class TextAsset : Object
    {
        public string text;
        public byte[] bytes;
    }

    // ──────────────────────────────────────────────────────────────
    // Debug
    // ──────────────────────────────────────────────────────────────

    public static class Debug
    {
        public static void Log(object message) => Console.WriteLine($"[LOG] {message}");
        public static void LogWarning(object message) => Console.WriteLine($"[WARN] {message}");
        public static void LogError(object message) => Console.WriteLine($"[ERROR] {message}");
        public static void LogException(Exception exception) => Console.WriteLine($"[EXCEPTION] {exception}");
        public static void LogFormat(string format, params object[] args) => Console.WriteLine($"[LOG] {string.Format(format, args)}");
        public static void LogWarningFormat(string format, params object[] args) => Console.WriteLine($"[WARN] {string.Format(format, args)}");
        public static void LogErrorFormat(string format, params object[] args) => Console.WriteLine($"[ERROR] {string.Format(format, args)}");
    }

    // ──────────────────────────────────────────────────────────────
    // Application
    // ──────────────────────────────────────────────────────────────

    public static class Application
    {
        public static string dataPath => AppDomain.CurrentDomain.BaseDirectory;
        public static string persistentDataPath => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DataCore_Persistent");
        public static string temporaryCachePath => System.IO.Path.GetTempPath();
        public static string streamingAssetsPath => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StreamingAssets");
        public static string platform => "Editor";
        public static bool isEditor => true;
        public static bool isPlaying => false;
    }

    // ──────────────────────────────────────────────────────────────
    // Resources
    // ──────────────────────────────────────────────────────────────

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object => default;
        public static T[] FindObjectsOfTypeAll<T>() where T : Object => Array.Empty<T>();
    }

    // ──────────────────────────────────────────────────────────────
    // Object.Find helpers
    // ──────────────────────────────────────────────────────────────

    public static class Object_Find
    {
        // These are actually on Object, but C# won't let us add static generics there easily.
        // Using a separate static class with extension-style isn't needed;
        // the source uses UnityEngine.FindFirstObjectByType<T>() which resolves to Object.
    }

    // Patch: add static methods to Object for FindFirstObjectByType / FindObjectOfType
    // C# doesn't allow generic static methods on non-generic classes easily,
    // so we re-open Object partial-style via a workaround.
    // Actually, let's just define them directly:

    // ──────────────────────────────────────────────────────────────
    // Attributes
    // ──────────────────────────────────────────────────────────────

    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeFieldAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class HideInInspectorAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class ContextMenuAttribute : Attribute
    {
        public string menuItem;
        public ContextMenuAttribute(string itemName) { menuItem = itemName; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class AddComponentMenuAttribute : Attribute
    {
        public AddComponentMenuAttribute(string menuName) { }
        public AddComponentMenuAttribute(string menuName, int order) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class DisallowMultipleComponentAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class RequireComponentAttribute : Attribute
    {
        public RequireComponentAttribute(Type t) { }
        public RequireComponentAttribute(Type t1, Type t2) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ExecuteInEditModeAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class DefaultExecutionOrderAttribute : Attribute
    {
        public DefaultExecutionOrderAttribute(int order) { }
    }

    // ──────────────────────────────────────────────────────────────
    // Coroutine stubs (commonly used in MonoBehaviour)
    // ──────────────────────────────────────────────────────────────

    public class Coroutine : YieldInstruction { }
    public class YieldInstruction { }
    public class WaitForSeconds : YieldInstruction
    {
        public WaitForSeconds(float seconds) { }
    }

    // ──────────────────────────────────────────────────────────────
    // Gizmos (static, sometimes referenced)
    // ──────────────────────────────────────────────────────────────

    public static class Gizmos
    {
        public static Color color;
        public static void DrawWireSphere(Vector3 center, float radius) { }
        public static void DrawLine(Vector3 from, Vector3 to) { }
    }

    // ──────────────────────────────────────────────────────────────
    // Mathf
    // ──────────────────────────────────────────────────────────────

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int v) => Math.Abs(v);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp(float val, float min, float max) => Math.Clamp(val, min, max);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static float Log(float f) => (float)Math.Log(f);
        public static float Log10(float f) => (float)Math.Log10(f);
        public static float Floor(float f) => (float)Math.Floor(f);
        public static float Ceil(float f) => (float)Math.Ceiling(f);
        public static float Round(float f) => (float)Math.Round(f);
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Tan(float f) => (float)Math.Tan(f);
        public static float Asin(float f) => (float)Math.Asin(f);
        public static float Acos(float f) => (float)Math.Acos(f);
        public static float Atan(float f) => (float)Math.Atan(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp(t, 0, 1);
        public static float Infinity => float.PositiveInfinity;
        public static float NegativeInfinity => float.NegativeInfinity;
        public static float Deg2Rad => PI / 180f;
        public static float Rad2Deg => 180f / PI;
    }

    // ──────────────────────────────────────────────────────────────
    // Time (sometimes used in MonoBehaviour)
    // ──────────────────────────────────────────────────────────────

    public static class Time
    {
        public static float time => 0f;
        public static float deltaTime => 0.016f;
        public static float fixedDeltaTime => 0.02f;
        public static float realtimeSinceStartup => 0f;
    }

    // ──────────────────────────────────────────────────────────────
    // Random (Unity's Random, not System.Random)
    // ──────────────────────────────────────────────────────────────

    public static class Random
    {
        private static readonly System.Random _rng = new();
        public static float Range(float min, float max) => (float)(_rng.NextDouble() * (max - min) + min);
        public static int Range(int min, int max) => _rng.Next(min, max);
        public static float value => (float)_rng.NextDouble();
    }

    // ──────────────────────────────────────────────────────────────
    // Static Find methods on Object (generic)
    // These are called as UnityEngine.Object.FindFirstObjectByType<T>()
    // ──────────────────────────────────────────────────────────────

    public partial class Object
    {
        public static T FindFirstObjectByType<T>() where T : Object => default;
        public static T FindObjectOfType<T>() where T : Object => default;
        public static T[] FindObjectsOfType<T>() where T : Object => Array.Empty<T>();
        public static T[] FindObjectsByType<T>(FindObjectsSortMode sortMode) where T : Object => Array.Empty<T>();
    }

    public enum FindObjectsSortMode { None, InstanceID }

    // ──────────────────────────────────────────────────────────────
    // SerializeReference, Tooltip, Header, Space, Range, Min, etc.
    // ──────────────────────────────────────────────────────────────

    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeReferenceAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string header) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class SpaceAttribute : Attribute
    {
        public SpaceAttribute() { }
        public SpaceAttribute(float height) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max) { }
        public RangeAttribute(int min, int max) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class MinAttribute : Attribute
    {
        public MinAttribute(float min) { }
    }

    // ──────────────────────────────────────────────────────────────
    // Handles, GUILayout, EditorGUILayout, EditorWindow, etc.
    // These are Editor-only but sometimes referenced in #if blocks.
    // We provide stubs to prevent compilation errors.
    // ──────────────────────────────────────────────────────────────
}

namespace UnityEngine
{
    // GUIStyle, GUILayout, GUIContent stubs
    public class GUIStyle
    {
        public int fontSize;
        public FontStyle fontStyle;
        public TextAnchor alignment;
        public Color normal = new();
        public Color hover = new();
        public Color active = new();
        public Color focused = new();
    }

    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }

    public class GUIContent
    {
        public string text;
        public string tooltip;
        public Texture2D image;
        public GUIContent() { }
        public GUIContent(string text) { this.text = text; }
        public GUIContent(string text, string tooltip) { this.text = text; this.tooltip = tooltip; }
        public static GUIContent none => new();
    }

    public class Texture2D : Object
    {
        public int width;
        public int height;
        public Texture2D(int width, int height) { this.width = width; this.height = height; }
        public void SetPixel(int x, int y, Color color) { }
        public void Apply() { }
        public Color[] GetPixels() => Array.Empty<Color>();
    }

    public class GUILayout
    {
        public static void Label(string text, params GUILayoutOption[] options) { }
        public static void Label(GUIContent content, params GUILayoutOption[] options) { }
        public static void Label(GUIContent content, GUIStyle style, params GUILayoutOption[] options) { }
        public static bool Button(string text, params GUILayoutOption[] options) => false;
        public static string TextField(string text, params GUILayoutOption[] options) => text;
        public static string TextField(string text, int maxLength, params GUILayoutOption[] options) => text;
        public static string TextArea(string text, params GUILayoutOption[] options) => text;
        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) => value;
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] options) { }
        public static void EndVertical() { }
        public static GUILayoutOption Width(float width) => null;
        public static GUILayoutOption Height(float height) => null;
        public static GUILayoutOption MinWidth(float minWidth) => null;
        public static GUILayoutOption MaxWidth(float maxWidth) => null;
        public static GUILayoutOption ExpandWidth(bool expand) => null;
        public static void Space(float pixels) { }
    }

    public class GUILayoutOption { }

    public class EditorGUILayout
    {
        public static string TextField(string label, string text, params GUILayoutOption[] options) => text;
        public static string TextField(string text, params GUILayoutOption[] options) => text;
        public static string TextArea(string text, params GUILayoutOption[] options) => text;
        public static bool Foldout(bool foldout, string text) => foldout;
        public static bool Foldout(bool foldout, string text, bool toggleOnLabelClick) => foldout;
        public static bool Toggle(bool value, string label) => value;
        public static bool ToggleLeft(string label, bool value) => value;
        public static int Popup(int selectedIndex, string[] displayedOptions, params GUILayoutOption[] options) => selectedIndex;
        public static void LabelField(string label, params GUILayoutOption[] options) { }
        public static void LabelField(string label, GUIStyle style, params GUILayoutOption[] options) { }
        public static void LabelField(GUIContent label, params GUILayoutOption[] options) { }
        public static void LabelField(GUIContent label, GUIStyle style, params GUILayoutOption[] options) { }
        public static void Space() { }
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] options) { }
        public static void EndVertical() { }
        public static void BeginFoldoutHeaderGroup(bool foldout, string text) { }
        public static void EndFoldoutHeaderGroup() { }
        public static bool BeginToggleGroup(string label, bool toggle) => toggle;
        public static void EndToggleGroup() { }
        public static float FloatField(string label, float value, params GUILayoutOption[] options) => value;
        public static int IntField(string label, int value, params GUILayoutOption[] options) => value;
        public static Vector2 Vector2Field(string label, Vector2 value) => value;
        public static Vector3 Vector3Field(string label, Vector3 value) => value;
        public static Color ColorField(string label, Color value) => value;
        public static UnityEngine.Object ObjectField(string label, UnityEngine.Object obj, Type objType, bool allowSceneObjects) => obj;
        public static Enum EnumPopup(string label, Enum selected, params GUILayoutOption[] options) => selected;
        public static void PropertyField(SerializedProperty property, bool includeChildren = true) { }
        public static void PropertyField(SerializedProperty property, GUIContent label, bool includeChildren = true) { }
        public static void HelpBox(string message, MessageType type) { }
        public static bool InspectorTitlebar(bool foldout, UnityEngine.Object targetObj) => foldout;
    }

    public enum MessageType { None, Info, Warning, Error }

    // SerializedProperty stub (used by EditorGUILayout.PropertyField)
    public class SerializedProperty
    {
        public string name;
        public string displayName;
        public SerializedPropertyType propertyType;
        public string stringValue;
        public int intValue;
        public float floatValue;
        public bool boolValue;
    }

    public enum SerializedPropertyType
    {
        Generic, Integer, Boolean, Float, String, Color, ObjectReference,
        LayerMask, Enum, Vector2, Vector3, Vector4, Rect, ArraySize,
        Character, AnimationCurve, Bounds, Gradient, Quaternion, ExposedReference,
        FixedBufferSize, Vector2Int, Vector3Int, RectInt, BoundsInt
    }
}

// ──────────────────────────────────────────────────────────────
// UnityEditor stubs
// ──────────────────────────────────────────────────────────────

namespace UnityEditor
{
    public static class EditorUtility
    {
        public static string SaveFilePanel(string title, string directory, string defaultName, string extension) => "";
        public static string OpenFilePanel(string title, string directory, string extension) => "";
        public static string OpenFolderPanel(string title, string folder, string defaultName) => "";
        public static bool DisplayDialog(string title, string message, string ok) => true;
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => true;
        public static bool DisplayCancelableProgressBar(string title, string info, float progress) => false;
        public static void ClearProgressBar() { }
        public static void DisplayProgressBar(string title, string info, float progress) { }
        public static void SetDirty(UnityEngine.Object target) { }
    }

    public static class EditorGUILayout
    {
        public static string TextField(string label, string text, params UnityEngine.GUILayoutOption[] options) => text;
        public static bool Foldout(bool foldout, string text) => foldout;
        public static bool Foldout(bool foldout, string text, bool toggleOnLabelClick) => foldout;
    }

    public class EditorWindow : UnityEngine.ScriptableObject
    {
        public string titleContent;
        public UnityEngine.Vector2 minSize;
        public UnityEngine.Vector2 maxSize;
        public void Show() { }
        public void ShowUtility() { }
        public void Close() { }
        public void Repaint() { }
        public void Focus() { }
        public static T GetWindow<T>(bool utility = false, string title = null) where T : EditorWindow => default;
        public static T GetWindow<T>(string title) where T : EditorWindow => default;
    }

    public static class Undo
    {
        public static void RecordObject(UnityEngine.Object objectToUndo, string name) { }
        public static void DestroyObjectImmediate(UnityEngine.Object objectToUndo) { }
        public static void RegisterCreatedObjectUndo(UnityEngine.Object objectToUndo, string name) { }
    }

    public static class Handles
    {
        public static UnityEngine.Color color;
        public static void DrawWireDisc(UnityEngine.Vector3 center, UnityEngine.Vector3 normal, float radius) { }
        public static void DrawSolidDisc(UnityEngine.Vector3 center, UnityEngine.Vector3 normal, float radius) { }
        public static void DrawLine(UnityEngine.Vector3 from, UnityEngine.Vector3 to) { }
        public static void DrawAAPolyLine(float width, params UnityEngine.Vector3[] points) { }
    }

    public static class AssetDatabase
    {
        public static string GetAssetPath(UnityEngine.Object asset) => "";
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => default;
    }

    public class CustomEditorAttribute : System.Attribute
    {
        public CustomEditorAttribute(System.Type inspectedType) { }
        public CustomEditorAttribute(System.Type inspectedType, bool editorForChildClasses) { }
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class InitializeOnLoadAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class InitializeOnLoadMethodAttribute : System.Attribute { }

    public enum EditorStyles { }
}
