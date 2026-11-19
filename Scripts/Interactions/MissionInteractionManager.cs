using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;

public partial class MissionInteractionManager : Node
{
	private const string SubmissionUiPath =
		"res://Scenes/Interfaces/mission_submission_ui.tscn";

	private readonly CancellationTokenSource _lifetimeCancellation = new();
	private PackedScene _submissionUiScene;
	private MissionSubmissionUi _activeUi;
	private QuestDefinition _activeQuest;
	private string _activeNpcId = "";
	private InteractionMode _interactionMode;
	private string _pendingOfferQuestId = "";
	private string _pendingOfferNpcId = "";
	private string _pendingOfferDialogueId = "";
	private string _pendingSuccessQuestId = "";
	private string _pendingSuccessNpcId = "";
	private string _pendingSuccessDialogueId = "";
	private bool _isCompletingMission;

	public static MissionInteractionManager Instance { get; private set; }

	private enum InteractionMode
	{
		None,
		Acceptance,
		Submission
	}

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		_submissionUiScene = GD.Load<PackedScene>(SubmissionUiPath);
		if (_submissionUiScene == null)
		{
			GD.PrintErr(
				"MissionInteractionManager: interface de submissão não encontrada."
			);
		}

		if (DialogueManager.Instance != null)
		{
			DialogueManager.Instance.DialogueFinished += OnDialogueFinished;
		}
		else
		{
			GD.PrintErr(
				"MissionInteractionManager: DialogueManager não encontrado."
			);
		}
	}

	public override void _ExitTree()
	{
		_lifetimeCancellation.Cancel();
		CloseActiveUi();

		if (DialogueManager.Instance != null)
		{
			DialogueManager.Instance.DialogueFinished -= OnDialogueFinished;
		}

		if (Instance == this)
			Instance = null;
	}

	public bool TryHandleInteraction(string npcId)
	{
		if (
			string.IsNullOrWhiteSpace(npcId) ||
			QuestManager.Instance == null ||
			DialogueManager.Instance == null
		)
		{
			return false;
		}

		if (
			_activeUi != null ||
			!string.IsNullOrEmpty(_pendingOfferQuestId) ||
			!string.IsNullOrEmpty(_pendingSuccessQuestId) ||
			_isCompletingMission
		)
		{
			return true;
		}

		QuestDefinition availableQuest =
			QuestManager.Instance.GetAvailableQuestForNpc(npcId);
		if (availableQuest != null)
			return StartMissionOffer(npcId, availableQuest);

		QuestDefinition activeQuest =
			QuestManager.Instance.GetActiveQuestForNpc(npcId);
		if (activeQuest == null)
			return false;

		return ShowQuestion(
			npcId,
			activeQuest,
			InteractionMode.Submission,
			"Conseguiu a flag?"
		);
	}

	private bool StartMissionOffer(
		string npcId,
		QuestDefinition quest
	)
	{
		if (string.IsNullOrWhiteSpace(quest.OfferDialogueId))
		{
			GD.PrintErr(
				"MissionInteractionManager: missão disponível sem diálogo de oferta."
			);
			return false;
		}

		_pendingOfferQuestId = quest.Id;
		_pendingOfferNpcId = npcId;
		_pendingOfferDialogueId = quest.OfferDialogueId;

		if (
			DialogueManager.Instance.StartDialogue(
				npcId,
				quest.OfferDialogueId
			)
		)
		{
			return true;
		}

		ClearPendingOffer();
		return false;
	}

	private async void OnDialogueFinished(
		string npcId,
		string dialogueId
	)
	{
		if (
			npcId == _pendingOfferNpcId &&
			dialogueId == _pendingOfferDialogueId
		)
		{
			string questId = _pendingOfferQuestId;
			ClearPendingOffer();

			QuestDefinition availableQuest =
				QuestManager.Instance?.GetAvailableQuestForNpc(npcId);
			if (availableQuest?.Id == questId)
			{
				ShowQuestion(
					npcId,
					availableQuest,
					InteractionMode.Acceptance,
					"Aceitar este desafio?"
				);
			}

			return;
		}

		if (
			npcId != _pendingSuccessNpcId ||
			dialogueId != _pendingSuccessDialogueId
		)
		{
			return;
		}

		string completedQuestId = _pendingSuccessQuestId;
		ClearPendingSuccess();
		_isCompletingMission = true;
		try
		{
			await CompleteMissionAfterDialogueAsync(completedQuestId);
		}
		finally
		{
			_isCompletingMission = false;
		}
	}

	private bool ShowQuestion(
		string npcId,
		QuestDefinition quest,
		InteractionMode mode,
		string prompt
	)
	{
		if (_submissionUiScene == null)
			return false;

		MissionSubmissionUi ui =
			_submissionUiScene.Instantiate<MissionSubmissionUi>();
		GetTree().Root.AddChild(ui);

		_activeUi = ui;
		_activeQuest = quest;
		_activeNpcId = npcId;
		_interactionMode = mode;
		ui.AnswerSelected += OnAnswerSelected;
		ui.FlagSubmitted += OnFlagSubmitted;
		ui.Cancelled += OnCancelled;
		ui.ShowQuestion(quest.Title, prompt);
		return true;
	}

	private void OnAnswerSelected(bool answeredYes)
	{
		if (_activeUi == null || _activeQuest == null)
			return;

		if (!answeredYes)
		{
			CloseActiveUi();
			return;
		}

		if (_interactionMode == InteractionMode.Acceptance)
		{
			bool started = QuestManager.Instance?.StartAvailableQuestForNpc(
				_activeNpcId
			) ?? false;
			CloseActiveUi();

			if (!started)
			{
				GD.PrintErr(
					"MissionInteractionManager: a missão não pôde ser aceita."
				);
			}

			return;
		}

		if (_interactionMode == InteractionMode.Submission)
			_activeUi.ShowFlagInput();
	}

	private void OnFlagSubmitted(string candidate)
	{
		if (
			_activeUi == null ||
			_activeQuest == null ||
			_interactionMode != InteractionMode.Submission
		)
		{
			return;
		}

		if (
			SaveManager.Instance == null ||
			!SaveManager.Instance.TryGetMissionRuntimeState(
				_activeQuest.Id,
				out MissionRuntimeSaveData runtimeState
			)
		)
		{
			_activeUi.ShowSubmissionError(
				"Os dados da missão não estão disponíveis. Tente novamente."
			);
			return;
		}

		if (!MissionFlagService.Matches(runtimeState.FlagToken, candidate))
		{
			_activeUi.ShowSubmissionError(
				"Flag incorreta. Verifique o valor e tente novamente."
			);
			return;
		}

		QuestDefinition quest = _activeQuest;
		string npcId = _activeNpcId;
		CloseActiveUi();

		if (string.IsNullOrWhiteSpace(quest.SuccessDialogueId))
		{
			GD.PrintErr(
				"MissionInteractionManager: missão sem diálogo de sucesso."
			);
			return;
		}

		_pendingSuccessQuestId = quest.Id;
		_pendingSuccessNpcId = npcId;
		_pendingSuccessDialogueId = quest.SuccessDialogueId;

		if (
			DialogueManager.Instance.StartDialogue(
				npcId,
				quest.SuccessDialogueId
			)
		)
		{
			return;
		}

		ClearPendingSuccess();
		ShowQuestion(
			npcId,
			quest,
			InteractionMode.Submission,
			"Conseguiu a flag?"
		);
		_activeUi?.ShowFlagInput();
		_activeUi?.ShowSubmissionError(
			"Não foi possível iniciar o diálogo final. Tente novamente."
		);
	}

	private void OnCancelled()
	{
		CloseActiveUi();
	}

	private async Task CompleteMissionAfterDialogueAsync(string questId)
	{
		if (QuestManager.Instance?.IsQuestActive(questId) != true)
			return;

		try
		{
			if (
				MissionInfrastructureManager.Instance == null ||
				!await MissionInfrastructureManager.Instance.CompleteMissionAsync(
					questId,
					_lifetimeCancellation.Token
				)
			)
			{
				GD.PrintErr(
					"MissionInteractionManager: não foi possível finalizar a missão."
				);
				return;
			}

			if (QuestManager.Instance?.IsQuestActive(questId) == true)
				QuestManager.Instance.CompleteQuest(questId);
		}
		catch (OperationCanceledException)
		{
			// The game scene is leaving the tree.
		}
	}

	private void CloseActiveUi()
	{
		if (_activeUi != null)
		{
			_activeUi.AnswerSelected -= OnAnswerSelected;
			_activeUi.FlagSubmitted -= OnFlagSubmitted;
			_activeUi.Cancelled -= OnCancelled;
			_activeUi.CloseUi();
		}

		_activeUi = null;
		_activeQuest = null;
		_activeNpcId = "";
		_interactionMode = InteractionMode.None;
	}

	private void ClearPendingOffer()
	{
		_pendingOfferQuestId = "";
		_pendingOfferNpcId = "";
		_pendingOfferDialogueId = "";
	}

	private void ClearPendingSuccess()
	{
		_pendingSuccessQuestId = "";
		_pendingSuccessNpcId = "";
		_pendingSuccessDialogueId = "";
	}
}
