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



    if (_verbose) {
      Console.WriteLine($"Executing: {fileName} {args}");
    }

    var process = new Process {
      StartInfo = new ProcessStartInfo {
        FileName = fileName,
        Arguments = args,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        // Set the encoding to UTF-8 for both standard output and error streams
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
        Environment = { ["PYTHONIOENCODING"] = "utf-8" } // Ensure Python uses UTF-8 encoding
      }
    };

    process.Start();
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (_verbose) {
      Console.WriteLine("Command output:");
      Console.WriteLine(output);
      if (!string.IsNullOrEmpty(error)) {
        Console.WriteLine("Error output:\n" + error);
      }
    }

    if (!string.IsNullOrEmpty(error)) {
      throw new InvalidOperationException($"Python CLI error: {error}");
    }

    return output.Trim();
  }
}