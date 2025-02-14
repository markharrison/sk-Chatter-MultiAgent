using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

#pragma warning disable SKEXP0001, SKEXP0110

namespace ChatterMultiAgent
{
    public class AgentsEngineQA
    {
        private readonly Kernel _kernel;
        string? questionAnswererAgentName;
        string? questionAnswererAgentInstruction;
        string? answerCheckerAgentName;
        string? answerCheckerAgentInstruction;
        string? linkCheckerAgentName;
        string? linkCheckerAgentInstruction;
        string? managerAgentName;
        string? managerAgentInstruction;
        string? questionContext;
        int characterLimit = 0;
        string? terminationFunctionInstruction;
        string? selectionFunctionInstruction;

        public AgentsEngineQA(Kernel kernel)
        {
            _kernel = kernel;

            questionContext = "Microsoft Azure AI";
            characterLimit = 2000;

            questionAnswererAgentName = "QuestionAnswerer-Agent";
            answerCheckerAgentName = "AnswerChecker-Agent";
            linkCheckerAgentName = "LinkChecker-Agent";
            managerAgentName = "Manager-Agent";

            questionAnswererAgentInstruction = $$$"""
            Take user question and give an answer from the perspective of {{{questionContext}}}, using documentation from the public web. 
            Also give relevant links to any websites that help clarify the answer.
            Do not address the user as 'you' - make all responses solely in the third person.
            If there is no information on a topic, then respond "NO INFORMATION".
            Do not emit an answer that is greater than {{{characterLimit}}} characters in length.
            """;

            answerCheckerAgentInstruction = $$$"""
            Validate the answer given by '{{{questionAnswererAgentName}}}', using public web sources when necessary. 
            Check everything in the answer is true and it fully addresses the user question.
            If the answer is good then respond "ANSWER CORRECT" with no further explanation.
            Otherwise, respond "ANSWER INCORRECT - " and explain any problems with the answer.
            If the answer exceeds {{{characterLimit}}} characters in length, then respond "ANSWER INCORRECT - " and explain the problem.
            Do not reply with anything that doesnt start with either: "ANSWER CORRECT" or "ANSWER INCORRECT".        
            """;

            linkCheckerAgentInstruction = $$$"""
            Only task is to check the web links in the answer given by '{{{questionAnswererAgentName}}}' are working - that is they return http status code 200.
            If there are no web links then respond "LINKS OK" with no further explanation.
            If all web links are working, then respond "LINKS OK" with no further explanation.
            Otherwise, for each bad web link, respond "LINK BROKEN - " and add the web link that is incorrect.
            Do not reply with anything doesnt start with either: "LINKS OK" or "LINK BROKEN".
            """;

            managerAgentInstruction = $$$"""
            Only task is to validate that the answer to the user question is correct and the links are ok.  
            If both the '{{{answerCheckerAgentName}}}' replies "ANSWER CORRECT", and the '{{{linkCheckerAgentName}}}' replies "LINKS OK", then respond "ANSWER APPROVED".
            Otherwise, respond "ANSWER REJECTED".
            Do not reply with anything other than: "ANSWER APPROVED" or "ANSWER REJECTED".
            """;

            terminationFunctionInstruction = $$$"""
            Examine the RESPONSE and determine whether the '{{{managerAgentName}}}' has approved the answer.
            If '{{{managerAgentName}}}' replied "ANSWER APPROVED", then respond "TERMINATE"
            If '{{{managerAgentName}}}' replied "ANSWER REJECTED", then respond "RETRY"
            Important - all responses must be either "TERMINATE" or "RETRY" - do not reply with any alternative.
            
            RESPONSE:
            {{$lastmessage}}
            """;

            selectionFunctionInstruction = $$$"""
            Determine which participant takes the next turn in a conversation.
            State only the name of the participant to take the next turn.
            
            Always follow these rules when selecting the next agent:
            - If the last is '{{{questionAnswererAgentName}}}', then it is '{{{answerCheckerAgentName}}}' turn.
            - If the last is '{{{answerCheckerAgentName}}}', it is '{{{linkCheckerAgentName}}}' turn.
            - If the last is '{{{linkCheckerAgentName}}}', then it is '{{{managerAgentName}}}' turn.
            - If the last is '{{{managerAgentName}}}', then it is '{{{questionAnswererAgentName}}}' turn.
            
            RESPONSE:
            {{$lastmessage}}
            """;

        }

        public ConsoleColor GetAgentColor(string? agentName)
        {
            return agentName switch
            {
                var name when name == questionAnswererAgentName => ConsoleColor.Green,
                var name when name == answerCheckerAgentName => ConsoleColor.Blue,
                var name when name == linkCheckerAgentName => ConsoleColor.Yellow,
                var name when name == managerAgentName => ConsoleColor.Red,
                _ => ConsoleColor.White
            };
        }

        public async Task RunAgents()
        {
            await Task.Run(() => { });

            ChatCompletionAgent QuestionAnswererAgent =
               new()
               {
                   Instructions = questionAnswererAgentInstruction,
                   Name = questionAnswererAgentName,
                   Kernel = _kernel,
                   Arguments = new KernelArguments(
                            new AzureOpenAIPromptExecutionSettings()
                            {
                                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                            })
               };

            ChatCompletionAgent AnswerCheckerAgent =
                  new()
                  {
                      Instructions = answerCheckerAgentInstruction,
                      Name = answerCheckerAgentName,
                      Kernel = _kernel,
                      Arguments = new KernelArguments(
                            new AzureOpenAIPromptExecutionSettings()
                            {
                                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                            })
                  };

            ChatCompletionAgent LinkCheckerAgent =
                new()
                {
                    Instructions = linkCheckerAgentInstruction,
                    Name = linkCheckerAgentName,
                    Kernel = _kernel,
                    Arguments = new KernelArguments(
                            new AzureOpenAIPromptExecutionSettings()
                            {
                                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                            })
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
                    new(QuestionAnswererAgent, AnswerCheckerAgent, LinkCheckerAgent, ManagerAgent)
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

                                        InitialAgent = QuestionAnswererAgent,

                                        HistoryVariableName = "lastmessage",

                                        ResultParser = (result) =>
                                        {
                                            Console.WriteLine("Next selected agent:" + result);
                                            return result.GetValue<string>() ?? "";
                                        },

                                    },
                            }
                    };



            Console.Write("What is your question: ");
            var Question = Console.ReadLine();

            chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, $"Question is: {Question}"));

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