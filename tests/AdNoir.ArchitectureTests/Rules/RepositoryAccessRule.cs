using System.Reflection;
using AdNoir.ArchitectureTests.Il;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AdNoir.ArchitectureTests.Rules;

/// <summary>Rejects direct Mongo collection data operations outside the shared repository hierarchy.</summary>
internal static class RepositoryAccessRule
{
    internal static IReadOnlyList<IlViolation> Check(Assembly assembly)
    {
        using var module = ModuleDefinition.ReadModule(assembly.Location);
        List<IlViolation> violations = [];
        foreach (var type in Flatten(module.Types))
        {
            var owner = type;
            while (owner.DeclaringType is not null)
            {
                owner = owner.DeclaringType;
            }

            var runtimeType = assembly.GetType(owner.FullName.Replace('/', '+'));
            if (IsRepository(runtimeType))
            {
                continue;
            }

            foreach (var method in type.Methods.Where(method => method.HasBody))
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    // Include method-group delegates, not just direct calls.
                    if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt &&
                        instruction.OpCode != OpCodes.Ldftn && instruction.OpCode != OpCodes.Ldvirtftn)
                    {
                        continue;
                    }

                    if (instruction.Operand is not MethodReference called || !IsDataOperation(called))
                    {
                        continue;
                    }
                    // Repository scope expansion cannot depend on the repository itself.
                    // This is a method-level read exception, never a type-wide write exemption.
                    bool resolverRead = owner.FullName == "AdNoir.IdentityAndOrg.Infrastructure.Persistence.OrgUnitSubtreeResolver" &&
                        (method.Name == "GetSubtreeIdsAsync" || type.Name.StartsWith("<GetSubtreeIdsAsync>d__", StringComparison.Ordinal)) &&
                        called.Name == "Find";
                    if (!resolverRead)
                    {
                        violations.Add(new(assembly.GetName().Name!, type.FullName, method.Name,
                            $"raw Mongo data access: {called.DeclaringType.FullName}.{called.Name}"));
                    }
                }
            }
        }
        return violations;
    }

    private static bool IsRepository(Type? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition().FullName ==
                "AdNoir.BuildingBlocks.Mongo.Repositories.MongoRepository`1")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDataOperation(MethodReference method)
    {
        string name = method.DeclaringType.FullName;
        bool collection = name.StartsWith("MongoDB.Driver.IMongoCollection`1", StringComparison.Ordinal) ||
            name == "MongoDB.Driver.IMongoCollectionExtensions" ||
            name == "MongoDB.Driver.Linq.LinqExtensions";
        if (!collection)
        {
            return false;
        }

        string[] operations = ["Find", "Insert", "Update", "Delete", "Replace", "BulkWrite", "Aggregate",
            "Count", "EstimatedDocumentCount", "Distinct", "AsQueryable", "Watch"];
        return operations.Any(prefix => method.Name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static IEnumerable<TypeDefinition> Flatten(IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in Flatten(type.NestedTypes))
            {
                yield return nested;
            }
        }
    }
}
