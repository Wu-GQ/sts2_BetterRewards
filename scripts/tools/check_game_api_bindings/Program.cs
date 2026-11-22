using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Resolves every MemberRef in a mod DLL against a game assembly and reports name+signature
// mismatches -- the MissingMethodException class of failure that a compile-time reference
// to an older build cannot catch.
//
// usage: check_bindings <mod.dll> <game sts2.dll>
class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: check_bindings <mod.dll> <game sts2.dll>");
            return 2;
        }

        var modPath = Path.GetFullPath(args[0]);
        var gamePath = Path.GetFullPath(args[1]);

        using var modFs = File.OpenRead(modPath);
        using var modPe = new PEReader(modFs);
        var mod = modPe.GetMetadataReader();

        using var gameFs = File.OpenRead(gamePath);
        using var gamePe = new PEReader(gameFs);
        var game = gamePe.GetMetadataReader();

        var provider = new SigProvider();

        var index = new Dictionary<string, List<(string Name, string Kind, string Sig)>>(StringComparer.Ordinal);
        var typeNames = new HashSet<string>(StringComparer.Ordinal);
        var nested = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var h in game.TypeDefinitions)
        {
            var td = game.GetTypeDefinition(h);
            var full = FullName(game, td, nested);
            typeNames.Add(full);

            var list = new List<(string, string, string)>();
            foreach (var mh in td.GetMethods())
            {
                var mdef = game.GetMethodDefinition(mh);
                string sig;
                try
                {
                    var s = mdef.DecodeSignature(provider, null);
                    sig = s.ReturnType + "(" + string.Join(",", s.ParameterTypes) + ")";
                }
                catch { sig = "?"; }
                list.Add((game.GetString(mdef.Name), "M", sig));
            }
            foreach (var fh in td.GetFields())
            {
                var fd = game.GetFieldDefinition(fh);
                string sig;
                try { sig = fd.DecodeSignature(provider, null); } catch { sig = "?"; }
                list.Add((game.GetString(fd.Name), "F", sig));
            }
            if (!index.TryGetValue(full, out var existing)) index[full] = list;
            else existing.AddRange(list);
        }

        var checkedCount = 0;
        var missingTypes = new SortedSet<string>(StringComparer.Ordinal);
        var mismatches = new List<string>();

        foreach (var h in mod.MemberReferences)
        {
            var mr = mod.GetMemberReference(h);
            var parent = ParentName(mod, mr.Parent, provider, nested);
            if (!parent.StartsWith("MegaCrit.", StringComparison.Ordinal)) continue;

            checkedCount++;
            var name = mod.GetString(mr.Name);

            if (!typeNames.Contains(parent))
            {
                // Generic instantiations of game types (MessageHandlerDelegate<T> etc.) resolve
                // through the generic definition; anything else with a Mod type argument is not
                // a game API at all.
                if (!parent.Contains("<BetterRewards.")) missingTypes.Add(parent + "::" + name);
                continue;
            }

            string wanted;
            string kind;
            if (mr.GetKind() == MemberReferenceKind.Field)
            {
                kind = "F";
                try { wanted = mr.DecodeFieldSignature(provider, null); } catch { wanted = "?"; }
            }
            else
            {
                kind = "M";
                try
                {
                    var s = mr.DecodeMethodSignature(provider, null);
                    wanted = s.ReturnType + "(" + string.Join(",", s.ParameterTypes) + ")";
                }
                catch { wanted = "?"; }
            }

            var candidates = index[parent];
            var sameName = candidates.Where(c => c.Name == name && c.Kind == kind).ToList();
            if (sameName.Count == 0)
            {
                mismatches.Add($"{parent}::{name} [{kind}] -- NAME ABSENT\n        wanted : {wanted}");
                continue;
            }
            if (sameName.Any(c => c.Sig == wanted)) continue;

            mismatches.Add($"{parent}::{name} [{kind}]\n        compiled: {wanted}\n        present : " +
                           string.Join("\n                  ", sameName.Select(c => c.Sig)));
        }

        Console.WriteLine($"{Path.GetFileName(gamePath)}: {checkedCount} game MemberRefs, {mismatches.Count} mismatches, {missingTypes.Count} unresolved types");
        foreach (var m in missingTypes) Console.WriteLine("  MISSING TYPE  " + m);
        foreach (var m in mismatches) Console.WriteLine("  MISMATCH  " + m);

        return mismatches.Count == 0 && missingTypes.Count == 0 ? 0 : 1;
    }

    static string FullName(MetadataReader md, TypeDefinition td, Dictionary<string, string> nestedCache)
    {
        var name = md.GetString(td.Name);
        var declaring = td.GetDeclaringType();
        if (!declaring.IsNil)
        {
            var outer = FullName(md, md.GetTypeDefinition(declaring), nestedCache);
            return outer + "+" + name;
        }
        var ns = md.GetString(td.Namespace);
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    static string ParentName(MetadataReader md, EntityHandle h, SigProvider p, Dictionary<string, string> nestedCache)
    {
        try
        {
            switch (h.Kind)
            {
                case HandleKind.TypeReference:
                    return TypeRefName(md, (TypeReferenceHandle)h);
                case HandleKind.TypeDefinition:
                    return FullName(md, md.GetTypeDefinition((TypeDefinitionHandle)h), nestedCache);
                case HandleKind.TypeSpecification:
                {
                    // A MemberRef parent is a TypeSpec only for a generic instantiation. Members
                    // live on the generic definition, so that is what must be looked up.
                    var spec = md.GetTypeSpecification((TypeSpecificationHandle)h).DecodeSignature(p, null);
                    var lt = spec.IndexOf('<');
                    return lt > 0 ? spec.Substring(0, lt) : spec;
                }
                default:
                    return "";
            }
        }
        catch { return ""; }
    }

    /// <summary>Nested types are referenced with their declaring type as the resolution scope.</summary>
    static string TypeRefName(MetadataReader md, TypeReferenceHandle h)
    {
        var tr = md.GetTypeReference(h);
        var name = md.GetString(tr.Name);
        if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            return TypeRefName(md, (TypeReferenceHandle)tr.ResolutionScope) + "+" + name;
        }
        var ns = md.GetString(tr.Namespace);
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    sealed class SigProvider : ISignatureTypeProvider<string, object?>
    {
        public string GetArrayType(string e, ArrayShape s) => e + "[,]";
        public string GetByReferenceType(string e) => e + "&";
        public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
        public string GetGenericInstantiation(string g, ImmutableArray<string> a) => g + "<" + string.Join(",", a) + ">";
        public string GetGenericMethodParameter(object? c, int i) => "!!" + i;
        public string GetGenericTypeParameter(object? c, int i) => "!" + i;
        public string GetModifiedType(string m, string u, bool r) => u;
        public string GetPinnedType(string e) => e;
        public string GetPointerType(string e) => e + "*";
        public string GetPrimitiveType(PrimitiveTypeCode t) => t.ToString();
        public string GetSZArrayType(string e) => e + "[]";
        public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k)
        {
            var td = r.GetTypeDefinition(h);
            var declaring = td.GetDeclaringType();
            if (!declaring.IsNil) return GetTypeFromDefinition(r, declaring, k) + "+" + r.GetString(td.Name);
            var ns = r.GetString(td.Namespace);
            var n = r.GetString(td.Name);
            return string.IsNullOrEmpty(ns) ? n : ns + "." + n;
        }
        public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) => TypeRefName(r, h);
        public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k)
            => r.GetTypeSpecification(h).DecodeSignature(this, c);
    }
}
