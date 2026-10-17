using System.Runtime.CompilerServices;

// The studio-look Inspector lives in its own assembly (it needs Veneer, which is optional) but reuses the
// Compile pipeline of this one: SurfaceCompiler and its name rule (ToIdentifier) are internal.
[assembly: InternalsVisibleTo("SurfaceSystem.Editor.Studio")]
