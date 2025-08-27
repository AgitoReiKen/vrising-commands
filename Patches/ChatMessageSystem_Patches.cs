using ProjectM.Network;
using ProjectM;
using Unity.Entities;
using HarmonyLib;
using Unity.Collections;
using System;
using System.Linq; 
using System.Text;
using BepInEx.Bootstrap;
using Chat.API;
using Commands.API;
using Commands.Core;
using Commands.Resources;
using Commands.Utils;
using Il2CppInterop.Runtime;

namespace Commands.Patches;

[HarmonyPatch]
public static class ChatMessageSystem_Patches
{
    public static IChatAPI ChatAPI => Chat.Plugin.Instance.API;
    public static bool Initialized = false;
    public static CommandsAPI API => (CommandsAPI)Plugin.Instance.API;
    public static ComponentType[] CreateCharacterEventComponents = [
        ComponentType.ReadOnly(Il2CppType.Of<ProjectM.Network.FromCharacter>()),
        ComponentType.ReadOnly(Il2CppType.Of<ProjectM.Network.ChatMessageEvent>()),
    ];

    public  static EntityQuery ChatMessageEventQuery;
 
    [HarmonyPatch(typeof(ChatMessageSystem), "OnUpdate")]
    [HarmonyPrefix]
    public static bool OnUpdate(ChatMessageSystem __instance)
    {
        Log.Debug("[ChatMessageSystem] OnUpdate");
        if (!Initialized)
        {
            Log.Info("[ChatMessageSystem] Initialize");

            ChatMessageEventQuery = __instance.World.EntityManager.CreateEntityQuery(CreateCharacterEventComponents);
        }
        NativeArray<Entity> entities = ChatMessageEventQuery.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            var from = __instance.EntityManager.GetComponentData<FromCharacter>(entity);
            var user = __instance.EntityManager.GetComponentData<User>(from.User);
            var _event = __instance.EntityManager.GetComponentData<ChatMessageEvent>(entity);
            var messageText = _event.MessageText.ToString();
            Log.Debug($"[ChatMessage] {user.PlatformId}: {messageText}");
            if (messageText == null) continue;
             
            var command = API.ParseCommand(messageText);
            if (command == null)
            {
                Log.Debug($"Command not parsed");
                continue;
            }
            API.FindCommand(user.PlatformId, CommandEnvironment.Chat, command, out var exactCandidates, out var similarCandidates);
            Log.Debug($"Command {command.CommandName}, ExactCandidates {exactCandidates.Count}, SimilarCandidates {similarCandidates.Count}");

            /*
             * if exactCandidates has 0 item, then we didnt find exact command. Show similarCandidates to the user
             * if exactCandidates has 1 item, then we found exact command. Execute the command
             * if exactCandidates has more items, then we can not decide what command to use. Show exactCandidates to the user
             */
            if (exactCandidates.Count == 0)
            {
                if (similarCandidates.Count > 0)
                {

                    StringBuilder candidates = new();
                    foreach (var candidate in similarCandidates)
                    {
                        candidates.Append($"{API.GetUserFriendlyPluginAlias(candidate.Key)}: ");
                        candidates.Append(String.Join(", ", candidate.Value.Take(3)));
                        candidates.AppendLine();
                    }
                    ChatAPI.SendMessage(from.User, 
                        Localized.ShowSimilarCandidates(user.PlatformId).Replace("{candidates}", candidates.ToString()));
                }
                else
                {
                    ChatAPI.SendMessage(from.User, 
                        Localized.CommandNotFound(user.PlatformId).Replace("{command}", command.CommandName));
                }
            } 
            else if (exactCandidates.Count == 1)
            {
                API.ExecuteCommand(new ChatCommandContext(user), 
                exactCandidates.First(),
                    command.Arguments, 
                    out var message);
                if (message != null)
                {
                    ChatAPI.SendMessage(from.User, message);
                }
            } 
            else
            {
                StringBuilder candidates = new();
                int i = 0;
                foreach (var candidate in exactCandidates)
                {
                    var plugin = API.GetUserFriendlyPluginAlias(candidate.PluginId);
                    var commandStr = $"{API.Config.Prefixes.First()}{plugin}{API.Config.PluginDelimiters.First()}{command.CommandName}";
                    candidates.AppendLine(commandStr);
                }
                ChatAPI.SendMessage(from.User,
                    Localized.ShowExactCandidates(user.PlatformId).Replace("{candidates}", candidates.ToString()));
            }

            __instance.World.EntityManager.DestroyEntity(entity);
        }

        return true;
    }
}