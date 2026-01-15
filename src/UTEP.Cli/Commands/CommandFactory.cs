using System.CommandLine;
using System.CommandLine.Invocation;
using Spectre.Console;
using UTEP.Cli.Domain;
using UTEP.Cli.IO;
using UTEP.Cli.Services;
using TaskStatus = UTEP.Cli.Domain.TaskStatus;
using TreeNode = UTEP.Cli.Domain.TreeNode;

namespace UTEP.Cli.Commands;

public sealed class CommandFactory
{
    private readonly Option<bool> _jsonOption;
    private readonly Option<string?> _goalOption;
    private readonly CommandServices _services;
    private readonly OutputWriter _output;

    public CommandFactory(Option<bool> jsonOption, Option<string?> goalOption)
    {
        _jsonOption = jsonOption;
        _goalOption = goalOption;
        _services = new CommandServices();
        _output = new OutputWriter();
    }

    public IReadOnlyList<Command> BuildAll()
    {
        return new List<Command>
        {
            BuildInit(),
            BuildGoalCommand(),
            BuildTaskCommand(),
            BuildNext(),
            BuildBottlenecks(),
            BuildValidate(),
            BuildDoctor(),
            BuildRender()
        };
    }

    private Command BuildInit()
    {
        var command = new Command("init", "Инициализировать репозиторий.");
        command.SetHandler(async context =>
        {
            var result = await Execute(context, "utep init", () =>
            {
                var repoRoot = Directory.GetCurrentDirectory();
                var paths = new RepoPaths(repoRoot);

                Directory.CreateDirectory(paths.GoalsDir);
                Directory.CreateDirectory(Path.GetDirectoryName(paths.ContextFile)!);

                var created = new List<string>();

                if (!File.Exists(paths.ConfigFile))
                {
                    _services.Store.WriteFileAtomic(paths.ConfigFile, DefaultConfig());
                    created.Add("utep.config.json");
                }

                if (!File.Exists(paths.ContextFile))
                {
                    _services.Store.WriteFileAtomic(paths.ContextFile, new ContextFile { CurrentGoalId = null });
                    created.Add(".utep/context.json");
                }

                return Task.FromResult(new CommandResult<InitResult>
                {
                    Ok = true,
                    Result = new InitResult { Created = created, RepoRoot = repoRoot },
                    ExitCode = ExitCodes.Success
                });
            });

            WriteResult(context, result);
        });

        return command;
    }

    private Command BuildGoalCommand()
    {
        var goal = new Command("goal", "Команды для целей.");

        var newGoal = new Command("new", "Создать цель.");
        var titleOption = new Option<string>("--title") { IsRequired = true };
        newGoal.AddOption(titleOption);
        newGoal.SetHandler(async (InvocationContext context) =>
        {
            var title = context.ParseResult.GetValueForOption(titleOption) ?? string.Empty;
            var result = await Execute(context, "utep goal new", () =>
            {
                var repoRoot = RequireRepoRoot(context);
                if (repoRoot == null)
                {
                    return Task.FromResult(RepoNotInitialized<GoalNewResult>());
                }

                var paths = new RepoPaths(repoRoot);
                var goalId = _services.IdGenerator.NextGoalId(paths.GoalsDir, _services.Clock.Now);
                var goalDir = paths.GoalDir(goalId);
                Directory.CreateDirectory(goalDir);
                Directory.CreateDirectory(paths.TasksDir(goalId));
                Directory.CreateDirectory(paths.LogsDir(goalId));
                Directory.CreateDirectory(paths.ArtifactsDir(goalId));

                var now = _services.Clock.Now.ToString("o");
                var goalFile = new GoalFile
                {
                    Version = 1,
                    Goal = new GoalData
                    {
                        Id = goalId,
                        Title = title,
                        Status = GoalStatus.Planned,
                        CreatedAt = now,
                        UpdatedAt = now,
                        SuccessCriteria = new List<string>(),
                        NextTaskId = null
                    },
                    Meta = new GoalMeta()
                };

                _services.Store.WriteFileAtomic(paths.GoalFile(goalId), goalFile);
                File.WriteAllText(paths.LogFile(goalId), string.Empty);
                File.WriteAllText(paths.IndexFile(goalId, DefaultConfig().Render.IndexFilename), string.Empty);

                return Task.FromResult(new CommandResult<GoalNewResult>
                {
                    Ok = true,
                    GoalId = goalId,
                    Result = new GoalNewResult
                    {
                        Goal = new GoalCreated
                        {
                            GoalId = goalId,
                            Title = title,
                            Status = GoalStatus.Planned,
                            File = paths.GoalFile(goalId).Replace('\\', '/'),
                            IndexFile = paths.IndexFile(goalId, DefaultConfig().Render.IndexFilename).Replace('\\', '/')
                        }
                    },
                    ExitCode = ExitCodes.Success
                });
            });

            WriteResult(context, result);
        });

        var open = new Command("open", "Открыть цель.");
        var goalIdArg = new Argument<string>("goal_id");
        open.AddArgument(goalIdArg);
        open.SetHandler(async (InvocationContext context) =>
        {
            var goalId = context.ParseResult.GetValueForArgument(goalIdArg);
            var result = await Execute(context, "utep goal open", () =>
            {
                var repoRoot = RequireRepoRoot(context);
                if (repoRoot == null)
                {
                    return Task.FromResult(RepoNotInitialized<GoalOpenResult>());
                }

                var paths = new RepoPaths(repoRoot);
                if (!File.Exists(paths.GoalFile(goalId)))
                {
                    return Task.FromResult(NotFound<GoalOpenResult>("goal", goalId, paths.GoalFile(goalId)));
                }

                _services.Store.WriteFileAtomic(paths.ContextFile, new ContextFile { CurrentGoalId = goalId });

                return Task.FromResult(new CommandResult<GoalOpenResult>
                {
                    Ok = true,
                    GoalId = goalId,
                    Result = new GoalOpenResult
                    {
                        GoalId = goalId,
                        ContextFile = paths.ContextFile.Replace('\\', '/')
                    },
                    ExitCode = ExitCodes.Success
                });
            });

            WriteResult(context, result);
        });

        var status = new Command("status", "Статус цели.");
        status.AddOption(_goalOption);
        status.SetHandler(async (InvocationContext context) =>
        {
            var goalId = context.ParseResult.GetValueForOption(_goalOption);
            var result = await Execute(context, "utep goal status", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    var counts = snapshot.Tasks.Values
                        .GroupBy(info => info.File.Task.Status)
                        .ToDictionary(group => group.Key.ToString(), group => group.Count());

                    foreach (TaskStatus statusValue in Enum.GetValues<TaskStatus>())
                    {
                        if (!counts.ContainsKey(statusValue.ToString()))
                        {
                            counts[statusValue.ToString()] = 0;
                        }
                    }

                    var summary = new GoalSummary
                    {
                        GoalId = snapshot.Goal.Goal.Id,
                        Title = snapshot.Goal.Goal.Title,
                        Status = snapshot.Goal.Goal.Status,
                        Counts = counts,
                        NextTaskId = snapshot.Goal.Goal.NextTaskId,
                        RepoPath = $"goals/{snapshot.Goal.Goal.Id}/"
                    };

                    return new CommandResult<GoalSummary>
                    {
                        Ok = true,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = summary,
                        ExitCode = ExitCodes.Success
                    };
                });
            });

            WriteResult(context, result);
        });

        var tree = new Command("tree", "Дерево цели.");
        tree.AddOption(_goalOption);
        tree.SetHandler(async (InvocationContext context) =>
        {
            var goalId = context.ParseResult.GetValueForOption(_goalOption);
            var result = await Execute(context, "utep goal tree", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    var graphBuilder = _services.GraphBuilder;
                    var computedBuilder = _services.ComputedBuilder;
                    var depths = graphBuilder.BuildDepths(snapshot.Tasks);
                    var childrenMap = graphBuilder.BuildChildrenMap(snapshot.Tasks);
                    var blocksCount = graphBuilder.BuildBlocksCount(snapshot.Tasks);

                    var nodes = snapshot.Tasks.Values
                        .Select(info =>
                        {
                            var depth = depths.TryGetValue(info.File.Task.Id, out var depthValue) ? depthValue : 0;
                            return new TreeNode
                            {
                                Task = new TaskRef
                                {
                                    TaskId = info.File.Task.Id,
                                    Title = info.File.Task.Title,
                                    Status = info.File.Task.Status,
                                    Priority = info.File.Task.Priority,
                                    Depth = depth,
                                    File = info.Path.Replace('\\', '/')
                                },
                                Computed = computedBuilder.Build(info.File, snapshot.Tasks, blocksCount),
                                Children = childrenMap.TryGetValue(info.File.Task.Id, out var list) ? list : new List<string>()
                            };
                        })
                        .OrderBy(node => node.Task.Depth)
                        .ThenBy(node => node.Task.Priority)
                        .ThenBy(node => node.Task.TaskId, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var roots = snapshot.Tasks.Values
                        .Where(info => string.IsNullOrWhiteSpace(info.File.Task.ParentId) || !snapshot.Tasks.ContainsKey(info.File.Task.ParentId))
                        .Select(info => info.File.Task.Id)
                        .OrderBy(id => depths.TryGetValue(id, out var depth) ? depth : 0)
                        .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    return new CommandResult<GoalTreeResult>
                    {
                        Ok = true,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = new GoalTreeResult
                        {
                            Goal = new GoalRef
                            {
                                GoalId = snapshot.Goal.Goal.Id,
                                Title = snapshot.Goal.Goal.Title
                            },
                            Nodes = nodes,
                            Roots = roots
                        },
                        ExitCode = ExitCodes.Success
                    };
                });
            });

            WriteResult(context, result);
        });

        goal.AddCommand(newGoal);
        goal.AddCommand(open);
        goal.AddCommand(status);
        goal.AddCommand(tree);

        return goal;
    }
    private Command BuildTaskCommand()
    {
        var task = new Command("task", "Команды для задач.");

        var newTask = new Command("new", "Создать задачу.");
        var titleOption = new Option<string>("--title") { IsRequired = true };
        var statusOption = new Option<TaskStatus>("--status", () => TaskStatus.Planned);
        var parentOption = new Option<string?>("--parent");
        var priorityOption = new Option<int>("--priority", () => 3);
        var riskOption = new Option<string>("--risk", () => "Med");
        var costOption = new Option<int>("--cost", () => 0);
        var confidenceOption = new Option<double>("--confidence", () => 0.5);
        var idOption = new Option<string?>("--id");

        newTask.AddOption(titleOption);
        newTask.AddOption(statusOption);
        newTask.AddOption(parentOption);
        newTask.AddOption(priorityOption);
        newTask.AddOption(riskOption);
        newTask.AddOption(costOption);
        newTask.AddOption(confidenceOption);
        newTask.AddOption(_goalOption);
        newTask.AddOption(idOption);

        newTask.SetHandler(async (InvocationContext context) =>
        {
            var title = context.ParseResult.GetValueForOption(titleOption) ?? string.Empty;
            var status = context.ParseResult.GetValueForOption(statusOption);
            var parentId = context.ParseResult.GetValueForOption(parentOption);
            var priority = context.ParseResult.GetValueForOption(priorityOption);
            var risk = context.ParseResult.GetValueForOption(riskOption) ?? "Med";
            var cost = context.ParseResult.GetValueForOption(costOption);
            var confidence = context.ParseResult.GetValueForOption(confidenceOption);
            var goalId = context.ParseResult.GetValueForOption(_goalOption);
            var explicitId = context.ParseResult.GetValueForOption(idOption);

            var result = await Execute(context, "utep task new", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    var paths = new RepoPaths(RequireRepoRoot(context)!);
                    var taskId = string.IsNullOrWhiteSpace(explicitId)
                        ? _services.IdGenerator.NextTaskId(paths.TasksDir(snapshot.Goal.Goal.Id))
                        : explicitId!;

                    var taskFile = new TaskFile
                    {
                        Version = 1,
                        Task = new TaskData
                        {
                            Id = taskId,
                            GoalId = snapshot.Goal.Goal.Id,
                            ParentId = parentId,
                            Title = title,
                            Status = status,
                            Priority = priority,
                            Risk = risk,
                            CostEstimateMinutes = cost,
                            SuccessCriteria = new List<string>(),
                            Confidence = confidence,
                            Dependencies = new TaskDependencies { BlockedBy = new List<string>() },
                            Assumptions = new List<Assumption>(),
                            OpenQuestions = new List<OpenQuestion>(),
                            Attempts = 0,
                            TimeSpentMinutes = 0,
                            Evidence = new List<Evidence>()
                        },
                        Links = new TaskLinks()
                    };

                    var filePath = Path.Combine(paths.TasksDir(snapshot.Goal.Goal.Id), $"{taskId}.task.json");
                    _services.Store.WriteFileAtomic(filePath, taskFile);

                    return new CommandResult<TaskNewResult>
                    {
                        Ok = true,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = new TaskNewResult
                        {
                            Task = new TaskCreated
                            {
                                TaskId = taskId,
                                Title = title,
                                Status = status,
                                ParentId = parentId,
                                GoalId = snapshot.Goal.Goal.Id,
                                File = filePath.Replace('\\', '/')
                            }
                        },
                        ExitCode = ExitCodes.Success
                    };
                });
            });

            WriteResult(context, result);
        });

        var show = new Command("show", "Показать задачу.");
        var taskIdArg = new Argument<string>("task_id");
        show.AddArgument(taskIdArg);
        show.AddOption(_goalOption);
        show.SetHandler(async (InvocationContext context) =>
        {
            var taskId = context.ParseResult.GetValueForArgument(taskIdArg);
            var goalId = context.ParseResult.GetValueForOption(_goalOption);
            var result = await Execute(context, "utep task show", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    if (!snapshot.Tasks.TryGetValue(taskId, out var info))
                    {
                        return NotFound<TaskShowResult>("task", taskId, $"goals/{snapshot.Goal.Goal.Id}/tasks/{taskId}.task.json");
                    }

                    var graphBuilder = _services.GraphBuilder;
                    var blocksCount = graphBuilder.BuildBlocksCount(snapshot.Tasks);
                    var computed = _services.ComputedBuilder.Build(info.File, snapshot.Tasks, blocksCount);
                    var childrenMap = graphBuilder.BuildChildrenMap(snapshot.Tasks);
                    var blocks = snapshot.Tasks.Values
                        .Where(child => child.File.Task.Dependencies.BlockedBy.Contains(taskId, StringComparer.OrdinalIgnoreCase))
                        .Select(child => child.File.Task.Id)
                        .ToList();

                    var result = new TaskShowResult
                    {
                        Task = info.File,
                        Computed = computed,
                        Relations = new TaskRelations
                        {
                            Children = childrenMap.TryGetValue(taskId, out var list) ? list : new List<string>(),
                            Parent = info.File.Task.ParentId,
                            Blocks = blocks
                        }
                    };

                    return new CommandResult<TaskShowResult>
                    {
                        Ok = true,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = result,
                        ExitCode = ExitCodes.Success
                    };
                });
            });

            WriteResult(context, result);
        });

        var setStatus = new Command("set-status", "Установить статус задачи.");
        var statusArg = new Argument<TaskStatus>("status");
        var noteOption = new Option<string?>("--note");
        setStatus.AddArgument(taskIdArg);
        setStatus.AddArgument(statusArg);
        setStatus.AddOption(noteOption);
        setStatus.AddOption(_goalOption);
        setStatus.SetHandler(async (InvocationContext context) =>
        {
            var taskId = context.ParseResult.GetValueForArgument(taskIdArg);
            var statusValue = context.ParseResult.GetValueForArgument(statusArg);
            var note = context.ParseResult.GetValueForOption(noteOption);
            var goalId = context.ParseResult.GetValueForOption(_goalOption);

            var result = await Execute(context, "utep task set-status", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    if (!snapshot.Tasks.TryGetValue(taskId, out var info))
                    {
                        return NotFound<TaskSetStatusResult>("task", taskId, $"goals/{snapshot.Goal.Goal.Id}/tasks/{taskId}.task.json");
                    }

                    var from = info.File.Task.Status;
                    if (!StatusRules.CanTransition(from, statusValue) && from != statusValue)
                    {
                        return InvalidTransition<TaskSetStatusResult>(taskId, from, statusValue);
                    }

                    info.File.Task.Status = statusValue;
                    _services.Store.WriteFileAtomic(info.Path, info.File);
                    var logId = _services.LogWriter.AppendStatusChange(
                        new RepoPaths(RequireRepoRoot(context)!).LogFile(snapshot.Goal.Goal.Id),
                        snapshot.Goal.Goal.Id,
                        taskId,
                        from,
                        statusValue,
                        note);

                    return new CommandResult<TaskSetStatusResult>
                    {
                        Ok = true,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = new TaskSetStatusResult
                        {
                            TaskId = taskId,
                            From = from,
                            To = statusValue,
                            Note = note,
                            LogEventId = logId,
                            Rendered = false
                        },
                        ExitCode = ExitCodes.Success
                    };
                });
            });

            WriteResult(context, result);
        });

        var start = new Command("start", "Начать задачу.");
        start.AddArgument(taskIdArg);
        start.AddOption(_goalOption);
        start.SetHandler(async (InvocationContext context) =>
        {
            var taskId = context.ParseResult.GetValueForArgument(taskIdArg);
            var goalId = context.ParseResult.GetValueForOption(_goalOption);

            var result = await Execute(context, "utep task start", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    if (!snapshot.Tasks.TryGetValue(taskId, out var info))
                    {
                        return NotFound<TaskStartResult>("task", taskId, $"goals/{snapshot.Goal.Goal.Id}/tasks/{taskId}.task.json");
                    }

                    var blocksCount = _services.GraphBuilder.BuildBlocksCount(snapshot.Tasks);
                    var computed = _services.ComputedBuilder.Build(info.File, snapshot.Tasks, blocksCount);
                    if (computed.EffectiveState != "Actionable")
                    {
                        return NotActionable<TaskStartResult>(taskId, computed.WaitingDependencies);
                    }

                    var from = info.File.Task.Status;
                    if (from != TaskStatus.Ready)
                    {
                        return InvalidTransition<TaskStartResult>(taskId, from, TaskStatus.InProgress);
                    }

                    info.File.Task.Status = TaskStatus.InProgress;
                    _services.Store.WriteFileAtomic(info.Path, info.File);
                    _services.LogWriter.AppendStatusChange(
                        new RepoPaths(RequireRepoRoot(context)!).LogFile(snapshot.Goal.Goal.Id),
                        snapshot.Goal.Goal.Id,
                        taskId,
                        from,
                        TaskStatus.InProgress,
                        "start");

                    return new CommandResult<TaskStartResult>
                    {
                        Ok = true,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = new TaskStartResult
                        {
                            TaskId = taskId,
                            From = from,
                            To = TaskStatus.InProgress,
                            AttemptSession = new AttemptSession
                            {
                                StartedAt = _services.Clock.Now.ToString("o"),
                                AttemptsBefore = info.File.Task.Attempts
                            },
                            Rendered = false
                        },
                        ExitCode = ExitCodes.Success
                    };
                });
            });

            WriteResult(context, result);
        });
        var attempt = new Command("attempt", "Зафиксировать попытку.");
        var noteOptionAttempt = new Option<string>("--note") { IsRequired = true };
        var minutesOption = new Option<int?>("--minutes");
        var evidenceFileOption = new Option<string?>("--evidence-file");
        attempt.AddArgument(taskIdArg);
        attempt.AddOption(noteOptionAttempt);
        attempt.AddOption(minutesOption);
        attempt.AddOption(evidenceFileOption);
        attempt.AddOption(_goalOption);
        attempt.SetHandler(async (InvocationContext context) =>
        {
            var taskId = context.ParseResult.GetValueForArgument(taskIdArg);
            var note = context.ParseResult.GetValueForOption(noteOptionAttempt) ?? string.Empty;
            var minutes = context.ParseResult.GetValueForOption(minutesOption);
            var evidenceFile = context.ParseResult.GetValueForOption(evidenceFileOption);
            var goalId = context.ParseResult.GetValueForOption(_goalOption);

            var result = await Execute(context, "utep task attempt", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    if (!snapshot.Tasks.TryGetValue(taskId, out var info))
                    {
                        return NotFound<TaskAttemptResult>("task", taskId, $"goals/{snapshot.Goal.Goal.Id}/tasks/{taskId}.task.json");
                    }

                    var beforeAttempts = info.File.Task.Attempts;
                    var beforeMinutes = info.File.Task.TimeSpentMinutes;

                    info.File.Task.Attempts += 1;
                    if (minutes.HasValue)
                    {
                        info.File.Task.TimeSpentMinutes += minutes.Value;
                    }

                    var evidence = new Evidence
                    {
                        Kind = "note",
                        Text = note,
                        At = _services.Clock.Now.ToString("o")
                    };

                    if (!string.IsNullOrWhiteSpace(evidenceFile) && File.Exists(evidenceFile))
                    {
                        evidence.Text = File.ReadAllText(evidenceFile);
                    }

                    info.File.Task.Evidence.Add(evidence);
                    _services.Store.WriteFileAtomic(info.Path, info.File);

                    _services.LogWriter.AppendAttempt(
                        new RepoPaths(RequireRepoRoot(context)!).LogFile(snapshot.Goal.Goal.Id),
                        snapshot.Goal.Goal.Id,
                        taskId,
                        note);

                    return new CommandResult<TaskAttemptResult>
                    {
                        Ok = true,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = new TaskAttemptResult
                        {
                            TaskId = taskId,
                            Attempts = new AttemptCount { Before = beforeAttempts, After = info.File.Task.Attempts },
                            TimeSpentMinutes = new TimeSpent { Before = beforeMinutes, After = info.File.Task.TimeSpentMinutes },
                            EvidenceAdded = new List<Evidence> { evidence },
                            Rendered = false
                        },
                        ExitCode = ExitCodes.Success
                    };
                });
            });

            WriteResult(context, result);
        });

        var complete = new Command("complete", "Завершить задачу.");
        var evidenceOption = new Option<string>("--evidence") { IsRequired = true };
        complete.AddArgument(taskIdArg);
        complete.AddOption(evidenceOption);
        complete.AddOption(_goalOption);
        complete.SetHandler(async (InvocationContext context) =>
        {
            var taskId = context.ParseResult.GetValueForArgument(taskIdArg);
            var evidenceText = context.ParseResult.GetValueForOption(evidenceOption) ?? string.Empty;
            var goalId = context.ParseResult.GetValueForOption(_goalOption);

            var result = await Execute(context, "utep task complete", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    if (!snapshot.Tasks.TryGetValue(taskId, out var info))
                    {
                        return NotFound<TaskCompleteResult>("task", taskId, $"goals/{snapshot.Goal.Goal.Id}/tasks/{taskId}.task.json");
                    }

                    if (info.File.Task.SuccessCriteria.Count == 0)
                    {
                        return CompletionRequirementsMissing<TaskCompleteResult>(taskId, "success_criteria empty");
                    }

                    var evidence = new Evidence
                    {
                        Kind = "completion",
                        Text = evidenceText,
                        At = _services.Clock.Now.ToString("o")
                    };
                    info.File.Task.Evidence.Add(evidence);

                    var from = info.File.Task.Status;
                    if (!StatusRules.CanTransition(from, TaskStatus.Completed) && from != TaskStatus.Completed)
                    {
                        return InvalidTransition<TaskCompleteResult>(taskId, from, TaskStatus.Completed);
                    }

                    info.File.Task.Status = TaskStatus.Completed;
                    _services.Store.WriteFileAtomic(info.Path, info.File);
                    _services.LogWriter.AppendStatusChange(
                        new RepoPaths(RequireRepoRoot(context)!).LogFile(snapshot.Goal.Goal.Id),
                        snapshot.Goal.Goal.Id,
                        taskId,
                        from,
                        TaskStatus.Completed,
                        "complete");

                    var parentCheck = RunParentCheck(snapshot, info.File.Task.ParentId);

                    return new CommandResult<TaskCompleteResult>
                    {
                        Ok = true,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = new TaskCompleteResult
                        {
                            TaskId = taskId,
                            From = from,
                            To = TaskStatus.Completed,
                            EvidenceAdded = new List<Evidence> { evidence },
                            ParentCheck = parentCheck,
                            Rendered = false
                        },
                        ExitCode = ExitCodes.Success
                    };
                });
            });

            WriteResult(context, result);
        });

        var invalidate = new Command("invalidate", "Инвалидировать задачу.");
        var reasonOption = new Option<string>("--reason") { IsRequired = true };
        invalidate.AddArgument(taskIdArg);
        invalidate.AddOption(reasonOption);
        invalidate.AddOption(_goalOption);
        invalidate.SetHandler(async (InvocationContext context) =>
        {
            var taskId = context.ParseResult.GetValueForArgument(taskIdArg);
            var reason = context.ParseResult.GetValueForOption(reasonOption) ?? string.Empty;
            var goalId = context.ParseResult.GetValueForOption(_goalOption);

            var result = await Execute(context, "utep task invalidate", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    return UpdateTerminalStatus(context, snapshot, taskId, TaskStatus.Invalidated, reason);
                });
            });

            WriteResult(context, result);
        });

        var cancel = new Command("cancel", "Отменить задачу.");
        cancel.AddArgument(taskIdArg);
        cancel.AddOption(reasonOption);
        cancel.AddOption(_goalOption);
        cancel.SetHandler(async (InvocationContext context) =>
        {
            var taskId = context.ParseResult.GetValueForArgument(taskIdArg);
            var reason = context.ParseResult.GetValueForOption(reasonOption) ?? string.Empty;
            var goalId = context.ParseResult.GetValueForOption(_goalOption);

            var result = await Execute(context, "utep task cancel", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    return UpdateTerminalStatus(context, snapshot, taskId, TaskStatus.Cancelled, reason);
                });
            });

            WriteResult(context, result);
        });

        var block = new Command("block", "Заблокировать задачу.");
        var questionOption = new Option<string>("--question-file") { IsRequired = true };
        block.AddArgument(taskIdArg);
        block.AddOption(questionOption);
        block.AddOption(_goalOption);
        block.SetHandler(async (InvocationContext context) =>
        {
            var taskId = context.ParseResult.GetValueForArgument(taskIdArg);
            var questionFile = context.ParseResult.GetValueForOption(questionOption) ?? string.Empty;
            var goalId = context.ParseResult.GetValueForOption(_goalOption);

            var result = await Execute(context, "utep task block", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    if (!snapshot.Tasks.TryGetValue(taskId, out var info))
                    {
                        return NotFound<TaskBlockResult>("task", taskId, $"goals/{snapshot.Goal.Goal.Id}/tasks/{taskId}.task.json");
                    }

                    var question = _services.Store.ReadFile<OpenQuestion>(questionFile, out var error);
                    if (question == null)
                    {
                        return QuestionParseError<TaskBlockResult>(questionFile, error);
                    }

                    if (string.IsNullOrWhiteSpace(question.Id))
                    {
                        question.Id = "Q-01";
                    }

                    info.File.Task.OpenQuestions.Add(question);
                    var from = info.File.Task.Status;
                    info.File.Task.Status = TaskStatus.Blocked;
                    _services.Store.WriteFileAtomic(info.Path, info.File);

                    _services.LogWriter.AppendStatusChange(
                        new RepoPaths(RequireRepoRoot(context)!).LogFile(snapshot.Goal.Goal.Id),
                        snapshot.Goal.Goal.Id,
                        taskId,
                        from,
                        TaskStatus.Blocked,
                        "block");

                    return new CommandResult<TaskBlockResult>
                    {
                        Ok = true,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = new TaskBlockResult
                        {
                            TaskId = taskId,
                            From = from,
                            To = TaskStatus.Blocked,
                            QuestionImported = new QuestionImported
                            {
                                File = questionFile.Replace('\\', '/'),
                                OpenQuestionId = question.Id,
                                Kind = question.Kind
                            },
                            Rendered = false
                        },
                        ExitCode = ExitCodes.Success
                    };
                });
            });

            WriteResult(context, result);
        });

        var dep = new Command("dep", "Управление зависимостями.");
        var depAdd = new Command("add", "Добавить зависимость.");
        var depRm = new Command("rm", "Удалить зависимость.");
        var blockedByOption = new Option<string>("--blocked-by") { IsRequired = true };

        depAdd.AddArgument(taskIdArg);
        depAdd.AddOption(blockedByOption);
        depAdd.AddOption(_goalOption);
        depAdd.SetHandler(async (InvocationContext context) =>
        {
            var taskId = context.ParseResult.GetValueForArgument(taskIdArg);
            var blockedBy = context.ParseResult.GetValueForOption(blockedByOption) ?? string.Empty;
            var goalId = context.ParseResult.GetValueForOption(_goalOption);

            var result = await Execute(context, "utep task dep add", async () =>
            {
                return await UpdateDependency(context, goalId, taskId, blockedBy, true);
            });

            WriteResult(context, result);
        });

        depRm.AddArgument(taskIdArg);
        depRm.AddOption(blockedByOption);
        depRm.AddOption(_goalOption);
        depRm.SetHandler(async (InvocationContext context) =>
        {
            var taskId = context.ParseResult.GetValueForArgument(taskIdArg);
            var blockedBy = context.ParseResult.GetValueForOption(blockedByOption) ?? string.Empty;
            var goalId = context.ParseResult.GetValueForOption(_goalOption);

            var result = await Execute(context, "utep task dep rm", async () =>
            {
                return await UpdateDependency(context, goalId, taskId, blockedBy, false);
            });

            WriteResult(context, result);
        });

        dep.AddCommand(depAdd);
        dep.AddCommand(depRm);

        var deps = new Command("deps", "Показать зависимости.");
        deps.AddArgument(taskIdArg);
        deps.AddOption(_goalOption);
        deps.SetHandler(async (InvocationContext context) =>
        {
            var taskId = context.ParseResult.GetValueForArgument(taskIdArg);
            var goalId = context.ParseResult.GetValueForOption(_goalOption);

            var result = await Execute(context, "utep task deps", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    if (!snapshot.Tasks.TryGetValue(taskId, out var info))
                    {
                        return NotFound<TaskDepsResult>("task", taskId, $"goals/{snapshot.Goal.Goal.Id}/tasks/{taskId}.task.json");
                    }

                    var blocks = snapshot.Tasks.Values
                        .Where(child => child.File.Task.Dependencies.BlockedBy.Contains(taskId, StringComparer.OrdinalIgnoreCase))
                        .Select(child => child.File.Task.Id)
                        .ToList();

                    return new CommandResult<TaskDepsResult>
                    {
                        Ok = true,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = new TaskDepsResult
                        {
                            TaskId = taskId,
                            BlockedBy = info.File.Task.Dependencies.BlockedBy,
                            Blocks = blocks
                        },
                        ExitCode = ExitCodes.Success
                    };
                });
            });

            WriteResult(context, result);
        });

        task.AddCommand(newTask);
        task.AddCommand(show);
        task.AddCommand(setStatus);
        task.AddCommand(start);
        task.AddCommand(attempt);
        task.AddCommand(complete);
        task.AddCommand(invalidate);
        task.AddCommand(cancel);
        task.AddCommand(block);
        task.AddCommand(dep);
        task.AddCommand(deps);

        return task;
    }
    private Command BuildNext()
    {
        var command = new Command("next", "Показать следующую задачу.");
        var countOption = new Option<int>("--count", () => 1);
        command.AddOption(countOption);
        command.AddOption(_goalOption);
        command.SetHandler(async (InvocationContext context) =>
        {
            var count = context.ParseResult.GetValueForOption(countOption);
            var goalId = context.ParseResult.GetValueForOption(_goalOption);
            var result = await Execute(context, "utep next", async () =>
            {
                return await WithGoalAllowDegraded(context, goalId, (snapshot, issues) =>
                {
                    var graphBuilder = _services.GraphBuilder;
                    var depths = graphBuilder.BuildDepths(snapshot.Tasks);
                    var blocksCount = graphBuilder.BuildBlocksCount(snapshot.Tasks);
                    var actionable = _services.NextSelector.SelectActionable(snapshot.Tasks, depths, blocksCount, count, _services.ComputedBuilder);

                    if (actionable.Count > 0)
                    {
                        return new CommandResult<NextResult>
                        {
                            Ok = true,
                            GoalId = snapshot.Goal.Goal.Id,
                            Result = new NextResult { Actionable = actionable.ToList(), Blocking = null },
                            Warnings = issues,
                            ExitCode = ExitCodes.Success
                        };
                    }

                    var blocking = BuildBlocking(snapshot, depths, blocksCount);
                    return new CommandResult<NextResult>
                    {
                        Ok = false,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = new NextResult { Actionable = new List<ActionableItem>(), Blocking = blocking },
                        Warnings = issues,
                        ExitCode = ExitCodes.NoActionable
                    };
                });
            });

            WriteResult(context, result);
        });

        return command;
    }

    private Command BuildBottlenecks()
    {
        var command = new Command("bottlenecks", "Топ блокеров.");
        var topOption = new Option<int>("--top", () => 5);
        command.AddOption(topOption);
        command.AddOption(_goalOption);
        command.SetHandler(async (InvocationContext context) =>
        {
            var top = context.ParseResult.GetValueForOption(topOption);
            var goalId = context.ParseResult.GetValueForOption(_goalOption);

            var result = await Execute(context, "utep bottlenecks", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    var depths = _services.GraphBuilder.BuildDepths(snapshot.Tasks);
                    var blocksCount = _services.GraphBuilder.BuildBlocksCount(snapshot.Tasks);
                    var items = _services.BottleneckAnalyzer.GetTop(snapshot.Tasks, depths, blocksCount, top, _services.ComputedBuilder);

                    return new CommandResult<BottlenecksResult>
                    {
                        Ok = true,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = new BottlenecksResult { Top = items.ToList() },
                        ExitCode = ExitCodes.Success
                    };
                });
            });

            WriteResult(context, result);
        });

        return command;
    }

    private Command BuildValidate()
    {
        var command = new Command("validate", "Валидация репозитория.");
        command.AddOption(_goalOption);
        command.SetHandler(async (InvocationContext context) =>
        {
            var goalId = context.ParseResult.GetValueForOption(_goalOption);
            var result = await Execute(context, "utep validate", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    var issues = _services.ValidationService.Validate(snapshot, RequireRepoRoot(context)!);
                    var summary = new ValidateSummary
                    {
                        Errors = issues.Count(issue => issue.Severity == "error"),
                        Warnings = issues.Count(issue => issue.Severity == "warning")
                    };

                    return new CommandResult<ValidateResult>
                    {
                        Ok = summary.Errors == 0,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = new ValidateResult
                        {
                            Summary = summary,
                            Issues = issues
                        },
                        ExitCode = summary.Errors == 0 ? ExitCodes.Success : ExitCodes.ValidationError,
                        Errors = issues.Where(issue => issue.Severity == "error").ToList(),
                        Warnings = issues.Where(issue => issue.Severity == "warning").ToList()
                    };
                });
            });

            WriteResult(context, result);
        });

        return command;
    }

    private Command BuildDoctor()
    {
        var command = new Command("doctor", "Диагностика и исправления.");
        var fixOption = new Option<bool>("--fix");
        command.AddOption(fixOption);
        command.AddOption(_goalOption);
        command.SetHandler(async (InvocationContext context) =>
        {
            var goalId = context.ParseResult.GetValueForOption(_goalOption);
            var fix = context.ParseResult.GetValueForOption(fixOption);
            var result = await Execute(context, "utep doctor", async () =>
            {
                return await WithGoal(context, goalId, snapshot =>
                {
                    var issues = _services.ValidationService.Validate(snapshot, RequireRepoRoot(context)!);
                    var doctorResult = _services.DoctorService.ApplyFixes(snapshot, new RepoPaths(RequireRepoRoot(context)!), issues, fix);

                    return new CommandResult<DoctorResult>
                    {
                        Ok = doctorResult.Summary.ErrorsAfter == 0,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = doctorResult,
                        ExitCode = doctorResult.Summary.ErrorsAfter == 0 ? ExitCodes.Success : ExitCodes.ValidationError,
                        Errors = doctorResult.RemainingIssues.Where(issue => issue.Severity == "error").ToList()
                    };
                });
            });

            WriteResult(context, result);
        });

        return command;
    }

    private Command BuildRender()
    {
        var command = new Command("render", "Сгенерировать index.md.");
        command.AddOption(_goalOption);
        command.SetHandler(async (InvocationContext context) =>
        {
            var goalId = context.ParseResult.GetValueForOption(_goalOption);
            var result = await Execute(context, "utep render", async () =>
            {
                return await WithGoalAllowDegraded(context, goalId, (snapshot, issues) =>
                {
                    var repoRoot = RequireRepoRoot(context)!;
                    var config = LoadConfig(repoRoot);
                    var paths = new RepoPaths(repoRoot);
                    var renderResult = _services.RenderService.Render(snapshot, paths, config.Render.IndexFilename);

                    return new CommandResult<RenderResult>
                    {
                        Ok = true,
                        GoalId = snapshot.Goal.Goal.Id,
                        Result = renderResult,
                        Warnings = issues,
                        ExitCode = ExitCodes.Success
                    };
                });
            });

            WriteResult(context, result);
        });

        return command;
    }


    private async Task<CommandResult<T>> WithGoal<T>(
        InvocationContext context,
        string? goalId,
        Func<TaskSnapshot, CommandResult<T>> handler)
    {
        var repoRoot = RequireRepoRoot(context);
        if (repoRoot == null)
        {
            return RepoNotInitialized<T>();
        }

        var resolvedGoalId = ResolveGoalId(repoRoot, goalId);
        if (string.IsNullOrWhiteSpace(resolvedGoalId))
        {
            return NotFound<T>("goal", "unknown", "context.json");
        }

        var issues = new List<ValidationIssue>();
        var snapshot = _services.RepositoryLoader.Load(new RepoPaths(repoRoot), resolvedGoalId, issues);
        if (issues.Count > 0)
        {
            return new CommandResult<T>
            {
                Ok = false,
                Errors = issues,
                ExitCode = ExitCodes.ValidationError
            };
        }

        return handler(snapshot);
    }

    private async Task<CommandResult<T>> WithGoalAllowDegraded<T>(
        InvocationContext context,
        string? goalId,
        Func<TaskSnapshot, List<ValidationIssue>, CommandResult<T>> handler)
    {
        var repoRoot = RequireRepoRoot(context);
        if (repoRoot == null)
        {
            return RepoNotInitialized<T>();
        }

        var resolvedGoalId = ResolveGoalId(repoRoot, goalId);
        if (string.IsNullOrWhiteSpace(resolvedGoalId))
        {
            return NotFound<T>("goal", "unknown", "context.json");
        }

        var issues = new List<ValidationIssue>();
        var snapshot = _services.RepositoryLoader.Load(new RepoPaths(repoRoot), resolvedGoalId, issues);
        return handler(snapshot, issues);
    }

    private async Task<CommandResult<T>> Execute<T>(InvocationContext context, string command, Func<Task<CommandResult<T>>> handler)
    {
        var result = await handler();
        result.Command = command;
        return result;
    }

    private void WriteResult<T>(InvocationContext context, CommandResult<T> result)
    {
        var json = context.ParseResult.GetValueForOption(_jsonOption);
        if (json)
        {
            var envelope = new Envelope<T>
            {
                Command = result.Command ?? (context.ParseResult.CommandResult.Command.Name == "root" ? "utep" : context.ParseResult.CommandResult.Command.Name),
                RepoRoot = RequireRepoRoot(context) ?? Directory.GetCurrentDirectory(),
                GoalId = result.GoalId,
                Ok = result.Ok,
                Result = result.Result,
                Warnings = result.Warnings,
                Errors = result.Errors,
                Meta = new EnvelopeMeta
                {
                    Timestamp = _services.Clock.Now.ToString("o"),
                    DurationMs = 0
                }
            };

            _output.WriteJson(envelope);
        }
        else
        {
            WriteHuman(result);
        }

        context.ExitCode = result.ExitCode;
    }

    private void WriteHuman<T>(CommandResult<T> result)
    {
        if (result.Ok)
        {
            AnsiConsole.MarkupLine("[green]OK[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Ошибка[/]");
            foreach (var error in result.Errors)
            {
                AnsiConsole.MarkupLine($"- {error.Code}: {error.Message}");
            }
        }
    }

    private CommandResult<TaskInvalidateResult> UpdateTerminalStatus(
        InvocationContext context,
        TaskSnapshot snapshot,
        string taskId,
        TaskStatus to,
        string reason)
    {
        if (!snapshot.Tasks.TryGetValue(taskId, out var info))
        {
            return NotFound<TaskInvalidateResult>("task", taskId, $"goals/{snapshot.Goal.Goal.Id}/tasks/{taskId}.task.json");
        }

        var from = info.File.Task.Status;
        if (!StatusRules.CanTransition(from, to) && from != to)
        {
            return InvalidTransition<TaskInvalidateResult>(taskId, from, to);
        }

        info.File.Task.Status = to;
        info.File.Task.Evidence.Add(new Evidence
        {
            Kind = "note",
            Text = reason,
            At = _services.Clock.Now.ToString("o")
        });

        _services.Store.WriteFileAtomic(info.Path, info.File);
        _services.LogWriter.AppendStatusChange(
            new RepoPaths(RequireRepoRoot(context)!).LogFile(snapshot.Goal.Goal.Id),
            snapshot.Goal.Goal.Id,
            taskId,
            from,
            to,
            reason);

        return new CommandResult<TaskInvalidateResult>
        {
            Ok = true,
            GoalId = snapshot.Goal.Goal.Id,
            Result = new TaskInvalidateResult
            {
                TaskId = taskId,
                From = from,
                To = to,
                Reason = reason,
                Rendered = false
            },
            ExitCode = ExitCodes.Success
        };
    }

    private Task<CommandResult<TaskDepChangeResult>> UpdateDependency(
        InvocationContext context,
        string? goalId,
        string taskId,
        string blockedBy,
        bool add)
    {
        return WithGoal(context, goalId, snapshot =>
        {
            if (!snapshot.Tasks.TryGetValue(taskId, out var info))
            {
                return NotFound<TaskDepChangeResult>("task", taskId, $"goals/{snapshot.Goal.Goal.Id}/tasks/{taskId}.task.json");
            }

            if (add)
            {
                if (!info.File.Task.Dependencies.BlockedBy.Contains(blockedBy, StringComparer.OrdinalIgnoreCase))
                {
                    info.File.Task.Dependencies.BlockedBy.Add(blockedBy);
                }
            }
            else
            {
                info.File.Task.Dependencies.BlockedBy.RemoveAll(id => string.Equals(id, blockedBy, StringComparison.OrdinalIgnoreCase));
            }

            if (add)
            {
                var cycleIssues = _services.ValidationService.Validate(snapshot, RequireRepoRoot(context)!)
                    .Where(issue => issue.Code == "E003")
                    .ToList();
                if (cycleIssues.Count > 0)
                {
                    return new CommandResult<TaskDepChangeResult>
                    {
                        Ok = false,
                        Errors = cycleIssues,
                        ExitCode = ExitCodes.ValidationError
                    };
                }
            }

            _services.Store.WriteFileAtomic(info.Path, info.File);

            return new CommandResult<TaskDepChangeResult>
            {
                Ok = true,
                GoalId = snapshot.Goal.Goal.Id,
                Result = new TaskDepChangeResult
                {
                    TaskId = taskId,
                    Change = add ? "added" : "removed",
                    BlockedBy = info.File.Task.Dependencies.BlockedBy,
                    Rendered = false
                },
                ExitCode = ExitCodes.Success
            };
        });
    }

    private ParentCheck RunParentCheck(TaskSnapshot snapshot, string? parentId)
    {
        var result = new ParentCheck { Ran = false };
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return result;
        }

        if (!snapshot.Tasks.TryGetValue(parentId, out var parentInfo))
        {
            return result;
        }

        var children = snapshot.Tasks.Values.Where(info => info.File.Task.ParentId == parentId).ToList();
        if (children.Count == 0)
        {
            return result;
        }

        var allTerminal = children.All(info =>
            info.File.Task.Status is TaskStatus.Completed or TaskStatus.Cancelled or TaskStatus.Invalidated);

        if (allTerminal && parentInfo.File.Task.Status == TaskStatus.Planned)
        {
            var from = parentInfo.File.Task.Status;
            parentInfo.File.Task.Status = TaskStatus.Ready;
            _services.Store.WriteFileAtomic(parentInfo.Path, parentInfo.File);
            result.Ran = true;
            result.AffectedTasks.Add(new ParentStatusChange
            {
                TaskId = parentId,
                From = from,
                To = TaskStatus.Ready,
                Reason = "all children terminal"
            });
        }

        return result;
    }

    private NextBlocking BuildBlocking(TaskSnapshot snapshot, Dictionary<string, int> depths, Dictionary<string, int> blocksCount)
    {
        var computedBuilder = _services.ComputedBuilder;

        var blockedTask = snapshot.Tasks.Values
            .Select(info => new
            {
                Task = info,
                Computed = computedBuilder.Build(info.File, snapshot.Tasks, blocksCount)
            })
            .Where(item => item.Computed.EffectiveState == "WaitingUser" && item.Task.File.Task.OpenQuestions.Count > 0)
            .OrderBy(item => depths.TryGetValue(item.Task.File.Task.Id, out var depth) ? depth : 0)
            .ThenBy(item => item.Task.File.Task.Priority)
            .ThenBy(item => item.Task.File.Task.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (blockedTask != null)
        {
            var openQuestion = blockedTask.Task.File.Task.OpenQuestions.First();
            return new NextBlocking
            {
                Kind = "user",
                BlockedTask = new BlockedTaskInfo
                {
                    Task = new TaskRef
                    {
                        TaskId = blockedTask.Task.File.Task.Id,
                        Title = blockedTask.Task.File.Task.Title,
                        Status = blockedTask.Task.File.Task.Status,
                        Priority = blockedTask.Task.File.Task.Priority,
                        Depth = depths.TryGetValue(blockedTask.Task.File.Task.Id, out var depth) ? depth : 0,
                        File = blockedTask.Task.Path.Replace('\\', '/')
                    },
                    Computed = blockedTask.Computed
                },
                Question = new OpenQuestionInfo
                {
                    TaskId = blockedTask.Task.File.Task.Id,
                    OpenQuestionId = openQuestion.Id,
                    Kind = openQuestion.Kind,
                    Question = openQuestion.Question,
                    Options = openQuestion.Options.Select(option => new QuestionOptionShort
                    {
                        Id = option.Id,
                        Title = option.Title
                    }).ToList(),
                    Recommendation = openQuestion.Recommendation,
                    RequestedAnswer = openQuestion.RequestedAnswer
                }
            };
        }

        var bottlenecks = _services.BottleneckAnalyzer.GetTop(snapshot.Tasks, depths, blocksCount, 1, computedBuilder);
        var top = bottlenecks.FirstOrDefault();
        var waitingExamples = snapshot.Tasks.Values
            .Select(info => new
            {
                info.File.Task.Id,
                Computed = computedBuilder.Build(info.File, snapshot.Tasks, blocksCount)
            })
            .Where(item => item.Computed.EffectiveState == "WaitingDependencies")
            .Select(item => new WaitingExample
            {
                TaskId = item.Id,
                WaitingOn = item.Computed.WaitingDependencies
            })
            .Take(5)
            .ToList();

        return new NextBlocking
        {
            Kind = "dependencies",
            RecommendedBlocker = top == null
                ? null
                : new RecommendedBlocker
                {
                    Task = top.Task,
                    Computed = computedBuilder.Build(snapshot.Tasks[top.Task.TaskId].File, snapshot.Tasks, blocksCount),
                    BlocksCount = top.BlocksCount
                },
            WaitingExamples = waitingExamples
        };
    }

    private string? RequireRepoRoot(InvocationContext context)
    {
        var root = _services.RepoLocator.FindRoot(Directory.GetCurrentDirectory());
        return root;
    }

    private string? ResolveGoalId(string repoRoot, string? goalId)
    {
        if (!string.IsNullOrWhiteSpace(goalId))
        {
            return goalId;
        }

        var paths = new RepoPaths(repoRoot);
        if (!File.Exists(paths.ContextFile))
        {
            return null;
        }

        var context = _services.Store.ReadFile<ContextFile>(paths.ContextFile, out _);
        return context?.CurrentGoalId;
    }

    private UtepConfig LoadConfig(string repoRoot)
    {
        var paths = new RepoPaths(repoRoot);
        if (!File.Exists(paths.ConfigFile))
        {
            return DefaultConfig();
        }

        var config = _services.Store.ReadFile<UtepConfig>(paths.ConfigFile, out _);
        return config ?? DefaultConfig();
    }

    private static UtepConfig DefaultConfig()
    {
        return new UtepConfig
        {
            Version = 1,
            Limits = new LimitsConfig
            {
                AttemptLimit = 3,
                TimeLimitMinutes = 90,
                LargeTaskMinutes = 240
            },
            Thresholds = new ThresholdsConfig { ConfidenceMin = 0.7 },
            Render = new RenderConfig
            {
                Index = true,
                IndexFilename = "index.md"
            },
            Output = new OutputConfig { Default = "human" }
        };
    }

    private static CommandResult<T> RepoNotInitialized<T>()
    {
        return new CommandResult<T>
        {
            Ok = false,
            Errors = new List<ValidationIssue>
            {
                new ValidationIssue
                {
                    Code = "E450",
                    Severity = "error",
                    Message = "Repo not initialized"
                }
            },
            ExitCode = ExitCodes.NotFound
        };
    }

    private static CommandResult<T> NotFound<T>(string kind, string id, string path)
    {
        return new CommandResult<T>
        {
            Ok = false,
            Errors = new List<ValidationIssue>
            {
                new ValidationIssue
                {
                    Code = "E440",
                    Severity = "error",
                    Message = $"{kind} not found",
                    Locations = new List<IssueLocation>
                    {
                        new IssueLocation { Kind = kind, Id = id, Path = path }
                    }
                }
            },
            ExitCode = ExitCodes.NotFound
        };
    }

    private static CommandResult<T> InvalidTransition<T>(string taskId, TaskStatus from, TaskStatus to)
    {
        return new CommandResult<T>
        {
            Ok = false,
            Errors = new List<ValidationIssue>
            {
                new ValidationIssue
                {
                    Code = "E400",
                    Severity = "error",
                    Message = "Invalid status transition",
                    Details = new Dictionary<string, object>
                    {
                        ["task_id"] = taskId,
                        ["from"] = from.ToString(),
                        ["to"] = to.ToString()
                    }
                }
            },
            ExitCode = ExitCodes.InvalidTransition
        };
    }

    private static CommandResult<T> NotActionable<T>(string taskId, List<string> waitingDependencies)
    {
        return new CommandResult<T>
        {
            Ok = false,
            Errors = new List<ValidationIssue>
            {
                new ValidationIssue
                {
                    Code = "E410",
                    Severity = "error",
                    Message = "Task is not actionable due to dependencies",
                    Details = new Dictionary<string, object>
                    {
                        ["task_id"] = taskId,
                        ["waiting_dependencies"] = waitingDependencies
                    },
                    Remedies = new List<Remedy>
                    {
                        new Remedy
                        {
                            Id = "R1",
                            Title = "Work on the blocker",
                            Commands = new List<string>
                            {
                                $"utep task show {waitingDependencies.FirstOrDefault() ?? "<task_id>"} --json",
                                "utep next --json"
                            }
                        }
                    }
                }
            },
            ExitCode = ExitCodes.InvalidTransition
        };
    }

    private static CommandResult<T> CompletionRequirementsMissing<T>(string taskId, string reason)
    {
        return new CommandResult<T>
        {
            Ok = false,
            Errors = new List<ValidationIssue>
            {
                new ValidationIssue
                {
                    Code = "E420",
                    Severity = "error",
                    Message = "Completion requirements not met",
                    Details = new Dictionary<string, object>
                    {
                        ["task_id"] = taskId,
                        ["reason"] = reason
                    }
                }
            },
            ExitCode = ExitCodes.ValidationError
        };
    }

    private static CommandResult<T> QuestionParseError<T>(string path, string? error)
    {
        return new CommandResult<T>
        {
            Ok = false,
            Errors = new List<ValidationIssue>
            {
                new ValidationIssue
                {
                    Code = "E430",
                    Severity = "error",
                    Message = "Question parse error",
                    Details = new Dictionary<string, object>
                    {
                        ["error"] = error ?? "Unknown error"
                    },
                    Locations = new List<IssueLocation>
                    {
                        new IssueLocation { Kind = "file", Path = path }
                    }
                }
            },
            ExitCode = ExitCodes.ValidationError
        };
    }
}
