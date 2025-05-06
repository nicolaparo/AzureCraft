namespace AzureCraft
{
    public class PowerShellService
    {
        public async Task<string> ExecuteCommandAsync(string command)
        {
            var escapedArgs = command.Replace("\"", "\\\"");
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            string result = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new Exception($"PowerShell command failed with error: {error}");
            }
            return result;
        }
    }
}
