using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Diagnostics;
using System.Linq;

namespace Rca.TestAdapter
{
    /// <summary>
    /// Helper class for initializing Revit for testing.
    /// </summary>
    internal static class RevitTestInitializer
    {
        /// <summary>
        /// Ensures that Revit is running and the RCA plugin is initialized.
        /// </summary>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool EnsureRevitIsInitialized()
        {
            try
            {
                if (!IsRevitRunning())
                {
                    Console.WriteLine("ERROR: Revit is not running.");
                    Console.WriteLine("Please start Autodesk Revit with the RCA plugin loaded.");
                    return false;
                }
                
                if (!CheckPipeServerResponsive())
                {
                    Console.WriteLine("ERROR: RCA pipe server not responsive.");
                    Console.WriteLine("The RCA plugin should auto-initialize when Revit starts.");
                    Console.WriteLine("Please wait a moment for initialization to complete and try again.");
                    return false;
                }
                
                Console.WriteLine("DEBUG: Pipe server is responsive, ready for test execution.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Revit for testing: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Checks if Revit is running.
        /// </summary>
        /// <returns>True if Revit is running, false otherwise.</returns>
        private static bool IsRevitRunning()
        {
            var possibleProcessNames = new[] 
            { 
                "Revit", "RevitAccelerator", "RevitPreview", 
                "Autodesk.Revit.UI", "Autodesk Revit"
            };
            
            // Check by process name
            foreach (var processName in possibleProcessNames)
            {
                try
                {
                    var processes = Process.GetProcessesByName(processName);
                    if (processes.Length > 0)
                    {
                        Console.WriteLine($"DEBUG: Found {processes.Length} Revit process(es)");
                        return true;
                    }
                }
                catch
                {
                    // Continue checking other process names
                }
            }
            
            // Check by window title containing "Revit"
            try
            {
                var revitProcesses = Process.GetProcesses().Where(p => {
                    try
                    {
                        return !string.IsNullOrEmpty(p.MainWindowTitle) && 
                               p.MainWindowTitle.IndexOf("Revit", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    catch
                    {
                        return false;
                    }
                }).ToList();
                
                if (revitProcesses.Count > 0)
                {
                    return true;
                }
            }
            catch
            {
                // Ignore errors when searching by window title
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if the pipe server is responsive.
        /// </summary>
        /// <returns>True if responsive, false otherwise.</returns>
        private static bool CheckPipeServerResponsive()
        {
            NamedPipeClientStream? pipeClient = null;
            StreamWriter? writer = null;
            StreamReader? reader = null;
            
            try
            {
                pipeClient = new NamedPipeClientStream(".", Constants.CommandPipeName, PipeDirection.InOut, PipeOptions.None);
                pipeClient.Connect(5000);
                
                if (!pipeClient.IsConnected)
                {
                    return false;
                }
                
                writer = new StreamWriter(pipeClient) { AutoFlush = true };
                reader = new StreamReader(pipeClient);
                
                // Send a status command
                var command = new PipeCommand { Command = "STATUS", Payload = "TEST_ADAPTER" };
                writer.WriteLine(JsonSerializer.Serialize(command));
                
                // Read the response
                var responseJson = reader.ReadLine();
                if (string.IsNullOrEmpty(responseJson))
                {
                    return false;
                }
                
                var response = JsonSerializer.Deserialize<PipeResponse>(responseJson);
                var success = response != null && !string.IsNullOrEmpty(response.Status);
                
                // Explicitly close streams before disposal
                writer.Close();
                reader.Close();
                
                return success;
            }
            catch (TimeoutException)
            {
                Console.WriteLine("WARNING: Could not connect to RCA pipe server.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Error checking pipe server: {ex.Message}");
                return false;
            }
            finally
            {
                // Clean up resources in proper order
                try { writer?.Dispose(); } catch { }
                try { reader?.Dispose(); } catch { }
                try 
                { 
                    if (pipeClient?.IsConnected == true) pipeClient.Close();
                    pipeClient?.Dispose(); 
                } 
                catch { }
            }
        }
    }
}
