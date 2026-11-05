#!/usr/bin/env python3
import json
import math
import re
from PIL import Image, ImageDraw, ImageFont

# ----------------------------------------------------------------------
# 1. Справочники имён (LORE.md и project.md)
# ----------------------------------------------------------------------
LORE_NAMES = {
    # Буквы Альманаха -> имена
    "A": "Willow",
    "D": "Sickle",
    "I": "Morvath",
    "W": "Corvin",
    "Z": "Lantern",
    "h": "Flint",
    "F": "Lashing",
    "G": "Barb",
    "L": "Spearhead",
    "J": "Mouser",
    "t": "Eddy",
    "n": "Cork",
    "T": "Peak",
    "s": "Tack",
    "X": "Dhanab",
    "U": "Buq",
    "x": "Sada",
    "a": "Witchlight",
    "B": "Watar",
    "N": "Naghma",
    "P": "Snath",
    "O": "Heel",
    "o": "Ain",
    "r": "Qarn",
    "H": "Lulu",
    "R": "Mikhlab",
    "E": "Oboshi",
    "k": "Kashira",
}

# Обновления названий созвездий
CONSTELLATION_RENAMES = {
    "The Sloop": "Corvin's Sloop",
}

# ----------------------------------------------------------------------
# 2. Парсинг AtlasStars.cs
# ----------------------------------------------------------------------
def load_atlas_stars(cs_path="AtlasStars.cs"):
    stars = {}
    pattern = re.compile(
        r'new\s+AtlasStar\s*\(\s*"([^"]+)"\s*,\s*(null|"[^"]*")\s*,\s*(null|"[^"]*")\s*,\s*([\d\.\-]+)f\s*,\s*([\d\.\-]+)f\s*,\s*([\d\.\-]+)f\s*\)'
    )
    with open(cs_path, "r", encoding="utf-8") as f:
        for line in f:
            m = pattern.search(line)
            if m:
                code = m.group(1)
                name = None if m.group(2) == "null" else m.group(2).strip('"')
                letter = (
                    None if m.group(3) == "null" else m.group(3).strip('"')
                )
                ra = float(m.group(4))
                dec = float(m.group(5))
                mag = float(m.group(6))

                # Приоритет отображаемого имени:
                # 1. Собственное имя (Oriens, Milnead, Nibble, Grumble...)
                # 2. Имя из LoreNames по букве Альманаха
                # 3. Сама буква (если вдруг нет имени)
                disp_name = name
                if not disp_name and letter and letter in LORE_NAMES:
                    disp_name = LORE_NAMES[letter]

                stars[code] = {
                    "code": code,
                    "name": disp_name,
                    "letter": letter,
                    "ra": ra,
                    "dec": dec,
                    "mag": mag,
                }
    print(f"Загружено звёзд из {cs_path}: {len(stars)}")
    return stars


def load_constellation_lines(json_path="constellation-lines.json"):
    with open(json_path, "r", encoding="utf-8") as f:
        data = json.load(f)
    print(f"Загружено созвездий из {json_path}: {len(data)}")
    return data


# Радиус точки звезды в пикселях по звёздной величине
def mag_to_radius(mag, scale=1.0):
    # mag от -1.3 до 6.4
    r = max(1.8, 7.5 - mag * 0.95)
    return r * scale


# ----------------------------------------------------------------------
# 3. Генерация круговой карты (Celestial Atlas: Север, Юг, Экватор)
#    Разрешение 4096 x 4096
# ----------------------------------------------------------------------
def generate_round_atlas(
    stars,
    constellations,
    output_path="star-chart-round-constellations-new.png",
):
    W, H = 4096, 4096
    img = Image.new("RGBA", (W, H), (255, 255, 255, 255))
    draw = ImageDraw.Draw(img)

    # Цвета
    COLOR_GRID = (70, 70, 70, 255)
    COLOR_STAR = (20, 20, 20, 255)
    COLOR_LINE = (120, 160, 100, 220)  # Тот самый оливково-зелёный цвет фигур
    COLOR_TITLE = (125, 45, 35, 255)  # Красно-коричневый для созвездий
    COLOR_LABEL = (40, 40, 40, 255)

    try:
        font_large = ImageFont.truetype("georgia.ttf", 64)
        font_med = ImageFont.truetype("georgia.ttf", 34)
        font_small = ImageFont.truetype("georgia.ttf", 24)
        font_italic = ImageFont.truetype("georgiai.ttf", 32)
    except Exception:
        font_large = font_med = font_small = font_italic = (
            ImageFont.load_default()
        )

    # --- ПАРАМЕТРЫ ПОЛЮСОВ ---
    R_polar = 920.0
    N_cx, N_cy = 1100.0, 1060.0
    S_cx, S_cy = 3000.0, 1060.0

    # Проекция Северного полюса (Polar Equidistant, Dec +90 .. 0)
    def proj_north(ra_deg, dec_deg):
        if dec_deg < -0.5:
            return None
        r = R_polar * (90.0 - dec_deg) / 90.0
        angle = math.radians(ra_deg)
        x = N_cx + r * math.sin(angle)
        y = N_cy - r * math.cos(angle)
        return x, y

    # Проекция Южного полюса (Polar Equidistant, Dec -90 .. 0)
    def proj_south(ra_deg, dec_deg):
        if dec_deg > 0.5:
            return None
        r = R_polar * (dec_deg + 90.0) / 90.0
        angle = math.radians(ra_deg)
        x = S_cx - r * math.sin(angle)
        y = S_cy - r * math.cos(angle)
        return x, y

    # Сетка для полюсов
    for is_south, cx, cy, name in [
        (False, N_cx, N_cy, "North"),
        (True, S_cx, S_cy, "South"),
    ]:
        draw.text(
            (cx - 70, cy + R_polar + 70), name, fill=COLOR_GRID, font=font_large
        )
        # Кольца склонения каждые 10°
        for d in range(10, 100, 10):
            r = R_polar * (d / 90.0)
            draw.ellipse(
                [cx - r, cy - r, cx + r, cy + r],
                outline=COLOR_GRID,
                width=2 if d == 90 else 1,
            )
        # Лучи прямых восхождений каждые 1 час (15°)
        for h in range(24):
            angle = math.radians(h * 15.0)
            sign = -1.0 if is_south else 1.0
            x2 = cx + sign * R_polar * math.sin(angle)
            y2 = cy - R_polar * math.cos(angle)
            draw.line([cx, cy, x2, y2], fill=COLOR_GRID, width=1)
            # Подписи часов
            xl = cx + sign * (R_polar + 35) * math.sin(angle)
            yl = cy - (R_polar + 35) * math.cos(angle)
            draw.text(
                (xl - 16, yl - 16), f"{h}h", fill=COLOR_GRID, font=font_small
            )

    # --- ПАРАМЕТРЫ ЭКВАТОРИАЛЬНОГО ПОЯСА (+40° .. -40°) ---
    strip_x0, strip_x1 = 280.0, 3880.0
    strip_y0, strip_y1 = 2400.0, 3800.0
    strip_w = strip_x1 - strip_x0
    strip_h = strip_y1 - strip_y0

    def proj_strip(ra_deg, dec_deg):
        if dec_deg < -41.0 or dec_deg > 41.0:
            return None
        # RA 24h (360°) слева -> 0h (0°) справа
        x = strip_x0 + (360.0 - ra_deg) / 360.0 * strip_w
        # Dec +40° сверху -> -40° снизу
        y = strip_y0 + (40.0 - dec_deg) / 80.0 * strip_h
        return x, y

    # Сетка экваториального пояса
    draw.rectangle(
        [strip_x0, strip_y0, strip_x1, strip_y1], outline=COLOR_GRID, width=3
    )
    for dec_val in [-40, -20, 0, 20, 40]:
        y = strip_y0 + (40.0 - dec_val) / 80.0 * strip_h
        draw.line([strip_x0, y, strip_x1, y], fill=COLOR_GRID, width=1)
        sign_str = "+" if dec_val > 0 else ""
        draw.text(
            (strip_x0 - 75, y - 14),
            f"{sign_str}{dec_val}°",
            fill=COLOR_GRID,
            font=font_small,
        )
        draw.text(
            (strip_x1 + 15, y - 14),
            f"{sign_str}{dec_val}°",
            fill=COLOR_GRID,
            font=font_small,
        )

    for h in range(0, 25, 2):
        x = strip_x0 + (24 - h) / 24.0 * strip_w
        draw.line([x, strip_y0, x, strip_y1], fill=COLOR_GRID, width=1)
        draw.text((x - 18, strip_y0 - 45), f"{h}h", fill=COLOR_GRID, font=font_med)

    # ------------------------------------------------------------------
    # РИСОВАНИЕ ЛИНИЙ СОЗВЕЗДИЙ
    # ------------------------------------------------------------------
    projections = [
        ("north", proj_north),
        ("south", proj_south),
        ("strip", proj_strip),
    ]

    for cname, edges in constellations.items():
        disp_cname = CONSTELLATION_RENAMES.get(cname, cname)
        for proj_name, proj_fn in projections:
            drawn_pts = []
            for c1, c2 in edges:
                if c1 in stars and c2 in stars:
                    s1 = stars[c1]
                    s2 = stars[c2]
                    p1 = proj_fn(s1["ra"], s1["dec"])
                    p2 = proj_fn(s2["ra"], s2["dec"])
                    if p1 and p2:
                        draw.line([p1, p2], fill=COLOR_LINE, width=3)
                        drawn_pts.append(p1)
                        drawn_pts.append(p2)

            # Название созвездия по центру нарисованной фигуры
            if drawn_pts and len(drawn_pts) >= 4:
                avg_x = sum(p[0] for p in drawn_pts) / len(drawn_pts)
                avg_y = sum(p[1] for p in drawn_pts) / len(drawn_pts)
                draw.text(
                    (avg_x - 40, avg_y - 35),
                    disp_cname,
                    fill=COLOR_TITLE,
                    font=font_italic,
                )

    # ------------------------------------------------------------------
    # РИСОВАНИЕ ЗВЁЗД И ПОДПИСЕЙ
    # ------------------------------------------------------------------
    for code, s in stars.items():
        r = mag_to_radius(s["mag"], scale=1.0)
        disp_name = s["name"]

        for proj_name, proj_fn in projections:
            pt = proj_fn(s["ra"], s["dec"])
            if pt:
                x, y = pt
                # Сама точка звезды
                draw.ellipse(
                    [x - r, y - r, x + r, y + r],
                    fill=COLOR_STAR,
                    outline=COLOR_STAR,
                )

                # Подпись (если есть имя и звезда яркая / ключевая)
                if disp_name and s["mag"] <= 3.5:
                    draw.text(
                        (x + r + 6, y - 14),
                        disp_name,
                        fill=COLOR_LABEL,
                        font=font_small,
                    )

    img.save(output_path, "PNG")
    print(f"Готово! Карта сохранена в {output_path}")


if __name__ == "__main__":
    stars = load_atlas_stars("AtlasStars.cs")
    constellations = load_constellation_lines("constellation-lines.json")
    generate_round_atlas(stars, constellations)