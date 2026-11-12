class_name CrosswordGenerator extends RefCounted

# Gerador de layout de Palavras Cruzadas (Crossword) em GDScript,
# inspirado em Rocket-007/Crossword-Layout-Generator-in-GDScript-better-verson-.
# Recebe uma lista de palavras/dicas de cibersegurança e monta uma grade
# interconectada (Horizontal / Vertical), retornando a matriz recortada e
# os metadados de cada palavra posicionada.

var cols: int = 28
var rows: int = 28
var empty_char: String = ""
var grid: Array = []
var placed_words: Array[Dictionary] = []

func _init(_cols: int = 28, _rows: int = 28) -> void:
	cols = _cols
	rows = _rows
	clear_grid()

func clear_grid() -> void:
	grid = []
	for r in rows:
		var row_arr: Array[String] = []
		row_arr.resize(cols)
		for c in cols:
			row_arr[c] = empty_char
		grid.append(row_arr)
	placed_words.clear()

static func normalize_word(raw: String) -> String:
	var s: String = raw.strip_edges().to_upper()
	var replacements: Dictionary = {
		"Á": "A", "À": "A", "Â": "A", "Ã": "A",
		"É": "E", "Ê": "E",
		"Í": "I",
		"Ó": "O", "Ô": "O", "Õ": "O",
		"Ú": "U", "Ü": "U",
		"Ç": "C"
	}
	for k in replacements.keys():
		s = s.replace(k, replacements[k])
	var clean: String = ""
	for i in s.length():
		var ch: String = s.substr(i, 1)
		if (ch >= "A" and ch <= "Z") or (ch >= "0" and ch <= "9"):
			clean += ch
	return clean

# Gera o layout para uma lista de entradas:
# Cada entrada pode conter: { "word": String, "prompt": String, "answer": String }
# Retorna Dictionary com:
#   "rows": int, "cols": int,
#   "words": Array[Dictionary] ( na mesma ordem das perguntas, com row, col, vertical, number, word, prompt )
func generate_from_questions(questions: Array) -> Dictionary:
	clear_grid()
	var entries: Array[Dictionary] = []
	for i in questions.size():
		var q: Dictionary = questions[i]
		var raw_w: String = String(q.get("word", q.get("answer", "")))
		var clean_w: String = normalize_word(raw_w)
		if clean_w.is_empty():
			continue
		entries.append({
			"orig_index": i,
			"number": i + 1,
			"word": clean_w,
			"prompt": String(q.get("prompt", "")),
			"placed": false,
			"row": 0,
			"col": 0,
			"vertical": false
		})

	# Ordena por tamanho decrescente para posicionar palavras longas primeiro (melhor conectividade),
	# de forma determinística para que Host e Clientes gerem exatamente a mesma grade.
	var sorted_entries: Array[Dictionary] = entries.duplicate()
	sorted_entries.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		if a["word"].length() != b["word"].length():
			return a["word"].length() > b["word"].length()
		return int(a["orig_index"]) < int(b["orig_index"])
	)

	var unplaced: Array[Dictionary] = []
	for idx in sorted_entries.size():
		var item: Dictionary = sorted_entries[idx]
		if placed_words.is_empty():
			var start_r: int = rows / 2
			var start_c: int = max(1, (cols - item["word"].length()) / 2)
			_place_word(item, start_r, start_c, false)
		else:
			if not _try_place_best_intersection(item):
				unplaced.append(item)

	# Segunda passagem para palavras que não cruzaram na primeira tentativa.
	var still_unplaced: Array[Dictionary] = []
	for item in unplaced:
		if not _try_place_best_intersection(item):
			still_unplaced.append(item)

	# Fallback determinístico caso alguma palavra não tenha letra em comum com as demais.
	for item in still_unplaced:
		_place_fallback(item)

	return _crop_and_build_result()

func _try_place_best_intersection(item: Dictionary) -> bool:
	var word: String = item["word"]
	var best_score: int = -1
	var best_r: int = -1
	var best_c: int = -1
	var best_vert: bool = false
	var center_r: float = rows / 2.0
	var center_c: float = cols / 2.0
	var best_dist: float = 999999.0

	for placed in placed_words:
		var p_word: String = placed["word"]
		var p_r: int = int(placed["row"])
		var p_c: int = int(placed["col"])
		var p_vert: bool = bool(placed["vertical"])
		var candidate_vert: bool = not p_vert

		for i in word.length():
			var ch: String = word.substr(i, 1)
			for j in p_word.length():
				if p_word.substr(j, 1) != ch:
					continue
				var cross_r: int = p_r + (j if p_vert else 0)
				var cross_c: int = p_c + (0 if p_vert else j)
				var start_r: int = cross_r - (i if candidate_vert else 0)
				var start_c: int = cross_c - (0 if candidate_vert else i)

				var score: int = check_fit_score(word, start_r, start_c, candidate_vert)
				if score > 0:
					var mid_r: float = start_r + (word.length() / 2.0 if candidate_vert else 0.0)
					var mid_c: float = start_c + (0.0 if candidate_vert else word.length() / 2.0)
					var dist: float = abs(mid_r - center_r) + abs(mid_c - center_c)
					if score > best_score or (score == best_score and dist < best_dist):
						best_score = score
						best_r = start_r
						best_c = start_c
						best_vert = candidate_vert
						best_dist = dist

	if best_score > 0:
		_place_word(item, best_r, best_c, best_vert)
		return true
	return false

# Verifica se a palavra cabe em (row, col) com orientação `vertical`.
# Retorna 0 se houver colisão inválida, ou >= 2 (1 + número de interseções válidas) se couber.
func check_fit_score(word: String, row: int, col: int, vertical: bool) -> int:
	var length: int = word.length()
	if row < 0 or col < 0:
		return 0
	if vertical and row + length > rows:
		return 0
	if not vertical and col + length > cols:
		return 0

	# Célula imediatamente anterior ao início da palavra deve estar vazia
	var before_r: int = row - (1 if vertical else 0)
	var before_c: int = col - (0 if vertical else 1)
	if _in_bounds(before_r, before_c) and grid[before_r][before_c] != empty_char:
		return 0

	# Célula imediatamente posterior ao fim da palavra deve estar vazia
	var after_r: int = row + (length if vertical else 0)
	var after_c: int = col + (0 if vertical else length)
	if _in_bounds(after_r, after_c) and grid[after_r][after_c] != empty_char:
		return 0

	var score: int = 1
	for i in length:
		var r: int = row + (i if vertical else 0)
		var c: int = col + (0 if vertical else i)
		var ch: String = word.substr(i, 1)
		var current_cell: String = grid[r][c]

		if current_cell != empty_char and current_cell != ch:
			return 0

		if current_cell == ch:
			# Evita sobrepor duas palavras na mesma direção
			for placed in placed_words:
				if bool(placed["vertical"]) == vertical:
					var pr: int = int(placed["row"])
					var pc: int = int(placed["col"])
					var plen: int = String(placed["word"]).length()
					if vertical and pc == c and r >= pr and r < pr + plen:
						return 0
					if not vertical and pr == r and c >= pc and c < pc + plen:
						return 0
			score += 1
		else:
			# Se a célula está vazia, as células vizinhas perpendiculares devem estar vazias
			if vertical:
				if _in_bounds(r, c - 1) and grid[r][c - 1] != empty_char:
					return 0
				if _in_bounds(r, c + 1) and grid[r][c + 1] != empty_char:
					return 0
			else:
				if _in_bounds(r - 1, c) and grid[r - 1][c] != empty_char:
					return 0
				if _in_bounds(r + 1, c) and grid[r + 1][c] != empty_char:
					return 0

	return score

func _place_word(item: Dictionary, row: int, col: int, vertical: bool) -> void:
	var word: String = item["word"]
	for i in word.length():
		var r: int = row + (i if vertical else 0)
		var c: int = col + (0 if vertical else i)
		grid[r][c] = word.substr(i, 1)
	item["row"] = row
	item["col"] = col
	item["vertical"] = vertical
	item["placed"] = true
	placed_words.append(item)

func _place_fallback(item: Dictionary) -> void:
	var word: String = item["word"]
	for r in range(1, rows - 1):
		var start_c: int = max(1, (cols - word.length()) / 2)
		if check_fit_score(word, r, start_c, false) > 0:
			_place_word(item, r, start_c, false)
			return

func _crop_and_build_result() -> Dictionary:
	if placed_words.is_empty():
		return { "rows": 1, "cols": 1, "words": [] }

	var min_r: int = rows
	var max_r: int = 0
	var min_c: int = cols
	var max_c: int = 0

	for item in placed_words:
		var r: int = int(item["row"])
		var c: int = int(item["col"])
		var length: int = String(item["word"]).length()
		var vert: bool = bool(item["vertical"])
		var end_r: int = r + (length - 1 if vert else 0)
		var end_c: int = c + (0 if vert else length - 1)
		min_r = min(min_r, r)
		max_r = max(max_r, end_r)
		min_c = min(min_c, c)
		max_c = max(max_c, end_c)

	var cropped_rows: int = max_r - min_r + 1
	var cropped_cols: int = max_c - min_c + 1

	# Reordena de volta pelo índice original da pergunta (1..N)
	var ordered: Array[Dictionary] = placed_words.duplicate()
	ordered.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		return int(a["orig_index"]) < int(b["orig_index"])
	)

	for item in ordered:
		item["row"] = int(item["row"]) - min_r
		item["col"] = int(item["col"]) - min_c

	return {
		"rows": cropped_rows,
		"cols": cropped_cols,
		"words": ordered
	}

func _in_bounds(r: int, c: int) -> bool:
	return r >= 0 and r < rows and c >= 0 and c < cols
