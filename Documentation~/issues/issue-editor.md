# Editor Tools — Design Issues

---

## 1. Silent Exception Swallowing in DrawDatasetsSection

**File:** DataCoreEditor.cs, lines ~68-78
**Severity:** 🟠 High

```csharp
catch
{
    // Ignore transient errors or disposed object exceptions during UI draw
}
```

The bare `catch` block silently swallows **all** exceptions when accessing the store. This hides real bugs — null references, type errors, threading issues — and makes debugging impossible. At minimum, log to `Debug.LogWarning` so issues are visible in the console. Better: catch specific exception types (`ObjectDisposedException`, `InvalidOperationException`) and let unexpected exceptions propagate.

---

## 2. EditorInputDialog Misuses SaveFilePanel

**File:** EditorInputDialog.cs, lines 6-14
**Severity:** 🟠 High

```csharp
public static string Show(string title, string message, string defaultValue = "")
{
    return EditorUtility.SaveFilePanel(title, "", defaultValue, "");
}
```

Both `Show()` and `ShowInputDialog()` call `SaveFilePanel` — a **file save** dialog, not a text input dialog. The `message` parameter is ignored entirely. In `DataCoreEditorTests.ImportCsvFromMenu()`, this is used to get a "dataset name" from the user, but it actually opens a file save picker. This produces unexpected UX and likely returns file paths instead of plain names. The class name and method signatures are deceptive.

---

## 3. CSV Delimiter Parsing Produces Null Character

**File:** DataCoreEditor.cs, DrawCsvImportSection method
**Severity:** 🟠 High

```csharp
csvDelimiter = EditorGUILayout.TextField(csvDelimiter.ToString(), GUILayout.Width(30)).FirstOrDefault();
if (csvDelimiter == '\0') csvDelimiter = ',';
```

`FirstOrDefault()` on a string returns `'\0'` when the string is empty. The null-character check handles this, but the logic is fragile — if a user types a multi-character delimiter (e.g., `||`), only the first character is used silently. Also, `TextField` returns an empty string when the user clears the field, which immediately resets to `,` — this may be surprising if the user intended to set an empty delimiter.

---

## 4. Menu CSV Import Hardcodes Options

**File:** DataCoreEditorTests.cs, `ImportCsvFromMenu()` method
**Severity:** 🟡 Medium

```csharp
dataCore.ImportCsvToTabular(path, finalName, true, ',');
```

The menu-based CSV import hardcodes `hasHeader=true` and delimiter=`,`, ignoring user preferences. Unlike the Inspector-based import which exposes these options, the menu path gives no control. Users with headerless CSVs or non-comma delimiters get silently wrong results.

---

## 5. PreviewWindow Creates New Instance Every Time

**File:** DataCorePreviewWindow.cs, `ShowWindow()` method
**Severity:** 🟡 Medium

```csharp
var window = CreateInstance<DataCorePreviewWindow>();
```

Every click on "Preview" creates a new `EditorWindow` instance. Rapid clicks spawn multiple windows for the same dataset with no deduplication. Should either reuse an existing window for the same dataset or use `GetWindow<T>()` for singleton behavior.

---

## 6. SessionWindow Uses FindFirstObjectByType in OnEnable

**File:** SessionWindow.cs, `OnEnable()` method
**Severity:** 🟡 Medium

```csharp
component = FindFirstObjectByType<DataCoreEditorComponent>();
```

`FindFirstObjectByType` is called in `OnEnable()` — a scene search on every window enable. If the component is added after the window opens, the window shows a warning until manually refreshed. No `OnSelectionChange` or scene-change callback to auto-detect when a component becomes available. The component reference is also never updated if the original is destroyed.

---

## 7. SessionWindow.Update() Causes Continuous Repaint

**File:** SessionWindow.cs, `Update()` method
**Severity:** 🟡 Medium

```csharp
private void Update()
{
    if (Time.frameCount % 60 == 0)
    {
        Repaint();
    }
}
```

Forces a repaint every ~1 second regardless of whether any data changed. This wastes CPU in the editor, especially when the window is open but not being used. Should use `EditorApplication.update` with a time check, or only repaint when session data actually changes (e.g., via events/callbacks).

---

## 8. Undo Registration on Test Object

**File:** DataCoreEditorTests.cs, `CreateDataCoreGameObject()` method
**Severity:** 🟡 Medium

```csharp
Undo.RegisterCreatedObjectUndo(go, "Create Data Core");
```

`DataCoreSelfTest` is also added to this GameObject, but test components shouldn't be undoable production objects. More importantly, if the user undoes this action, the DataCore component and its data are destroyed — which could be surprising if the user only intended to undo a different action.

---

## 9. No Dataset Name Uniqueness Validation

**File:** DataCoreEditor.cs, `DrawCreateDatasetSection()` method
**Severity:** 🟡 Medium

Creating a tabular or graph dataset doesn't check if the name already exists. Depending on the store's behavior, this could silently overwrite an existing dataset or throw an unhandled exception. The import methods have the same issue — `csvDatasetName` / `graphmlDatasetName` aren't validated against existing names.

---

## 10. Self-Test Creates and Immediately Destroys GameObject

**File:** DataCoreEditor.cs, `RunSelfTest()` method
**Severity:** 🟡 Medium

```csharp
var go = new GameObject("DataCore Test Runner");
var test = go.AddComponent<DataCoreSelfTest>();
test.RunTests();
DestroyImmediate(go);
```

Same pattern in `DataCoreEditorTests.RunSelfTest()`. If `RunTests()` is async or triggers callbacks, the immediate destruction could corrupt state. Also, `DestroyImmediate` in editor context can cause issues if other systems hold references to the test component.

---

## 11. Foldout State Lost on Inspector Recreate

**File:** DataCoreEditor.cs, all foldout dictionaries
**Severity:** 🟢 Low

```csharp
private Dictionary<string, bool> datasetFoldouts = new Dictionary<string, bool>();
```

All foldout state, scroll positions, and import field values are held in instance fields. When Unity recreates the Inspector (e.g., on domain reload, script recompilation, or deselecting/reselecting the object), all state is lost. Users must re-expand sections after every recompile. Consider using `SessionState` or `EditorPrefs` for persistence.

---

## 12. Truncated Cell Values May Mislead

**File:** DataCoreEditor.cs (`ShowTabularPreview`) and DataCorePreviewWindow.cs (`ShowTabularPreview`)
**Severity:** 🟢 Low

```csharp
var display = value.Length > 10 ? value.Substring(0, 10) + "..." : value;  // Editor
var displayValue = value.Length > 15 ? value.Substring(0, 15) + "..." : value;  // PreviewWindow
```

Truncation limits differ between the Inspector (10 chars) and PreviewWindow (15 chars). The Inspector's 10-char limit is very aggressive — column values like "Temperature" become "Temperat...". Consider using `GUILayout.ExpandWidth(true)` with a reasonable minimum instead of fixed-width truncation.

---

## 13. ShowOpenDatasetDialog and ShowCreateDatasetDialog Are Stubs

**File:** SessionWindow.cs, lines ~196-208
**Severity:** 🟢 Low

```csharp
private void ShowOpenDatasetDialog(ISession session)
{
    EditorUtility.DisplayDialog("Open Dataset", "This feature will be implemented in a future version.", "OK");
}
```

Two "Quick Actions" buttons in the Session window are unimplemented stubs. They show a "future version" dialog. These should either be implemented or hidden behind a feature flag to avoid confusing users.

---

## 14. Shared previewScrollPosition Between Datasets

**File:** DataCoreEditor.cs, field `previewScrollPosition`
**Severity:** 🟢 Low

All inline dataset previews share a single `previewScrollPosition`. When scrolling one dataset's preview, then expanding another, the scroll position carries over — the second dataset starts scrolled to wherever the first one left off. Each preview should have its own scroll state (keyed by dataset name).

---

## 15. GraphML Import Section Missing Delimiter/Options Compared to CSV

**File:** DataCoreEditor.cs, `DrawGraphMLImportSection()`
**Severity:** 🟢 Low

The GraphML import section only has file path and dataset name. While GraphML is XML-based and doesn't need a delimiter, there's no option for encoding selection or namespace handling. Minor, but worth noting for consistency and future extensibility.
