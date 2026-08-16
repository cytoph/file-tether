using System.ComponentModel.DataAnnotations;

namespace FileTether.Cli.Models;

public enum ConflictChoice
{
    [Display(Name = "overwrite with remote")]
    Apply,

    [Display(Name = "keep local (skip)")]
    Skip,

    [Display(Name = "show diff")]
    ShowDiff,
}
