"""Reference sheets for drawing constellation art (e.g. with an image model).

For every constellation: kit/<slug>.png, a clean black-on-white star diagram (north up, east left,
the same framing as the Sea Legends scroll pictures) to upload, and the prompt in kit/prompts.md.
"""
import json, math, os, re, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, 'kit')
os.makedirs(OUT, exist_ok=True)
W, H = 1400, 1040
cs = open(REPO + '/RealisticSkies/AtlasStars.cs', encoding='utf-8').read()
STARS = {}
for m in re.finditer(r'new AtlasStar\("([^"]+)", (null|"[^"]*"), (null|"[^"]*"), ([-\d.]+)f, ([-\d.]+)f, ([-\d.]+)f\)', cs):
    STARS[m[1]] = dict(ra=float(m[4]), dec=float(m[5]), mag=float(m[6]))
LINES = json.load(open(REPO + '/docs/constellation-lines.json'))

def vec(ra, dec):
    a, d = math.radians(ra), math.radians(dec)
    return np.array([math.cos(d) * math.cos(a), math.cos(d) * math.sin(a), math.sin(d)])

def layout(figs, w, h):
    """Same projection as the scroll: gnomonic, north up, east left, figure filling 84 x 80 %."""
    codes = sorted({c for f in figs for e in LINES[f] for c in e})
    vs = [vec(STARS[c]['ra'], STARS[c]['dec']) for c in codes]
    cen = sum(vs); cen /= np.linalg.norm(cen)
    up0 = np.array([0, 0, 1.0]) if abs(cen[2]) < 0.999 else np.array([1.0, 0, 0])
    east = np.cross(up0, cen); east /= np.linalg.norm(east); north = np.cross(cen, east)
    def pr(v):
        c = v @ cen
        return None if c <= 0.05 else np.array([-(v @ east) / c, (v @ north) / c])
    pts = np.array([pr(v) for v in vs]); lo, hi = pts.min(0), pts.max(0)
    span = max((hi - lo)[0] / (w * 0.84), (hi - lo)[1] / (h * 0.80)); mid = (lo + hi) / 2
    px = lambda p: (w / 2 + (p[0] - mid[0]) / span, h / 2 - (p[1] - mid[1]) / span)
    return codes, pr, px

def sheet(figs, lines=True):
    codes, pr, px = layout(figs, W, H)
    img = Image.new('RGB', (W, H), 'white'); dr = ImageDraw.Draw(img)
    rad = lambda mag: max(4.0, 22.0 - 3.8 * mag)
    for c, s in STARS.items():              # faint background stars
        if s['mag'] > 5.0 or c in codes: continue
        p = pr(vec(s['ra'], s['dec']))
        if p is None: continue
        x, y = px(p)
        if 0 < x < W and 0 < y < H:
            r = rad(s['mag']) * 0.6; dr.ellipse([x - r, y - r, x + r, y + r], fill=(150, 150, 150))
    pos = {c: px(pr(vec(STARS[c]['ra'], STARS[c]['dec']))) for c in codes}
    if lines:
        for f in figs:
            for a, b in LINES[f]:
                dr.line([pos[a], pos[b]], fill=(170, 170, 170), width=4)
    for c in codes:
        x, y = pos[c]; r = rad(STARS[c]['mag'])
        dr.ellipse([x - r, y - r, x + r, y + r], fill='black')
    return img

FIGS = [  # slug, constellations drawn, figure to paint
 ('sloop', ['The Sloop'], "Corvin's Sloop: a small single-masted sailing sloop seen from the side, sailing to the RIGHT: the bow is the sharp point on the right, the stern is the left end. The long narrow outline of stars is the hull and the sail."),
 ('widows-grief', ["Widow's Grief"], "Widow's Grief: a grieving widow kneeling, seen from the side. In this picture she is UPSIDE DOWN: her shins and knees are along the short hook at the TOP, her body runs DOWN the long line, her head is the bright star at the BOTTOM, and her two arms reach on downwards and to the side, raised in prayer. Long dress and veil."),
 ('morvaths-maw', ["Morvath's Maw"], "Morvath's Maw: the wide-open jaws of a huge sea monster (part serpent, part whale), teeth showing, the loop of stars is the gaping mouth."),
 ('northern-bow', ['Northern Bow'], "The Northern Bow: an old wooden longbow, the curved line of stars is the bow itself; draw the bowstring as a thin line between its two ends."),
 ('arrowhead', ['The Arrowhead'], "The Arrowhead: a knapped flint arrowhead with a notched base; the star outline is its edge, the point is at the bottom."),
 ('harpoon', ['The Harpoon'], "The Harpoon: an old iron whaling harpoon on a wooden shaft. The bright star at the TOP is the end of the shaft (with a rope ring), the shaft runs straight DOWN to the iron point at the bottom, and the short side line is the backward-pointing barb."),
 ('ships-cat', ["Ship's Cat"], "The Ship's Cat: a cat peeking over a ship's rail, head at the TOP-RIGHT bright star with two paws reaching out beside it, body running down to the lower star where her hind legs part."),
 ('mouse', ['The Mouse'], "The Mouse: a little mouse scurrying away, body in the small triangle, long thin tail trailing down along the long line, a biscuit in its mouth."),
 ('gull', ['The Gull'], "The Gull: a seagull with wings spread wide, one wing reaching up, the other out to the side with a small hook at its tip, short tail at the bottom."),
 ('pipe', ['The Pipe'], "The Pipe: a sailor's long-stemmed clay tobacco pipe: small bowl at the bottom (the four-sided group of stars), long straight stem running up to the mouthpiece at the top. A thin curl of smoke from the bowl."),
 ('whirlpool', ['The Whirlpool'], "The Whirlpool: a swirling whirlpool of sea water spiralling down into the deep, following the crooked chain of stars."),
 ('fisherman', ['The Fisherman', "The Fisherman's Net"], "The Fisherman and his Net: an old fisherman standing with his arms spread wide (shouting), and below him his empty, torn fishing net hanging from the four-sided loop of stars."),
 ('lug-sail', ['The Lug Sail'], "The Lug Sail: a four-cornered lug sail with its yard, the top corner (peak) at the top bright star, the yard running down from it, the stepped side is the front edge (luff) with reef points, the long bottom edge is the foot."),
 ('scorpion', ['The Scorpion'], "The Scorpion: a scorpion; its tail is on the LEFT, curling up into a raised sting at the short line at the far left, and its two claws open wide at the upper RIGHT."),
 ('hunting-horn', ['The Hunting Horn'], "The Hunting Horn: a curved hunting horn made of an animal horn with brass fittings; wide bell at the top, narrow mouthpiece at the bottom, a cord hanging from it."),
 ('witchs-hat', ["The Witch's Hat"], "The Witch's Hat: a crooked, pointed witch's hat with a wide brim. In this picture it hangs UPSIDE DOWN: the brim runs along the top, the point is at the bottom. A bit of seaweed on the band."),
 ('harp', ['The Harp'], "The Harp: the frame of a tall old harp, the triangle of stars is its frame (pillar, neck and soundboard); strings drawn as fine lines."),
 ('haymaker', ['The Haymaker'], "The Haymaker: a peasant mower with legs apart, head raised (the star near the top), swinging a huge scythe: the long handle runs right across the figure from the bright star on the LEFT to the bright star on the RIGHT, where the curved blade begins and bends upwards."),
 ('bull', ['The Bull'], "The Bull: the head of a big bull lowered as if to charge, two long horns both pointing to the LEFT (the two long lines), broad brow along the top, muzzle at the bottom point, and the lone star inside the outline is his eye."),
 ('cobra', ['The Cobra'], "The Cobra: a cobra rearing up to strike. In this picture she is UPSIDE DOWN: the tip of her tail is the bright star at the TOP, her body runs down and curves to the left, and her hooded head (the small four-sided loop) is at the BOTTOM LEFT, ready to strike."),
 ('fiddler-crab', ['The Fiddler Crab'], "The Fiddler Crab: a fiddler crab with a square shell, two jointed legs raised behind, ONE huge open claw (the triangle at the top right) and one small claw at the top left holding a glowing pearl (the bright star at the end of that short line)."),
]
STYLE = ("Style: an old sailor's star atlas, hand-drawn engraving / ink line art in dark sepia, fine hatching, "
         "no colour fills, white background, no text, no frame, no extra stars. ")
RULES = ("IMPORTANT: draw the figure AROUND the black star dots. Keep every black dot exactly where it is, "
         "the same size, and do not add, remove or move any dot. The grey lines only show how the stars are "
         "joined and may be covered by the drawing. Keep the image size and composition unchanged.")
md = ['# Constellation art kit', '',
      'For each constellation upload its PNG to the image model together with the prompt. '
      'Pictures: north up, east left, exactly as in the Sea Legends scroll.', '',
      '**Common part of every prompt**', '', '> ' + STYLE + RULES, '']
pages = []
fT = ImageFont.truetype('/usr/share/texmf/fonts/opentype/public/tex-gyre/texgyrepagella-bold.otf', 46)
fB = ImageFont.truetype('/usr/share/texmf/fonts/opentype/public/tex-gyre/texgyrepagella-regular.otf', 30)
import textwrap
for i, (slug, figs, desc) in enumerate(FIGS, 1):
    img = sheet(figs); fn = f'{i:02d}-{slug}.png'; img.save(os.path.join(OUT, fn))
    prompt = 'Turn this star diagram into a constellation drawing. ' + desc + ' ' + STYLE + RULES
    md += [f'## {i}. {figs[0]}', '', f'Upload `{fn}`, then:', '', '```', prompt, '```', '']
    pg = Image.new('RGB', (1600, 2263), 'white'); d = ImageDraw.Draw(pg)
    d.text((100, 80), f'{i}. {desc.split(":")[0]}', font=fT, fill='black')
    pg.paste(img, (100, 170)); d.rectangle([100, 170, 1500, 1210], outline=(120, 120, 120), width=2)
    d.text((100, 1240), f'File: {fn}', font=fB, fill=(90, 90, 90))
    y = 1300
    for para in [prompt]:
        for line in textwrap.wrap(para, 95):
            d.text((100, y), line, font=fB, fill='black'); y += 40
    pages.append(pg)
open(os.path.join(OUT, 'prompts.md'), 'w', encoding='utf-8').write('\n'.join(md))
pages[0].save(os.path.join(OUT, 'constellation-art-kit.pdf'), save_all=True, append_images=pages[1:], resolution=200)
print(len(FIGS), 'sheets')
