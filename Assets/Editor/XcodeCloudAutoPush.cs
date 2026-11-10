using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using System.Diagnostics;
using System.IO;

public class XcodeCloudAutoPush
{
    [PostProcessBuild(999)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        UnityEngine.Debug.Log("iOS Build complete. Targeting Xcode export directory for Git push...");

        // pathToBuiltProject is the exact folder where Unity just exported the Xcode project
        // E.g., /Users/michel/UnityProjects/MyGame/iOSBuild
        string workingDirectory = pathToBuiltProject; 

        // Explicit path to Git on macOS
        string gitPath = "/usr/bin/git"; 
        
        // 1. Check if the export folder is actually initialized as a git repo
        // If not, we initialize it, add the remote, and switch to main branch
        if (!Directory.Exists(Path.Combine(workingDirectory, ".git")))
        {
            UnityEngine.Debug.Log("Initializing new Git repository in Xcode export folder...");
            ExecuteGitCommand(gitPath, "init", workingDirectory);
            
            // IMPORTANT: Replace this URL with your actual Xcode repository URL (SSH recommended)
            string remoteUrl = "git@github.com:MichelPT/again-app.git";
            ExecuteGitCommand(gitPath, $"remote add origin {remoteUrl}", workingDirectory);
            
            ExecuteGitCommand(gitPath, "branch -M main", workingDirectory);
        }

        // 2. Add, Commit, and Push
        ExecuteGitCommand(gitPath, "add .", workingDirectory);
        ExecuteGitCommand(gitPath, "commit -m \"Automated iOS build export for Xcode Cloud\"", workingDirectory);
        
        // Using -u origin main ensures the upstream branch is tracked, required for the first push
        ExecuteGitCommand(gitPath, "push -u origin main", workingDirectory);
    }

    private static void ExecuteGitCommand(string gitPath, string arguments, string workingDirectory)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = gitPath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            process.WaitForExit();
            
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            
            if (process.ExitCode == 0) 
            {
                UnityEngine.Debug.Log($"[Git Success] {arguments}\n{output}");
            } 
            else 
            {
                if (error.Contains("nothing to commit") || output.Contains("nothing to commit"))
                {
                    UnityEngine.Debug.Log("Git: No new changes to commit in Xcode folder.");
                }
                else
                {
                    UnityEngine.Debug.LogError($"[Git Error] {arguments}\n{error}");
                }
            }
        }
    }
}