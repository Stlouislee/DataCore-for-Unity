# DataCore Dataset Preview Functionality

## Overview
The DataCore package now includes comprehensive dataset preview functionality that allows you to visualize your tabular and graph datasets directly in the Unity Editor.

## Features

### Tabular Data Preview
- **Column Information**: View all column names and structure
- **Data Preview**: See the first N rows of data (configurable)
- **Smart Data Display**: Automatically detects numeric and string columns
- **Scrollable View**: Handle large datasets with scrollable preview windows

### Graph Data Preview
- **Node Preview**: View node IDs and properties
- **Edge Preview**: See connections between nodes with properties
- **Limited Display**: Configurable limits to prevent performance issues
- **Property Display**: Shows key-value properties for nodes and edges

## Usage

### Accessing Preview
1. Add a `DataCoreEditorComponent` to your scene
2. Select the component in the Inspector
3. Create or load datasets
4. Expand dataset details and click "Data Preview" or "Graph Preview"

### Preview Settings
You can configure preview limits in the Inspector:
- **Max Preview Rows**: Controls how many rows to show for tabular data (5-50)
- **Max Preview Nodes**: Controls how many nodes/edges to show for graph data (10-100)

### Testing Preview
Use the built-in test window:
1. Go to `Tools/DataCore/Preview Test`
2. Create sample datasets
3. Open the DataCore Editor Component to see previews

## Performance Considerations

The preview system is designed to be efficient:
- **Lazy Loading**: Only loads data when preview is expanded
- **Limited Display**: Shows only a subset of data to prevent performance issues
- **Smart Caching**: Reuses loaded data when possible

## Example Usage

```csharp
// Create sample tabular data
var component = FindObjectOfType<DataCoreEditorComponent>();
component.CreateTabularDataset("MyData");
var tabular = component.GetStore().Get<Tabular.TabularData>("MyData");

// Add data
tabular.AddColumn("ID", new double[] {1, 2, 3});
tabular.AddColumn("Name", new string[] {"Alice", "Bob", "Charlie"});

// Preview will automatically show this data in the editor
```

## Troubleshooting

**Preview not showing data?**
- Ensure the dataset is loaded (click "Load Now" if needed)
- Check that the dataset contains actual data
- Verify the preview settings are appropriate

**Performance issues?**
- Reduce preview limits in the settings
- Use lazy loading for large datasets
- Consider splitting very large datasets

## Best Practices

1. **Use appropriate preview limits** for your dataset sizes
2. **Enable lazy loading** for large datasets to improve editor performance
3. **Save datasets regularly** to persist your data
4. **Use the test window** to verify preview functionality before working with real data