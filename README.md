# DirectorySync

One-way folder synchronization tool written in C#.

## Requirements

- .NET 8.0+

## Usage

```bash
dotnet run <source_path> <replica_path> <interval_seconds> <log_path>
```

## Example

```bash
dotnet run "/home/user/docs" "/backup/docs" 60 "/var/log/sync.log"
```

## Build

```bash
git clone https://github.com/DominikZydek/DirectorySync.git
cd DirectorySync
dotnet build
```
