using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace BetterRewards.Features.Multiplayer;

internal static class BetterRewardsModelLookup
{
    public static RelicModel? FindRelic(string id, Player player) =>
        FindModel<RelicModel>(id, player);

    public static PotionModel? FindPotion(string id, Player player) =>
        FindModel<PotionModel>(id, player);

    public static CardModel? FindCard(string id, Player player) =>
        FindModel<CardModel>(id, player);

    public static CardCreationResult? CreateCardCreationResult(CardModel card)
    {
        var type = typeof(CardCreationResult);
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (method.ReturnType != type)
            {
                continue;
            }

            if (TryInvoke(method, null, card) is CardCreationResult fromFactory)
            {
                return fromFactory;
            }
        }

        foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (TryCreate(ctor, card) is CardCreationResult created)
            {
                return created;
            }
        }

        Log.Warn("[BetterRewards] Could not construct CardCreationResult.");
        return null;
    }

    public static async Task<bool> AddCardToDeck(CardModel card, Player player)
    {
        var add = typeof(CardPileCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "Add")
            .OrderByDescending(method => method.GetParameters().Length)
            .ToArray();

        foreach (var method in add)
        {
            var parameters = method.GetParameters();
            if (!parameters.Any(parameter => parameter.ParameterType.IsAssignableFrom(typeof(CardModel))))
            {
                continue;
            }

            if (!TryBuildAddArguments(parameters, card, player, out var args))
            {
                continue;
            }

            var result = method.Invoke(null, args);
            if (result is Task task)
            {
                await task;
                return ReadAddSuccess(task);
            }

            if (IsSuccessfulAddResult(result))
            {
                return true;
            }
        }

        try
        {
            return (await CardPileCmd.Add(card, PileType.Deck)).success;
        }
        catch (Exception ex)
        {
            Log.Warn($"[BetterRewards] CardPileCmd.Add failed: {ex.Message}");
            return false;
        }
    }

    private static T? FindModel<T>(string id, Player player) where T : class
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var modelDb = Type.GetType("MegaCrit.Sts2.Core.Models.ModelDb, sts2")
            ?? Type.GetType("MegaCrit.Sts2.Core.Models.ModelDb")
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("MegaCrit.Sts2.Core.Models.ModelDb"))
                .FirstOrDefault(type => type != null);

        if (modelDb != null)
        {
            var found = FindViaModelDb<T>(modelDb, id);
            if (found != null)
            {
                return ToMutableIfNeeded(found);
            }
        }

        foreach (var type in new[] { typeof(T).Assembly, player.GetType().Assembly })
        {
            var found = FindViaStaticIdLookup<T>(type, id);
            if (found != null)
            {
                return ToMutableIfNeeded(found);
            }
        }

        Log.Warn($"[BetterRewards] Could not find {typeof(T).Name} '{id}'.");
        return null;
    }

    private static T? FindViaModelDb<T>(Type modelDb, string id) where T : class
    {
        foreach (var method in modelDb.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            var result = TryInvokeModelDb<T>(method, id);
            if (result != null)
            {
                return result;
            }
        }

        foreach (var method in modelDb.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (!method.Name.Contains("All", StringComparison.OrdinalIgnoreCase)
                && !method.Name.Contains("GetAll", StringComparison.OrdinalIgnoreCase)
                && !method.Name.Contains("Enumerate", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            object? enumerated = null;
            try
            {
                if (method.IsGenericMethodDefinition && method.GetParameters().Length == 0)
                {
                    enumerated = method.MakeGenericMethod(typeof(T)).Invoke(null, null);
                }
                else if (method.IsGenericMethod && method.GetParameters().Length == 0 && method.GetGenericArguments().Length == 0)
                {
                    enumerated = method.Invoke(null, null);
                }
            }
            catch
            {
                // Try the next lookup path.
            }

            if (enumerated is IEnumerable<T> typed)
            {
                return typed.FirstOrDefault(model => HasId(model, id));
            }

            if (enumerated is System.Collections.IEnumerable raw)
            {
                foreach (var item in raw)
                {
                    if (item is T model && HasId(model, id))
                    {
                        return model;
                    }
                }
            }
        }

        return null;
    }

    private static T? TryInvokeModelDb<T>(MethodInfo method, string id) where T : class
    {
        try
        {
            MethodInfo? closed = method;
            if (method.IsGenericMethodDefinition)
            {
                var arity = method.GetGenericArguments().Length;
                if (arity != 1)
                {
                    return null;
                }

                closed = method.MakeGenericMethod(typeof(T));
            }
            else if (method.IsGenericMethod && method.GetGenericArguments().Length == 1 && method.GetGenericArguments()[0] != typeof(T))
            {
                return null;
            }

            var parameters = closed.GetParameters();
            object? result = null;
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
            {
                result = closed.Invoke(null, [id]);
            }
            else if (parameters.Length == 2
                     && parameters[0].ParameterType == typeof(string)
                     && parameters[1].ParameterType.IsByRef)
            {
                var args = new object?[] { id, null };
                result = closed.Invoke(null, args);
                if (result is true && args[1] is T found)
                {
                    return found;
                }

                return args[1] as T;
            }

            return result as T;
        }
        catch
        {
            return null;
        }
    }

    private static T? FindViaStaticIdLookup<T>(Assembly assembly, string id) where T : class
    {
        try
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!typeof(T).IsAssignableFrom(type) && type != typeof(T))
                {
                    continue;
                }

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.GetParameters() is [{ ParameterType: var parameterType }]
                        && parameterType == typeof(string)
                        && typeof(T).IsAssignableFrom(method.ReturnType))
                    {
                        if (method.Invoke(null, [id]) is T found)
                        {
                            return found;
                        }
                    }
                }
            }
        }
        catch
        {
            // Some game assemblies do not allow a full type scan.
        }

        return null;
    }

    private static bool HasId(object model, string id)
    {
        var property = model.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        return property?.GetValue(model)?.ToString() == id;
    }

    private static T ToMutableIfNeeded<T>(T model) where T : class
    {
        var type = model.GetType();
        var isMutable = type.GetProperty("IsMutable", BindingFlags.Public | BindingFlags.Instance)?.GetValue(model) as bool?;
        if (isMutable == true)
        {
            return model;
        }

        var toMutable = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(method => method.Name == "ToMutable" && method.GetParameters().Length == 0);
        if (toMutable?.Invoke(model, null) is T mutable)
        {
            return mutable;
        }

        return model;
    }

    private static object? TryInvoke(MethodInfo method, object? target, CardModel card)
    {
        var parameters = method.GetParameters();
        if (!TryBuildFactoryArguments(parameters, card, out var args))
        {
            return null;
        }

        try
        {
            return method.IsGenericMethodDefinition ? null : method.Invoke(target, args);
        }
        catch
        {
            return null;
        }
    }

    private static object? TryCreate(ConstructorInfo ctor, CardModel card)
    {
        if (!TryBuildFactoryArguments(ctor.GetParameters(), card, out var args))
        {
            return null;
        }

        try
        {
            return ctor.Invoke(args);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryBuildFactoryArguments(ParameterInfo[] parameters, CardModel card, out object?[] args)
    {
        args = new object?[parameters.Length];
        var usedCard = false;
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter.ParameterType.IsAssignableFrom(typeof(CardModel)))
            {
                args[i] = card;
                usedCard = true;
            }
            else if (parameter.HasDefaultValue)
            {
                args[i] = parameter.DefaultValue;
            }
            else if (parameter.ParameterType.IsValueType)
            {
                args[i] = Activator.CreateInstance(parameter.ParameterType);
            }
            else
            {
                args[i] = null;
            }
        }

        return usedCard || parameters.Length == 0;
    }

    private static bool TryBuildAddArguments(ParameterInfo[] parameters, CardModel card, Player player, out object?[] args)
    {
        args = new object?[parameters.Length];
        var usedCard = false;
        var usedPlayer = false;
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter.ParameterType.IsAssignableFrom(typeof(CardModel)))
            {
                args[i] = card;
                usedCard = true;
            }
            else if (parameter.ParameterType.IsAssignableFrom(typeof(Player)))
            {
                args[i] = player;
                usedPlayer = true;
            }
            else if (parameter.ParameterType == typeof(PileType))
            {
                args[i] = PileType.Deck;
            }
            else if (parameter.HasDefaultValue)
            {
                args[i] = parameter.DefaultValue;
            }
            else if (parameter.ParameterType.IsValueType)
            {
                args[i] = Activator.CreateInstance(parameter.ParameterType);
            }
            else
            {
                args[i] = null;
            }
        }

        return usedCard && (usedPlayer || parameters.Length <= 2);
    }

    private static bool ReadAddSuccess(Task task)
    {
        var resultProperty = task.GetType().GetProperty("Result");
        return IsSuccessfulAddResult(resultProperty?.GetValue(task));
    }

    private static bool IsSuccessfulAddResult(object? result)
    {
        if (result is null)
        {
            return false;
        }

        if (result is bool flag)
        {
            return flag;
        }

        var type = result.GetType();
        var field = type.GetField("success") ?? type.GetField("Success");
        if (field?.GetValue(result) is true)
        {
            return true;
        }

        var property = type.GetProperty("success") ?? type.GetProperty("Success");
        return property?.GetValue(result) is true;
    }
}
