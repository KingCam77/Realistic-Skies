"""Find where the stars are in each drawing in docs/art (<slug>.png, slugs as in make_art_kit.py)
and save the mapping from the kit's reference sheet to the drawing in docs/art/fits.json."""
import json, math, os, itertools, glob
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, 'make_art_kit.py'), encoding='utf-8').read()
pre, rest = src.split('FIGS = [')
g = {'__file__': os.path.join(HERE, 'make_art_kit.py')}
exec(pre.replace('os.makedirs(OUT, exist_ok=True)', ''), g)
exec('FIGS = [' + rest.split('\nSTYLE')[0], g)
FIG = {s: f for s, f, d in g['FIGS']}

def refpts(figs):
    codes, pr, px = g['layout'](figs, g['W'], g['H'])
    return codes, np.array([px(pr(g['vec'](g['STARS'][c]['ra'], g['STARS'][c]['dec']))) for c in codes])

def blobs(path):
    a = np.asarray(Image.open(path).convert('L')).astype(float)
    yy, xx = np.ogrid[-4:5, -4:5]
    op = ndi.binary_opening(a < 70, structure=xx * xx + yy * yy <= 16)
    lab, n = ndi.label(op); out = []
    for i, sl in enumerate(ndi.find_objects(lab), 1):
        m = lab[sl] == i; area = m.sum(); h = sl[0].stop - sl[0].start; w = sl[1].stop - sl[1].start
        if area > 30 and area / (h * w) > 0.55 and 0.6 < h / w < 1.6:
            cy, cx = ndi.center_of_mass(m); out.append(complex(sl[1].start + cx, sl[0].start + cy))
    return np.array(out)

fits = {}
for path in sorted(glob.glob(os.path.join(HERE, 'art', '*.png'))):
    slug = os.path.splitext(os.path.basename(path))[0]
    if slug not in FIG: continue
    codes, R = refpts(FIG[slug]); Rz = R[:, 0] + 1j * R[:, 1]; B = blobs(path)
    best = None
    for i, j in itertools.permutations(range(len(Rz)), 2):
        for k, l in itertools.permutations(range(len(B)), 2):
            a = (B[l] - B[k]) / (Rz[j] - Rz[i])
            if not 0.5 < abs(a) < 1.3: continue
            b = B[k] - a * Rz[i]; d = np.abs((a * Rz + b)[:, None] - B[None, :]).min(1)
            sc = (d < 10).sum() - 0.01 * np.minimum(d, 10).sum()
            if best is None or sc > best[0]: best = (sc, a, b)
    _, a, b = best
    d = np.abs((a * Rz + b)[:, None] - B[None, :]); idx = d.argmin(1); inl = d.min(1) < 12
    a, b = np.linalg.lstsq(np.c_[Rz[inl], np.ones(inl.sum())], B[idx[inl]], rcond=None)[0]
    fits[slug] = {'a': [a.real, a.imag], 'b': [b.real, b.imag], 'stars_found': int(inl.sum()), 'stars': len(Rz)}
    print(slug, 'scale %.3f rot %.1f' % (abs(a), math.degrees(np.angle(a))), '%d/%d stars' % (inl.sum(), len(Rz)))
json.dump(fits, open(os.path.join(HERE, 'art', 'fits.json'), 'w'), indent=1)
