using System.Xml.Linq;

namespace AngleFlow.Workflow.Tests;

public sealed class WorkflowModelTests
{
    private static readonly XNamespace Bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    private static readonly XNamespace Zeebe = "http://camunda.org/schema/zeebe/1.0";
    private static readonly XNamespace Dmn = "https://www.omg.org/spec/DMN/20191111/MODEL/";

    [Fact]
    public void Bpmn_defines_executable_asset_finance_process_and_all_worker_contracts()
    {
        var document = XDocument.Load(ProcessPath("asset-finance-application.bpmn"));
        var process = Assert.Single(document.Descendants(Bpmn + "process"));

        Assert.Equal("asset-finance-application", (string?)process.Attribute("id"));
        Assert.Equal("true", (string?)process.Attribute("isExecutable"));

        var jobTypes = process
            .Descendants(Zeebe + "taskDefinition")
            .Select(item => (string?)item.Attribute("type"))
            .Where(item => item is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        var required = new[]
        {
            "validate-application",
            "identity-check",
            "credit-check",
            "affordability-check",
            "fraud-check",
            "asset-check",
            "persist-decision",
            "create-offer",
            "send-notification",
            "escalate-review",
            "complete-application"
        };
        Assert.All(required, jobType => Assert.Contains(jobType, jobTypes));
    }

    [Fact]
    public void Bpmn_runs_five_checks_between_parallel_split_and_join()
    {
        var process = XDocument.Load(ProcessPath("asset-finance-application.bpmn"))
            .Descendants(Bpmn + "process")
            .Single();
        var flows = process.Descendants(Bpmn + "sequenceFlow").ToArray();

        Assert.Equal(5, flows.Count(item => (string?)item.Attribute("sourceRef") == "Gateway_ChecksSplit"));
        Assert.Equal(5, flows.Count(item => (string?)item.Attribute("targetRef") == "Gateway_ChecksJoin"));
    }

    [Fact]
    public void Bpmn_has_manual_review_timer_and_interrupting_withdrawal_message()
    {
        var document = XDocument.Load(ProcessPath("asset-finance-application.bpmn"));

        Assert.Contains(
            document.Descendants(Bpmn + "message"),
            item => (string?)item.Attribute("name") == "application-withdrawn");
        Assert.Contains(
            document.Descendants(Bpmn + "timeDuration"),
            item => item.Value == "PT2M");
        Assert.Contains(
            document.Descendants(Bpmn + "subProcess"),
            item => (string?)item.Attribute("triggeredByEvent") == "true");
        Assert.Contains(
            document.Descendants(Bpmn + "startEvent"),
            item => (string?)item.Attribute("isInterrupting") == "true"
                && item.Element(Bpmn + "messageEventDefinition") is not null);
    }

    [Fact]
    public void Dmn_returns_each_supported_underwriting_outcome()
    {
        var document = XDocument.Load(ProcessPath("application-risk.dmn"));
        var decision = Assert.Single(document.Descendants(Dmn + "decision"));
        Assert.Equal("application-risk", (string?)decision.Attribute("id"));

        var outputs = document
            .Descendants(Dmn + "outputEntry")
            .Elements(Dmn + "text")
            .Select(item => item.Value.Trim().Trim('"'))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("AUTO_APPROVE", outputs);
        Assert.Contains("MANUAL_REVIEW", outputs);
        Assert.Contains("DECLINE", outputs);
    }

    [Fact]
    public void Models_do_not_reuse_element_ids()
    {
        foreach (var file in new[] { "asset-finance-application.bpmn", "application-risk.dmn" })
        {
            var ids = XDocument.Load(ProcessPath(file))
                .Descendants()
                .Attributes("id")
                .Select(attribute => attribute.Value)
                .ToArray();
            Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        }
    }

    private static string ProcessPath(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "process", fileName));
}
