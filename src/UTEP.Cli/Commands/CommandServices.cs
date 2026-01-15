using UTEP.Cli.IO;
using UTEP.Cli.Services;

namespace UTEP.Cli.Commands;

public sealed class CommandServices
{
    public CommandServices()
    {
        Store = new JsonFileStore();
        Clock = new SystemClock();
        RepoLocator = new RepoLocator();
        IdGenerator = new IdGenerator();
        GraphBuilder = new TaskGraphBuilder();
        ComputedBuilder = new TaskComputedBuilder();
        NextSelector = new NextSelector();
        BottleneckAnalyzer = new BottleneckAnalyzer();
        ValidationService = new ValidationService();
        LogWriter = new LogWriter(Clock);
        RenderService = new RenderService(GraphBuilder, ComputedBuilder, BottleneckAnalyzer);
        RepositoryLoader = new RepositoryLoader(Store);
        DoctorService = new DoctorService(Store, Clock);
    }

    public JsonFileStore Store { get; }

    public IClock Clock { get; }

    public RepoLocator RepoLocator { get; }

    public IdGenerator IdGenerator { get; }

    public TaskGraphBuilder GraphBuilder { get; }

    public TaskComputedBuilder ComputedBuilder { get; }

    public NextSelector NextSelector { get; }

    public BottleneckAnalyzer BottleneckAnalyzer { get; }

    public ValidationService ValidationService { get; }

    public LogWriter LogWriter { get; }

    public RenderService RenderService { get; }

    public RepositoryLoader RepositoryLoader { get; }

    public DoctorService DoctorService { get; }
}
