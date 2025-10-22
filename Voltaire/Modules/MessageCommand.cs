using System.Threading.Tasks;
using Discord.Interactions;
using System.Linq;
using Discord.WebSocket;
using System;
using Discord;

namespace Voltaire.Modules
{
  public class MessageCommand : InteractionsBase
  {
      public MessageCommand(DataBase database): base(database) {}

      [MessageCommand("Create Thread Anonymously")]
      public async Task MessageCommandHandler(IMessage msg)
      {
          var channel = msg.Channel as Discord.ITextChannel;
          await channel.CreateThreadAsync("anonbot thread", message: msg);
          await RespondAsync("Thread Created!", ephemeral: true);
      }
  }
}