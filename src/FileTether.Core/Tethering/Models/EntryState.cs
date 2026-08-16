namespace FileTether.Core.Tethering.Models;

public enum EntryState
{
    New,              // never pulled - first pull pending
    UpToDate,
    PullPending,      // remote moved, local untouched
    LocallyModified,  // local differs from recorded state, remote unchanged
    Conflict,         // remote moved AND local differs
    Error,            // provider failure during version lookup
}
