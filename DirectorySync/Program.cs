using System.Security.Cryptography;
using FileInfo = System.IO.FileInfo;

// 1. check cli arguments (paths, interval, log)
// 2. every X seconds sync:
//      - scan source directory - make a list with hashes
//      - scan replica directory - make a list with hashes
//      - compare files and decide on action
//      - execute operations (add, remove, update)
//      - log
// 3. repeat 2 indefinitely

internal class Program
{
    public static void Main(string[] args)
    {
        if (args.Length != 4)
        {
            Console.WriteLine("Usage: DirectorySync.exe <source_path> <replica_path> <interval_seconds> <log_path>");
            return;
        }
        
        string sourcePath = args[0];
        string replicaPath = args[1];
        int intervalSeconds = int.Parse(args[2]);
        string logPath = args[3];
        
        // check if directories exist
        if (!Directory.Exists(sourcePath))
        {
            Console.WriteLine($"Source directory does not exist at {sourcePath}. Exiting...");
            return;
        }

        if (!Directory.Exists(replicaPath))
        {
            Directory.CreateDirectory(replicaPath);
            Console.WriteLine($"Replica directory created at {replicaPath}");
        }
        
        Console.WriteLine($"Sync interval set to {intervalSeconds} seconds");
        Console.WriteLine($"Source directory: {sourcePath}");
        Console.WriteLine($"Replica directory: {replicaPath}");
        Console.WriteLine($"Log: {logPath}");
        Console.WriteLine("Press Ctrl+C to exit");
        
        var timer = new System.Timers.Timer(intervalSeconds * 1000);
        timer.Elapsed += (sender, e) => Sync(sourcePath, replicaPath, logPath);
        timer.Start();
        
        // first sync
        Sync(sourcePath, replicaPath, logPath);
        
        Console.ReadLine();
    }
    
    static void Sync(string sourcePath, string replicaPath, string logPath)
        {
            log($"[{DateTime.Now}] SYNC STARTED", logPath);
    
            var sourceFiles = ScanDirectory(sourcePath);
            var replicaFiles = ScanDirectory(replicaPath);
    
            log($"[{DateTime.Now}] SOURCE: {sourceFiles.Count} files", logPath);
            log($"[{DateTime.Now}] REPLICA: {replicaFiles.Count} files", logPath);
    
            var (toAdd, toUpdate, toDelete) = CompareDirectories(sourceFiles, replicaFiles);
    
            log($"[{DateTime.Now}] TO ADD: {toAdd.Count} files", logPath);
            log($"[{DateTime.Now}] TO UPDATE: {toUpdate.Count} files", logPath);
            log($"[{DateTime.Now}] TO DELETE: {toDelete.Count} files", logPath);
    
            ExecuteOperations(toAdd, toUpdate, toDelete, replicaPath, logPath);
    
            log($"[{DateTime.Now}] SYNC FINISHED", logPath);
        }

        static string CalculateHash(string filePath)
        {
            try
            {
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hashBytes = md5.ComputeHash(stream);
                    return Convert.ToHexString(hashBytes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while calculating hash for {filePath}: {ex.Message}");
                return "ERROR";
            }
        }

        static List<DirectorySync.FileInfo> ScanDirectory(string path)
        {
            var files = new List<DirectorySync.FileInfo>();

            try
            {
                // get all files recursively
                string[] allFiles = Directory.GetFiles(path, "*",  SearchOption.AllDirectories);

                foreach (string file in allFiles)
                {
                    var fileInfo = new FileInfo(file);

                    var myFileInfo = new DirectorySync.FileInfo
                    {
                        FullPath = file,
                        RelativePath = Path.GetRelativePath(path, file),
                        LastModified = fileInfo.LastWriteTime,
                        Size = fileInfo.Length,
                        Hash = CalculateHash(file)
                    };
        
                    files.Add(myFileInfo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while scanning {path}: {ex.Message}");
            }
    
            return files;
        }

        static (List<DirectorySync.FileInfo> toAdd, List<DirectorySync.FileInfo> toUpdate, List<string> toDelete
            )
            CompareDirectories(List<DirectorySync.FileInfo> sourceFiles, List<DirectorySync.FileInfo> replicaFiles)
        {
            var toAdd = new List<DirectorySync.FileInfo>();
            var toUpdate = new List<DirectorySync.FileInfo>();
            var toDelete = new List<string>();
    
            // for faster lookup
            var replicaDict = replicaFiles.ToDictionary(file => file.RelativePath, file => file);
            var sourceDict = sourceFiles.ToDictionary(file => file.RelativePath, file => file);

            // go through source
            foreach (var sourceFile in sourceFiles)
            {
                if (replicaDict.ContainsKey(sourceFile.RelativePath))
                {
                    // file exists in both directories - either update or skip
                    var replicaFile = replicaDict[sourceFile.RelativePath];
                    if (sourceFile.Hash != replicaFile.Hash)
                    {
                        // hash is different - update
                        toUpdate.Add(sourceFile);
                    }
                    // hash is the same - skip
                }
                else
                {
                    // file exists only in source - add to replica
                    toAdd.Add(sourceFile);
                }
            }
    
            // go through replica
            foreach (var replicaFile in replicaFiles)
            {
                if (!sourceDict.ContainsKey(replicaFile.RelativePath))
                {
                    // file exists only in replica - delete
                    toDelete.Add(replicaFile.RelativePath);
                }
            }
    
            return (toAdd, toUpdate, toDelete);
        }

        static void ExecuteOperations(List<DirectorySync.FileInfo> toAdd, List<DirectorySync.FileInfo> toUpdate,
            List<string> toDelete, string replicaPath, string logPath)
        {
            // add new files
            foreach (var file in toAdd)
            {
                try
                {
                    string targetPath = Path.Combine(replicaPath, file.RelativePath);
            
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            
                    File.Copy(file.FullPath, targetPath, true);

                    log($"[{DateTime.Now}] ADDED: {file.RelativePath}", logPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while copying {file.RelativePath}: {ex.Message}");
                }
            }
    
            // update changed files
            foreach (var file in toUpdate)
            {
                try
                {
                    string targetPath = Path.Combine(replicaPath, file.RelativePath);
            
                    File.Copy(file.FullPath, targetPath, true);
            
                    log($"[{DateTime.Now}] UPDATED: {file.RelativePath}", logPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while updating {file.RelativePath}: {ex.Message}");
                }
            }
    
            // delete unnecessary files
            foreach (var relativePath in toDelete)
            {
                try
                {
                    string targetPath =  Path.Combine(replicaPath, relativePath);
            
                    File.Delete(targetPath);
            
                    log($"[{DateTime.Now}] DELETED: {relativePath}", logPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while deleting {relativePath}: {ex.Message}");
                }
            }
        }

        static void log(string log, string logPath)
        {
            Console.WriteLine(log);
            File.AppendAllText(logPath, log + Environment.NewLine);
        }
}