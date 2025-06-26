namespace CorebrainCS;

using System;
using System.Diagnostics;
using System.IO;
using System.Text;          // Encoding and StringBuilder


/// <summary>
/// Creates the main corebrain interface.
/// </summary>
/// <param name="pythonPath">Path to the python which works with the corebrain cli, for example if you create the ./.venv you pass the path to the ./.venv python executable</param>
/// <param name="scriptPath">Path to the corebrain cli script, if you installed it globally you just pass the `corebrain` path</param>
/// <param name="verbose"></param>
public class CorebrainCS(string pythonPath = "python", string scriptPath = "corebrain", bool verbose = false) {
  private readonly string _pythonPath = Path.GetFullPath(pythonPath);
  private readonly string _scriptPath = Path.IsPathRooted(scriptPath) ? Path.GetFullPath(scriptPath) : scriptPath;
  private readonly bool _verbose = verbose;


  public async Task<string> Help() {
    return await ExecuteCommand("--help");
  }

  public async Task<string> Version() {
    return await ExecuteCommand("--version");
  }

  public async Task<string> Configure() {
    return await ExecuteCommand("--configure");
  }

  public async Task<string> ListConfigs() {
    return await ExecuteCommand("--list-configs");
  }

  public async Task<string> RemoveConfig() {
    return await ExecuteCommand("--remove-config");
  }

  public async Task<string> ShowSchema() {
    return await ExecuteCommand("--show-schema");
  }

  public async Task<string> ExtractSchema() {
    return await ExecuteCommand("--extract-schema");
  }

  public async Task<string> ExtractSchemaToDefaultFile() {
    return await ExecuteCommand("--extract-schema --output-file test");
  }

  public async Task<string> ConfigID() {
    return await ExecuteCommand("--extract-schema --config-id config");
  }

  public async Task<string> SetToken(string token) {
    return await ExecuteCommand($"--token {token}");
  }

  public async Task<string> ApiKey(string apikey) {
    return await ExecuteCommand($"--api-key {apikey}");
  }

  public async Task<string> ApiUrl(string apiurl) {
    if (string.IsNullOrWhiteSpace(apiurl)) {
      throw new ArgumentException("API URL cannot be empty or whitespace", nameof(apiurl));
    }

    if (!Uri.TryCreate(apiurl, UriKind.Absolute, out var uriResult) ||
        (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps)) {
      throw new ArgumentException("Invalid API URL format. Must be a valid HTTP/HTTPS URL", nameof(apiurl));
    }

    // Escape the URL for command line safety
    var escapedUrl = apiurl.Replace("\"", "\\\"");
    return await ExecuteCommand($"--api-url \"{escapedUrl}\"");
  }

  /// <summary>
  /// Runs the Corebrain CLI command asynchronously and returns its output.
  /// 
  /// Features:
  /// - Non-blocking execution using async/await.
  /// - Streams standard output and error in real-time to handle long-running commands efficiently.
  /// - Prints output live to the console when verbose mode is enabled, showing errors in red.
  /// - Automatically decides whether to run the command via the Python interpreter (for .py scripts) or directly (for executables).
  /// - Ensures all input and output use UTF-8 encoding for consistent text handling.
  /// </summary>

  public async Task<string> ExecuteCommand(string arguments) {    // Asynchronous method to support non-blocking execution
    string fileName;
    string args;

    // Determine if the script is a Python file or a CLI executable
    if (_scriptPath.EndsWith(".py")) { // If it's a Python script, run it using a Python interpreter
      fileName = _pythonPath;
      args = $"\"{_scriptPath}\" {arguments}";
    }
    else {  // Otherwise, assume it's a CLI command or executable and use it directly without using Python interpreter
      fileName = _scriptPath;
      args = arguments;
    }

    var outputBuilder = new StringBuilder();
    var errorBuilder = new StringBuilder();

    if (_verbose) {
      Console.WriteLine($"Executing: {fileName} {args}");
    }

    var process = new Process {
      StartInfo = new ProcessStartInfo {
        FileName = fileName,
        Arguments = args,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        RedirectStandardInput = true, // Enable writing input to the process if needed 
        UseShellExecute = false,
        CreateNoWindow = true,
        // Set the encoding to UTF-8 for both standard output and error streams
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
        Environment = { ["PYTHONIOENCODING"] = "utf-8" } // Ensure Python uses UTF-8 encoding
      }
    };

    process.Start();

    // Start tasks to read standard output and error asynchronously
    var standardOutputTask = Task.Run(async () => {
      var buffer = new char[1024];
      int read;
      while ((read = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length)) > 0) { // Keep reading as long as there is data in the stream
        var text = new string(buffer, 0, read);
        outputBuilder.Append(text);
        if (_verbose) {
          Console.Write(text);
        }
      }
    });

    var standardErrorTask = Task.Run(async () => {
      // Allocate a character buffer to read in chunks
      var buffer = new char[1024];
      int read;
      while ((read = await process.StandardError.ReadAsync(buffer, 0, buffer.Length)) > 0) {
        var text = new string(buffer, 0, read);
        errorBuilder.Append(text);
        if (_verbose) {
          // Change the console color to red for error output
          var prevColor = Console.ForegroundColor;
          Console.ForegroundColor = ConsoleColor.Red;
          Console.Error.Write(text);
          Console.ForegroundColor = prevColor;
        }
      }
    });

    var inputTask = Task.Run(() => {  // Task to handle input from the console
      while (!process.HasExited) {  // Keep reading input while the process is still running
        try {
          var input = Console.ReadLine();
          // if the input is empty or null, break the loop
          if (input == null) {
            break;
          }

          process.StandardInput.WriteLine(input);
          process.StandardInput.Flush();  // Ensure the input is sent immediately
        }
        catch {
          break; // If terminal is non-interactive, exit the loop
        }
      }
    });

    await Task.WhenAll(standardOutputTask, standardErrorTask);  // Wait for both output and error tasks to finish
    await process.WaitForExitAsync(); // Wait for the process to exit completely

    if (process.ExitCode != 0) {  // Check if the process exited with a non-zero exit code
      var error = errorBuilder.ToString();  // Collect the error output
      process.Dispose();  // Clean up the process resources
      throw new InvalidOperationException($"Process exited with code {process.ExitCode}:\n{error}");  // Throw an exception with the error message
    }

    process.Dispose();  // Clean up the process resources
    return outputBuilder.ToString();  // Return the collected output as a string
  }
}