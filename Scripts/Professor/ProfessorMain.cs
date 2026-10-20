using Godot;
using System.Collections.Generic;
using System.Globalization;

/// Console do professor: conecta ao server central, lista as salas, exibe as
/// submissoes de minigame recebidas e devolve nota + comentario (MINIGAME_GRADE).
/// Port direto de professor_main.gd.
public partial class ProfessorMain : Control
{
	private const string MenuScene = "res://Scenes/Interfaces/main_menu.tscn";
	private const string QuizPath = "res://professor/data/minigame_quiz.json";

	private Button _backButton;
	private LineEdit _ipLine;
	private LineEdit _portLine;
	private Button _connectButton;
	private Label _statusLabel;
	private ItemList _roomList;
	private Button _refreshButton;
	private ItemList _submissionList;
	private Label _detailLabel;
	private RichTextLabel _detail;
	private SpinBox _gradeSpin;
	private LineEdit _commentLine;
	private Button _sendGradeButton;
	private ProfessorNetwork _network;

	// room_id -> RoomSummary (ultima atualizacao recebida)
	private readonly Dictionary<int, RoomSummary> _rooms = new();
	// Entradas achatadas de submissao.
	private class SubmissionEntry
	{
		public int RoomId;
		public int TeamIndex;
		public Godot.Collections.Dictionary Team;
	}
	private readonly List<SubmissionEntry> _entries = new();
	// -1 = sem filtro de sala
	private int _selectedRoom = -1;
	private readonly List<string> _questions = new();

	public override void _Ready()
	{
		_backButton = GetNode<Button>("Margin/Root/Header/BackButton");
		_ipLine = GetNode<LineEdit>("Margin/Root/ConnectionRow/IpLine");
		_portLine = GetNode<LineEdit>("Margin/Root/ConnectionRow/PortLine");
		_connectButton = GetNode<Button>("Margin/Root/ConnectionRow/ConnectButton");
		_statusLabel = GetNode<Label>("Margin/Root/ConnectionRow/StatusLabel");
		_roomList = GetNode<ItemList>("Margin/Root/Split/RoomsBox/RoomList");
		_refreshButton = GetNode<Button>("Margin/Root/Split/RoomsBox/RefreshButton");
		_submissionList = GetNode<ItemList>("Margin/Root/Split/SubmissionsBox/SubmissionList");
		_detailLabel = GetNode<Label>("Margin/Root/Split/SubmissionsBox/DetailLabel");
		_detail = GetNode<RichTextLabel>("Margin/Root/Split/SubmissionsBox/Detail");
		_gradeSpin = GetNode<SpinBox>("Margin/Root/Split/SubmissionsBox/GradeBox/GradeSpin");
		_commentLine = GetNode<LineEdit>("Margin/Root/Split/SubmissionsBox/GradeBox/CommentLine");
		_sendGradeButton = GetNode<Button>("Margin/Root/Split/SubmissionsBox/GradeBox/SendGradeButton");
		_network = GetNode<ProfessorNetwork>("Network");

		_ipLine.Text = "127.0.0.1";
		_portLine.Text = "42069";
		_questions.AddRange(LoadQuestions());

		_network.ConnectedToServer += OnConnected;
		_network.DisconnectedFromServer += OnDisconnected;
		_network.StatusChanged += OnStatus;
		_network.RoomsReceived += OnRooms;
		_network.SubmissionReceived += OnSubmission;

		_backButton.Pressed += OnBackPressed;
		_connectButton.Pressed += OnConnectPressed;
		_refreshButton.Pressed += OnRefreshPressed;
		_roomList.ItemSelected += OnRoomSelected;
		_submissionList.ItemSelected += OnSubmissionSelected;
		_sendGradeButton.Pressed += OnSendGradePressed;

		SetStatus("Desconectado. Informe o IP do servidor e conecte.");
		RebuildRoomList();
		RebuildSubmissionList();
		UpdateGradeControls();
	}

	private void OnBackPressed()
	{
		_network.DisconnectFromServer();
		GetTree().ChangeSceneToFile(MenuScene);
	}

	private void OnConnectPressed()
	{
		if (_network.IsConnectedToServer())
		{
			_network.DisconnectFromServer();
			return;
		}
		string ip = _ipLine.Text.Trim();
		if (ip.Length == 0)
		{
			SetStatus("Informe o IP do servidor");
			return;
		}
		string portText = _portLine.Text.Trim();
		if (
			!int.TryParse(portText, out int port)
			|| port < 1
			|| port > 65535
		)
		{
			SetStatus("Porta invalida (1-65535)");
			return;
		}
		// O botao volta a ficar ativo no callback de status (conexao e assincrona).
		_connectButton.Disabled = true;
		_ipLine.Editable = false;
		_portLine.Editable = false;

		_network.ConnectToServer(ip, port);
	}

	private void OnRefreshPressed()
	{
		_network.RequestRooms();
	}

	private void OnConnected()
	{
		_connectButton.Text = "Desconectar";
		_connectButton.Disabled = false;
		UpdateGradeControls();
	}

	private void OnDisconnected()
	{
		_connectButton.Text = "Conectar";
		_connectButton.Disabled = false;
		UpdateGradeControls();
	}

	private void OnStatus(string text)
	{
		SetStatus(text);
	}

	private void SetStatus(string text)
	{
		_statusLabel.Text = text;
	}

	private void OnRooms(RefreshClass refresh)
	{
		int previousSelection = _selectedRoom;
		_rooms.Clear();
		foreach (RoomSummary summary in refresh.Summaries)
		{
			_rooms[summary.Id] = summary;
		}
		RebuildRoomList();
		// Mantem a sala filtrada mesmo com o push periodico do server.
		if (previousSelection != -1)
		{
			SelectRoom(_rooms.ContainsKey(previousSelection) ? previousSelection : -1);
		}
	}

	private void OnSubmission(MinigameSubmissionPkt submission)
	{
		int countBefore = _entries.Count;
		for (int teamIndex = 0; teamIndex < submission.Teams.Count; teamIndex++)
		{
			_entries.Add(new SubmissionEntry
			{
				RoomId = submission.RoomId,
				TeamIndex = teamIndex,
				Team = submission.Teams[teamIndex]
			});
		}
		RebuildSubmissionList();
		UpdateGradeControls();
		if (countBefore == 0 && _entries.Count > 0)
		{
			_submissionList.Select(0);
			ShowEntryDetail(VisibleEntries()[0]);
		}
		SetStatus($"Submissao recebida da sala {submission.RoomId}");
	}

	private void OnRoomSelected(long index)
	{
		SelectRoom(index == 0 ? -1 : _roomList.GetItemMetadata((int)index).AsInt32());
	}

	private void SelectRoom(int roomId)
	{
		_selectedRoom = roomId;
		int visibleIndex = 0;
		for (int i = 0; i < _roomList.ItemCount; i++)
		{
			int itemRoom = i == 0 ? -1 : _roomList.GetItemMetadata(i).AsInt32();
			if (itemRoom == roomId)
			{
				visibleIndex = i;
				break;
			}
		}
		if (_roomList.ItemCount > 0)
		{
			_roomList.Select(visibleIndex);
		}
		RebuildSubmissionList();
		UpdateGradeControls();
	}

	private void OnSubmissionSelected(long index)
	{
		List<SubmissionEntry> entries = VisibleEntries();
		if (index < entries.Count)
		{
			ShowEntryDetail(entries[(int)index]);
		}
	}

	private void OnSendGradePressed()
	{
		int[] selected = _submissionList.GetSelectedItems();
		List<SubmissionEntry> entries = VisibleEntries();
		if (selected.Length == 0 || selected[0] >= entries.Count)
		{
			SetStatus("Selecione uma submissao para corrigir");
			return;
		}
		SubmissionEntry entry = entries[(int)selected[0]];
		bool sent = _network.SendGrade(
			entry.RoomId,
			entry.TeamIndex,
			(float)_gradeSpin.Value,
			_commentLine.Text.Trim()
		);
		if (sent)
		{
			_commentLine.Clear();
		}
	}

	private List<SubmissionEntry> VisibleEntries()
	{
		var visible = new List<SubmissionEntry>();
		foreach (SubmissionEntry entry in _entries)
		{
			if (_selectedRoom == -1 || entry.RoomId == _selectedRoom)
			{
				visible.Add(entry);
			}
		}
		return visible;
	}

	private void RebuildRoomList()
	{
		int previous = _selectedRoom;
		_roomList.Clear();
		_roomList.AddItem("Todas as salas");
		_roomList.SetItemMetadata(0, -1);
		var roomIds = new List<int>(_rooms.Keys);
		roomIds.Sort();
		foreach (int roomId in roomIds)
		{
			RoomSummary summary = _rooms[roomId];
			string label = $"Sala {roomId} - {summary.PlayerCount} jogador(es)";
			if (summary.PlayerNames.Count > 0)
			{
				label += ": " + string.Join(", ", summary.PlayerNames);
			}
			_roomList.AddItem(label);
			_roomList.SetItemMetadata(_roomList.ItemCount - 1, roomId);
		}
		int visibleIndex = 0;
		for (int i = 0; i < _roomList.ItemCount; i++)
		{
			int itemRoom = i == 0 ? -1 : _roomList.GetItemMetadata(i).AsInt32();
			if (itemRoom == previous)
			{
				visibleIndex = i;
				break;
			}
		}
		if (_roomList.ItemCount > 0)
		{
			_roomList.Select(visibleIndex);
		}
	}

	private void RebuildSubmissionList()
	{
		_submissionList.Clear();
		foreach (SubmissionEntry entry in VisibleEntries())
		{
			Godot.Collections.Dictionary team = entry.Team;
			_submissionList.AddItem(string.Format(
				CultureInfo.InvariantCulture,
				"Sala {0} - Dupla {1} ({2}) - {3}",
				entry.RoomId,
				entry.TeamIndex + 1,
				JoinIds(team["members"].AsGodotArray()),
				FormatTime(team["total_msec"].AsInt64())
			));
		}
		if (_submissionList.ItemCount > 0)
		{
			_submissionList.Select(0);
			ShowEntryDetail(VisibleEntries()[0]);
		}
		else
		{
			_detail.Text = "";
			_detailLabel.Text = "Detalhes da dupla";
		}
	}

	private void ShowEntryDetail(SubmissionEntry entry)
	{
		int roomId = entry.RoomId;
		int teamIndex = entry.TeamIndex;
		Godot.Collections.Dictionary team = entry.Team;
		_detailLabel.Text = $"Detalhes - Sala {roomId}, dupla {teamIndex + 1}";

		var lines = new List<string>();
		lines.Add($"[b]Integrantes:[/b] {JoinIds(team["members"].AsGodotArray())}");
		lines.Add($"[b]Tempo total:[/b] {FormatTime(team["total_msec"].AsInt64())}");
		lines.Add("");

		Godot.Collections.Array answers = team["answers"].AsGodotArray();
		Godot.Collections.Array answerMsecs = team["submission_msecs"].AsGodotArray();
		for (int i = 0; i < answers.Count; i++)
		{
			lines.Add($"[b]Questao {i + 1}[/b]");
			if (i < _questions.Count)
			{
				lines.Add($"[color=#8fd8e0]{EscapeBbcode(_questions[i])}[/color]");
			}
			lines.Add($"[b]Resposta:[/b] {EscapeBbcode(VariantStr(answers[i]))}");
			lines.Add($"Tempo de resposta: {FormatTime(answerMsecs[i].AsInt64())}");
			lines.Add("");
		}
		_detail.Text = string.Join("\n", lines);
	}

	private void UpdateGradeControls()
	{
		bool selected = _submissionList.GetSelectedItems().Length > 0;
		_gradeSpin.Editable = selected;
		_commentLine.Editable = selected;

		_sendGradeButton.Disabled = selected == false || !_network.IsConnectedToServer();
	}

	private List<string> LoadQuestions()
	{
		var prompts = new List<string>();
		FileAccess file = FileAccess.Open(QuizPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PushWarning($"Professor: nao foi possivel ler {QuizPath}");
			return prompts;
		}
		Variant parsed = Json.ParseString(file.GetAsText());
		file.Close();
		if (parsed.VariantType != Variant.Type.Dictionary)
		{
			return prompts;
		}
		Godot.Collections.Dictionary dict = parsed.AsGodotDictionary();
		if (!dict.ContainsKey("questions"))
		{
			return prompts;
		}
		foreach (Variant question in dict["questions"].AsGodotArray())
		{
			Godot.Collections.Dictionary q = question.AsGodotDictionary();
			prompts.Add(q.TryGetValue("prompt", out Variant prompt) ? VariantStr(prompt) : "");
		}
		return prompts;
	}

	// Respostas vem do aluno e podem conter "[" que quebrariam o BBCode do detalhe.
	private static string EscapeBbcode(string text) => text.Replace("[", "[lb]");

	private static string VariantStr(Variant value)
	{
		return value.VariantType switch
		{
			Variant.Type.String => value.AsString(),
			Variant.Type.Int => value.AsInt64().ToString(CultureInfo.InvariantCulture),
			Variant.Type.Float => value.AsDouble().ToString(CultureInfo.InvariantCulture),
			_ => value.AsString()
		};
	}

	private static string JoinIds(Godot.Collections.Array members)
	{
		var parts = new List<string>();
		foreach (Variant member in members)
		{
			parts.Add(VariantStr(member));
		}
		return string.Join(", ", parts);
	}

	private static string FormatTime(long msec)
	{
		return string.Create(
			CultureInfo.InvariantCulture,
			$"{msec / 1000.0:F1} s"
		);
	}
}
