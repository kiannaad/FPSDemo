namespace Fps.ServerHost;

public static class ServerHostCommandLine
{
    public static ServerHostOptions Parse(IReadOnlyList<string> args)
    {
        string? contentPath = null;
        int port = 0;
        int tickRate = 30;
        int healthPort = 0;

        for (int index = 0; index < args.Count; index += 2)
        {
            if (index + 1 >= args.Count)
            {
                throw new ArgumentException($"Missing value for command-line option {args[index]}.");
            }

            string option = args[index];
            string value = args[index + 1];
            switch (option)
            {
                case "--content":
                    contentPath = value;
                    break;
                case "--port":
                    port = ParseInt(option, value);
                    break;
                case "--tick-rate":
                    tickRate = ParseInt(option, value);
                    break;
                case "--health-port":
                    healthPort = ParseInt(option, value);
                    break;
                default:
                    throw new ArgumentException($"Unknown command-line option {option}.");
            }
        }

        if (string.IsNullOrWhiteSpace(contentPath))
        {
            throw new ArgumentException("The --content option is required.");
        }

        return new ServerHostOptions(contentPath, port, tickRate, healthPort);
    }

    private static int ParseInt(string option, string value)
    {
        return int.TryParse(value, out int parsed)
            ? parsed
            : throw new ArgumentException($"Option {option} requires an integer value.");
    }
}
