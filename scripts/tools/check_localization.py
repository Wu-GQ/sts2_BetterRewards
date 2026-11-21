#!/usr/bin/env python3
"""Check BetterRewards localization tables against the settings actually declared in code.

Validates JSON syntax, official-language key parity for every table, and that each
settings property/section has the keys the settings UI looks up:
  {PREFIX}-{SLUG}.title, {PREFIX}-{SLUG}.hover.desc, {PREFIX}-{SECTION_SLUG}.title
It also reports settings_ui keys that no property, section, or convention uses.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LOC = ROOT / "localization"
SETTINGS_SRC = ROOT / "src" / "Features" / "Settings" / "BetterRewardsSettings.cs"
MODEL_SRC = ROOT / "src" / "Features" / "Settings" / "BetterRewardsSettingsModel.cs"
TABLES = ("settings_ui", "events", "better_rewards_shop")
LANGS = (
    "eng", "zhs", "zht",
    "deu", "esp", "fra", "ind", "ita", "jpn", "kor",
    "pol", "ptb", "rus", "spa", "tha", "tur", "ara",
)
PREFIX = "BETTERREWARDS"

# Settings strings that are not derived from a model property or a section name:
# the reset section label, its button, and the confirmation dialog.
FIXED_SETTINGS_KEYS = frozenset({
    f"{PREFIX}-RESET.title",
    f"{PREFIX}-RESET_ALL.title",
    f"{PREFIX}-RESET_ALL.button",
    f"{PREFIX}-RESET_ALL.hover.desc",
    f"{PREFIX}-RESET_CONFIRM.title",
    f"{PREFIX}-RESET_CONFIRM.button",
    f"{PREFIX}-RESET_CONFIRM.hover.desc",
    f"{PREFIX}-RESET_CANCEL.button",
})


def slugify(text: str) -> str:
    """Mirror the previous BaseLib/MegaCrit StringHelper.Slugify used by existing keys."""
    chars: list[str] = []
    for index, char in enumerate(text.strip()):
        if char.isupper() and index > 0 and text[index - 1].isalnum():
            chars.append("_")
        chars.append(char)
    slug = re.sub(r"\s+", "_", "".join(chars).upper())
    return re.sub(r"[^A-Z0-9_]", "", slug)


def read_table(language: str, table: str) -> dict[str, str]:
    path = LOC / language / f"{table}.json"
    return json.loads(path.read_text(encoding="utf-8"))


def declared_settings() -> tuple[list[str], list[str]]:
    source = SETTINGS_SRC.read_text(encoding="utf-8")
    # The model moved into its own game-free file; fall back to it so the checker keeps
    # validating the same property list after the split.
    model_source = source
    if MODEL_SRC.exists():
        model_source = MODEL_SRC.read_text(encoding="utf-8")
    model = re.search(r"class BetterRewardsSettingsModel\s*\{(.*?)^\s*public void CopyFrom", model_source, re.S | re.M)
    if model:
        properties = re.findall(r"public (?:double|int) (\w+)\s*\{", model.group(1))
    else:
        properties = re.findall(r"public (?:double|int) (\w+)\s*\{", model_source)
    sections_match = re.search(r"ConfigSectionNames\s*=\s*\[(.*?)\]", source, re.S)
    sections = re.findall(r'"([^"]+)"', sections_match.group(1)) if sections_match else []
    return properties, sections


def main() -> int:
    problems: list[str] = []
    properties, sections = declared_settings()

    loaded: dict[tuple[str, str], dict[str, str]] = {}
    for language in LANGS:
        for table in TABLES:
            try:
                loaded[(language, table)] = read_table(language, table)
            except json.JSONDecodeError as error:
                problems.append(f"{language}/{table}.json is not valid JSON: {error}")

    for table in TABLES:
        eng = loaded.get(("eng", table), {})
        for language in LANGS:
            if language == "eng":
                continue
            other = loaded.get((language, table), {})
            for key in sorted(set(eng) - set(other)):
                problems.append(f"{table}: missing {language} key {key}")
            for key in sorted(set(other) - set(eng)):
                problems.append(f"{table}: extra {language} key {key}")

    for language in LANGS:
        loc = loaded.get((language, "settings_ui"), {})
        expected: set[str] = {f"{PREFIX}.mod_title", f"{PREFIX}.mod_desc"}
        for prop in properties:
            expected.add(f"{PREFIX}-{slugify(prop)}.title")
            expected.add(f"{PREFIX}-{slugify(prop)}.hover.desc")
        for section in sections:
            expected.add(f"{PREFIX}-{slugify(section)}.title")
        # Keys used by the reset section and its confirmation dialog, which are addressed
        # directly by name rather than derived from a property or section slug.
        expected |= FIXED_SETTINGS_KEYS

        for key in sorted(expected - set(loc)):
            problems.append(f"{language}/settings_ui.json is missing {key}")
        for key in sorted(set(loc) - expected):
            problems.append(f"{language}/settings_ui.json has unused key {key}")

    if problems:
        print("Localization check failed:")
        for problem in problems:
            print(f"  - {problem}")
        return 1

    print(f"OK: {len(properties)} settings, {len(sections)} sections, all tables parse,")
    print(f"    {len(LANGS)} official languages match eng keys, and settings_ui has no unused keys.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
