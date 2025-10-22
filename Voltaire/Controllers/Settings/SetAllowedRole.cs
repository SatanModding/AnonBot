﻿using Discord.WebSocket;
using Voltaire.Controllers.Messages;
using System.Threading.Tasks;
using Voltaire.Controllers.Helpers;

namespace Voltaire.Controllers.Settings
{
    class SetAllowedRole
    {
        public static async Task PerformAsync(UnifiedContext context, SocketRole role, DataBase db)
        {
            var guild = await FindOrCreateGuild.Perform(context.Guild, db);
            guild.AllowedRole = role.Id.ToString();
            await db.SaveChangesAsync();
            await Send.SendMessageToContext(context, $"{role.Name} is now the only role that can use AnonBot on this server");
        }
    }
}
