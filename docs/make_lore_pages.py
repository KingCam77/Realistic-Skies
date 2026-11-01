import re, json, math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
REPO = '/home/claude/rs/Realistic-Skies'
OUT = sys.argv[1] if len(sys.argv) > 1 else 'out'
os.makedirs(OUT, exist_ok=True)
S = 2048; M = 150                       # page size and margin
INK = (52, 34, 20); LINE = (52, 34, 20)
TG = '/usr/share/texmf/fonts/opentype/public/tex-gyre/'
F_BODY = ImageFont.truetype(TG + 'texgyrepagella-regular.otf', 50)
F_IT = ImageFont.truetype(TG + 'texgyrepagella-italic.otf', 50)
F_HEAD = ImageFont.truetype(TG + 'texgyrepagella-bold.otf', 66)
F_PART = ImageFont.truetype(TG + 'texgyrechorus-mediumitalic.otf', 120)
F_TITLE = ImageFont.truetype(TG + 'texgyrechorus-mediumitalic.otf', 170)
F_SMALL = ImageFont.truetype(TG + 'texgyrepagella-italic.otf', 34)
F_LBL = ImageFont.truetype(TG + 'texgyrepagella-regular.otf', 34)
LH = 68

# ---------- data ----------
cs = open(REPO + '/RealisticSkies/AtlasStars.cs', encoding='utf-8').read()
STARS = {}
for m in re.finditer(r'new AtlasStar\("([^"]+)", (null|"[^"]*"), (null|"[^"]*"), ([-\d.]+)f, ([-\d.]+)f, ([-\d.]+)f\)', cs):
    STARS[m[1]] = dict(name=None if m[2] == 'null' else m[2].strip('"'), letter=None if m[3] == 'null' else m[3].strip('"'),
                       ra=float(m[4]), dec=float(m[5]), mag=float(m[6]))
lore_txt = open(REPO + '/LORE.md', encoding='utf-8').read()
NAME_ROWS = re.findall(r'^\| (\S+) \| \*\*([^*]+)\*\* \| ([^|]+) \| ([^|]+) \|$', lore_txt, re.M)
LORE_NAMES = {k: n for k, n, c, m in NAME_ROWS if len(k) <= 2}
CODE_NAMES = {k: n for k, n, c, m in NAME_ROWS if len(k) > 2}
LINES = json.load(open(REPO + '/docs/constellation-lines.json'))
lore = open(REPO + '/LORE.md', encoding='utf-8').read()

def vec(ra, dec):
    a, d = math.radians(ra), math.radians(dec)
    return np.array([math.cos(d) * math.cos(a), math.cos(d) * math.sin(a), math.sin(d)])

# ---------- star diagrams ----------
def diagram(figs, w, h, pole=None, pointer=None, label_extra=()):
    codes = sorted({c for f in figs for e in LINES[f] for c in e})
    vs = [vec(STARS[c]['ra'], STARS[c]['dec']) for c in codes]
    cen = sum(vs); cen /= np.linalg.norm(cen)
    if pole is not None:
        cen = np.array([0, 0, 1.0 if pole > 0 else -1.0]) * 0.35 + cen * 0.65; cen /= np.linalg.norm(cen)
    # tangent basis: north up, east left
    up0 = np.array([0, 0, 1.0])
    if abs(cen[2]) > 0.999: up0 = np.array([1.0, 0, 0])
    east = np.cross(up0, cen); east /= np.linalg.norm(east)
    north = np.cross(cen, east)
    def pr(v):
        c = v @ cen
        if c <= 0.05: return None
        return np.array([-(v @ east) / c, (v @ north) / c])
    pts = [pr(v) for v in vs]
    extra = []
    if pole is not None: extra.append(pr(np.array([0, 0, 1.0 if pole > 0 else -1.0])))
    allp = np.array(pts + extra)
    lo, hi = allp.min(0), allp.max(0)
    span = max((hi - lo)[0] / (w * 0.84), (hi - lo)[1] / (h * 0.80))
    mid = (lo + hi) / 2
    def px(p): return (w / 2 + (p[0] - mid[0]) / span, h / 2 - (p[1] - mid[1]) / span)
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0)); dr = ImageDraw.Draw(img)
    # thin frame
    dr.rectangle([2, 2, w - 3, h - 3], outline=INK + (150,), width=3)
    def rad(mag): return max(3.0, 15.0 - 2.6 * mag)
    # background stars
    for c, s in STARS.items():
        if s['mag'] > 5.0 or c in codes: continue
        p = pr(vec(s['ra'], s['dec']))
        if p is None: continue
        x, y = px(p)
        if 10 < x < w - 10 and 10 < y < h - 10:
            r = rad(s['mag']) * 0.7
            dr.ellipse([x - r, y - r, x + r, y + r], fill=INK + (150,))
    pos = {c: px(p) for c, p in zip(codes, pts)}
    for f in figs:
        for a, b in LINES[f]:
            (x1, y1), (x2, y2) = pos[a], pos[b]
            L = math.hypot(x2 - x1, y2 - y1) or 1; ux, uy = (x2 - x1) / L, (y2 - y1) / L
            g1, g2 = rad(STARS[a]['mag']) + 7, rad(STARS[b]['mag']) + 7
            if L > g1 + g2: dr.line([(x1 + ux * g1, y1 + uy * g1), (x2 - ux * g2, y2 - uy * g2)], fill=LINE + (235,), width=5)
    if pointer:
        a, b = pointer
        (x1, y1), (x2, y2) = pos[a], pos[b]; xp, yp = px(extra[0])
        L = math.hypot(xp - x2, yp - y2); n = int(L // 26)
        for k in range(1, n):
            t0, t1 = k / n, (k + 0.45) / n
            dr.line([(x2 + (xp - x2) * t0, y2 + (yp - y2) * t0), (x2 + (xp - x2) * t1, y2 + (yp - y2) * t1)], fill=LINE + (200,), width=4)
    for c in codes:
        x, y = pos[c]; r = rad(STARS[c]['mag'])
        dr.ellipse([x - r - 4, y - r - 4, x + r + 4, y + r + 4], fill=(0, 0, 0, 0))
        dr.ellipse([x - r, y - r, x + r, y + r], fill=INK + (255,))
    for c in codes:
        x, y = pos[c]; r = rad(STARS[c]['mag'])
        s = STARS[c]
        # the scroll uses only the sailors' names, never Almanac letters
        lbl = LORE_NAMES.get(s['letter']) if s['letter'] else (s['name'] or CODE_NAMES.get(c))
        if lbl:
            tw = dr.textlength(lbl, font=F_LBL)
            tx = x + r + 6 if x + r + 6 + tw < w - 8 else x - r - 6 - tw
            l, t, r2, b2 = dr.textbbox((tx, y - 20), lbl, font=F_LBL)
            dr.rectangle([l - 4, t - 3, r2 + 4, b2 + 3], fill=(0, 0, 0, 0))   # clear lines under the label
            dr.text((tx, y - 20), lbl, font=F_LBL, fill=INK + (255,))
    if pole is not None:
        xp, yp = px(extra[0])
        dr.line([(xp - 16, yp), (xp + 16, yp)], fill=INK + (255,), width=4); dr.line([(xp, yp - 16), (xp, yp + 16)], fill=INK + (255,), width=4)
        near = min(math.hypot(xp - x, yp - y) for x, y in pos.values())
        if near > 40: dr.text((xp + 14, yp + 6), 'pole', font=F_SMALL, fill=INK + (255,))
    return img

# ---------- engraved pictures (docs/art, fitted by fit_art.py) ----------
from PIL import ImageFilter
_kit_src = open(os.path.join(REPO, 'docs', 'make_art_kit.py'), encoding='utf-8').read().split('FIGS = [')[0]
KIT = {'__file__': os.path.join(REPO, 'docs', 'make_art_kit.py')}
exec(_kit_src.replace('os.makedirs(OUT, exist_ok=True)', ''), KIT)
ART_DIR = os.path.join(REPO, 'docs', 'art')
FITS = json.load(open(os.path.join(ART_DIR, 'fits.json'))) if os.path.exists(os.path.join(ART_DIR, 'fits.json')) else {}

# per drawing fixes
ART_SHIFT = {   # artistic licence: move a star dot (in drawing pixels) onto the drawn feature
    'fiddler-crab': {'3X-113-446': (-46, 22)},      # Lulu into the pearl
}
LABEL_SPOT = {'Mouser': (1, -1, 14),        # up and right, off the cat's face
              'Witchlight': (-1, -1, 26)}   # above the hat's tip, not on it
ART_ERASE_DOTS = {'witchs-hat': [(413, 281), (377, 168)]}    # 700x520 picture px
ART_ERASE_LINES = {   # stray straight lines the model drew between these stars
    'mouse': [('3C-090-489', '3C-097-397')],
}

def _blobs(gray):
    from scipy import ndimage as ndi
    yy, xx = np.ogrid[-3:4, -3:4]
    op = ndi.binary_opening(gray < 90, structure=xx * xx + yy * yy <= 9)
    lab, n = ndi.label(op); out = []
    for i, sl in enumerate(ndi.find_objects(lab), 1):
        m = lab[sl] == i; area = m.sum(); hh = sl[0].stop - sl[0].start; ww = sl[1].stop - sl[1].start
        if 20 < area < 2500 and area / (hh * ww) > 0.55 and 0.6 < hh / ww < 1.6:
            cy, cx = ndi.center_of_mass(m); out.append((sl[1].start + cx, sl[0].start + cy, math.sqrt(area / math.pi)))
    return out

def art_diagram(slug, figs, w, h, upright=False, caption=None):
    RW, RH = KIT['W'], KIT['H']
    art = Image.open(os.path.join(ART_DIR, slug + '.png')).convert('RGB')
    a = complex(*FITS[slug]['a']); b0 = complex(*FITS[slug]['b'])
    paper_rgb = tuple(int(v) for v in np.percentile(np.asarray(art).reshape(-1, 3), 90, axis=0))
    warped = art.transform((RW, RH), Image.AFFINE, (a.real, -a.imag, b0.real, a.imag, a.real, b0.imag),
                           resample=Image.BICUBIC, fillcolor=paper_rgb)
    codes, pr, pxf = KIT['layout'](figs, RW, RH)
    ref = {c: complex(*pxf(pr(KIT['vec'](STARS[c]['ra'], STARS[c]['dec'])))) for c in codes}
    for c, (dx, dy) in ART_SHIFT.get(slug, {}).items():
        if c in ref: ref[c] += complex(dx, dy) / a
    dr0 = ImageDraw.Draw(warped)
    # the model sometimes inks background stars as big black dots: wipe dots sitting on catalogue stars
    bg = []
    for c, st in STARS.items():
        if c in ref or st['mag'] > 5.0: continue
        p = pr(KIT['vec'](st['ra'], st['dec']))
        if p is not None: bg.append(complex(*pxf(p)))
    gray0 = np.asarray(warped.convert('L')).astype(float); pap0 = np.percentile(gray0, 90)
    for x, y, r in _blobs(gray0):
        z = complex(x, y)
        if min(abs(z - q) for q in ref.values()) <= 14: continue
        # isolated dots (stars the model inked, or invented) go; dots inside the drawing stay
        yy, xx = np.ogrid[:gray0.shape[0], :gray0.shape[1]]
        d2 = (xx - x) ** 2 + (yy - y) ** 2
        ring = (d2 > (r + 5) ** 2) & (d2 < (r + 16) ** 2)
        if (gray0[ring] < pap0 - 60).mean() < 0.06:
            dr0.ellipse([x - r - 4, y - r - 4, x + r + 4, y + r + 4], fill=paper_rgb)
    for p, q in ART_ERASE_LINES.get(slug, []):
        dr0.line([(ref[p].real, ref[p].imag), (ref[q].real, ref[q].imag)], fill=paper_rgb, width=9)
    if upright: warped = warped.rotate(180)
    px_ = np.asarray(warped).astype(float)
    lum = px_.mean(2); paper = np.percentile(lum, 90); sat = px_.max(2) - px_.min(2)
    lum = np.where((sat < 14) & (lum > 115), paper, lum)          # grey guide lines from the sheet
    alpha = np.clip((paper - lum) / max(paper - 40, 1), 0, 1) ** 0.9
    rgba = np.zeros((RH, RW, 4), np.uint8); rgba[..., :3] = INK; rgba[..., 3] = (alpha * 255).astype(np.uint8)
    img = Image.fromarray(rgba, 'RGBA').resize((w, h), Image.LANCZOS)
    k = w / RW
    pos = {}
    for c, z in ref.items():
        x, y = z.real * k, z.imag * k
        if upright: x, y = w - x, h - y
        pos[c] = (x, y)
    for ex, ey in ART_ERASE_DOTS.get(slug, []):     # a star the model drew that is not in the sky
        ex, ey = ex * w / 700, ey * h / 520
        a = np.asarray(img).copy(); yy, xx = np.ogrid[:h, :w]
        a[..., 3][(xx - ex) ** 2 + (yy - ey) ** 2 < 7 ** 2] = 0; img = Image.fromarray(a, 'RGBA')
    dr = ImageDraw.Draw(img)
    def rad(mag): return max(3.0, 15.0 - 2.6 * mag)
    # faint background stars from the catalogue (the model's own dots were wiped above)
    A0 = np.asarray(img)[..., 3].astype(float)
    for z in bg:
        x, y = z.real * k, z.imag * k
        if upright: x, y = w - x, h - y
        if 10 < x < w - 10 and 10 < y < h - 10:
            # only out in the open sky, never on the drawing itself
            if A0[max(0, int(y) - 22):int(y) + 22, max(0, int(x) - 22):int(x) + 22].mean() > 12: continue
            dr.ellipse([x - 2.2, y - 2.2, x + 2.2, y + 2.2], fill=INK + (130,))
    # thin figure lines, like on the star charts
    for f in figs:
        for p, q in LINES[f]:
            (x1, y1), (x2, y2) = pos[p], pos[q]
            L = math.hypot(x2 - x1, y2 - y1) or 1; ux, uy = (x2 - x1) / L, (y2 - y1) / L
            g1, g2 = rad(STARS[p]['mag']) + 6, rad(STARS[q]['mag']) + 6
            if L > g1 + g2: dr.line([(x1 + ux * g1, y1 + uy * g1), (x2 - ux * g2, y2 - uy * g2)], fill=INK + (190,), width=2)
    for c in codes:
        x, y = pos[c]; r = rad(STARS[c]['mag'])
        dr.ellipse([x - r - 2, y - r - 2, x + r + 2, y + r + 2], fill=(0, 0, 0, 0))
        dr.ellipse([x - r, y - r, x + r, y + r], fill=INK + (255,))
    # names: try spots around the star, take the one covering the least of the engraving
    taken = []
    for c in sorted(codes, key=lambda c: STARS[c]['mag']):
        x, y = pos[c]; r = rad(STARS[c]['mag']); st = STARS[c]
        lbl = LORE_NAMES.get(st['letter']) if st['letter'] else (st['name'] or CODE_NAMES.get(c))
        if not lbl: continue
        l0, t0, r0, b0_ = dr.textbbox((0, 0), lbl, font=F_LBL); tw, th = r0 - l0, b0_ - t0
        A = np.asarray(img)[..., 3].astype(float)
        best = None
        spots = [(1, 0, 0), (1, -1, 0), (0, -1, 0), (-1, -1, 0), (-1, 0, 0), (-1, 1, 0), (0, 1, 0), (1, 1, 0)]
        if lbl in LABEL_SPOT: spots = [LABEL_SPOT[lbl]]
        for dxs, dys, lift in spots:
            gx = r + 8
            bx = x + gx if dxs > 0 else (x - gx - tw if dxs < 0 else x - tw / 2)
            by = y - th / 2 - t0 + dys * (r + th / 2 + 6) - lift
            bx = min(max(bx, 10), w - 10 - tw)      # slide along instead of dropping spots near the frame
            box = (bx - 4, by + t0 - 3, bx + tw + 4, by + t0 + th + 3)
            if box[0] < 6 or box[1] < 6 or box[2] > w - 6 or box[3] > h - 6: continue
            cost = A[int(box[1]):int(box[3]), int(box[0]):int(box[2])].sum() / 255.0
            for (ox, oy), orad in [(pos[o], rad(STARS[o]['mag'])) for o in codes if o != c]:
                if box[0] - orad < ox < box[2] + orad and box[1] - orad < oy < box[3] + orad: cost += 5000
            for tb in taken:
                if not (box[2] < tb[0] or box[0] > tb[2] or box[3] < tb[1] or box[1] > tb[3]): cost += 5000
            cost += 40 * (abs(dxs) + abs(dys)) * (dxs <= 0)    # mild preference for the right-hand side
            if best is None or cost < best[0]: best = (cost, bx, by, box)
        if best is None: continue
        _, bx, by, box = best; taken.append(box)
        m = Image.new('L', (w, h), 0); ImageDraw.Draw(m).text((bx, by), lbl, font=F_LBL, fill=255)
        m = m.filter(ImageFilter.MaxFilter(9))
        arr = np.asarray(img).copy(); arr[..., 3][np.asarray(m) > 0] = 0; img = Image.fromarray(arr, 'RGBA'); dr = ImageDraw.Draw(img)
        dr.text((bx, by), lbl, font=F_LBL, fill=INK + (255,))
    if caption:
        tw = dr.textlength(caption, font=F_SMALL); cx = (w - tw) / 2
        m = Image.new('L', (w, h), 0); ImageDraw.Draw(m).text((cx, h - 46), caption, font=F_SMALL, fill=255)
        m = m.filter(ImageFilter.MaxFilter(9))
        arr = np.asarray(img).copy(); arr[..., 3][np.asarray(m) > 0] = 0; img = Image.fromarray(arr, 'RGBA'); dr = ImageDraw.Draw(img)
        dr.text((cx, h - 46), caption, font=F_SMALL, fill=INK + (255,))
    dr.rectangle([2, 2, w - 3, h - 3], outline=INK + (150,), width=3)
    return img

# ---------- text layout ----------
class Book:
    def __init__(s): s.pages = []; s.new()
    def new(s):
        s.img = Image.new('RGBA', (S, S), (0, 0, 0, 0)); s.dr = ImageDraw.Draw(s.img); s.y = M; s.pages.append(s.img)
        s.float = None   # (x0, x1, bottom) region taken by a picture
    def room(s, hgt): return s.y + hgt <= S - M - 40
    def col(s):
        if s.float and s.y < s.float[2]:
            x0, x1, _ = s.float
            return (x1 + 40, S - M) if x0 <= M + 5 else (M, x0 - 40)
        return (M, S - M)
    def words_line(s, words, font, width):
        line = []
        while words:
            t = ' '.join(line + [words[0]])
            if s.dr.textlength(t, font=font) <= width or not line: line.append(words.pop(0))
            else: break
        return line
    def para(s, text, font=None, indent=False, center=False):
        font = font or F_BODY
        words = text.split()
        first = True
        while words:
            if not s.room(LH): s.new()
            x0, x1 = s.col()
            ind = 50 if (indent and first) else 0
            line = s.words_line(words, font, x1 - x0 - ind)
            last = not words
            t = ' '.join(line)
            if center:
                s.dr.text(((S - s.dr.textlength(t, font=font)) / 2, s.y), t, font=font, fill=INK + (255,))
            elif last or len(line) == 1:
                s.dr.text((x0 + ind, s.y), t, font=font, fill=INK + (255,))
            else:   # justify
                widths = [s.dr.textlength(wd, font=font) for wd in line]
                gap = (x1 - x0 - ind - sum(widths)) / (len(line) - 1)
                x = x0 + ind
                for wd, wdt in zip(line, widths):
                    s.dr.text((x, s.y), wd, font=font, fill=INK + (255,)); x += wdt + gap
            s.y += LH; first = False
        s.y += 18
    def heading(s, text, font=None, need=LH * 5):
        font = font or F_HEAD
        if not s.room(need + font.size + 26):
            print('break before', text, s.y, need); s.new()
        s.float = None
        s.dr.text((M, s.y), text, font=font, fill=INK + (255,)); s.y += font.size + 26
    def rule(s):
        if not s.room(60): s.new(); return
        s.dr.line([(S / 2 - 260, s.y + 20), (S / 2 + 260, s.y + 20)], fill=INK + (200,), width=3)
        s.dr.text((S / 2 - 18, s.y - 8), '✦', font=F_HEAD, fill=INK + (255,)) if False else None
        s.y += 60
    def picture(s, img, side):
        w, h = img.size
        if not s.room(h + 20): s.new()
        x = M if side == 'left' else S - M - w
        s.img.alpha_composite(img, (int(x), int(s.y)))
        s.float = (x, x + w, s.y + h + 20)
    def end_float(s):
        if s.float and s.y < s.float[2]: s.y = s.float[2]
        s.float = None
    def centre(s, text, font, gap=20):
        s.dr.text(((S - s.dr.textlength(text, font=font)) / 2, s.y), text, font=font, fill=INK + (255,)); s.y += font.size + gap

def md_clean(t): return re.sub(r'\*\*?([^*]+)\*\*?', r'\1', t).replace('`', '')
def section(title):
    m = re.search(r'^### ' + re.escape(title) + r'\n(.+?)(?=\n#|\n---|\Z)', lore, re.S | re.M)
    assert m, title
    return [md_clean(p.strip()) for p in m.group(1).strip().split('\n\n')]

b = Book()
# title page
b.y = 420
b.centre('Sea Legends', F_TITLE, 10)
b.centre('and the Stars of the Sailors', F_PART, 80)
b.para('Every sailor learns the stars twice: once from the Almanac, and once from the old hands on the night watch. This scroll is the second lesson.', F_IT, center=True)
b.y += 40
b.para('Here are the figures sailors see among the stars, how to find them and what they point to, the names they give to the brightest stars and to the planets, and the tales behind them all.', F_IT, center=True)
b.y += 60
b.para('Each picture shows the sky as you see it looking up: north at the top, east to the left.', F_SMALL, center=True)
b.new()

ENTRIES = [
    ('Around the North Star', [
        ("Corvin's Sloop", ['The Sloop'], dict(art='sloop')),
        ("Morvath's Maw", ["Morvath's Maw"], dict(art='morvaths-maw')),
        ("Widow's Grief", ["Widow's Grief"], dict(art='widows-grief', upright=True, caption='as she kneels high above the North Star')),
        ('The Northern Bow', ['Northern Bow'], dict(art='northern-bow')),
        ('The Arrowhead', ['The Arrowhead'], dict(art='arrowhead')),
    ]),
    ('Around Oriens', [
        ('The Harpoon', ['The Harpoon'], dict(art='harpoon')),
        ('Oriens to Milnead', ['The Harpoon', "Ship's Cat"], {}),
        ("Ship's Cat", ["Ship's Cat"], dict(art='ships-cat')),
        ('The Mouse', ['The Mouse'], dict(art='mouse')),
        ('The Gull', ['The Gull'], dict(art='gull')),
        ('The Pipe', ['The Pipe'], dict(art='pipe')),
        ('The Whirlpool', ['The Whirlpool'], dict(art='whirlpool')),
        ('The Fisherman and his Net', ['The Fisherman', "The Fisherman's Net"], dict(art='fisherman')),
        ('The Lug Sail', ['The Lug Sail'], dict(art='lug-sail')),
    ]),
    ('The Other Half of the Sky', [
        ('The Scorpion', ['The Scorpion'], dict(art='scorpion')),
        ('The Hunting Horn', ['The Hunting Horn'], dict(art='hunting-horn')),
        ("The Witch's Hat", ["The Witch's Hat"], dict(art='witchs-hat')),
        ('The Harp', ['The Harp'], dict(art='harp')),
        ('The Haymaker', ['The Haymaker'], dict(art='haymaker')),
    ]),
    ('The Far South', [
        ('The Bull', ['The Bull'], dict(art='bull')),
        ('The Cobra', ['The Cobra'], dict(art='cobra')),
        ('The Fiddler Crab', ['The Fiddler Crab'], dict(art='fiddler-crab')),
        ('The Southern Pole', ['The Cobra'], dict(pole=-1, pointer=('2X-084-558', '2Z-084-642'))),
    ]),
]
side = 'left'
for part, entries in ENTRIES:
    if b.y > M + 10: b.new()
    b.y = M + 20; b.centre(part, F_PART, 50)
    for title, figs, opt in entries:
        texts = section(title)
        # keep each entry on one page: measure its height first
        narrow = S - 2 * M - 700 - 40; full = S - 2 * M
        lines = 0; hpic = 520 + 20
        for t in texts:
            words = t.split()
            while words:
                wdt = narrow if lines * LH < hpic else full
                b.words_line(words, F_BODY, wdt); lines += 1
        height = F_HEAD.size + 26 + max(hpic, lines * LH + 18 * len(texts)) + 30
        if not b.room(height): b.new()
        b.heading(title, need=540)
        if opt.get('art') and opt['art'] in FITS:
            img = art_diagram(opt['art'], figs, 700, 520, upright=opt.get('upright', False), caption=opt.get('caption'))
        else:
            img = diagram(figs, 700, 520, pole=opt.get('pole'), pointer=opt.get('pointer'))
        b.picture(img, side)
        for t in texts: b.para(t, indent=False)
        b.end_float(); b.y += 30
        side = 'right' if side == 'left' else 'left'

# names
b.new(); b.y = M + 20; b.centre("Sailors' Names", F_PART, 50)
intro = re.search(r'^Sailors have names for the brightest stars.+$', lore_txt, re.M).group(0)
b.para(md_clean(intro.split(' The Almanac shows')[0]), F_IT)
for k, n, c, m in NAME_ROWS:
    b.para(f'{n}, {c.strip()}: {m.strip()}.')
b.y += 20
b.para('And the planets, as sailors know them:', F_IT)
rows = re.findall(r'^\| Planet (\d) \| \*\*(\w+)\*\* \| (.+?) \|$', lore, re.M)
for n, nm, d in rows:
    b.para(f'{nm} (Planet {n}): {md_clean(d)}')

# legends
LEG = ["The Legend of Corvin's Sloop", "The Legend of the Witch's Hat", "The Ship's Cat and the Mouse",
       "The Fisherman and the Gull", "The Haymaker and the Bull", "The Legend of the Cobra"]
b.new(); b.y = M + 20; b.centre('Legends', F_PART, 60)
for i, t in enumerate(LEG):
    if i: 
        if b.room(LH * 8): b.y += 40
        else: b.new()
    tt = t.replace('The Legend of ', ''); b.heading(tt[0].upper() + tt[1:], need=LH * 6)
    for p in section(t): b.para(p, indent=True)

for i, p in enumerate(b.pages):
    # page number
    d = ImageDraw.Draw(p); t = str(i + 1)
    if i: d.text(((S - d.textlength(t, font=F_SMALL)) / 2, S - M + 20), t, font=F_SMALL, fill=INK + (255,))
    p.save(f'{OUT}/page{i + 1:02d}.png', optimize=True)
print(len(b.pages), 'pages')
