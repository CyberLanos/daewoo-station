#!/usr/bin/env python3
"""
find_case_mismatches.py

Шукає у всіх YAML-прототипах (Resources/Prototypes/**.yml) посилання на
спрайти/текстури (sprite:, texture:, icon:) і перевіряє, чи існує такий
шлях НА ДИСКУ З ТОЧНО ТАКИМ САМИМ РЕГІСТРОМ символів.

Чому це потрібно:
  - Windows (NTFS) ігнорує регістр у назвах файлів — там усе "просто працює".
  - Linux (ext4 і т.д.) регістрозалежний — той самий шлях з іншим регістром
    просто не знайдеться, і RSI/спрайт не завантажиться (саме це і трапилось
    з '/Textures/_F14/Objects/SCP/scp330candies.rsi' на Ubuntu CI).

Як користуватись:
  1. Поклади цей файл у корінь репозиторію (там, де лежить папка Resources).
  2. Запусти:  python find_case_mismatches.py
  3. Скрипт виведе список усіх "підозрілих" шляхів:
       - CASE MISMATCH  -> файл існує, але з іншим регістром (саме твій баг)
       - NOT FOUND      -> шлях не знайдено взагалі (можливо, інша проблема)

Запускати краще там, де код реально лежить (Windows-машина розробника),
бо результат не залежить від ОС — порівняння регістру робиться вручну
рядок-в-рядок, а не через файлову систему.
"""

import os
import re
import sys

REPO_ROOT = os.path.dirname(os.path.abspath(__file__))
PROTOTYPES_DIR = os.path.join(REPO_ROOT, "Resources", "Prototypes")
TEXTURES_DIR = os.path.join(REPO_ROOT, "Resources", "Textures")

# Ключі в YAML, які зазвичай містять шлях до спрайту/текстури/RSI
SPRITE_KEYS = ("sprite", "texture", "icon", "background", "stateBackground")

# Регекс: "  sprite: SomePath/file.rsi" або 'sprite: "Some/Path.png"'
LINE_RE = re.compile(
    r'^\s*(' + "|".join(SPRITE_KEYS) + r')\s*:\s*["\']?([^"\'\s#]+\.(?:rsi|png))["\']?\s*(#.*)?$'
)


def find_yaml_files(root):
    for dirpath, _, filenames in os.walk(root):
        for f in filenames:
            if f.lower().endswith((".yml", ".yaml")):
                yield os.path.join(dirpath, f)


def build_real_texture_index(textures_root):
    """
    Будує множину всіх реальних шляхів (відносно Resources/Textures),
    зберігаючи ТОЧНИЙ регістр, як він записаний на диску.
    Окремо індексує і файли, і директорії (для .rsi-папок).
    """
    real_paths = set()
    real_paths_lower = {}  # lower() -> set реальних варіантів (для діагностики)

    for dirpath, dirnames, filenames in os.walk(textures_root):
        rel_dir = os.path.relpath(dirpath, textures_root).replace("\\", "/")
        if rel_dir == ".":
            rel_dir = ""

        entries = list(dirnames) + list(filenames)
        for name in entries:
            rel_path = f"{rel_dir}/{name}" if rel_dir else name
            real_paths.add(rel_path)
            real_paths_lower.setdefault(rel_path.lower(), set()).add(rel_path)

    return real_paths, real_paths_lower


def normalize_reference(raw_path):
    """
    Перетворює шлях, як він написаний у YAML, на шлях відносно
    Resources/Textures/, без зміни регістру.
    """
    p = raw_path.strip()
    if p.startswith("/Textures/"):
        p = p[len("/Textures/"):]
    elif p.startswith("/"):
        # Шлях типу "/Something/else.png", що не починається з Textures —
        # це не наш кейс (можливо Audio/інше), пропускаємо.
        return None
    # інакше — це вже відносний шлях під Resources/Textures/, як є
    return p.replace("\\", "/")


def main():
    if not os.path.isdir(PROTOTYPES_DIR):
        print(f"Не знайдено {PROTOTYPES_DIR}. Поклади скрипт у корінь репозиторію.")
        sys.exit(1)
    if not os.path.isdir(TEXTURES_DIR):
        print(f"Не знайдено {TEXTURES_DIR}.")
        sys.exit(1)

    print("Індексую реальні файли в Resources/Textures ...")
    real_paths, real_paths_lower = build_real_texture_index(TEXTURES_DIR)
    print(f"  знайдено {len(real_paths)} файлів/папок.\n")

    case_mismatches = []  # (yaml_file, line_no, raw_ref, normalized, real_variant)
    not_found = []        # (yaml_file, line_no, raw_ref, normalized)
    checked = 0

    print("Сканую YAML-прототипи ...")
    for yaml_path in find_yaml_files(PROTOTYPES_DIR):
        try:
            with open(yaml_path, "r", encoding="utf-8") as f:
                lines = f.readlines()
        except UnicodeDecodeError:
            continue

        for i, line in enumerate(lines, 1):
            m = LINE_RE.match(line)
            if not m:
                continue
            raw_ref = m.group(2)
            normalized = normalize_reference(raw_ref)
            if normalized is None:
                continue

            checked += 1

            if normalized in real_paths:
                continue  # все ок, точний регістр співпадає

            lower_key = normalized.lower()
            if lower_key in real_paths_lower:
                for real_variant in real_paths_lower[lower_key]:
                    case_mismatches.append(
                        (yaml_path, i, raw_ref, normalized, real_variant)
                    )
            else:
                not_found.append((yaml_path, i, raw_ref, normalized))

    print(f"  перевірено {checked} посилань на спрайти/текстури.\n")

    print("=" * 80)
    print(f"CASE MISMATCH (працює на Windows, ламається на Linux): {len(case_mismatches)}")
    print("=" * 80)
    for yaml_path, line_no, raw_ref, normalized, real_variant in case_mismatches:
        rel_yaml = os.path.relpath(yaml_path, REPO_ROOT)
        print(f"\n[{rel_yaml}:{line_no}]")
        print(f"  У YAML написано : {raw_ref}")
        print(f"  Реально на диску: {real_variant}")

    print("\n" + "=" * 80)
    print(f"NOT FOUND (шлях не знайдено взагалі): {len(not_found)}")
    print("=" * 80)
    for yaml_path, line_no, raw_ref, normalized in not_found:
        rel_yaml = os.path.relpath(yaml_path, REPO_ROOT)
        print(f"\n[{rel_yaml}:{line_no}]")
        print(f"  Шлях відсутній  : {raw_ref}")

    if not case_mismatches and not not_found:
        print("\nНічого підозрілого не знайдено. ✅")


if __name__ == "__main__":
    main()