using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Naming",
    "CA1707:Identifiers should not contain underscores",
    Justification = "Test method names use the Member_ExpectedOutcome_WhenCondition convention.",
    Scope = "NamespaceAndDescendants",
    Target = "~N:FileTether.Core.Tests")]
