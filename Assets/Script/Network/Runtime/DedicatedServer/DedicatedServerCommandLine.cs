using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CGame.Network
{
    public static class DedicatedServerCommandLine
    {
        public static DedicatedServerLaunchConfiguration Parse(IReadOnlyList<string> arguments)
        {
            long matchId = 0;
            int dataPort = 0;
            int healthPort = 0;
            string credential = null;
            string levelId = null;
            string contentVersion = null;
            var pawns = new List<DedicatedAuthorityPawnConfiguration>();

            for (int index = 0; index < arguments.Count; index += 2)
            {
                if (index + 1 >= arguments.Count) throw new ArgumentException($"Missing value for {arguments[index]}.");
                string option = arguments[index];
                string value = arguments[index + 1];
                switch (option)
                {
                    case "--match-id": matchId = ParseLong(option, value); break;
                    case "--data-port": dataPort = ParseInt(option, value); break;
                    case "--health-port": healthPort = ParseInt(option, value); break;
                    case "--credential": credential = value; break;
                    case "--level-id": levelId = value; break;
                    case "--content-version": contentVersion = value; break;
                    case "--authority-pawn": pawns.Add(ParsePawn(value)); break;
                    default: throw new ArgumentException($"Unknown Dedicated Server option {option}.");
                }
            }

            if (matchId <= 0 || dataPort <= 0 || healthPort <= 0 ||
                string.IsNullOrWhiteSpace(credential) || string.IsNullOrWhiteSpace(levelId) ||
                string.IsNullOrWhiteSpace(contentVersion) || pawns.Count == 0)
            {
                throw new ArgumentException("Dedicated Server launch arguments are incomplete.");
            }

            return new DedicatedServerLaunchConfiguration(
                matchId, dataPort, healthPort, credential, levelId, contentVersion, pawns);
        }

        private static DedicatedAuthorityPawnConfiguration ParsePawn(string value)
        {
            string[] fields = value.Split('|');
            if (fields.Length != 5) throw new ArgumentException("Authority pawn requires five fields.");
            return new DedicatedAuthorityPawnConfiguration(
                ParseLong("pawn-id", fields[0]),
                ParseLong("owner-player-id", fields[1]),
                ParseLong("possession-revision", fields[2]),
                Encoding.UTF8.GetString(Convert.FromBase64String(fields[3])),
                Encoding.UTF8.GetString(Convert.FromBase64String(fields[4])));
        }

        private static int ParseInt(string option, string value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : throw new ArgumentException($"{option} requires an integer.");

        private static long ParseLong(string option, string value) =>
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
                ? parsed
                : throw new ArgumentException($"{option} requires an integer.");
    }
}
