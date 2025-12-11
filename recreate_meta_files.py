#!/usr/bin/env python3
import os
import hashlib
from pathlib import Path

# Templates
FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FILE_META = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

SCRIPT_META = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

PLUGIN_META = """fileFormatVersion: 2
guid: {guid}
PluginImporter:
  externalObjects: {{}}
  serializedVersion: 2
  iconMap: {{}}
  executionOrder: {{}}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      : Any
    second:
      enabled: 1
      settings:
        ExcludeEditor: 0
        ExcludeLinux64: 0
        ExcludeOSXUniversal: 0
        ExcludeWin: 0
        ExcludeWin64: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

def generate_guid(path_str):
    """Generate deterministic GUID based on file path"""
    # Normalize path separators to ensure consistency across platforms
    normalized_path = path_str.replace('\\', '/')
    hash_object = hashlib.md5(normalized_path.encode())
    hex_hash = hash_object.hexdigest()
    return f"{hex_hash[:8]}-{hex_hash[8:12]}-{hex_hash[12:16]}-{hex_hash[16:20]}-{hex_hash[20:32]}"

def get_meta_content(path, relative_path):
    guid = generate_guid(str(relative_path))
    
    if path.is_dir():
        return FOLDER_META.format(guid=guid)
    
    suffix = path.suffix.lower()
    if suffix == '.cs':
        return SCRIPT_META.format(guid=guid)
    elif suffix in ['.dll', '.pdb', '.so', '.dylib']:
        return PLUGIN_META.format(guid=guid)
    else:
        return FILE_META.format(guid=guid)

def main():
    root_dir = Path("/workspaces/DataCore-for-Unity")
    exclude_dirs = ['.git', '.vs', '.vscode', '__pycache__']
    exclude_files = ['.DS_Store']
    
    print("Recreating meta files...")
    
    # First, remove all existing meta files to ensure clean slate
    print("Cleaning up existing meta files...")
    for meta_file in root_dir.rglob("*.meta"):
        try:
            meta_file.unlink()
        except Exception as e:
            print(f"Error deleting {meta_file}: {e}")

    # Walk and create new ones
    count = 0
    for root, dirs, files in os.walk(root_dir):
        # Filter directories
        dirs[:] = [d for d in dirs if d not in exclude_dirs]
        
        current_path = Path(root)
        
        # Create meta for current directory (unless it's the root)
        if current_path != root_dir:
            relative_path = current_path.relative_to(root_dir)
            meta_path = current_path.parent / (current_path.name + ".meta")
            content = get_meta_content(current_path, relative_path)
            with open(meta_path, 'w') as f:
                f.write(content)
            print(f"Created: {meta_path.name}")
            count += 1

        for file in files:
            if file in exclude_files or file.endswith('.meta') or file == os.path.basename(__file__):
                continue
                
            file_path = current_path / file
            relative_path = file_path.relative_to(root_dir)
            meta_path = file_path.with_suffix(file_path.suffix + ".meta")
            
            content = get_meta_content(file_path, relative_path)
            with open(meta_path, 'w') as f:
                f.write(content)
            print(f"Created: {meta_path.name}")
            count += 1

    print(f"Finished! Created {count} meta files.")

if __name__ == "__main__":
    main()