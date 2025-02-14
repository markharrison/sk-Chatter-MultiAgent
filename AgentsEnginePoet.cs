using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

#pragma warning disable SKEXP0001, SKEXP0110

namespace ChatterMultiAgent
{
    public class AgentsEnginePoet
    {
        private readonly Kernel _kernel;
        string? poetAgentName;
        string? poetAgentInstruction;
        string? colorCheckerAgentName;
        string? colorCheckerAgentInstruction;
        string? managerAgentName;
        string? managerAgentInstruction;
        string? terminationFunctionInstruction;
        string? selectionFunctionInstruction;

        public AgentsEnginePoet(Kernel kernel)
        {
            _kernel = kernel;

            poetAgentName = "Poet-Agent";
            colorCheckerAgentName = "ColorChecker-Agent";
            managerAgentName = "Manager-Agent";

            poetAgentInstruction = $$$"""
            Take user's subject and generate a short funny poem. Be creative and be funny. Let your imagination run wild.
            The poem must be at least 3 lines long and no more than 12 lines long.
            """;

            colorCheckerAgentInstruction = $$$"""
            Validate that the poem written by '{{{poetAgentName}}}' includes one or more colors.
            Color names are those defined by Pantone or W3C Web Colors.
            If the poem includes a color, respond "COLORFUL - " followed by a list of all the colors found in the poem.
            If the poem does not contain any colors, respond "DULL".
            All responses must start with either "COLORFUL - " or "DULL", do not reply with any alternative. 
            """;

            managerAgentInstruction = $$$"""
            Only task is to validate that the poem meets the criterial of the '{{{poetAgentName}}}' and '{{{colorCheckerAgentName}}}'. 
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

                                        MaximumIterations = 12,
                                    },

                                SelectionStrategy =
                                    new KernelFunctionSelectionStrategy(selectionFunction, _kernel)
                                    {

                                        InitialAgent = PoetAgent,

                                        HistoryVariableName = "lastmessage",

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