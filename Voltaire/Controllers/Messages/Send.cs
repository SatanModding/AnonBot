﻿using Discord;
using Discord.WebSocket;
using Rijndael256;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Voltaire.Controllers.Messages
{
    class Send
    {
        public static async Task PerformAsync(UnifiedContext context, string channelName, string message, bool reply, DataBase db)
        {
            var candidateGuilds = GuildList(context);
            switch (candidateGuilds.Count())
            {
                case 0:
                    await SendErrorWithDeleteReaction(context, "It doesn't look like you belong to any servers where AnonBot is installed. Please add AnonBot to your desired server.");
                    break;
                case 1:
                    await SendToGuild.LookupAndSendAsync(candidateGuilds.First(), context, channelName, message, reply, db);
                    break;
                default:
                    var view = Views.Info.MultipleGuildSendResponse.Response(candidateGuilds, message);
                    await SendErrorWithDeleteReaction(context, view.Item1, view.Item2);
                    break;
            }
        }

        public static Func<string, string, Task<IUserMessage>> SendMessageToChannel(IMessageChannel channel, bool replyable, UnifiedContext context, bool forceEmbed = false)
        {
            if (!replyable)
            {
                return async (username, message) =>
                {
                    message = CheckForMentions(channel, message);
                    if (forceEmbed)
                    {
                        var view = Views.Message.Response(username, message);
                        return await SendMessageAndCatchError(() => { return channel.SendMessageAsync(view.Item1, embed: view.Item2); }, context);
                    }

                    if (string.IsNullOrEmpty(username))
                    {
                        return await SendMessageAndCatchError(() => { return channel.SendMessageAsync(message); }, context);
                    }
                    return await SendMessageAndCatchError(() => { return channel.SendMessageAsync($"**{username}**: {message}"); }, context);
                };
            }
            return async (username, message) =>
            {
                var key = LoadConfig.Instance.config["encryptionKey"];
                var replyHash = Rijndael.Encrypt(context.User.Id.ToString(), key, KeySize.Aes256);
                var view = Views.Message.Response(username, message);
                return await SendMessageAndCatchError(() => {
                    var builder = new ComponentBuilder()
                        .WithButton("Send DM Reply", $"prompt-reply:{replyHash}:{false}")
                        .WithButton("Send Repliable DM", $"prompt-reply:{replyHash}:{true}");
                    return channel.SendMessageAsync(view.Item1, embed: view.Item2, components: builder.Build());
                }, context);
            };
        }

        public static async Task<IUserMessage> SendMessageAndCatchError(Func<Task<IUserMessage>> send, UnifiedContext context)
        {
            try
            {
                return await send();
            }
            catch (Discord.Net.HttpException e)
            {
                switch (e.DiscordCode)
                {
                    case DiscordErrorCode.CannotSendMessageToUser:
                        await SendMessageToContext(context, "AnonBot has been blocked by this user, or they have DMs disabled.");
                        break;
                    case DiscordErrorCode.InsufficientPermissions:
                    case DiscordErrorCode.MissingPermissions:
                        await SendMessageToContext(context, "AnonBot doesn't have the " +
                        "permissions required to send this message. Ensure AnonBot can access the channel you are trying to send to, and that it has " +
                        " \"Embed Links\" and \"Use External Emojis\" permission.");
                        break;
                }

                throw e;
            }
        }

        private static string CheckForMentions(IMessageChannel channel, string message)
        {
            var words = message.Split().Where(x => x.StartsWith("@"));
            if (!words.Any())
                return message;

            var users = AsyncEnumerableExtensions.Flatten(channel.GetUsersAsync());

            users.Select(x => $"@{x.Username}").Intersect(words.ToAsyncEnumerable()).ForEachAsync(async x =>
            {
                var user = await users.FirstAsync(y => y.Username == x.Substring(1));
                message = message.Replace(x, user.Mention);
            });

            users.Select(x => $"@{x.Username}#{x.Discriminator}").Intersect(words.ToAsyncEnumerable()).ForEachAsync(async x =>
            {
                var user = await users.FirstAsync(y => $"@{y.Username}#{y.Discriminator}" == x);
                message = message.Replace(x, user.Mention);
            });

            if (channel is SocketTextChannel)
            {
                var castChannel = (SocketTextChannel)channel;
                var roles = castChannel.Guild.Roles;
                roles.Select(x => $"@{x.Name}").Intersect(words).ToList().ForEach(x =>
                {
                    var role = roles.First(y => y.Name == x.Substring(1));
                    message = message.Replace(x, role.Mention);
                });
            }

            return message;
        }

        public static IEnumerable<SocketGuild> GuildList(UnifiedContext currentContext)
        {
            var guilds = currentContext.Client.Guilds.Where(x => x.Users.Any(u => u.Id == currentContext.User.Id));
            return guilds;
        }

        public static async Task SendMessageToContext(UnifiedContext context, string message, Embed embed = null)
        {
            if (context is CommandBasedContext commandContext) {
                await commandContext.Channel.SendMessageAsync(message, embed: embed);
            } else if ( context is InteractionBasedContext interactionContext) {
                await interactionContext.Responder(message, embed);
            }
        }

        public static async Task SendSentEmoteIfCommand(UnifiedContext context)
        {
            if (context is CommandBasedContext commandContext) {
                var emote = Emote.Parse(LoadConfig.Instance.config["sent_emoji"]);
                await commandContext.Message.AddReactionAsync(emote);
            } else if ( context is InteractionBasedContext interactionContext) {
                await interactionContext.Responder("Sent!", null);
            }
        }

        public static async Task SendErrorWithDeleteReaction(UnifiedContext context, string errorMessage, Embed embed = null)
        {
            if (context is CommandBasedContext commandContext) {
                var message = await commandContext.Channel.SendMessageAsync(errorMessage, embed: embed);
                await AddReactionToMessage(message);
            } else if ( context is InteractionBasedContext interactionContext) {
                await interactionContext.Responder(errorMessage, embed);
            }
        }

        public static async Task AddReactionToMessage(IUserMessage message)
        {
            var emote = new Emoji(DeleteEmote);
            await message.AddReactionAsync(emote);
        }

        public static string DeleteEmote = "🗑";
    }
}
