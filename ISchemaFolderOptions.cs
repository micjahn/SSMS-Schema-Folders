
namespace SsmsSchemaFolders
{
    using System.Collections.Generic;

    public interface ISchemaFolderOptions
    {
        bool Enabled { get; }
        bool AppendDot { get; }
        bool CloneParentNode { get; }
        bool UseObjectIcon { get; }
        bool RenameNode { get; }
        int QuickSchema { get; }
        int UnresponsiveTimeout { get; }
        int UseClear { get; }
        List<string> RegularExpressions { get; }
        FolderNameCase FolderNameCase { get; }
    }

    public enum FolderNameCase
    {
        Original,
        Lower,
        Upper
    }
}
