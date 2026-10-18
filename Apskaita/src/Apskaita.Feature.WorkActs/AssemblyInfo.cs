using System.Runtime.CompilerServices;

// The composition layer (Apskaita.Web) implements the feature's private write store over the shared
// DbContext; no other assembly can see or inject IWorkActStore. Apskaita.Tests is granted access for
// white-box tests.
[assembly: InternalsVisibleTo("Apskaita.Web")]
[assembly: InternalsVisibleTo("Apskaita.Tests")]
