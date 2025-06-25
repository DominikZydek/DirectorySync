namespace DirectorySync;

public class FileInfo
{
    public string RelativePath { get; set; }
    public string FullPath { get; set; }
    public DateTime LastModified { get; set; }
    public long Size { get; set; }
    public string Hash { get; set; }

    public override string ToString()
    {
        return $"{RelativePath} {FullPath} {LastModified} {Size} {Hash}";
    }
}