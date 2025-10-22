﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Voltaire.Controllers.Helpers;

namespace Voltaire.Controllers.Messages
{
    class SendDirectMessage
    {
        public static async Task PerformAsync(UnifiedContext context, string userName, string message, bool replyable, DataBase db)
        {
            // convert special discord tag to regular ID format
            userName = userName.StartsWith("<@!") && userName.EndsWith('>') ? userName.Substring(3, userName.Length - 4) : userName;
            userName = userName.StartsWith("<@") && userName.EndsWith('>') ? userName.Substring(2, userName.Length - 3) : userName;

            userName = userName.StartsWith('@') ? userName.Substring(1) : userName;
            try
            {
                var guildList = Send.GuildList(context);
                List<SocketGuildUser> allUsersList = ToUserList(guildList);

                var userList = allUsersList.Where(x => x.Username != null &&
                    (
                        // simple username
                        x.Username.ToLower() == userName.ToLower() ||
                        // id
                        x.Id.ToString() == userName ||
                        // username with discriminator
                        $"{x.Username}#{x.Discriminator}".ToLower() == userName.ToLower()
                    )
                    && !x.IsBot);

                var allowDmList = userList.Where(x => FilterGuildByDirectMessageSetting(x, db));

                if (!allowDmList.Any() && userList.Any())
                {
                    await Send.SendErrorWithDeleteReaction(context, "user found, but channel permissions do not allow annonymous direct messaging");
                    return;
                }

                var requiredRoleList = allowDmList.Where(x => FilterGuildByRole(x, context.User, db));

                if (!requiredRoleList.Any() && allowDmList.Any())
                {
                    await Send.SendErrorWithDeleteReaction(context, "user found, but you do not have the role required to DM them");
                    return;
                }

                var list = requiredRoleList.ToList().Select(async x => Tuple.Create(x, await FindOrCreateGuild.Perform(x.Guild, db)));

                var userGuild = list.FirstOrDefault(x => !PrefixHelper.UserBlocked(context.User.Id, x.Result.Item2));

                if (userGuild == null && requiredRoleList.Any())
                {
                    await Send.SendErrorWithDeleteReaction(context, "user found, but you have been banned from using AnonBot on your shared server");
                }
                else if (userGuild == null)
                {
                    await Send.SendErrorWithDeleteReaction(context, "user not found");
                    return;
                }

                var userChannel = await userGuild.Result.Item1.CreateDMChannelAsync();
                var prefix = PrefixHelper.ComputePrefix(context, userGuild.Result.Item2, "anonymous user");
                var messageFunction = Send.SendMessageToChannel(userChannel, replyable, context);
                var sentMessage = await messageFunction(prefix, message);
                await Send.AddReactionToMessage(sentMessage);
                await Send.SendSentEmoteIfCommand(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return;
        }

        public static List<SocketGuildUser> ToUserList(IEnumerable<SocketGuild> guildList)
        {
            return guildList.Aggregate(new List<SocketGuildUser>(), (acc, item) => acc.Concat(item.Users).ToList());
        }

        private static bool FilterGuildByDirectMessageSetting(SocketGuildUser user, DataBase db)
        {
            return ! db.Guilds.Any(x => x.DiscordId == user.Guild.Id.ToString() && !x.AllowDirectMessage);
        }

        private static bool FilterGuildByRole(SocketGuildUser reciver, IUser sender, DataBase db)
        {
            var guild = db.Guilds.FirstOrDefault(x => x.DiscordId == reciver.Guild.Id.ToString());

            return UserHasRole.Perform(reciver.Guild, sender, guild);
        }
    }
}
