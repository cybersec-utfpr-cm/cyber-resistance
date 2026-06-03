using Godot;
using System;
using System.Threading.Tasks;

public partial class MissionInfrastructureManager : Node
{
    public static MissionInfrastructureManager Instance { get; private set; }

    [Export] public string PlayerMachineContainerName = "player_machine";

    private DockerManager _docker;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        _docker = new DockerManager(PlayerMachineContainerName);

        if (QuestManager.Instance == null)
        {
            GD.PrintErr("MissionInfrastructureManager: QuestManager não encontrado.");
            return;
        }

        QuestManager.Instance.QuestStarted += OnQuestStarted;
        QuestManager.Instance.QuestCompleted += OnQuestCompleted;

        GD.Print("MissionInfrastructureManager: pronto.");
    }

    private async void OnQuestStarted(string questId)
    {
        try
        {
            var quest = QuestManager.Instance.GetQuestDefinition(questId);

            if (quest == null)
                return;

            if (quest.Network == null || quest.Machines == null || quest.Machines.Count == 0)
                return;

            await StartQuestInfrastructure(quest);
        }
        catch (Exception e)
        {
            GD.PrintErr($"MissionInfrastructureManager: erro ao iniciar infraestrutura da missão {questId}: {e.Message}");
        }
    }

    private async void OnQuestCompleted(string questId)
    {
        try
        {
            var quest = QuestManager.Instance.GetQuestDefinition(questId);

            if (quest == null)
                return;

            if (quest.Network == null || quest.Machines == null || quest.Machines.Count == 0)
                return;

            await StopQuestInfrastructure(quest);
        }
        catch (Exception e)
        {
            GD.PrintErr($"MissionInfrastructureManager: erro ao parar infraestrutura da missão {questId}: {e.Message}");
        }
    }

    private async Task StartQuestInfrastructure(QuestDefinition quest)
    {
        string networkName = quest.Network.Name;
        string driver = string.IsNullOrWhiteSpace(quest.Network.Driver) ? "bridge" : quest.Network.Driver;

        GD.Print($"MissionInfrastructureManager: criando rede {networkName}.");
        await _docker.EnsureNetworkExistsAsync(networkName, driver);

        GD.Print($"MissionInfrastructureManager: conectando player_machine na rede {networkName}.");
        await _docker.ConnectToNetworkAsync(networkName, PlayerMachineContainerName, "player");

        foreach (var machine in quest.Machines)
        {
            if (!machine.StartOnQuestStart)
                continue;

            GD.Print($"MissionInfrastructureManager: iniciando máquina {machine.Id} ({machine.ContainerName}).");

            await _docker.EnsureContainerRunningFromImageAsync(
                machine.ContainerName,
                machine.Image,
                machine.Hostname,
                networkName,
                machine.NetworkAlias
            );
        }
    }

    private async Task StopQuestInfrastructure(QuestDefinition quest)
    {
        foreach (var machine in quest.Machines)
        {
            if (!machine.StopOnQuestComplete)
                continue;

            GD.Print($"MissionInfrastructureManager: removendo máquina {machine.Id} ({machine.ContainerName}).");
            await _docker.StopAndRemoveContainerAsync(machine.ContainerName);
        }
    }

    public override void _ExitTree()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.QuestStarted -= OnQuestStarted;
            QuestManager.Instance.QuestCompleted -= OnQuestCompleted;
        }

        _docker?.Dispose();
    }
}