#!/usr/bin/env python3
"""Gera a trilha lo-fi original do Cyber Resistance.

Requisitos de desenvolvimento: Python 3, NumPy e FFmpeg.
Os arquivos finais são OGG Vorbis estéreo em 44,1 kHz.
"""

from __future__ import annotations

import math
import subprocess
import tempfile
import wave
from dataclasses import dataclass
from pathlib import Path

import numpy as np


SAMPLE_RATE = 44_100
ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = ROOT / "Assets" / "Audio" / "Music"


@dataclass(frozen=True)
class Track:
	filename: str
	bpm: int
	seed: int
	progression: tuple[tuple[int, ...], ...]
	scale: tuple[int, ...]
	melody_root: int
	drum_energy: float
	vinyl: float


TRACKS = (
	Track(
		"lofi_signal_at_dusk.ogg",
		72,
		1103,
		(
			(50, 53, 57, 60, 64),
			(46, 50, 53, 57),
			(53, 57, 60, 64),
			(48, 52, 55, 62),
		),
		(0, 2, 3, 5, 7, 9, 10),
		62,
		0.48,
		0.75,
	),
	Track(
		"lofi_city_after_rain.ogg",
		76,
		2207,
		(
			(45, 48, 52, 55),
			(41, 45, 48, 52),
			(48, 52, 55, 59),
			(43, 47, 50, 52),
		),
		(0, 2, 3, 5, 7, 8, 10),
		69,
		0.72,
		0.62,
	),
	Track(
		"lofi_coffee_and_code.ogg",
		82,
		3301,
		(
			(48, 52, 55, 59, 62),
			(45, 48, 52, 55, 59),
			(50, 53, 57, 60, 64),
			(43, 47, 50, 53, 57),
		),
		(0, 2, 4, 5, 7, 9, 11),
		72,
		0.82,
		0.52,
	),
	Track(
		"lofi_quiet_terminal.ogg",
		70,
		4409,
		(
			(40, 43, 47, 50, 54),
			(48, 52, 55, 59),
			(45, 48, 52, 55, 59),
			(47, 52, 54, 57),
		),
		(0, 2, 3, 5, 7, 9, 10),
		64,
		0.58,
		0.82,
	),
	Track(
		"lofi_study_loop.ogg",
		78,
		5519,
		(
			(41, 45, 48, 52),
			(40, 43, 47, 50),
			(50, 53, 57, 60, 64),
			(43, 47, 50, 53, 57),
		),
		(0, 2, 4, 5, 7, 9, 11),
		65,
		0.66,
		0.58,
	),
)


def midi_frequency(note: int) -> float:
	return 440.0 * (2.0 ** ((note - 69) / 12.0))


def envelope(
	length: int,
	attack: float,
	decay: float,
	sustain: float,
	release: float,
) -> np.ndarray:
	result = np.ones(length, dtype=np.float32) * sustain
	attack_n = min(length, max(1, int(attack * SAMPLE_RATE)))
	decay_n = min(length - attack_n, max(1, int(decay * SAMPLE_RATE)))
	release_n = min(length, max(1, int(release * SAMPLE_RATE)))
	result[:attack_n] = np.linspace(0.0, 1.0, attack_n, endpoint=False)
	if decay_n:
		result[attack_n : attack_n + decay_n] = np.linspace(
			1.0, sustain, decay_n, endpoint=False
		)
	result[-release_n:] *= np.linspace(1.0, 0.0, release_n)
	return result


def stereo_gains(pan: float) -> tuple[float, float]:
	angle = (np.clip(pan, -1.0, 1.0) + 1.0) * math.pi / 4.0
	return math.cos(angle), math.sin(angle)


def mix_mono(
	buffer: np.ndarray,
	start_seconds: float,
	signal: np.ndarray,
	pan: float = 0.0,
) -> None:
	start = int(start_seconds * SAMPLE_RATE)
	if start >= len(buffer):
		return
	end = min(len(buffer), start + len(signal))
	signal = signal[: end - start]
	left, right = stereo_gains(pan)
	buffer[start:end, 0] += signal * left
	buffer[start:end, 1] += signal * right


def add_rhodes(
	buffer: np.ndarray,
	start: float,
	duration: float,
	note: int,
	amplitude: float,
	pan: float,
) -> None:
	length = max(1, int(duration * SAMPLE_RATE))
	t = np.arange(length, dtype=np.float32) / SAMPLE_RATE
	frequency = midi_frequency(note)
	wobble = 0.018 * np.sin(2.0 * np.pi * 0.42 * t)
	phase = 2.0 * np.pi * frequency * t + wobble
	waveform = (
		np.sin(phase)
		+ 0.34 * np.sin(2.01 * phase + 0.4) * np.exp(-2.2 * t)
		+ 0.12 * np.sin(3.0 * phase) * np.exp(-3.8 * t)
	)
	waveform *= envelope(length, 0.018, 0.42, 0.46, 0.34)
	mix_mono(buffer, start, waveform * amplitude, pan)


def add_pad(
	buffer: np.ndarray,
	start: float,
	duration: float,
	note: int,
	amplitude: float,
	pan: float,
) -> None:
	length = max(1, int(duration * SAMPLE_RATE))
	t = np.arange(length, dtype=np.float32) / SAMPLE_RATE
	frequency = midi_frequency(note)
	phase = 2.0 * np.pi * frequency * t
	waveform = (
		0.72 * np.sin(phase + 0.035 * np.sin(2.0 * np.pi * 0.31 * t))
		+ 0.2 * np.sin(phase * 0.5)
		+ 0.08 * np.sin(phase * 2.0)
	)
	waveform *= envelope(length, 0.34, 0.6, 0.68, 0.65)
	mix_mono(buffer, start, waveform * amplitude, pan)


def add_bass(
	buffer: np.ndarray,
	start: float,
	duration: float,
	note: int,
	amplitude: float,
) -> None:
	length = max(1, int(duration * SAMPLE_RATE))
	t = np.arange(length, dtype=np.float32) / SAMPLE_RATE
	frequency = midi_frequency(note)
	phase = 2.0 * np.pi * frequency * t
	waveform = np.sin(phase) + 0.22 * np.sin(2.0 * phase)
	waveform *= envelope(length, 0.012, 0.18, 0.62, 0.22)
	mix_mono(buffer, start, np.tanh(waveform * 1.25) * amplitude, -0.04)


def add_kick(buffer: np.ndarray, start: float, amplitude: float) -> None:
	duration = 0.46
	length = int(duration * SAMPLE_RATE)
	t = np.arange(length, dtype=np.float32) / SAMPLE_RATE
	frequency = 44.0 + 92.0 * np.exp(-t * 22.0)
	phase = 2.0 * np.pi * np.cumsum(frequency) / SAMPLE_RATE
	waveform = np.sin(phase) * np.exp(-t * 9.5)
	mix_mono(buffer, start, waveform * amplitude, 0.0)


def add_snare(
	buffer: np.ndarray,
	start: float,
	amplitude: float,
	rng: np.random.Generator,
) -> None:
	duration = 0.31
	length = int(duration * SAMPLE_RATE)
	t = np.arange(length, dtype=np.float32) / SAMPLE_RATE
	noise = rng.standard_normal(length).astype(np.float32)
	noise = np.concatenate(([0.0], np.diff(noise))).astype(np.float32)
	tone = np.sin(2.0 * np.pi * 176.0 * t)
	waveform = (0.78 * noise + 0.22 * tone) * np.exp(-t * 15.0)
	mix_mono(buffer, start, waveform * amplitude, 0.08)


def add_hat(
	buffer: np.ndarray,
	start: float,
	amplitude: float,
	pan: float,
	rng: np.random.Generator,
	open_hat: bool = False,
) -> None:
	duration = 0.23 if open_hat else 0.075
	length = int(duration * SAMPLE_RATE)
	t = np.arange(length, dtype=np.float32) / SAMPLE_RATE
	noise = rng.standard_normal(length).astype(np.float32)
	noise = np.concatenate(([0.0], np.diff(noise))).astype(np.float32)
	waveform = noise * np.exp(-t * (18.0 if open_hat else 52.0))
	mix_mono(buffer, start, waveform * amplitude, pan)


def add_vinyl(
	buffer: np.ndarray,
	amount: float,
	rng: np.random.Generator,
) -> None:
	length = len(buffer)
	noise = rng.standard_normal(length).astype(np.float32) * (0.0018 * amount)
	buffer[:, 0] += noise
	buffer[:, 1] += np.roll(noise, 31) * 0.92
	click_count = max(1, int(length / SAMPLE_RATE * 1.7 * amount))
	for sample in rng.integers(0, length - 48, click_count):
		click_length = int(rng.integers(8, 42))
		click = (
			rng.choice((-1.0, 1.0))
			* np.exp(-np.arange(click_length) / 7.0)
			* rng.uniform(0.012, 0.035)
		)
		buffer[sample : sample + click_length, :] += click[:, None]


def chord_root(chord: tuple[int, ...]) -> int:
	return min(chord)


def render_track(track: Track) -> np.ndarray:
	rng = np.random.default_rng(track.seed)
	seconds_per_beat = 60.0 / track.bpm
	bars = 16
	duration = bars * 4.0 * seconds_per_beat
	buffer = np.zeros((int(duration * SAMPLE_RATE), 2), dtype=np.float32)

	for bar in range(bars):
		bar_start = bar * 4.0 * seconds_per_beat
		chord = track.progression[bar % len(track.progression)]
		root = chord_root(chord)

		for index, note in enumerate(chord):
			add_pad(
				buffer,
				bar_start,
				4.15 * seconds_per_beat,
				note,
				0.018,
				(index - (len(chord) - 1) / 2.0) * 0.16,
			)

		for beat, length, velocity in ((0.0, 1.65, 1.0), (2.45, 1.2, 0.72)):
			for index, note in enumerate(chord):
				add_rhodes(
					buffer,
					bar_start + beat * seconds_per_beat,
					length * seconds_per_beat,
					note + 12,
					0.029 * velocity,
					(index - (len(chord) - 1) / 2.0) * 0.18,
				)

		add_bass(
			buffer,
			bar_start,
			1.55 * seconds_per_beat,
			root - 12,
			0.105,
		)
		add_bass(
			buffer,
			bar_start + 2.5 * seconds_per_beat,
			0.92 * seconds_per_beat,
			root - 12,
			0.077,
		)

		for beat in (0.0, 2.0):
			add_kick(
				buffer,
				bar_start + beat * seconds_per_beat,
				0.2 * track.drum_energy,
			)
		if bar % 4 in (1, 3):
			add_kick(
				buffer,
				bar_start + 3.25 * seconds_per_beat,
				0.105 * track.drum_energy,
			)

		for beat in (1.0, 3.0):
			add_snare(
				buffer,
				bar_start + beat * seconds_per_beat,
				0.052 * track.drum_energy,
				rng,
			)

		for step in range(8):
			beat = step * 0.5 + (0.055 if step % 2 else 0.0)
			add_hat(
				buffer,
				bar_start + beat * seconds_per_beat,
				(0.010 if step % 2 else 0.007) * track.drum_energy,
				-0.28 if step % 2 else 0.24,
				rng,
				open_hat=step == 7 and bar % 4 == 3,
			)

		if bar % 2 == 1:
			melody_steps = (0.65, 1.55, 2.8)
			for melody_index, beat in enumerate(melody_steps):
				scale_index = (bar + melody_index * 2 + track.seed) % len(track.scale)
				note = track.melody_root + track.scale[scale_index]
				if melody_index == 2 and bar % 4 == 3:
					note -= 12
				add_rhodes(
					buffer,
					bar_start + beat * seconds_per_beat,
					0.58 * seconds_per_beat,
					note,
					0.042,
					0.32 if melody_index % 2 else -0.24,
				)

	add_vinyl(buffer, track.vinyl, rng)

	# Saturação suave, leve oscilação de fita e proteção contra cliques.
	t = np.arange(len(buffer), dtype=np.float32) / SAMPLE_RATE
	tape = 1.0 + 0.012 * np.sin(2.0 * np.pi * 0.21 * t)
	buffer *= tape[:, None]
	buffer = np.tanh(buffer * 1.35)
	fade_n = int(0.012 * SAMPLE_RATE)
	buffer[:fade_n] *= np.linspace(0.0, 1.0, fade_n)[:, None]
	buffer[-fade_n:] *= np.linspace(1.0, 0.0, fade_n)[:, None]
	peak = float(np.max(np.abs(buffer)))
	if peak > 0.0:
		buffer *= 0.86 / peak
	return buffer


def write_wav(path: Path, samples: np.ndarray) -> None:
	pcm = np.int16(np.clip(samples, -1.0, 1.0) * 32767.0)
	with wave.open(str(path), "wb") as output:
		output.setnchannels(2)
		output.setsampwidth(2)
		output.setframerate(SAMPLE_RATE)
		output.writeframes(pcm.tobytes())


def encode_ogg(wav_path: Path, ogg_path: Path) -> None:
	filter_chain = (
		"highpass=f=32,lowpass=f=11800,"
		"acompressor=threshold=-20dB:ratio=2:attack=24:release=260:makeup=2,"
		"loudnorm=I=-17:TP=-2:LRA=7"
	)
	subprocess.run(
		(
			"ffmpeg",
			"-hide_banner",
			"-loglevel",
			"error",
			"-y",
			"-i",
			str(wav_path),
			"-af",
			filter_chain,
			"-c:a",
			"libvorbis",
			"-ar",
			str(SAMPLE_RATE),
			"-q:a",
			"4",
			str(ogg_path),
		),
		check=True,
	)


def main() -> None:
	OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
	with tempfile.TemporaryDirectory(prefix="cyber-resistance-lofi-") as temp:
		temp_dir = Path(temp)
		for track in TRACKS:
			print(f"Gerando {track.filename} ({track.bpm} BPM)...")
			samples = render_track(track)
			wav_path = temp_dir / f"{Path(track.filename).stem}.wav"
			write_wav(wav_path, samples)
			encode_ogg(wav_path, OUTPUT_DIR / track.filename)
	print(f"Trilha gerada em {OUTPUT_DIR}")


if __name__ == "__main__":
	main()
