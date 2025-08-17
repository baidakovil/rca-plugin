# GitHub Copilot Prompts for GPT-5: Revit Chat Assistant UI

Below are a series of concise, GPT-5-optimized prompts you can feed into GitHub Copilot to generate the pyRevit Forms UI (WPF XAML and Python script) for your Revit Chat Assistant add-in. Each prompt defines the role, context, and precise task following the OpenAI “Optimize Prompts” and “GPT-5 Prompting Guide” recommendations. Include the attached PNGs (`chat_window_simple.png` and `chat_window.png`) as visual references in Copilot.

***

## Prompt 1: XAML Layout Definition

**Role & Context:**  
You are an AI UI engineer specializing in pyRevit Forms with WPF. The project is a Revit Chat Assistant add-in that connects to a FastAPI server for LLM-powered workflows. Future iterations will expand controls and history panes.

**Task:**  
Generate `ChatWindow.xaml` containing:
- A main window titled “Revit Chat Assistant.”
- Top toolbar with two buttons: **NEW CHAT** (right aligned) and **CHAT HISTORY** (left aligned of NEW CHAT for future version).
- Central scrollable panel for:
  1. A read-only text block for server replies.
  2. A collapsible code snippet region with an **EXECUTE** button.
- Bottom panel with:
  1. A multi-line input textbox (with vertical scrollbar).
  2. A **SEND** button.
  3. Placeholder for future “Choose Model” dropdown (in second iteration).
- Name all elements with clear x:Name attributes for binding.
- Structure with Grid and DockPanel to allow easy addition of controls later.
- Follow exactly the layout in `chat_window_simple.png`.  

```xml
<!-- ChatWindow.xaml -->
<Window x:Class="RCA.ChatWindow"
        Title="Revit Chat Assistant"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Height="600" Width="400">
  <DockPanel>
    <!-- Top toolbar -->
    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" HorizontalAlignment="Right" Margin="5">
      <Button x:Name="btnNewChat" Content="NEW CHAT" Width="80" Margin="2"/>
      <!-- Future: <Button x:Name="btnHistory" Content="CHAT HISTORY" Width="100"/> -->
    </StackPanel>
    <!-- Main content -->
    <ScrollViewer DockPanel.Dock="Top" VerticalScrollBarVisibility="Auto" Margin="5">
      <StackPanel x:Name="panelConversation" Orientation="Vertical" />
    </ScrollViewer>
    <!-- Bottom input -->
    <Grid DockPanel.Dock="Bottom" Margin="5">
      <Grid.RowDefinitions>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
      </Grid.RowDefinitions>
      <TextBox x:Name="txtPrompt" Grid.Row="0" AcceptsReturn="True" VerticalScrollBarVisibility="Auto"/>
      <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,5,0,0">
        <!-- Future: <ComboBox x:Name="cmbModels" Width="120"/> -->
        <Button x:Name="btnSend" Content="SEND" Width="60" Margin="2"/>
      </StackPanel>
    </Grid>
  </DockPanel>
</Window>
```

***

## Prompt 2: Python Code-Behind (`script.py`)

**Role & Context:**  
You are a pyRevit + FastAPI integration developer. The XAML UI is defined and displays elements named according to `ChatWindow.xaml`. The server already handles message routing, code execution, and LLM orchestration.

**Task:**  
Generate `script.py` with:
- Class `ChatWindow` inheriting `forms.WPFWindow`.
- Constructor loading `ChatWindow.xaml`.
- Event handlers:
  1. `btnSend_Click`: read `txtPrompt.Text`, disable controls, send JSON to server endpoint `/chat`, await response, append server reply in `panelConversation`, re-enable controls.
  2. `btnNewChat_Click`: clear `panelConversation`, reset state, focus `txtPrompt`.
  3. `btnExecute_Click`: locate last code block in conversation, run via `subprocess.run(["pyrevit", "run", scriptPath])`, capture output or errors and append to conversation.
- Ensure thread safety (use `asyncio` or background threads).
- Prepare for expansion (e.g., future model selector).
- Follow best practices: error handling, awaitable HTTP client, logging.

```python
# script.py
import subprocess
import threading
import json
import httpx
from pyrevit import forms

class ChatWindow(forms.WPFWindow):
    def __init__(self):
        super().__init__(xaml_file='ChatWindow.xaml')
        self.btnSend.Click += self.on_send
        self.btnNewChat.Click += self.on_new_chat
        self.panelConversation.Children.Clear()

    def on_new_chat(self, sender, args):
        self.panelConversation.Children.Clear()
        self.txtPrompt.Text = ''
        self.txtPrompt.Focus()

    def on_send(self, sender, args):
        prompt = self.txtPrompt.Text.strip()
        if not prompt:
            return
        self.btnSend.IsEnabled = False
        threading.Thread(target=self.send_request, args=(prompt,)).start()

    def send_request(self, prompt):
        try:
            response = httpx.post('http://localhost:8000/chat', json={'prompt': prompt})
            data = response.json()
            self.append_response(data)
        except Exception as e:
            self.append_text(f"Error: {e}")
        finally:
            self.btnSend.IsEnabled = True

    def append_response(self, data):
        # Add text block
        self.panelConversation.Children.Add(forms.TextBlock(text=data['text']))
        # Add code block with EXECUTE
        code = data.get('code', '')
        if code:
            code_block = forms.TextBox(text=code, IsReadOnly=True, VerticalScrollBarVisibility='Auto')
            exec_btn = forms.Button(Content='EXECUTE')
            exec_btn.Click += lambda s,a: self.on_execute(code)
            container = forms.StackPanel(Orientation='Horizontal')
            container.Children.Add(code_block)
            container.Children.Add(exec_btn)
            self.panelConversation.Children.Add(container)

    def on_execute(self, code):
        threading.Thread(target=self.execute_code, args=(code,)).start()

    def execute_code(self, code):
        try:
            script_file = self.save_temp_script(code)
            result = subprocess.run(['pyrevit', 'run', script_file], capture_output=True, text=True)
            self.append_text(result.stdout or result.stderr)
        except Exception as e:
            self.append_text(f"Execution error: {e}")

    def save_temp_script(self, code):
        path = forms.save_to_temp('script.py', code)
        return path

    def append_text(self, text):
        self.panelConversation.Children.Add(forms.TextBlock(text=text))
```

***

## Prompt 3: Instruction to Copilot

Feed Copilot with:

```
# Visual Reference:
# - Use chat_window_simple.png for initial layout.
# - Observe future controls in chat_window.png.
```

**Role & Context:**  
You are GitHub Copilot writing a pyRevit add-in UI and integration code.

**Task:**  
“Using the provided `chat_window_simple.png` and `chat_window.png` as exact layout guides, generate both `ChatWindow.xaml` and `script.py`. Ensure elements are named for future extension. Follow WPF best practices and the structure shown above. The server side (FastAPI + LangChat) is already implemented. Use `forms.WPFWindow`, `httpx` for HTTP calls, and `subprocess.run` for execution. Keep prompts and code concise, readable, and maintainable.”

***

## Prompt 4: Folder Structure and Project Setup

**Role & Context:**  
You are creating the add-in boilerplate in the existing repository folder:

```
addin/rca.extension/RCA.tab/Revit Chat Assistant.panel/Go.pushbutton/
```

**Task:**  
Generate a `.pushbutton` folder containing:
- `script.py`
- `ChatWindow.xaml`
- `ChatWindow.xaml.csproj` or config if needed.
- Update `extension.json` or `bundle` manifest to reference the new UI command.
- Ensure automatically bundled resources.

Provide the Copilot prompt:

```
Generate the pushbutton folder for a pyRevit add-in under Go.pushbutton that includes ChatWindow.xaml, script.py, and necessary manifest updates. Follow the existing repository structure at https://github.com/baidakovil/rca/tree/main/addin/rca.extension/RCA.tab/Revit%20Chat%20Assistant.panel/Go.pushbutton. Use WPF + pyRevit Forms and refer to chat_window_simple.png and chat_window.png for layout.
```

***

Use these prompts with GitHub Copilot and GPT-5 to quickly scaffold a maintainable, extensible Revit Chat Assistant UI.

Sources
[1] chat_window_simple.jpeg https://ppl-ai-file-upload.s3.amazonaws.com/web/direct-files/attachments/images/69135643/523dd33a-f2b3-48d6-8e2d-642911b39faf/chat_window_simple.jpeg
[2] chat_window.jpeg https://ppl-ai-file-upload.s3.amazonaws.com/web/direct-files/attachments/images/69135643/df9ade5a-da11-4514-aacd-2ca5105c2912/chat_window.jpeg
