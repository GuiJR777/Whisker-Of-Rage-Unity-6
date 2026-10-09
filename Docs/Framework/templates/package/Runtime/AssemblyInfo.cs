// Purpose: Grants the package editor and test assemblies access to internal members.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("{{ASSEMBLY_ROOT}}.Editor")]
[assembly: InternalsVisibleTo("{{ASSEMBLY_ROOT}}.Tests.Editor")]
[assembly: InternalsVisibleTo("{{ASSEMBLY_ROOT}}.Tests.Runtime")]
