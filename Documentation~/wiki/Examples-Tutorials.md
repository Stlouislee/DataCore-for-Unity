# Examples & Tutorials

This page contains a series of tutorials from beginner to advanced to help you master DataCore's features.

## 1. Basic CRUD Operations (Tabular)

This tutorial shows how to add, update, delete, and query data in a tabular dataset.

```csharp
using AroAro.DataCore;
using System.Collections.Generic;

public class BasicTabularUsage
{
    public void Execute()
    {
        var store = new DataCoreStore();
        
        // 1. Create/Get dataset
        var table = store.GetOrCreateTabular("inventory");
        
        // 2. Add column data (Batch)
        table.AddStringColumn("item_id", new[] { "A01", "B02", "C03" });
        table.AddNumericColumn("count", new double[] { 50, 20, 100 });
        
        // 3. Add single row
        table.AddRow(new Dictionary<string, object> {
            { "item_id", "D04" },
            { "count", 5.0 }
        });
        
        // 4. Query (Using QueryOp)
        var lowStockIndices = table.Query()
            .Where("count", QueryOp.Lt, 20)
            .ToRowIndices();
            
        // 5. Update
        if (lowStockIndices.Count > 0)
        {
            table.UpdateRow(lowStockIndices[0], new Dictionary<string, object> { { "count", 15.0 } });
        }
        
        // Save changes to disk
        store.Checkpoint();
    }
}
```

---

## 2. Social Network Modeling (Graph)

Shows how to establish relationships between nodes and edges using a Graph dataset.

```csharp
using AroAro.DataCore;
using System.Collections.Generic;

public void SocialGraphExample()
{
    var store = new DataCoreStore();
    var graph = store.GetOrCreateGraph("my_social_net");
    
    // Add nodes
    graph.AddNode("alice", new Dictionary<string, object> { { "role", "admin" } });
    graph.AddNode("bob", new Dictionary<string, object> { { "role", "user" } });
    
    // Add relationship (edge) with properties
    graph.AddEdge("alice", "bob", new Dictionary<string, object> { { "type", "friends" } });
    
    // Get neighbors
    var aliceFriends = graph.GetNeighbors("alice");
    
    store.Checkpoint();
}
```

---

## 3. CSV Data Bulk Import

If you have existing CSV data, you can easily import it.

```csharp
public void CsvImport()
{
    // Access the singleton component
    var component = DataCoreEditorComponent.Instance;
    
    // Import CSV file
    // Signature: (path, datasetName, hasHeader, delimiter)
    component.ImportCsvToTabular("Assets/Data/items.csv", "InventoryData", true, ',');
    
    UnityEngine.Debug.Log("CSV Imported successfully!");
}
```

---

## 4. Analysis Workflow (Advanced)

Use `Session` to manage complex data processing pipelines without affecting the stability of the main store.

1. **Create Session**: Use `var session = store.SessionManager.CreateSession("Analysis")`.
2. **Load Data Copy**: Use `session.OpenDataset("SourceData")` to load a copy.
3. **Data Processing**: Perform cleaning, transformation, and analysis (optionally using DataFrames).
4. **Persist Results**: Save the final results back to the global store using `session.PersistDataset("ResultData")`.

---

## 5. Performance Optimization: Lazy Loading

For large files containing tens of thousands of rows, lazy loading is recommended to save memory and editor startup time.

In the Unity Inspector:
1. Locate the `DataCoreEditorComponent`.
2. Ensure `Lazy Loading` is enabled (it usually is by default for large datasets).
3. The dataset will appear as "Not Loaded" in the list.
4. Data is only loaded into memory when you click the "Preview" button or access it via code (`store.GetTabular("name")`).

---

For more samples, please check the `Runtime/Examples` folder in the project. If you have any questions, feel free to open an issue on [GitHub](https://github.com/Stlouislee/DataCore-for-Unity/issues).
