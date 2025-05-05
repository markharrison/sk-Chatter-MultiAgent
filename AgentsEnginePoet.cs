using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

#pragma warning disable SKEXP0001, SKEXP0110

namespace ChatterMultiAgent
{

    public class LastMessageReducer : IChatHistoryReducer
    {
        //public string ReduceHistory(IReadOnlyList<ChatMessageContent> history)
        //{
        //    // Get only the last message which is sufficient for our selection logic
        //    if (history.Count > 0)
        //    {
        //        var lastMessage = history[^1];

        //        // Match the filtering logic used in ReduceAsync
        //        if (lastMessage.Role == AuthorRole.Assistant)
        //        {
        //            return $"{lastMessage.AuthorName}: {lastMessage.Content}";
        //        }
        //    }
        //    return string.Empty;
        //}

        public Task<IEnumerable<ChatMessageContent>?> ReduceAsync(IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken = default)
        {
            // For the async version, return only the last message if available
            if (history.Count > 0)
            {
                var lastMessage = history[^1];

                // Make sure this message is actually from an agent (not system/user)
                if (lastMessage.Role == AuthorRole.Assistant)
                {
                    if (lastMessage.AuthorName == AgentsEnginePoet.colorCheckerAgentName && lastMessage.Content != null)
                    {
                        if (!(lastMessage.Content.StartsWith("DULL") || lastMessage.Content.StartsWith("COLORFUL")))
                        {
                            lastMessage.Content = "DULL"; // Default fallback
                        }
                    }

                    if (lastMessage.AuthorName == AgentsEnginePoet.managerAgentName && lastMessage.Content != null)
                    {
                        if (!(lastMessage.Content.StartsWith("POEM APPROVED") || lastMessage.Content.StartsWith("POEM REJECTED")))
                        {
                            lastMessage.Content = "POEM REJECTED"; // Default fallback
                        }
                    }

                    return Task.FromResult<IEnumerable<ChatMessageContent>?>(new[] { lastMessage });
                }
            }
            return Task.FromResult<IEnumerable<ChatMessageContent>?>(Array.Empty<ChatMessageContent>());
        }
    }




    public class AgentsEnginePoet
    {
        private readonly Kernel _kernel;

        public static string poetAgentName { get; private set; } = "Poet-Agent";
        public static string colorCheckerAgentName { get; private set; } = "ColorChecker-Agent";
        public static string managerAgentName { get; private set; } = "Manager-Agent";

        string? poetAgentInstruction;
        string? colorCheckerAgentInstruction;
        string? managerAgentInstruction;
        string? terminationFunctionInstruction;
        string? selectionFunctionInstruction;

        public AgentsEnginePoet(Kernel kernel)
        {
            _kernel = kernel;

            poetAgentInstruction = $$$"""
            You are a poet.
            Take user's subject and generate a short funny poem. Be creative and be funny. Let your imagination run wild.
            The poem must be at least 3 lines long and no more than 12 lines long.
            """;

            colorCheckerAgentInstruction = $$$"""
            You are a color detection agent. Your response must start strictly with either "COLORFUL -" or "DULL", with no additional commentary or formatting.
            Your task is to validate whether the last message written by '{{{poetAgentName}}}' includes one or more of the following color names (case-insensitive match / exact word match):
            "red", "green", "blue", "yellow", "purple", "orange", "pink", "brown", "black", "white", "gray", "grey", "violet", "magenta", "turquoise", "maroon", "amber", "crimson", "indigo", "teal", "lavender", "beige", "gold", "silver", "copper", "peach", "lime", "olive", "navy", "aqua", "coral", "charcoal".
            If the poem includes one or more valid color names, respond with: "COLORFUL -" followed by a comma-separated list of the color names found .
            If no valid color names are found, respond with: "DULL" .
            """;

            managerAgentInstruction = $$$"""
            If the '{{{colorCheckerAgentInstruction}}}' replies "COLORFUL then respond "POEM APPROVED".
            Otherwise, respond "POEM REJECTED".
            Do not reply with anything other than: "POEM APPROVED" or "POEM REJECTED".
            """;

            terminationFunctionInstruction = $$$"""
            Examine the RESPONSE and determine whether the '{{{managerAgentName}}}' has approved the answer.
            If '{{{managerAgentName}}}' replied "POEM APPROVED", then respond "TERMINATE"
            If '{{{managerAgentName}}}' replied "POEM REJECTED", then respond "RETRY"
            Important - all responses must be either "TERMINATE" or "RETRY", do not reply with any alternative.
            
            RESPONSE:
            {{$lastmessage}}
            """;

            selectionFunctionInstruction = $$$"""
            Determine which participant takes the next turn in a conversation.
            State only the name of the participant to take the next turn.
            
            Always follow these rules when selecting the next agent:
            - If the last is '{{{poetAgentName}}}', then it is '{{{colorCheckerAgentName}}}' turn.
            - If the last is '{{{colorCheckerAgentName}}}', then it is '{{{managerAgentName}}}' turn.
            - If the last is '{{{managerAgentName}}}', then it is '{{{poetAgentName}}}' turn.
            
            RESPONSE:
            {{$lastmessage}}
            """;

        }

        public ConsoleColor GetAgentColor(string? agentName)
        {
            return agentName switch
            {
                var name when name == poetAgentName => ConsoleColor.Green,
                var name when name == colorCheckerAgentName => ConsoleColor.Blue,
                var name when name == managerAgentName => ConsoleColor.Red,
                _ => ConsoleColor.White
            };
        }

        public async Task RunAgents()
        {
            await Task.Run(() => { });

            ChatCompletionAgent PoetAgent =
               new()
               {
                   Instructions = poetAgentInstruction,
                   Name = poetAgentName,
                   Kernel = _kernel
               };

            ChatCompletionAgent ColorCheckerAgent =
                  new()
                  {
                      Instructions = colorCheckerAgentInstruction,
                      Name = colorCheckerAgentName,
                      Kernel = _kernel
                  };

            ChatCompletionAgent ManagerAgent =
                new()
                {
                    Instructions = managerAgentInstruction,
                    Name = managerAgentName,
                    Kernel = _kernel
                };

            KernelFunction selectionFunction = AgentGroupChat.CreatePromptFunctionForStrategy(selectionFunctionInstruction ?? "", safeParameterNames: "lastmessage");
            KernelFunction terminationFunction = AgentGroupChat.CreatePromptFunctionForStrategy(terminationFunctionInstruction ?? "", safeParameterNames: "lastmessage");

            AgentGroupChat chat =
                    new(PoetAgent, ColorCheckerAgent, ManagerAgent)
                    {
                        ExecutionSettings =
                            new()
                            {
                                TerminationStrategy =
                                    new KernelFunctionTerminationStrategy(terminationFunction, _kernel)
                                    {

                                        Agents = [ManagerAgent],

                                        ResultParser = (result) =>
                                        {
                                            Console.WriteLine("Result:" + result);
                                            return result.GetValue<string>()?.Contains("TERMINATE", StringComparison.OrdinalIgnoreCase) ?? false;
                                        },

                                        HistoryVariableName = "lastmessage",

                                        HistoryReducer = new LastMessageReducer(),

                                        MaximumIterations = 12,
                                    },

                                SelectionStrategy =
                                    new KernelFunctionSelectionStrategy(selectionFunction, _kernel)
                                    {

                                        InitialAgent = PoetAgent,

                                        HistoryVariableName = "lastmessage",

                                        HistoryReducer = new LastMessageReducer(),

                                        ResultParser = (result) =>
                                        {
                                            Console.WriteLine("Next selected agent:" + result);
                                            return result.GetValue<string>() ?? "";
                                        },

                                    },
                            }
                    };


            Console.Write("What is your poem subject: ");
            var subject = Console.ReadLine();

            chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, $"Write a poem about: {subject}"));

            try
            {
                await foreach (var content in chat.InvokeAsync())
                {
                    var contentText = content.Content;
                    var AgentName = content.AuthorName;

                    if (AgentName == AgentsEnginePoet.colorCheckerAgentName && contentText != null)
                    {
                        if (!(contentText.StartsWith("DULL") || contentText.StartsWith("COLORFUL")))
                        {
                            contentText = "DULL"; // Default fallback
                        }
                    }

                    if (AgentName == AgentsEnginePoet.managerAgentName && contentText != null)
                    {
                        if (!(contentText.StartsWith("POEM APPROVED") || contentText.StartsWith("POEM REJECTED")))
                        {
                            contentText = "POEM REJECTED"; // Default fallback
                        }
                    }

                    Console.ForegroundColor = GetAgentColor(AgentName);
                    Console.WriteLine($"{AgentName} : {contentText}");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Environment.Exit(-1);
            }

            Console.WriteLine($"Conversation completed: {chat.IsComplete}");

        }
    }
}