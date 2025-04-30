using Discord.Commands;
using System.Linq;
using System;
using System.Threading.Tasks;
using Voltaire.Controllers.Messages;

namespace Voltaire.Controllers.Reactions
{
    class React
    {
        public static async Task PerformAsync(UnifiedContext context, ulong messageId, string emoji, DataBase db)
        {
            var guildList = Send.GuildList(context);

            Discord.IMessage message;
            try {
                message = await context.Channel.GetMessageAsync(messageId);
            } catch {
                await Send.SendErrorWithDeleteReaction(context, "message not found");
                return;
            }

            // test for simple emoji (😃)
            try {
                var d = new Discord.Emoji(emoji);
                await message.AddReactionAsync(d);
                await Send.SendSentEmoteIfCommand(context);
                return;
            } catch (Discord.Net.HttpException) {}

            // look for custom discord emotes
            var emote = guildList.SelectMany(x => x.Emotes).FirstOrDefault(x => $":{x.Name}:".IndexOf(
                emoji, StringComparison.OrdinalIgnoreCase) != -1);

            if (emote != null) {
                await message.AddReactionAsync(emote);
                await Send.SendSentEmoteIfCommand(context);
            } else {
                await Send.SendErrorWithDeleteReaction(context, "Emoji not found. To send a custom emote, use the emote's name.");
            }
            return;
        }
    }
}
