# Configuration Files Refactoring

**Date:** 2024-10-05  
**Impact:** Low (internal reorganization, no functional changes)

## Summary

Moved `Rca.addin` manifest file from inline XML generation in MSBuild to a static file in `Resources` folder, aligning with the pattern established for `settings.json`.

## Changes

### Before
- `Rca.addin` content was hardcoded in [Common.targets](../build/Common.targets) as CDATA XML
- Generated dynamically during build process
- Difficult to edit and track changes in version control

### After  
- `Rca.addin` is now a standalone file in [Resources/Rca.addin](../src/Rca.Loader/Resources/Rca.addin)
- Copied to output directory as Content item
- Deployed by simplified MSBuild target that just copies the file
- Consistent with `settings.json` placement and deployment pattern

## Benefits

1. **Consistency**: All configuration files (`.addin`, `settings.json`) now in same location
2. **Version Control**: Addin manifest changes are now clearly visible in Git diffs
3. **Maintainability**: Easier to edit - just modify the XML file directly
4. **Simplicity**: MSBuild targets are simpler (copy vs generate)
5. **Transparency**: Developers can see exact addin configuration without parsing MSBuild files

## File Structure

```
src/Rca.Loader/Resources/
├── Rca.addin              # Revit addin manifest (NEW)
├── settings.json          # Plugin settings
├── OpenAssistant16.png    # Button icons
├── OpenAssistant32.png
├── ReloadRuntime16.png
└── ReloadRuntime32.png
```

## Deployment Flow

1. **Build time**: 
   - `Rca.addin` and `settings.json` copied to `bin/Debug/net8.0-windows/`
   
2. **Post-build**:
   - `GenerateRcaAddinFile` target → copies `Rca.addin` to Revit addins folder
   - `DeploySettingsJson` target → copies `settings.json` (only if doesn't exist)

## References

- [Common.targets](../build/Common.targets) - Simplified `GenerateRcaAddinFile` target
- [Rca.Loader.csproj](../src/Rca.Loader/Rca.Loader.csproj) - Added Content item for `Rca.addin`
- [Settings-System.md](Settings-System.md) - Updated build integration documentation
