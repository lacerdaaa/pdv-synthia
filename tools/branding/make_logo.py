"""
Gera a logo com fundo transparente a partir de um print (fundo branco, moldura escura e botão de edição).

    python3 tools/branding/make_logo.py print.png saida/logo.png [saida/logo-contorno.png]

- Mascote (acima de TEXT_TOP): silhueta fechada, mantém os brancos do desenho (olhos, luvas, contorno do X).
- Texto (abaixo de TEXT_TOP): todo branco vira transparente, inclusive o miolo das letras.
- Bordas: transparência pela mistura com o branco; cor do pixel sólido mais próximo (sem halo claro).
- Opcional: variante "adesivo" com contorno branco, para fundos coloridos.

Requer numpy, scipy e Pillow. Os números (raio do círculo, posição do lápis, TEXT_TOP) são do print original.
"""
import sys

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

TEXT_TOP = 480         # acima: mascote; abaixo: "VEM PRO Xandão SUPERMERCADOS"
CIRCLE_RADIUS = 440    # círculo branco do print
PENCIL = (832, 832, 92)  # botão de edição do print (x, y, raio)


def extract(src: str) -> Image.Image:
    rgb = np.asarray(Image.open(src).convert("RGB")).astype(np.float32)
    h, w, _ = rgb.shape
    yy, xx = np.mgrid[0:h, 0:w]

    near_white = rgb.min(axis=2) > 215
    outside = (np.hypot(xx - (w - 1) / 2, yy - (h - 1) / 2) > CIRCLE_RADIUS) | (
        (xx - PENCIL[0]) ** 2 + (yy - PENCIL[1]) ** 2 < PENCIL[2] ** 2)
    content = ~near_white & ~outside

    disk = np.hypot(*np.mgrid[-7:8, -7:8]) <= 7
    mascot = ndimage.binary_fill_holes(ndimage.binary_closing(content & (yy < TEXT_TOP), structure=disk))
    mascot &= yy < TEXT_TOP
    text = content & (yy >= TEXT_TOP)
    background = ~(mascot | text)

    band = ndimage.binary_dilation(background, iterations=3) & ~background
    solid = ~background & ~band
    mix = np.clip((255.0 - rgb.min(axis=2)) / 255.0, 0.0, 1.0)
    alpha = np.where(background, 0.0, np.where(band, np.clip(mix * 255.0 * 1.6, 0, 255), 255.0))
    _, (iy, ix) = ndimage.distance_transform_edt(~solid, return_indices=True)
    color = np.where(band[..., None], rgb[iy, ix], rgb)

    img = Image.fromarray(np.dstack([color, alpha]).astype(np.uint8), "RGBA")
    ys, xs = np.nonzero(alpha > 8)
    pad = 10
    return img.crop((max(xs.min() - pad, 0), max(ys.min() - pad, 0), min(xs.max() + pad + 1, w), min(ys.max() + pad + 1, h)))


def sticker(logo: Image.Image, stroke: int = 10, pad: int = 14) -> Image.Image:
    opaque = np.pad(np.asarray(logo)[..., 3] > 20, pad)
    halo = ndimage.binary_dilation(opaque, structure=np.hypot(*np.mgrid[-stroke:stroke + 1, -stroke:stroke + 1]) <= stroke)
    mask = Image.fromarray((halo * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))
    out = Image.composite(Image.new("RGBA", mask.size, (255, 255, 255, 255)), Image.new("RGBA", mask.size, (255, 255, 255, 0)), mask)
    out.alpha_composite(logo, (pad, pad))
    return out


if __name__ == "__main__":
    logo = extract(sys.argv[1])
    logo.save(sys.argv[2], optimize=True)
    print(sys.argv[2], logo.size)
    if len(sys.argv) > 3:
        s = sticker(logo)
        s.save(sys.argv[3], optimize=True)
        print(sys.argv[3], s.size)
