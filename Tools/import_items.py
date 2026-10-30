# -*- coding: utf-8 -*-
"""把用户放在 界面/ 的道具素材统一转换并搬到 道具/ 目录。

- webp -> png（Unity 不支持 webp）
- 能量水晶.jpg 白底抠图（从边缘泛洪去白，保留水晶内部白色高光）
- 血包.png 已有透明通道，直接搬
- 统一命名与 ItemDB key 一致：血包/能量水晶/磁铁/攻击力/生命上限/伤害减免
"""
from PIL import Image
from collections import deque
import os

SRC = r"D:\UNITY项目\YuansuPlan\Assets\Resources\Sprites\界面"
DST = r"D:\UNITY项目\YuansuPlan\Assets\Resources\Sprites\道具"
os.makedirs(DST, exist_ok=True)


def remove_white_bg(im, thresh=242):
    """从边缘泛洪去除白底（只清与边缘连通的白色，保留内部高光）。"""
    a = im.convert("RGBA")
    px = a.load()
    w, h = a.size
    seen = [[False] * w for _ in range(h)]
    q = deque()
    for x in range(w):
        q.append((x, 0)); q.append((x, h - 1))
    for y in range(h):
        q.append((0, y)); q.append((w - 1, y))
    while q:
        x, y = q.popleft()
        if x < 0 or y < 0 or x >= w or y >= h or seen[y][x]:
            continue
        seen[y][x] = True
        r, g, b, al = px[x, y]
        if r >= thresh and g >= thresh and b >= thresh:
            px[x, y] = (255, 255, 255, 0)
            q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return a


jobs = [
    # (源文件, 目标名, 是否抠白底)
    ("血包.png",       "血包.png",       False),
    ("能量水晶.jpg",   "能量水晶.png",   True),
    ("磁铁.webp",      "磁铁.png",       False),
    ("攻击力.webp",    "攻击力.png",     False),
    ("生命上限.webp",  "生命上限.png",   False),
    ("伤害减免.webp",  "伤害减免.png",   False),
]

for src, dst, cut in jobs:
    sp = os.path.join(SRC, src)
    im = Image.open(sp)
    if cut:
        im = remove_white_bg(im)
        print(src, "-> 抠白底完成")
    im = im.convert("RGBA")
    im.save(os.path.join(DST, dst))
    print(dst, im.size)
    # 删除界面/下的源文件和 meta（已转换搬走，避免重复素材）
    for p in (sp, sp + ".meta"):
        if os.path.exists(p):
            os.remove(p)

# 能量条.webp 是上轮遗留（已有 能量条.png），一并清掉
stale = os.path.join(SRC, "能量条.webp")
for p in (stale, stale + ".meta"):
    if os.path.exists(p):
        os.remove(p)
        print("removed stale:", os.path.basename(p))

print("done")
