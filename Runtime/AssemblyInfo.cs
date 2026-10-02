using System.Runtime.CompilerServices;

// Lets SurfaceData's Compile step (and its custom Inspector) reach internal members - e.g. SurfaceGroup.id -
// without exposing public setters that gameplay code could use to change a group's identity at runtime.
[assembly: InternalsVisibleTo("SurfaceSystem.Editor")]
