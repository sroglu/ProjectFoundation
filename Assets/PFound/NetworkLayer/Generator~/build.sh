#!/usr/bin/env bash
# Builds the PFound.NetworkLayer source generator to a netstandard2.0 DLL using mono's csc,
# referencing the Roslyn 4.3 netstandard2.0 reference assemblies (the version Unity 6000.3 hosts).
# Prefer `dotnet build -c Release` when a .NET SDK is available; this script is the no-SDK fallback.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PKGS="${PKGS:-$HERE/.pkgs}"
OUT="${OUT:-$HERE/../Plugins/PFound.NetworkLayer.Generator.dll}"

# 1. Restore the reference assemblies via NuGet (once): the Roslyn 4.3 netstandard2.0 refs plus the
#    canonical NETStandard 2.0 reference assembly (self-contained, so no mono facades leak in).
if [ ! -d "$PKGS/Microsoft.CodeAnalysis.CSharp.4.3.0" ]; then
  nuget install Microsoft.CodeAnalysis.CSharp -Version 4.3.0 -DependencyVersion Highest -OutputDirectory "$PKGS"
fi
if [ ! -d "$PKGS/NETStandard.Library.2.0.3" ]; then
  nuget install NETStandard.Library -Version 2.0.3 -OutputDirectory "$PKGS"
fi
# Pin System.Collections.Immutable to 6.0.0 — the exact version Unity 6000.3's Roslyn host binds.
# (A newer Immutable makes Unity's compiler silently fail to load the analyzer.)
if [ ! -d "$PKGS/System.Collections.Immutable.6.0.0" ]; then
  nuget install System.Collections.Immutable -Version 6.0.0 -OutputDirectory "$PKGS"
fi

NSREF="$PKGS/NETStandard.Library.2.0.3/build/netstandard2.0/ref/netstandard.dll"
CA_C="$PKGS/Microsoft.CodeAnalysis.Common.4.3.0/lib/netstandard2.0/Microsoft.CodeAnalysis.dll"
CA_CS="$PKGS/Microsoft.CodeAnalysis.CSharp.4.3.0/lib/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll"
IMM="$PKGS/System.Collections.Immutable.6.0.0/lib/netstandard2.0/System.Collections.Immutable.dll"
MEM="$(ls "$PKGS"/System.Memory.*/lib/netstandard2.0/System.Memory.dll | head -1)"
UNSAFE="$(ls "$PKGS"/System.Runtime.CompilerServices.Unsafe.*/lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll | head -1)"

mkdir -p "$(dirname "$OUT")"
# Compile every generator/analyzer source in this folder into the one shipped DLL — the analyzer
# (NetworkMessageAnalyzer.cs) MUST live in the same assembly as the generator.
csc -nologo -nostdlib -noconfig -target:library -langversion:latest -optimize+ \
  -out:"$OUT" \
  -r:"$NSREF" -r:"$IMM" -r:"$MEM" -r:"$UNSAFE" -r:"$CA_C" -r:"$CA_CS" \
  "$HERE"/*.cs

echo "Built: $OUT"
