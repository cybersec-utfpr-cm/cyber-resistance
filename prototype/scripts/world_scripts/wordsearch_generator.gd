class_name WordSearchGenerator extends RefCounted

# Gerador determinístico de Caça-Palavras (Word Search) em GDScript.
# Mantém paridade exata com Scripts/Minigames/WordSearchGenerator.cs.

const DIRECTIONS: Array = [
	[0, 1],   # Horizontal ->
	[1, 0],   # Vertical v
	[1, 1],   # Diagonal se
	[-1, 1]   # Diagonal ne
]

const DIRECTION_LABELS: Array[String] = [
	"Horizontal →",
	"Vertical ↓",
	"Diagonal ↘",
	"Diagonal ↗"
]

var rows: int = 12
var cols: int = 12
var _seed: int = 2166136261

func _init(_rows: int = 12, _cols: int = 12) -> void:
	rows = _rows
	cols = _cols

func generate_from_questions(questions: Array) -> Dictionary:
	var grid: Array = []
	for r in rows:
		var row_arr: Array[String] = []
		row_arr.resize(cols)
		for c in cols:
			row_arr[c] = ""
		grid.append(row_arr)

	_seed = 2166136261
	var entries: Array[Dictionary] = []
	for i in questions.size():
		var q: Dictionary = questions[i]
		var raw_w: String = String(q.get("word", q.get("answer", "")))
		var clean_w: String = CrosswordGenerator.normalize_word(raw_w)
		if clean_w.is_empty():
			continue
		for ch_idx in clean_w.length():
			_seed = ((_seed ^ clean_w.unicode_at(ch_idx)) * 16777619) & 0xFFFFFFFF
		entries.append({
			"orig_index": i,
			"number": i + 1,
			"word": clean_w,
			"prompt": String(q.get("prompt", "")),
			"start_row": 0,
			"start_col": 0,
			"dr": 0,
			"dc": 1,
			"direction_name": DIRECTION_LABELS[0]
		})

	var sorted: Array[Dictionary] = entries.duplicate()
	sorted.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		if String(a["word"]).length() != String(b["word"]).length():
			return String(a["word"]).length() > String(b["word"]).length()
		return int(a["orig_index"]) < int(b["orig_index"])
	)

	for item in sorted:
		_place_word_deterministic(grid, item)

	entries.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		return int(a["orig_index"]) < int(b["orig_index"])
	)

	var alphabet: String = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
	for r in rows:
		for c in cols:
			if String(grid[r][c]).is_empty():
				var idx: int = _next_rand() % alphabet.length()
				grid[r][c] = alphabet.substr(idx, 1)

	return {
		"rows": rows,
		"cols": cols,
		"grid": grid,
		"words": entries
	}

func _place_word_deterministic(grid: Array, item: Dictionary) -> void:
	var word: String = item["word"]
	var total_cells: int = rows * cols
	var start_dir_idx: int = _next_rand() % DIRECTIONS.size()
	var start_cell_idx: int = _next_rand() % total_cells

	for pass_idx in 2:
		for d_offset in DIRECTIONS.size():
			var dir_idx: int = (start_dir_idx + d_offset) % DIRECTIONS.size()
			var dr: int = DIRECTIONS[dir_idx][0]
			var dc: int = DIRECTIONS[dir_idx][1]
			for c_offset in total_cells:
				var cell_idx: int = (start_cell_idx + c_offset * 7) % total_cells
				var r: int = cell_idx / cols
				var c: int = cell_idx % cols
				var fit: int = _can_place(grid, word, r, c, dr, dc)
				if pass_idx == 0 and fit >= 2:
					_commit_word(grid, item, r, c, dr, dc, DIRECTION_LABELS[dir_idx])
					return
				if pass_idx == 1 and fit >= 1:
					_commit_word(grid, item, r, c, dr, dc, DIRECTION_LABELS[dir_idx])
					return

	var fallback_row: int = int(item["orig_index"]) % rows
	for i in min(word.length(), cols):
		grid[fallback_row][i] = word.substr(i, 1)
	item["start_row"] = fallback_row
	item["start_col"] = 0
	item["dr"] = 0
	item["dc"] = 1
	item["direction_name"] = DIRECTION_LABELS[0]

func _can_place(grid: Array, word: String, start_r: int, start_c: int, dr: int, dc: int) -> int:
	var length: int = word.length()
	var end_r: int = start_r + dr * (length - 1)
	var end_c: int = start_c + dc * (length - 1)
	if end_r < 0 or end_r >= rows or end_c < 0 or end_c >= cols:
		return 0
	var score: int = 1
	for i in length:
		var r: int = start_r + dr * i
		var c: int = start_c + dc * i
		var ch: String = word.substr(i, 1)
		var existing: String = grid[r][c]
		if not existing.is_empty() and existing != ch:
			return 0
		if existing == ch:
			score += 1
	return score

func _commit_word(grid: Array, item: Dictionary, start_r: int, start_c: int, dr: int, dc: int, dir_label: String) -> void:
	var word: String = item["word"]
	for i in word.length():
		var r: int = start_r + dr * i
		var c: int = start_c + dc * i
		grid[r][c] = word.substr(i, 1)
	item["start_row"] = start_r
	item["start_col"] = start_c
	item["dr"] = dr
	item["dc"] = dc
	item["direction_name"] = dir_label

func _next_rand() -> int:
	_seed = (_seed ^ ((_seed << 13) & 0xFFFFFFFF)) & 0xFFFFFFFF
	_seed = (_seed ^ (_seed >> 17)) & 0xFFFFFFFF
	_seed = (_seed ^ ((_seed << 5) & 0xFFFFFFFF)) & 0xFFFFFFFF
	return _seed
