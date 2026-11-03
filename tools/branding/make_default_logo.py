"""Gera a logo padrão do Synthia, usada enquanto a loja não cadastra a própria.

Símbolo "Fita" (um S desenhado como a fita do cupom, com o picote nas pontas) sobre o nome em Archivo ExtraBold.
Desenho em grade de 64 unidades, traço 10; mesmo desenho do guia de marca.

    python3 tools/branding/make_default_logo.py saida.png
"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

PETROLEO = (11, 85, 99, 255)   # #0B5563
TINTA = (22, 32, 30, 255)      # #16201E
FONT = Path(__file__).resolve().parents[2] / "src/Synthia.Terminal.App/Assets/Fonts/Archivo-ExtraBold.ttf"

SS = 4                      # supersampling para bordas suaves
W, H = 520, 300
img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
d = ImageDraw.Draw(img)


def symbol(x0, y0, size, color):
    u = size * SS / 64

    def p(x, y):
        return (x0 * SS + x * u, y0 * SS + y * u)

    def rect(x1, y1, x2, y2):
        d.rectangle([p(x1, y1), p(x2, y2)], fill=color)

    def arc(cx, cy, start, end):
        r = 14.5  # raio 9,5 + metade do traço
        d.arc([p(cx - r, cy - r), p(cx + r, cy + r)], start, end, fill=color, width=round(10 * u))

    rect(24, 8, 50, 18)
    rect(24, 27, 40, 37)
    rect(14, 46, 40, 56)
    arc(24, 22.5, 90, 270)    # curva de cima, pela esquerda
    arc(40, 41.5, 270, 90)    # curva de baixo, pela direita
    d.polygon([p(*q) for q in [(50, 8), (54, 8), (56.5, 10.5), (54, 13), (56.5, 15.5), (54, 18), (50, 18)]], fill=color)
    d.polygon([p(*q) for q in [(14, 46), (10, 46), (7.5, 48.5), (10, 51), (7.5, 53.5), (10, 56), (14, 56)]], fill=color)


symbol((W - 150) / 2, 6, 150, PETROLEO)

font = ImageFont.truetype(str(FONT), 76 * SS)
text = "Synthia"
tw = d.textlength(text, font=font)
d.text(((W * SS - tw) / 2, 172 * SS), text, font=font, fill=TINTA)

img.resize((W, H), Image.LANCZOS).save(sys.argv[1])
