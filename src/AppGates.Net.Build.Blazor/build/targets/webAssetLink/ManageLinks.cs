using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

public class ManageLinks: Task
{
    [Required]
    public string ProjectDirectoryPath { get; set; }

    [Required]
    public ITaskItem[] Links { get; set; }

    void LogMessage(string message) => this.Log.LogMessage(MessageImportance.High, message);
    public override bool Execute()
    {
        if (string.IsNullOrEmpty(this.ProjectDirectoryPath))
        {
            return true;
        }
        var linkComment= "# auto-generated debug link - don't modifiy this and the next line, remove the dependency instead";
        var projectDirectory = CreateDirectoryInfo(this.ProjectDirectoryPath);

        LogMessage($"Manage debug and compile links for project '{projectDirectory.FullName}' ...");

        IEnumerable<(DirectoryInfo LinkedDirectory, Action RemoveLink)> GetLinks(DirectoryInfo projectDir, string linkCom,IList<string> gitIgnoreLines)
        {
            for (int lineIndexTmp = 0; lineIndexTmp < gitIgnoreLines.Count; ++lineIndexTmp)
            {
                var lineIndex = lineIndexTmp;
                var line = gitIgnoreLines[lineIndex];
                if (line.Equals(linkCom))
                {
                    var path = Path.Combine(projectDir.FullName,
                        gitIgnoreLines[lineIndex + 1].Replace('/', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar));
                    var absoluteIgnoreDirectory = CreateDirectoryInfo(path);
                    LogMessage($"Found link definition '{absoluteIgnoreDirectory.FullName}'");

                    yield return (absoluteIgnoreDirectory, () =>
                    {
                        // remove it
                        gitIgnoreLines[lineIndex] = null;
                        gitIgnoreLines[lineIndex + 1] = null;
                        absoluteIgnoreDirectory.Delete(true);
                    }
                    );
                }
            }
        }

        var expectedLinks = this.Links.Select(
            x =>(Hide: bool.Parse(x.GetMetadata("Hide")), Target: CreateDirectoryInfo(x.GetMetadata("AbsoluteTargetPath")),Source: CreateDirectoryInfo(x.GetMetadata("SourcePath")))).ToArray();
      
        var gitIgnoreFile = new FileInfo(Path.Combine(projectDirectory.FullName, ".gitIgnore"));
        var gitIgnoreLineList = gitIgnoreFile.Exists ? File.ReadAllLines(gitIgnoreFile.FullName).ToList() : new List<string>();

        try
        {
            var existingLinks = GetLinks(projectDirectory,linkComment, gitIgnoreLineList).ToArray();

            var mappings = FullOuterJoin(expectedLinks,existingLinks, x => x.Target.FullName, x => x.LinkedDirectory.FullName,
                (expected, existing,_) => (expected, existing)).ToArray();

            LogMessage( $"Discovered '{expectedLinks.Length}' expected links and '{existingLinks.Length}' existing links. Created '{mappings.Length}' mappings.");

            foreach (var mapping in mappings)
            {
                if (mapping.expected.Target == default)
                {
                    LogMessage($"Remove link '{ mapping.existing.LinkedDirectory.FullName}' ...");
                    mapping.existing.RemoveLink();
                }
                else if (mapping.existing == default)
                {
                    var gitIgnore = mapping.expected.Target.FullName
                        .Replace(projectDirectory.FullName, String.Empty)
                        .Replace(Path.DirectorySeparatorChar, '/');

                    LogMessage($"Add link '{gitIgnore}' to git ignore ...");
                    gitIgnoreLineList.Add(linkComment);
                    gitIgnoreLineList.Add(gitIgnore);

                }
                var expected = mapping.expected;
                if(expected.Target != null)
                {
                    if (expected.Target.Exists)
                    {
                        var actualSource = CreateDirectoryInfo(NativeMethods.GetFinalPathName(expected.Target.FullName));

                        if (actualSource.FullName != expected.Source.FullName)
                        {
                            LogMessage($"Delete link because the link source changed from '{actualSource.FullName}' to '{expected.Source.FullName}'");

                            expected.Target.Delete(true);
                            expected.Target.Refresh();
                        }
                    }

                    if (!expected.Target.Exists)
                    {

                        CreateSymbolicLink(expected.Source, expected.Target,LogMessage);
                       
                        if (expected.Hide)
                        {
                            expected.Target.Refresh();
                            LogMessage($"Hide '{expected.Target.FullName}'");
                            if ((expected.Target.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                            {
                                //Add Hidden flag    
                                expected.Target.Attributes |= FileAttributes.Hidden;
                            }
                        }
                    }
                }
            }

        }
        finally {

            File.WriteAllLines(gitIgnoreFile.FullName, gitIgnoreLineList.Where(x => x != null));
        }
        return true;

    }

    public void CreateSymbolicLink(DirectoryInfo source, DirectoryInfo target, Action<string> logMessage = null)
    {
        var message = $"Link '{source.FullName}' to '{target.FullName}'";
        if (!target.Parent.Exists)
            target.Parent.Create();

        // #if NET6_0_OR_GREATER  // compilation symbols seem not to be set by the inline task compiler.
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (!isWindows && Environment.Version.Major >= 6) // on windows the .NET 6 API seems not to work. The call is successful, but no symlink is created afterwards.
        {
            logMessage?.Invoke($"{message} with .NET6+ Api, because framework version is {Environment.Version}.");
            //Directory.CreateSymbolicLink( target.FullName,source.FullName);
            typeof(Directory).InvokeMember("CreateSymbolicLink",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.InvokeMethod, null, null,
                new object[] { target.FullName, source.FullName });
        }
        else
        {
            //#else
            logMessage?.Invoke($"{message} with fallback approach, because framework version is {Environment.Version}.");
            if (isWindows)
            {
                var startInfo = new ProcessStartInfo(
                    "cmd.exe",
                    $"/c mklink /J \"{target.FullName}\" \"{source.FullName}\"");
                startInfo.CreateNoWindow = true;

                var linkProcess = Process.Start(startInfo);
                linkProcess.WaitForExit();
                if (linkProcess.ExitCode != 0)
                {
                    throw new ApplicationException($"{message} failed.");
                }
            }
            else
            {
                var startInfo = new ProcessStartInfo(
                    "ln",
                    $"-sf \"{source.FullName}\"´\"{target.FullName}\"");
                startInfo.CreateNoWindow = true;

                var linkProcess = Process.Start(startInfo);
                linkProcess.WaitForExit();
                if (linkProcess.ExitCode != 0)
                {
                    throw new ApplicationException($"{message} failed.");
                }
            }
        }
        //#endif
        logMessage?.Invoke($"{message} successful.");

    }

    private static DirectoryInfo CreateDirectoryInfo(string directoryPath)
    {
        var absolutePath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar);
        var path = Environment.OSVersion.Platform == PlatformID.Win32NT ? GetWin32LongPath(absolutePath) : absolutePath;
        return new DirectoryInfo(path);
    }

    private static string GetWin32LongPath(string path)
    {
        if (path.StartsWith(@"\\?\")) return path;

        if (path.StartsWith("\\"))
        {
            path = @"\\?\UNC\" + path.Substring(2);
        }
        else if (path.Contains(":"))
        {
            path = @"\\?\" + path;
        }
        else
        {
            var currdir = Environment.CurrentDirectory;
            path = Path.Combine(currdir, path);
            while (path.Contains("\\.\\")) path = path.Replace("\\.\\", "\\");
            path = @"\\?\" + path;
        }
        return path.TrimEnd('.'); ;
    }


    public static IEnumerable<TResult> FullOuterJoin<TA, TB, TKey, TResult>(
        IEnumerable<TA> a,
       IEnumerable<TB> b,
       Func<TA, TKey> selectKeyA,
       Func<TB, TKey> selectKeyB,
       Func<TA, TB, TKey, TResult> projection,
       TA defaultA = default(TA),
       TB defaultB = default(TB),
       IEqualityComparer<TKey> cmp = null)
    {

        cmp = cmp ?? EqualityComparer<TKey>.Default;
        var adict = a.ToDictionary(selectKeyA, cmp);
        var bdict = b.ToDictionary(selectKeyB, cmp);

        var keys = new HashSet<TKey>(adict.Keys, cmp);
        keys.UnionWith(bdict.Keys);

        var join = from key in keys
                   let xa = GetOrDefault(adict,key, defaultA)
                   let xb = GetOrDefault(bdict,key, defaultB)
                   select projection(xa, xb, key);

        return join;
    }

    public static T GetOrDefault<K, T>( IDictionary<K, T> d, K k, T def = default(T))
        => d.TryGetValue(k, out T value) ? value : def;

    public static class NativeMethods
    {
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        private const uint FILE_READ_EA = 0x0008;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x2000000;

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint GetFinalPathNameByHandle(IntPtr hFile, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CreateFile(
                [MarshalAs(UnmanagedType.LPTStr)] string filename,
                [MarshalAs(UnmanagedType.U4)] uint access,
                [MarshalAs(UnmanagedType.U4)] FileShare share,
                IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
                [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
                [MarshalAs(UnmanagedType.U4)] uint flagsAndAttributes,
                IntPtr templateFile);

        public static string GetFinalPathName(string path)
        {
            try
            {
                
                var h = CreateFile(path,
                    FILE_READ_EA,
                    FileShare.ReadWrite | FileShare.Delete,
                    IntPtr.Zero,
                    FileMode.Open,
                    FILE_FLAG_BACKUP_SEMANTICS,
                    IntPtr.Zero);
                if (h == INVALID_HANDLE_VALUE)
                    throw new Win32Exception();

                try
                {
                    var sb = new StringBuilder(1024);
                    var res = GetFinalPathNameByHandle(h, sb, 1024, 0);
                    if (res == 0)
                        throw new Win32Exception();

                    return sb.ToString();
                }
                finally
                {
                    CloseHandle(h);
                }

            }catch(Exception ex)
            {
                throw new ApplicationException($"Unable to get the long path version of '{path}'. See inner exception for more details", ex);
            }
        }
    }
}

