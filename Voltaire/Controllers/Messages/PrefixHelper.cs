﻿using Discord.Commands;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Voltaire.Models;

namespace Voltaire.Controllers.Messages
{
    class PrefixHelper
    {

        public static string ComputePrefix(UnifiedContext context, Guild guild, string defaultValue = "")
        {
            if (!guild.UseUserIdentifiers)
            {
                return defaultValue;
            }
            return Generate(GetIdentifierInteger(context.User.Id, guild));
        }

        public static bool UserBlocked(ulong userId, Guild guild)
        {
            var identifier = IdentifierString(GetIdentifierInteger(userId, guild));
            return guild.BannedIdentifiers.Any(x => x.Identifier == identifier);
        }

        public static string GetIdentifierString(ulong userId, Guild guild)
        {
            return IdentifierString(GetIdentifierInteger(userId, guild));
        }

        private static string IdentifierString(int identifier)
        {
            return Math.Abs(identifier).ToString("0000000000").Substring(0, 10);
        }

        private static string Generate(int identifierInt)
        {
            //Console.WriteLine($"{resultString} {integer} {offset}");

            //var generator = new Generator(seed: identifierInt)
            //{
            //    Casing = Casing.PascalCase,
            //    Parts = new WordBank[] { WordBank.Titles, WordBank.Nouns }
            //};
            return $"User#{IdentifierString(identifierInt)}";
        }

        public static int GetIdentifierInteger(ulong userId, Guild guild)
        {
            var seed = guild.UserIdentifierSeed;

            string password = LoadConfig.Instance.config["encryptionKey"];

            //var offset = (ulong)(new Random().Next(0, 10000));

            var id = (userId + (ulong)seed).ToString();

            var bytes = GetHash(id, password);

            var resultString = BitConverter.ToString(bytes);

            var integer = BitConverter.ToInt32(bytes, bytes.Length - 4);
            return integer;
        }

        public static Byte[] GetHash(String text, String key)
        {
            ASCIIEncoding encoding = new ASCIIEncoding();
            Byte[] textBytes = encoding.GetBytes(text);
            Byte[] keyBytes = encoding.GetBytes(key);

            Byte[] hashBytes;

            using (HMACSHA256 hash = new HMACSHA256(keyBytes))
                hashBytes = hash.ComputeHash(textBytes);

            return hashBytes;
        }
    }
}
