# Troubleshooting

If you encounter issues while using DataCore, please check the common solutions below.

## 1. Common Errors & Solutions

### Error: `Collection was modified; enumeration operation may not execute`
- **Cause**: This occurs when iterating over `store.Names` while a dataset is being auto-loaded or metadata is being updated.
- **Solution**: 
  - When iterating over dataset names manually, always create a copy first: `var names = new List<string>(store.Names);`.
  - Ensure you are using the latest version of the package, as many of these scenarios are handled internally in the core API.

### Error: `Database file is locked`
- **Cause**: The LiteDB database file is being accessed by another process or another instance of Unity.
- **Solution**: 
  - Close any external database viewers (like LiteDB Studio) that might have the file open.
  - Ensure you are not instantiating multiple `DataCoreStore` objects pointing to the same file. Use `DataCoreEditorComponent.Instance` for a shared singleton.

### Error: CSV data appears incorrect (Garbage characters/Misalignment)
- **Cause**: Usually due to an incorrect delimiter setting or encoding mismatch (DataCore expects UTF-8).
- **Solution**: 
  - Verify the `Delimiter` setting in the Inspector matches your CSV file.
  - Check if your CSV contains complex fields (like quoted multiline strings) that might require standard parser settings instead of fast/manual parsing.

---

## 2. Editor-Specific Issues

### Issue: Inspector buttons are unresponsive
- **Check**: 
  - Ensure the `DataCoreEditorComponent` is correctly attached to an active GameObject in your scene.
  - Check the Unity Console for any "GUI Error" or "NullReferenceException" that might be breaking the Inspector's repaint loop.

### Issue: Dataset loaded but "Preview" is empty
- **Check**: 
  - Is the dataset in a "Not Loaded" state (Lazy Loading enabled)? Click "Preview" or "Load Now" to populate the data.
  - Check if the dataset actually contains rows/nodes. An empty dataset will show empty headers.

---

## 3. Reporting New Issues

If the steps above don't resolve your problem, please submit an issue on [GitHub Issues](https://github.com/Stlouislee/DataCore-for-Unity/issues) with the following information:

1. **Unity Version**: (e.g., 2022.3.x LSP)
2. **Platform**: (Windows, macOS, Linux, etc.)
3. **DataCore Version**: (Found in `package.json`)
4. **Reproduction Steps**: A clear description of how to trigger the issue.
5. **Console Logs**: Copy and paste the full stack trace of any error messages.
