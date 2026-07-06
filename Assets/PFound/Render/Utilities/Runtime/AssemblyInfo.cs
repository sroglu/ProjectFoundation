using System.Runtime.CompilerServices;

// The test assemblies construct TextureResizeHandle instances directly to exercise the
// ownership/dispose semantics without going through the GPU resize path.
[assembly: InternalsVisibleTo("PFound.Render.Utilities.Tests")]
[assembly: InternalsVisibleTo("PFound.Render.Utilities.Tests.PlayMode")]
