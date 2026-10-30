# -*- coding: utf-8 -*-
"""元素战机 title screen v2 — richer composition using ONLY in-game assets.

Creative additions over v1:
  * god rays from behind the logo
  * element-colored aura behind each character
  * floating element orbs (real bullet sprites) around each character
  * drifting colored particles in the sky
  * cinematic color grade + vignette
Prompt text is NOT baked into the game-ready version (Unity pulses its own);
a poster version with a faint static prompt is also saved.
"""
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageOps, ImageEnhance
import os, random, math

ROOT = r"D:\UNITY项目\YuansuPlan\Assets\Resources\Sprites"
OUT_POSTER = r"D:\UNITY项目\YuansuPlan\Assets\Art\Title\TitleScreen_1920x1080.png"
OUT_GAME   = r"D:\UNITY项目\YuansuPlan\Assets\Resources\Sprites\界面\首页背景.png"
FONT = r"C:\Windows\Fonts\msyhbd.ttc"
random.seed(42)

# ---------- 1. background crop ----------
bg = Image.open(os.path.join(ROOT, "界面", "主页面背景.png")).convert("RGB")
w, h = bg.size
th = int(w * 9 / 16)
top = 150
bg = bg.crop((0, top, w, top + th)).resize((1920, 1080), Image.LANCZOS)

# cinematic grade
bg = ImageEnhance.Color(bg).enhance(1.10)
bg = ImageEnhance.Contrast(bg).enhance(1.05)
canvas = bg.convert("RGBA")

W, H = canvas.size

# ---------- 2. god rays from behind logo ----------
rays = Image.new("RGBA", (W, H), (0, 0, 0, 0))
rd = ImageDraw.Draw(rays)
apex = (960, -520)
for ang, width in [(-38, 60), (-20, 90), (0, 70), (18, 95), (36, 60)]:
    a = math.radians(ang)
    L = 1900
    p1 = apex
    p2 = (apex[0] + L * math.sin(a - width / 3000), apex[1] + L * math.cos(a))
    p3 = (apex[0] + L * math.sin(a + width / 3000), apex[1] + L * math.cos(a))
    rd.polygon([p1, p2, p3], fill=(255, 250, 220, 26))
rays = rays.filter(ImageFilter.GaussianBlur(28))
canvas = Image.alpha_composite(canvas, rays)

# ---------- 3. characters + auras + orbs ----------
CHAR_ORDER = ["冰角色", "雷角色", "火角色", "水角色", "岩角色"]
ORB = {"冰角色": "雪花子弹.png", "雷角色": "闪电子弹.png", "火角色": "火焰子弹.png",
       "水角色": "水滴子弹.png", "岩角色": "岩石子弹.png"}
GLOW = {"冰角色": (102, 204, 255), "雷角色": (186, 122, 232), "火角色": (255, 99, 56),
        "水角色": (58, 143, 240), "岩角色": (255, 179, 71)}
CX = [430, 700, 960, 1225, 1500]
FEET_Y = [1015, 1030, 1045, 1030, 1015]
HEIGHTS = [400, 415, 480, 415, 400]

# auras (radial gradient behind each character)
aura = Image.new("RGBA", (W, H), (0, 0, 0, 0))
inv = ImageOps.invert(Image.radial_gradient("L"))  # bright center -> dark edge
for name, cx, fy, ch_h in zip(CHAR_ORDER, CX, FEET_Y, HEIGHTS):
    r = int(ch_h * 0.85)
    m = inv.resize((2 * r, 2 * r))
    c = GLOW[name]
    solid = Image.new("RGBA", (2 * r, 2 * r), (c[0], c[1], c[2], 88))
    aura.paste(solid, (cx - r, fy - int(ch_h * 0.55) - r), m)
aura = aura.filter(ImageFilter.GaussianBlur(30))
canvas = Image.alpha_composite(canvas, aura)

# ground glows
glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
for name, cx, fy, ch_h in zip(CHAR_ORDER, CX, FEET_Y, HEIGHTS):
    c = GLOW[name]
    gw, gh = int(ch_h * 0.62), int(ch_h * 0.12)
    gd.ellipse((cx - gw // 2, fy - gh // 2, cx + gw // 2, fy + gh // 2),
               fill=(c[0], c[1], c[2], 120))
glow = glow.filter(ImageFilter.GaussianBlur(12))
canvas = Image.alpha_composite(canvas, glow)

# characters (fire on top)
char_sprites = []
for name, cx, fy, ch_h in zip(CHAR_ORDER, CX, FEET_Y, HEIGHTS):
    im = Image.open(os.path.join(ROOT, "角色", name + ".png")).convert("RGBA")
    r = ch_h / im.height
    im = im.resize((int(im.width * r), ch_h), Image.NEAREST)
    char_sprites.append((im, cx, fy, name))

order = [s for s in char_sprites if s[3] != "火角色"] + [s for s in char_sprites if s[3] == "火角色"]
for im, cx, fy, name in order:
    y0 = fy - im.height
    sh = Image.new("RGBA", im.size, (0, 0, 0, 0))
    sh.putalpha(im.split()[3].point(lambda a: 90 if a > 40 else 0))
    canvas.alpha_composite(sh, (cx - im.width // 2 + 10, y0 + 8))
    canvas.alpha_composite(im, (cx - im.width // 2, y0))

# floating element orbs (real bullet sprites) around shoulders/head
orbs = Image.new("RGBA", (W, H), (0, 0, 0, 0))
for im, cx, fy, name in char_sprites:
    spr = Image.open(os.path.join(ROOT, "子弹", ORB[name])).convert("RGBA")
    ch_h = im.height
    spots = [(-0.52, 0.30, 88, 235, 18), (0.55, 0.16, 70, 205, -14)]  # (dx, dy_frac, size, alpha, rot)
    if name == "火角色":
        spots.append((0.0, 0.52, 96, 255, 0))
    for dx, dyf, size, alpha, rot in spots:
        s = spr.resize((size, size), Image.NEAREST).rotate(rot, expand=True, resample=Image.NEAREST)
        a = s.split()[3].point(lambda v: v * alpha // 255)
        s.putalpha(a)
        ox = cx + int(dx * ch_h) - size // 2
        oy = fy - int(ch_h * 0.62) + int(dyf * ch_h) - size // 2
        # tiny glow behind orb
        g = Image.new("RGBA", (size * 2, size * 2), (0, 0, 0, 0))
        gm = inv.resize((size * 2, size * 2)).point(lambda v: v * 120 // 255)
        gc = GLOW[name]
        gsolid = Image.new("RGBA", (size * 2, size * 2), (gc[0], gc[1], gc[2], 120))
        g.paste(gsolid, (0, 0), gm)
        orbs.alpha_composite(g, (ox + size // 2 - size, oy + size // 2 - size))
        orbs.alpha_composite(s, (ox, oy))
orbs = orbs.filter(ImageFilter.GaussianBlur(0.6))
canvas = Image.alpha_composite(canvas, orbs)

# ---------- 4. drifting particles in the sky ----------
parts = Image.new("RGBA", (W, H), (0, 0, 0, 0))
pd = ImageDraw.Draw(parts)
all_colors = list(GLOW.values()) + [(255, 255, 255)]
for _ in range(46):
    x, y = random.randint(40, W - 40), random.randint(60, 620)
    r = random.choice([2, 2, 3, 3, 4, 6])
    c = random.choice(all_colors)
    a = random.randint(50, 150)
    pd.ellipse((x - r, y - r, x + r, y + r), fill=(c[0], c[1], c[2], a))
parts_soft = parts.filter(ImageFilter.GaussianBlur(2))
canvas = Image.alpha_composite(canvas, parts_soft)
canvas = Image.alpha_composite(canvas, parts)  # crisp dots on top

# ---------- 5. logo with soft glow ----------
logo = Image.open(os.path.join(ROOT, "界面", "元素战机.png")).convert("RGBA")
lw = 1000
logo = logo.resize((lw, int(logo.height * lw / logo.width)), Image.NEAREST)
lx, ly = (W - lw) // 2, 42
GW, GH = lw + 120, logo.height + 120
lglow = Image.new("RGBA", (GW, GH), (0, 0, 0, 0))
lm = inv.resize((GW, GW))  # square radial; crop center band to logo box
lm = lm.crop((0, (GW - GH) // 2, GW, (GW - GH) // 2 + GH)).point(lambda v: v * 70 // 255)
lg = Image.new("RGBA", (GW, GH), (255, 130, 220, 90))
lglow.paste(lg, (0, 0), lm)
lglow = lglow.filter(ImageFilter.GaussianBlur(18))
canvas.alpha_composite(lglow, (lx - 60, ly - 60))
shadow = Image.new("RGBA", logo.size, (0, 0, 0, 0))
shadow.putalpha(logo.split()[3].point(lambda a: 130 if a > 40 else 0))
canvas.alpha_composite(shadow, (lx + 8, ly + 10))
canvas.alpha_composite(logo, (lx, ly))

# ---------- 6. vignette ----------
vig = Image.new("L", (W, H), 0)
vd = ImageDraw.Draw(vig)
vd.ellipse((-int(W * 0.18), -int(H * 0.25), W + int(W * 0.18), H + int(H * 0.25)), fill=255)
vig = ImageOps.invert(vig).filter(ImageFilter.GaussianBlur(120)).point(lambda v: v * 90 // 255)
dark = Image.new("RGBA", (W, H), (10, 8, 30, 255))
dark.putalpha(vig)
canvas = Image.alpha_composite(canvas, dark)

os.makedirs(os.path.dirname(OUT_GAME), exist_ok=True)
canvas.convert("RGB").save(OUT_GAME, quality=95)   # game-ready: no prompt text
print("game-ready:", OUT_GAME)

# ---------- 7. poster version with faint static prompt ----------
poster = canvas.copy()
d = ImageDraw.Draw(poster)
text = "—— 按 任 意 键 开 始 ——"
f = ImageFont.truetype(FONT, 40)
sw = 4
bbox = d.textbbox((0, 0), text, font=f, stroke_width=sw)
tx = (W - (bbox[2] - bbox[0])) // 2
ty = ly + logo.height + 30
glow_txt = Image.new("RGBA", (W, H), (0, 0, 0, 0))
ImageDraw.Draw(glow_txt).text((tx, ty), text, font=f, fill=(200, 180, 255, 200),
                              stroke_width=sw, stroke_fill=(120, 80, 200, 200))
glow_txt = glow_txt.filter(ImageFilter.GaussianBlur(6))
poster = Image.alpha_composite(poster, glow_txt)
d = ImageDraw.Draw(poster)
d.text((tx, ty), text, font=f, fill=(255, 255, 255, 165), stroke_width=sw, stroke_fill=(90, 60, 160, 165))
poster.convert("RGB").save(OUT_POSTER, quality=95)
print("poster:", OUT_POSTER)
