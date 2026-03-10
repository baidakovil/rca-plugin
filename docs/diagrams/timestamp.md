```mermaid
flowchart TD
  %% Initial Data (Blue, black text)
  style A1 fill:#cce5ff,stroke:#333,stroke-width:2px,color:#000
  style A2 fill:#cce5ff,stroke:#333,stroke-width:2px,color:#000
  style A3 fill:#cce5ff,stroke:#333,stroke-width:2px,color:#000

  %% Intermediate Data (Yellow, black text)
  style B1 fill:#fff3cd,stroke:#333,stroke-width:2px,color:#000
  style B2 fill:#fff3cd,stroke:#333,stroke-width:2px,color:#000
  style B3 fill:#fff3cd,stroke:#333,stroke-width:2px,color:#000

  %% Output Data (Green, black text)
  style C1 fill:#d4edda,stroke:#333,stroke-width:2px,color:#000

  %% Methods/Files (Gray, black text)
  style M1 fill:#e2e3e5,stroke:#333,stroke-width:2px,color:#000
  style M2 fill:#e2e3e5,stroke:#333,stroke-width:2px,color:#000
  style M3 fill:#e2e3e5,stroke:#333,stroke-width:2px,color:#000
  style M4 fill:#e2e3e5,stroke:#333,stroke-width:2px,color:#000

  %% Nodes
  A1["Input: <br/>TargetPath (timestamp file path)"] 
  A2["Input: <br/>TtlSec (TTL in seconds, default 60)"]
  A3["Input: <br/>ForceStr (force flag as string, default 'false')"]

  M1["Parse flags<br/>PowerShell: <br/>$force = ($ForceStr -eq '1') -or ($ForceStr.ToLower() -eq 'true')"]
  B1["$force (bool):<br/>Should forcibly renew timestamp?"]

  M2["Ensure directory exists<br/>PowerShell:<br/>New-Item -ItemType Directory -Path $dir -Force"]
  B2["$dir:<br/>Directory of TargetPath"]

  M3["Cross-process coordination<br/>PowerShell:<br/>System.Threading.Mutex('Global\\RCA_BuildStamp')"]
  B3["Mutex acquired:<br/>Only one process can update timestamp at a time"]

  M4["Timestamp logic<br/>PowerShell:<br/>Check if file exists, age, TTL, force"]
  D1{"Should write new timestamp?"}
  D2["File exists?"]

  C1["Output: <br/>Timestamp written to TargetPath<br/>Format: yyyyMMdd_HHmmss"]

  %% Links to files/methods
  click M1 "https://github.com/baidakovil/rca-plugin/blob/main/build/Scripts/EnsureRcaStamp.ps1#L7" "Parse flags code"
  click M2 "https://github.com/baidakovil/rca-plugin/blob/main/build/Scripts/EnsureRcaStamp.ps1#L13" "Directory creation code"
  click M3 "https://github.com/baidakovil/rca-plugin/blob/main/build/Scripts/EnsureRcaStamp.ps1#L19" "Mutex code"
  click M4 "https://github.com/baidakovil/rca-plugin/blob/main/build/Scripts/EnsureRcaStamp.ps1#L23" "Timestamp logic code"

  %% Flow
  A1 --> M1
  A2 --> M1
  A3 --> M1
  M1 --> B1
  B1 --> M2
  A1 --> M2
  M2 --> B2
  B2 --> M3
  M3 --> B3
  B3 --> M4
  A1 --> M4
  A2 --> M4
  B1 --> M4

  M4 --> D1
  D1 -- Yes --> C1
  D1 -- No --> E1["No change: existing timestamp is valid"]


  %% Output
  C1 --> F1["Release mutex and exit"]
  E1 --> F1
  ```