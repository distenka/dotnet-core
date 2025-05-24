namespace Distenka.Tests.Cli;
public class TestProcessor : Processor<Config>
{
    public TestProcessor(Config config)
        : base(config) { }

    public override async Task<Result> ProcessAsync()
    {
        await Task.Delay(2000);

        return Result.Success();
    }
}
