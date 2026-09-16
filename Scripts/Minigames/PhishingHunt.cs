using Godot;
using System;
using System.Collections.Generic;

public partial class PhishingHunt : Control
{
	private const string FairModeMenuScenePath = "res://Scenes/Interfaces/fair_mode_menu.tscn";
	private const float RoundDurationSeconds = 75.0f;
	private const int CasesPerRound = 8;
	private const float FeedbackDurationSeconds = 1.65f;

	private sealed class MessageCase
	{
		public string Sender { get; }
		public string Subject { get; }
		public string Body { get; }
		public string Link { get; }
		public string RealSender { get; }
		public string RealDestination { get; }
		public string Clue { get; }
		public string Explanation { get; }
		public bool IsPhishing { get; }

		public MessageCase(
			string sender,
			string subject,
			string body,
			string link,
			string realSender,
			string realDestination,
			string clue,
			string explanation,
			bool isPhishing)
		{
			Sender = sender;
			Subject = subject;
			Body = body;
			Link = link;
			RealSender = realSender;
			RealDestination = realDestination;
			Clue = clue;
			Explanation = explanation;
			IsPhishing = isPhishing;
		}
	}

	private readonly List<MessageCase> _cases = new()
	{
		new(
			"Equipe do Jogo Online <premios@jogoonline-brindes.net>",
			"VOCÊ GANHOU 10.000 MOEDAS!",
			"Parabéns! Seu usuário foi escolhido. Confirme sua senha agora para receber as moedas antes que o prêmio expire.",
			"https://jogoonline-brindes.net/resgatar",
			"premios@jogoonline-brindes.net",
			"jogoonline-brindes.net",
			"O endereço usa um domínio diferente do site oficial e a mensagem pede sua senha.",
			"É phishing: promessa de prêmio + urgência + pedido de senha são sinais clássicos de golpe.",
			true),
		new(
			"Portal da Escola <avisos@portal.escola.edu.br>",
			"Novo material disponível na turma",
			"A professora publicou um novo arquivo na disciplina de Ciências. Entre no portal normalmente para visualizar o material.",
			"https://portal.escola.edu.br/turmas",
			"avisos@portal.escola.edu.br",
			"portal.escola.edu.br",
			"O remetente e o destino pertencem ao mesmo domínio institucional e não há pedido de senha por mensagem.",
			"É confiável: o domínio é consistente e a mensagem orienta a acessar o portal institucional.",
			false),
		new(
			"Segurança da Conta <alerta@banc0-seguro.xyz>",
			"URGENTE: sua conta será bloqueada em 10 minutos",
			"Detectamos uma compra suspeita. Clique abaixo e informe seus dados para impedir o bloqueio imediato.",
			"https://banc0-seguro.xyz/confirmar",
			"alerta@banc0-seguro.xyz",
			"banc0-seguro.xyz",
			"A palavra 'banco' foi escrita com o número zero e o domínio termina em .xyz.",
			"É phishing: o endereço imita uma instituição e usa medo e urgência para induzir o clique.",
			true),
		new(
			"Biblioteca Escolar <biblioteca@escola.edu.br>",
			"Lembrete de devolução",
			"O livro reservado vence na sexta-feira. Você pode devolver na biblioteca ou consultar seus empréstimos no portal da escola.",
			"https://biblioteca.escola.edu.br/emprestimos",
			"biblioteca@escola.edu.br",
			"biblioteca.escola.edu.br",
			"O domínio é institucional, a mensagem é informativa e não solicita credenciais nem pagamento.",
			"É confiável: há coerência entre remetente, link e assunto, sem tentativa de obter dados sensíveis.",
			false),
		new(
			"Suporte da Nuvem <conta@micros0ft-suporte.com>",
			"Armazenamento cheio — confirme sua conta",
			"Seu armazenamento atingiu 99%. Faça login pelo link abaixo para evitar a exclusão automática dos seus arquivos.",
			"https://micros0ft-suporte.com/login",
			"conta@micros0ft-suporte.com",
			"micros0ft-suporte.com",
			"O nome usa o número zero no lugar da letra 'o' e tenta parecer uma empresa conhecida.",
			"É phishing: pequenas alterações no nome do domínio são usadas para enganar quem lê rapidamente.",
			true),
		new(
			"Autenticação <nao-responda@contas.escola.edu.br>",
			"Seu código de acesso: 482731",
			"Use este código apenas na tela de login que você abriu. A equipe da escola nunca solicitará este código por mensagem.",
			"",
			"nao-responda@contas.escola.edu.br",
			"Nenhum link externo",
			"A mensagem não pede resposta, senha ou clique e ainda orienta a não compartilhar o código.",
			"É confiável: códigos de autenticação podem chegar por mensagem, mas nunca devem ser enviados a outra pessoa.",
			false),
		new(
			"Colega da turma <joao.fotos@correio-gratis.info>",
			"vota em mim??? é rapidinho",
			"Oi! Estou participando de um concurso. Abre esse link e entra com sua conta para votar em mim, por favor!!!",
			"https://encurta.link/vote-agora",
			"joao.fotos@correio-gratis.info",
			"encurta.link → destino oculto",
			"O link encurtado esconde o destino e a mensagem pede login em uma página externa.",
			"É phishing: contas comprometidas podem enviar mensagens que parecem vir de amigos para roubar novas contas.",
			true),
		new(
			"Plataforma de Estudos <notificacoes@estudos.edu.br>",
			"Você concluiu o desafio de lógica",
			"Seu resultado foi registrado. Continue seus exercícios entrando pelo endereço oficial da plataforma quando quiser.",
			"https://estudos.edu.br/painel",
			"notificacoes@estudos.edu.br",
			"estudos.edu.br",
			"Remetente e link usam o mesmo domínio e não há ameaça, prêmio inesperado ou pedido de dados.",
			"É confiável: a mensagem apenas informa uma atividade e aponta para o domínio oficial.",
			false),
		new(
			"Entrega Express <rastreio@entrega-pendente.click>",
			"Seu pacote está retido",
			"Não conseguimos entregar sua encomenda. Pague a taxa de R$ 2,99 nas próximas 2 horas para evitar a devolução.",
			"https://entrega-pendente.click/taxa",
			"rastreio@entrega-pendente.click",
			"entrega-pendente.click",
			"O domínio genérico não identifica uma transportadora e a mensagem pressiona por um pagamento imediato.",
			"É phishing: taxas pequenas e urgentes são usadas para induzir pagamentos e capturar dados do cartão.",
			true),
		new(
			"Secretaria <secretaria@colegio.edu.br>",
			"Horário especial na sexta-feira",
			"Na sexta-feira as aulas terminam às 11h30 por causa da reunião pedagógica. O aviso também está publicado no mural do colégio.",
			"https://colegio.edu.br/avisos",
			"secretaria@colegio.edu.br",
			"colegio.edu.br",
			"A informação pode ser conferida em outro canal oficial e nenhum dado pessoal é solicitado.",
			"É confiável: a mensagem usa canal institucional e oferece uma forma independente de confirmar o aviso.",
			false),
		new(
			"Central de Jogos <suporte@conta-jogo.com>",
			"Item raro gratuito por tempo limitado",
			"Você foi selecionado para receber um item lendário. Instale o arquivo abaixo para liberar a recompensa.",
			"https://conta-jogo.com/download/presente.exe",
			"suporte@conta-jogo.com",
			"conta-jogo.com/download/presente.exe",
			"A mensagem tenta convencer você a baixar um arquivo executável para receber um prêmio inesperado.",
			"É phishing: anexos e programas oferecidos como prêmio podem instalar malware no computador.",
			true),
		new(
			"Sistema da Biblioteca <nao-responda@biblioteca.escola.edu.br>",
			"Reserva confirmada",
			"Sua reserva foi confirmada. O livro ficará disponível no balcão por três dias úteis. Não é necessário clicar em nenhum link.",
			"",
			"nao-responda@biblioteca.escola.edu.br",
			"Nenhum link externo",
			"A mensagem apenas confirma uma ação e não tenta obter senha, pagamento ou instalação de arquivo.",
			"É confiável: uma notificação legítima também pode ser simples e não exigir nenhuma ação imediata.",
			false)
	};

	[Export] public NodePath TimeLabelPath { get; set; }
	[Export] public NodePath ScoreLabelPath { get; set; }
	[Export] public NodePath ComboLabelPath { get; set; }
	[Export] public NodePath ProgressLabelPath { get; set; }
	[Export] public NodePath SenderLabelPath { get; set; }
	[Export] public NodePath SubjectLabelPath { get; set; }
	[Export] public NodePath BodyLabelPath { get; set; }
	[Export] public NodePath LinkLabelPath { get; set; }
	[Export] public NodePath InvestigationPanelPath { get; set; }
	[Export] public NodePath InvestigationTextPath { get; set; }
	[Export] public NodePath FeedbackLabelPath { get; set; }
	[Export] public NodePath InvestigateButtonPath { get; set; }
	[Export] public NodePath TrustworthyButtonPath { get; set; }
	[Export] public NodePath PhishingButtonPath { get; set; }
	[Export] public NodePath BackButtonPath { get; set; }
	[Export] public NodePath TutorialOverlayPath { get; set; }
	[Export] public NodePath StartButtonPath { get; set; }
	[Export] public NodePath ResultOverlayPath { get; set; }
	[Export] public NodePath ResultTitlePath { get; set; }
	[Export] public NodePath ResultSummaryPath { get; set; }
	[Export] public NodePath RetryButtonPath { get; set; }
	[Export] public NodePath ResultBackButtonPath { get; set; }

	private Label _timeLabel;
	private Label _scoreLabel;
	private Label _comboLabel;
	private Label _progressLabel;
	private Label _senderLabel;
	private Label _subjectLabel;
	private Label _bodyLabel;
	private Label _linkLabel;
	private Control _investigationPanel;
	private Label _investigationText;
	private Label _feedbackLabel;
	private Button _investigateButton;
	private Button _trustworthyButton;
	private Button _phishingButton;
	private Button _backButton;
	private Control _tutorialOverlay;
	private Button _startButton;
	private Control _resultOverlay;
	private Label _resultTitle;
	private Label _resultSummary;
	private Button _retryButton;
	private Button _resultBackButton;

	private readonly List<int> _roundOrder = new();
	private readonly RandomNumberGenerator _rng = new();
	private int _caseIndex;
	private int _answered;
	private int _correct;
	private int _score;
	private int _combo;
	private int _maxCombo;
	private float _timeRemaining;
	private float _feedbackRemaining;
	private bool _running;
	private bool _investigated;
	private bool _waitingForNextCase;

	private static readonly Color SuccessColor = new(0.42f, 0.94f, 0.72f, 1.0f);
	private static readonly Color DangerColor = new(0.98f, 0.48f, 0.50f, 1.0f);
	private static readonly Color NeutralColor = new(0.66f, 0.79f, 0.83f, 1.0f);

	public override void _Ready()
	{
		AudioManager.Instance?.SetMenuContext();
		_rng.Randomize();
		CacheNodes();

		if (!HasRequiredNodes())
		{
			GD.PrintErr("PhishingHunt: estrutura da interface não encontrada.");
			return;
		}

		ConnectSignals();
		PrepareRound(showTutorial: true);
	}

	public override void _ExitTree()
	{
		DisconnectSignals();
	}

	public override void _Process(double delta)
	{
		if (!_running)
			return;

		_timeRemaining = Math.Max(0.0f, _timeRemaining - (float)delta);
		UpdateHud();

		if (_timeRemaining <= 0.0f)
		{
			FinishRound("TEMPO ESGOTADO");
			return;
		}

		if (!_waitingForNextCase)
			return;

		_feedbackRemaining -= (float)delta;
		if (_feedbackRemaining <= 0.0f)
			AdvanceCase();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel") && !@event.IsEcho())
		{
			OpenFairModeMenu();
			GetViewport().SetInputAsHandled();
		}
	}

	private void CacheNodes()
	{
		_timeLabel = GetNodeOrNull<Label>(TimeLabelPath);
		_scoreLabel = GetNodeOrNull<Label>(ScoreLabelPath);
		_comboLabel = GetNodeOrNull<Label>(ComboLabelPath);
		_progressLabel = GetNodeOrNull<Label>(ProgressLabelPath);
		_senderLabel = GetNodeOrNull<Label>(SenderLabelPath);
		_subjectLabel = GetNodeOrNull<Label>(SubjectLabelPath);
		_bodyLabel = GetNodeOrNull<Label>(BodyLabelPath);
		_linkLabel = GetNodeOrNull<Label>(LinkLabelPath);
		_investigationPanel = GetNodeOrNull<Control>(InvestigationPanelPath);
		_investigationText = GetNodeOrNull<Label>(InvestigationTextPath);
		_feedbackLabel = GetNodeOrNull<Label>(FeedbackLabelPath);
		_investigateButton = GetNodeOrNull<Button>(InvestigateButtonPath);
		_trustworthyButton = GetNodeOrNull<Button>(TrustworthyButtonPath);
		_phishingButton = GetNodeOrNull<Button>(PhishingButtonPath);
		_backButton = GetNodeOrNull<Button>(BackButtonPath);
		_tutorialOverlay = GetNodeOrNull<Control>(TutorialOverlayPath);
		_startButton = GetNodeOrNull<Button>(StartButtonPath);
		_resultOverlay = GetNodeOrNull<Control>(ResultOverlayPath);
		_resultTitle = GetNodeOrNull<Label>(ResultTitlePath);
		_resultSummary = GetNodeOrNull<Label>(ResultSummaryPath);
		_retryButton = GetNodeOrNull<Button>(RetryButtonPath);
		_resultBackButton = GetNodeOrNull<Button>(ResultBackButtonPath);
	}

	private bool HasRequiredNodes()
	{
		return _timeLabel != null && _scoreLabel != null && _comboLabel != null &&
			_progressLabel != null && _senderLabel != null && _subjectLabel != null &&
			_bodyLabel != null && _linkLabel != null && _investigationPanel != null &&
			_investigationText != null && _feedbackLabel != null && _investigateButton != null &&
			_trustworthyButton != null && _phishingButton != null && _backButton != null &&
			_tutorialOverlay != null && _startButton != null && _resultOverlay != null &&
			_resultTitle != null && _resultSummary != null && _retryButton != null &&
			_resultBackButton != null;
	}

	private void ConnectSignals()
	{
		_investigateButton.Pressed += OnInvestigatePressed;
		_trustworthyButton.Pressed += OnTrustworthyPressed;
		_phishingButton.Pressed += OnPhishingPressed;
		_backButton.Pressed += OpenFairModeMenu;
		_startButton.Pressed += OnStartPressed;
		_retryButton.Pressed += OnRetryPressed;
		_resultBackButton.Pressed += OpenFairModeMenu;
	}

	private void DisconnectSignals()
	{
		if (_investigateButton != null) _investigateButton.Pressed -= OnInvestigatePressed;
		if (_trustworthyButton != null) _trustworthyButton.Pressed -= OnTrustworthyPressed;
		if (_phishingButton != null) _phishingButton.Pressed -= OnPhishingPressed;
		if (_backButton != null) _backButton.Pressed -= OpenFairModeMenu;
		if (_startButton != null) _startButton.Pressed -= OnStartPressed;
		if (_retryButton != null) _retryButton.Pressed -= OnRetryPressed;
		if (_resultBackButton != null) _resultBackButton.Pressed -= OpenFairModeMenu;
	}

	private void PrepareRound(bool showTutorial)
	{
		_running = false;
		_waitingForNextCase = false;
		_timeRemaining = RoundDurationSeconds;
		_feedbackRemaining = 0.0f;
		_caseIndex = 0;
		_answered = 0;
		_correct = 0;
		_score = 0;
		_combo = 0;
		_maxCombo = 0;
		BuildRoundOrder();

		_resultOverlay.Visible = false;
		_tutorialOverlay.Visible = showTutorial;
		_investigationPanel.Visible = false;
		_feedbackLabel.Text = "Analise a mensagem. Se precisar, investigue antes de decidir.";
		_feedbackLabel.AddThemeColorOverride("font_color", NeutralColor);
		SetAnswerButtonsEnabled(false);
		UpdateHud();
		ShowCurrentCase();

		if (showTutorial)
			_startButton.GrabFocus();
	}

	private void BuildRoundOrder()
	{
		_roundOrder.Clear();
		for (int index = 0; index < _cases.Count; index++)
			_roundOrder.Add(index);

		for (int index = _roundOrder.Count - 1; index > 0; index--)
		{
			int swapIndex = _rng.RandiRange(0, index);
			(_roundOrder[index], _roundOrder[swapIndex]) = (_roundOrder[swapIndex], _roundOrder[index]);
		}

		if (_roundOrder.Count > CasesPerRound)
			_roundOrder.RemoveRange(CasesPerRound, _roundOrder.Count - CasesPerRound);
	}

	private void OnStartPressed()
	{
		_tutorialOverlay.Visible = false;
		_running = true;
		SetAnswerButtonsEnabled(true);
		_investigateButton.GrabFocus();
	}

	private void OnRetryPressed()
	{
		PrepareRound(showTutorial: false);
		_running = true;
		SetAnswerButtonsEnabled(true);
		_investigateButton.GrabFocus();
	}

	private MessageCase CurrentCase => _cases[_roundOrder[_caseIndex]];

	private void ShowCurrentCase()
	{
		MessageCase current = CurrentCase;
		_investigated = false;
		_investigationPanel.Visible = false;
		_investigateButton.Text = "INVESTIGAR PISTAS";
		_investigateButton.Disabled = !_running;
		_senderLabel.Text = $"De: {current.Sender}";
		_subjectLabel.Text = $"Assunto: {current.Subject}";
		_bodyLabel.Text = current.Body;
		_linkLabel.Visible = !string.IsNullOrWhiteSpace(current.Link);
		_linkLabel.Text = string.IsNullOrWhiteSpace(current.Link) ? "" : $"Link exibido: {current.Link}";
		_progressLabel.Text = $"CASO {_caseIndex + 1}/{_roundOrder.Count}";
		_feedbackLabel.Text = "Golpe ou mensagem legítima? Observe antes de escolher.";
		_feedbackLabel.AddThemeColorOverride("font_color", NeutralColor);
		UpdateHud();
	}

	private void OnInvestigatePressed()
	{
		if (!_running || _waitingForNextCase)
			return;

		MessageCase current = CurrentCase;
		_investigated = true;
		_investigationPanel.Visible = true;
		_investigationText.Text =
			$"REMETENTE REAL\n{current.RealSender}\n\nDESTINO DO LINK\n{current.RealDestination}\n\nPISTA\n{current.Clue}";
		_investigateButton.Text = "PISTAS REVELADAS";
		_investigateButton.Disabled = true;
	}

	private void OnTrustworthyPressed()
	{
		SubmitAnswer(guessedPhishing: false);
	}

	private void OnPhishingPressed()
	{
		SubmitAnswer(guessedPhishing: true);
	}

	private void SubmitAnswer(bool guessedPhishing)
	{
		if (!_running || _waitingForNextCase)
			return;

		MessageCase current = CurrentCase;
		bool correctAnswer = guessedPhishing == current.IsPhishing;
		_answered++;

		if (correctAnswer)
		{
			_correct++;
			_combo++;
			_maxCombo = Math.Max(_maxCombo, _combo);
			int comboBonus = Math.Min(_combo, 6) * 25;
			int investigationBonus = _investigated ? 25 : 0;
			_score += 200 + comboBonus + investigationBonus;
			_feedbackLabel.Text = $"ACERTO! {current.Explanation}";
			_feedbackLabel.AddThemeColorOverride("font_color", SuccessColor);
		}
		else
		{
			_combo = 0;
			_score = Math.Max(0, _score - 60);
			_feedbackLabel.Text = $"ATENÇÃO! {current.Explanation}";
			_feedbackLabel.AddThemeColorOverride("font_color", DangerColor);
		}

		_waitingForNextCase = true;
		_feedbackRemaining = FeedbackDurationSeconds;
		SetAnswerButtonsEnabled(false);
		UpdateHud();
	}

	private void AdvanceCase()
	{
		_waitingForNextCase = false;
		_caseIndex++;

		if (_caseIndex >= _roundOrder.Count)
		{
			FinishRound("INVESTIGAÇÃO CONCLUÍDA");
			return;
		}

		ShowCurrentCase();
		SetAnswerButtonsEnabled(true);
	}

	private void SetAnswerButtonsEnabled(bool enabled)
	{
		if (_investigateButton != null)
			_investigateButton.Disabled = !enabled || _investigated;
		if (_trustworthyButton != null)
			_trustworthyButton.Disabled = !enabled;
		if (_phishingButton != null)
			_phishingButton.Disabled = !enabled;
	}

	private void UpdateHud()
	{
		int seconds = Math.Max(0, (int)Math.Ceiling(_timeRemaining));
		_timeLabel.Text = $"TEMPO  {seconds / 60:00}:{seconds % 60:00}";
		_scoreLabel.Text = $"PONTOS  {_score}";
		_comboLabel.Text = _combo > 1 ? $"COMBO  x{_combo}" : "COMBO  —";
	}

	private void FinishRound(string title)
	{
		if (!_running)
			return;

		_running = false;
		_waitingForNextCase = false;
		SetAnswerButtonsEnabled(false);
		_resultTitle.Text = title;

		int accuracy = _answered == 0 ? 0 : (int)Math.Round(_correct * 100.0 / _answered);
		string observation = accuracy >= 88
			? "Você identificou muito bem os sinais de fraude."
			: accuracy >= 63
				? "Bom trabalho. Continue conferindo remetente, domínio e urgência."
				: "Golpes podem parecer convincentes. Investigue remetentes e links antes de agir.";

		_resultSummary.Text =
			$"Pontuação: {_score}\n" +
			$"Acertos: {_correct}/{_answered} ({accuracy}%)\n" +
			$"Maior combo: x{_maxCombo}\n\n" +
			observation +
			"\n\nRegra prática: desconfie de urgência, prêmios inesperados e pedidos de senha. Confira o endereço real antes de clicar.";

		_resultOverlay.Visible = true;
		_retryButton.GrabFocus();
	}

	private void OpenFairModeMenu()
	{
		GetTree().ChangeSceneToFile(FairModeMenuScenePath);
	}
}
