using System.Runtime.CompilerServices;

// The composition layer (Apskaita.Web) implements the feature's private write store over the shared
// DbContext; no other assembly can see or inject IAccountStore. Apskaita.Tests is granted access for
// white-box unit tests that construct the feature's internal services over an in-memory store.
[assembly: InternalsVisibleTo("Apskaita.Web")]
[assembly: InternalsVisibleTo("Apskaita.Tests")]
