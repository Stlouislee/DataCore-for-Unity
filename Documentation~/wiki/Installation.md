# Installation Guide

## System Requirements

- **Unity Version**: 2020.3 or higher.
- **.NET Runtime**: .NET Standard 2.1 or .NET Core 3.1+ (via Unity's .NET 4.x or .NET Standard profiles).
- **Platform Support**: Windows, macOS, Linux (Standalone).

## Installation Methods

### Option 1: Unity Package Manager (Recommended)

#### Using Git URL
1. Open your Unity Editor.
2. Navigate to **Window → Package Manager**.
3. Click the **+** button in the top left.
4. Select **Add package from git URL...**.
5. Paste the following URL:
   ```
   https://github.com/Stlouislee/DataCore-for-Unity.git
   ```
6. Click **Add**.

#### Using manifest.json
1. Open the `Packages/manifest.json` file in your project directory.
2. Add the dependency line:
   ```json
   {
     "dependencies": {
       "com.aroaro.datacore": "https://github.com/Stlouislee/DataCore-for-Unity.git"
     }
   }
   ```
3. Save the file. Unity will automatically pull the package.

### Option 2: Manual Installation

1. Download the latest release from the [DataCore-for-Unity Releases](https://github.com/Stlouislee/DataCore-for-Unity/releases).
2. Extract the content into your project's `Packages/` folder.
3. Rename the folder to `com.aroaro.datacore`.
4. Restart Unity if it doesn't automatically detect the new package.

## Dependencies

DataCore ships with the following pre-compiled managed DLLs. You do not need to install these separately:

### Core Data & Math
- **NumSharp.Core.dll**: High-performance N-dimensional array processing.
- **Apache.Arrow.dll**: Industry-standard columnar memory format.
- **LiteDB.dll**: Serverless embedded document database.
- **Microsoft.Data.Analysis.dll**: DataFrame support for Unity.

## Verifying the Installation

After installation, verify the setup with these steps:

### 1. Check Package Manager
Ensure "AroAro DataCore" appears in the list with no error icons.

### 2. Create a Manager
1. In your scene, create an empty GameObject.
2. Add the `DataCoreEditorComponent`.
3. If the custom Inspector (with "Datasets" and "Import CSV" sections) appears, the installation is correct.

### 3. Run Self-Tests
1. Open the Unity Test Runner (**Window → General → Test Runner**).
2. Run the tests under the **Playmode** and **Editmode** tabs for AroAro.DataCore.

## Troubleshooting

### Git URL Timeout
**Solution**: Ensure you have Git installed and configured on your system PATH. Try using the SSH URL (`git@github.com:Stlouislee/DataCore-for-Unity.git`) if HTTPS fails.

### Missing Dependencies (Compiler Errors)
**Solution**: 
- Ensure your Project Settings use **Api Compatibility Level: .NET Standard 2.1** or **.NET 4.x**.
- Delete the `Library` folder in your project and let Unity re-import everything.

### LiteDB File Locks
**Solution**: Ensure no other process (like an external SQLite/LiteDB viewer) is accessing the `.db` file while Unity is running.

## Updating

- **UPM**: Click the **Update** button in the Package Manager.
- **Manual**: Delete the old `com.aroaro.datacore` folder in `Packages/` and replace it with the new version.
