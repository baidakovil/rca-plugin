there is some problem with core of hot-reload system. this system is described in the

look at screenshot for problem screenshout.

1. When i run revit, i see "Rca.Loader.dll: Loaded - Current - a (hash: aedccd)", where "a" is some folder name. but actually folder name is a timestamp. check and fix it
2. when i change some code in my addin and recompile it, i see "Rca.Runtime.dll: Loaded - OUTDATED - 20250926_183655 (hash: n/a)". And click "Reload Runtime" button. I see "Runtime reloaded successfully". Then I expect to see "Rca.Runtime.dll: Loaded - Current - timestamp (hash: somehash)". Please fix it
3. Even with "<HotReloadNotify Condition="'$(HotReloadNotify)' == ''">true</HotReloadNotify>" in my csproj, i see "Last MSBuild signal: empty" in the UI. Please fix it

4. Somehow i see "Rca.Runtime.dll: Loaded - OUTDATED - 20250926_183655 (hash: n/a)", that means that hot-reloaded system able to read folder, but not able to read hash AssemblyAttribute. Please fix it


First, make a plan how to fix it. Describe which files should be changed and how. If there is an options, describe pros and cons of each option. After that, implement the fix step by step. After each step, show me the changed code and explain what you changed and why. Wait for my confirmation before proceeding to the next step.
