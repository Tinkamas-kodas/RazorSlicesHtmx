using System.Runtime.CompilerServices;

// The composition layer (Apskaita.Web) implements Admin's private write store over the shared DbContext;
// no other assembly can see or inject IAdminStore. Apskaita.Tests is granted access for white-box unit
// tests that construct the registries' content services over an in-memory store.
[assembly: InternalsVisibleTo("Apskaita.Web")]
[assembly: InternalsVisibleTo("Apskaita.Tests")]
