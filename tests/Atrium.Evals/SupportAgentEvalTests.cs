using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;

namespace Atrium.Evals;

public class SupportAgentEvalTests
{
    // One ReportingConfiguration per test class: stores results under eval-results/ next to the test
    // binary, enables response caching so re-runs hit the cache, and uses the Ollama 14B judge.
    private static readonly ReportingConfiguration Reporting =
        DiskBasedReportingConfiguration.Create(
            storageRootPath: Path.Combine(AppContext.BaseDirectory, "eval-results"),
            evaluators:
            [
                new RelevanceEvaluator(),
                new GroundednessEvaluator(),
                new ToolCallAccuracyEvaluator(),
            ],
            chatConfiguration: OllamaJudge.Configuration(),
            enableResponseCaching: true,
            executionName: Environment.GetEnvironmentVariable("EVAL_RUN") ?? "local"
        );

    // ---------- helpers ----------

    private static async Task<(
        List<ChatMessage> Messages,
        ChatResponse Response
    )> RunSupportChatAsync(string userInput, CancellationToken ct)
    {
        var (chat, system) = SupportEvalHarness.Build();
        var messages = new List<ChatMessage>(system) { new(ChatRole.User, userInput) };
        var options = new ChatOptions { Tools = SupportEvalHarness.Tools };
        var response = await chat.GetResponseAsync(messages, options, ct);
        return (messages, response);
    }

    // ---------- Scenario 1 — order-status lookup (HARD ASSERT on ToolCallAccuracy) ----------

    [Fact]
    public async Task Order_status_question_calls_the_tool_and_stays_grounded()
    {
        Assert.SkipUnless(await OllamaJudge.UpAsync(), "Ollama not running at localhost:11434");

        var (messages, response) = await RunSupportChatAsync(
            "Where's my order 1234?",
            TestContext.Current.CancellationToken
        );

        await using var run = await Reporting.CreateScenarioRunAsync(
            $"{nameof(SupportAgentEvalTests)}.{nameof(Order_status_question_calls_the_tool_and_stays_grounded)}",
            cancellationToken: TestContext.Current.CancellationToken
        );

        var result = await run.EvaluateAsync(
            messages,
            response,
            additionalContext:
            [
                // Grounding context: the only facts the agent is allowed to cite.
                new GroundednessEvaluatorContext(
                    "Order 1234: Confirmed, placed 2026-06-30, 2 items, $58.00."
                ),
                // Tool definitions so the evaluator can verify call accuracy.
                new ToolCallAccuracyEvaluatorContext(SupportEvalHarness.Tools),
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );

        // HARD ASSERT: the 7B model reliably calls GetOrderStatus(1234) for this prompt.
        // Interpretation.Failed == true means the evaluator detected a problem.
        var toolAcc = result.Get<BooleanMetric>(
            ToolCallAccuracyEvaluator.ToolCallAccuracyMetricName
        );
        Assert.False(toolAcc.Interpretation?.Failed ?? false, toolAcc.Reason);
    }

    // ---------- Scenario 2 — product search ("lamp" → Desk Lamp, persist only) ----------

    [Fact]
    public async Task Product_search_for_lamp_calls_FindProduct()
    {
        Assert.SkipUnless(await OllamaJudge.UpAsync(), "Ollama not running at localhost:11434");

        var (messages, response) = await RunSupportChatAsync(
            "Do you sell desk lamps?",
            TestContext.Current.CancellationToken
        );

        await using var run = await Reporting.CreateScenarioRunAsync(
            $"{nameof(SupportAgentEvalTests)}.{nameof(Product_search_for_lamp_calls_FindProduct)}",
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Persist relevance + tool-call accuracy; no hard assert (local 7B can vary).
        await run.EvaluateAsync(
            messages,
            response,
            additionalContext: [new ToolCallAccuracyEvaluatorContext(SupportEvalHarness.Tools)],
            cancellationToken: TestContext.Current.CancellationToken
        );
    }

    // ---------- Scenario 3 — order not found (persist only) ----------

    [Fact]
    public async Task Not_found_order_returns_informative_response()
    {
        Assert.SkipUnless(await OllamaJudge.UpAsync(), "Ollama not running at localhost:11434");

        var (messages, response) = await RunSupportChatAsync(
            "What's the status of order 9999?",
            TestContext.Current.CancellationToken
        );

        await using var run = await Reporting.CreateScenarioRunAsync(
            $"{nameof(SupportAgentEvalTests)}.{nameof(Not_found_order_returns_informative_response)}",
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Persist: tool-call accuracy + groundedness against the "not found" fact.
        await run.EvaluateAsync(
            messages,
            response,
            additionalContext:
            [
                new GroundednessEvaluatorContext("No order 9999 found."),
                new ToolCallAccuracyEvaluatorContext(SupportEvalHarness.Tools),
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
    }

    // ---------- Scenario 4 — greeting, no tool call expected (persist only) ----------

    [Fact]
    public async Task Greeting_is_handled_politely_without_tool_call()
    {
        Assert.SkipUnless(await OllamaJudge.UpAsync(), "Ollama not running at localhost:11434");

        var (messages, response) = await RunSupportChatAsync(
            "Hi there!",
            TestContext.Current.CancellationToken
        );

        await using var run = await Reporting.CreateScenarioRunAsync(
            $"{nameof(SupportAgentEvalTests)}.{nameof(Greeting_is_handled_politely_without_tool_call)}",
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Persist relevance only — a polite greeting needs no tools.
        await run.EvaluateAsync(
            messages,
            response,
            additionalContext: [],
            cancellationToken: TestContext.Current.CancellationToken
        );
    }

    // ---------- Scenario 5 — off-topic ask (weather), persist only ----------

    [Fact]
    public async Task Off_topic_ask_is_declined_gracefully()
    {
        Assert.SkipUnless(await OllamaJudge.UpAsync(), "Ollama not running at localhost:11434");

        var (messages, response) = await RunSupportChatAsync(
            "What's the weather like in New York today?",
            TestContext.Current.CancellationToken
        );

        await using var run = await Reporting.CreateScenarioRunAsync(
            $"{nameof(SupportAgentEvalTests)}.{nameof(Off_topic_ask_is_declined_gracefully)}",
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Persist relevance — expect the model to politely redirect, not call tools.
        await run.EvaluateAsync(
            messages,
            response,
            additionalContext: [],
            cancellationToken: TestContext.Current.CancellationToken
        );
    }

    // ---------- Scenario 6 — unrecognized product (persist only) ----------

    [Fact]
    public async Task Unrecognized_product_returns_no_matches_response()
    {
        Assert.SkipUnless(await OllamaJudge.UpAsync(), "Ollama not running at localhost:11434");

        var (messages, response) = await RunSupportChatAsync(
            "Do you carry coffee machines?",
            TestContext.Current.CancellationToken
        );

        await using var run = await Reporting.CreateScenarioRunAsync(
            $"{nameof(SupportAgentEvalTests)}.{nameof(Unrecognized_product_returns_no_matches_response)}",
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Persist: tool-call accuracy + groundedness against "No matches." fact.
        await run.EvaluateAsync(
            messages,
            response,
            additionalContext:
            [
                new GroundednessEvaluatorContext("No matches."),
                new ToolCallAccuracyEvaluatorContext(SupportEvalHarness.Tools),
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
    }
}
