"""Gera a logo neutra do PDV Synthia, usada enquanto a loja não cadastra a própria.

    python3 tools/branding/make_default_logo.py saida.png
"""
import sys
from PIL import Image, ImageDraw, ImageFont

NAVY = (43, 76, 126, 255)
ACCENT = (224, 122, 31, 255)
FONT = "/usr/share/fonts/opentype/inter/Inter-Bold.otf"

W, H = 520, 300
img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# Marca: carrinho estilizado num quadrado arredondado.
d.rounded_rectangle((185, 10, 335, 160), radius=34, fill=NAVY)
d.line([(215, 55), (238, 55), (258, 118), (305, 118), (318, 72), (246, 72)], fill="white", width=11, joint="curve")
d.ellipse((252, 126, 270, 144), fill=ACCENT)
d.ellipse((292, 126, 310, 144), fill=ACCENT)

big = ImageFont.truetype(FONT, 66)
small = ImageFont.truetype(FONT, 26)
for text, font, y, color in [("Synthia", big, 172, NAVY), ("P D V", small, 254, ACCENT)]:
    w = d.textlength(text, font=font)
    d.text(((W - w) / 2, y), text, font=font, fill=color)

img.save(sys.argv[1])
